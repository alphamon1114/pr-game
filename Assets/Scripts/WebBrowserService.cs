using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace PrGame
{
    [Serializable]
    public sealed class WebCommand
    {
        public string type,id,url,text,map,mutex,request;
        public int width=960,height=600,x,y,button,count=1,dx,dy,key,modifiers;
        public bool flag;
    }
    [Serializable]
    public sealed class WebMessage
    {
        public string kind,id,url,text,request;
        public bool flag,back,forward;
    }
    // The game owns the process and its pipes. Web content has no C# bindings,
    // access to the virtual filesystem, or commands that launch desktop programs.
    public sealed class WebBrowserService : IDisposable
    {
        readonly ConcurrentQueue<string> incoming=new ConcurrentQueue<string>();
        readonly Dictionary<string,WebBrowserPage> pages=new Dictionary<string,WebBrowserPage>();
        Process process;
        bool disposed;
        public string Error { get; private set; }
        public bool Ready { get; private set; }
        public event Action<WebMessage> Message;
        public static string RuntimeDirectory
        {
            get
            {
#if UNITY_EDITOR
                string custom=Environment.GetEnvironmentVariable("PR_GAME_BROWSER_RUNTIME");
                return string.IsNullOrEmpty(custom)?Path.GetFullPath(Path.Combine(Application.dataPath,"../BrowserRuntime/Windows")):custom;
#else
                return Path.Combine(Application.dataPath,"BrowserRuntime");
#endif
            }
        }
        public WebBrowserService()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                string executable=Path.Combine(RuntimeDirectory,"PrGame.BrowserHost.exe");
                if(!File.Exists(executable))throw new FileNotFoundException("웹 엔진이 설치되지 않았어요. 프로젝트의 브라우저 설치 안내를 확인해 주세요.");
                string cache=Path.GetFullPath(Path.Combine(DesktopFileSystem.OverrideDirectory??Path.Combine(Application.persistentDataPath,"Desktop"),"WebCache"));
                var info=new ProcessStartInfo(executable)
                {
                    Arguments=Process.GetCurrentProcess().Id+" \""+cache+"\"",WorkingDirectory=RuntimeDirectory,
                    UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,
                    RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,
                    StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8
                };
                process=new Process{StartInfo=info,EnableRaisingEvents=true};
                process.OutputDataReceived+=(_,e)=>{if(e.Data!=null&&e.Data.StartsWith("PRWEB:",StringComparison.Ordinal))incoming.Enqueue(e.Data.Substring(6));};
                process.ErrorDataReceived+=(_,e)=>{};
                process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();
            }
            catch(Exception e){Error=e.Message;}
#else
            Error="실제 웹 탐색은 현재 Windows 버전에서 지원해요.";
#endif
        }
        public WebBrowserPage CreatePage()
        {
            if(disposed)throw new ObjectDisposedException(nameof(WebBrowserService));
            if(Error!=null)throw new InvalidOperationException(Error);
            var page=new WebBrowserPage(this);pages.Add(page.Id,page);return page;
        }
        public void Tick()
        {
            while(incoming.TryDequeue(out string json))
            {
                WebMessage message;
                try{message=JsonUtility.FromJson<WebMessage>(json);}catch{continue;}
                if(message.kind=="ready")Ready=true;
                if(message.kind=="fatal")Error=message.text;
                if(message.id!=null&&pages.TryGetValue(message.id,out var page))page.Receive(message);
                Message?.Invoke(message);
            }
            if(!disposed&&process!=null&&process.HasExited&&Error==null)Error="웹 엔진이 종료됐어요. 브라우저 창을 닫고 다시 열어 주세요.";
        }
        internal void Send(WebCommand command)
        {
            if(disposed||process==null||Error!=null)return;
            try
            {
                // ASCII wire encoding avoids depending on the player's Windows code page.
                string json=JsonUtility.ToJson(command);var encoded=new StringBuilder(json.Length);
                foreach(char c in json){if(c>127)encoded.Append("\\u").Append(((int)c).ToString("x4"));else encoded.Append(c);}
                process.StandardInput.WriteLine(encoded.ToString());process.StandardInput.Flush();
            }
            catch(Exception e){Error="웹 엔진 연결이 끊겼어요. "+e.Message;}
        }
        internal void Remove(WebBrowserPage page){pages.Remove(page.Id);Send(new WebCommand{type="close",id=page.Id});}
        public void Dispose()
        {
            if(disposed)return;
            Send(new WebCommand{type="quit"});disposed=true;
            foreach(var page in new List<WebBrowserPage>(pages.Values))page.Dispose();pages.Clear();
            var child=process;process=null;
            if(child!=null)Task.Run(()=>{try{child.StandardInput.Close();if(!child.WaitForExit(3000))child.Kill();}catch{}finally{child.Dispose();}});
        }
    }
    public sealed class WebBrowserPage : IDisposable
    {
        const long Capacity=32L+1920*1200*4;
        readonly WebBrowserService service;
        readonly MemoryMappedFile map;
        readonly MemoryMappedViewAccessor memory;
        readonly Mutex gate;
        byte[] pixels,flipped;
        int sequence,width=960,height=600;
        bool disposed,visible;
        public string Id { get; }=Guid.NewGuid().ToString("N");
        public string Url { get; private set; }="";
        public string Cursor { get; private set; }="Pointer";
        public bool CanBack { get; private set; }
        public bool CanForward { get; private set; }
        public bool Loading { get; private set; }
        public Texture2D Texture { get; private set; }
        public event Action<WebMessage> Message;
        public int Width=>width;
        public int Height=>height;
        internal WebBrowserPage(WebBrowserService service)
        {
            this.service=service;string name="PRGameWeb-"+Id;
            map=MemoryMappedFile.CreateNew(name,Capacity);memory=map.CreateViewAccessor();gate=new Mutex(false,name+"-gate");
            Send(new WebCommand{type="create",map=name,mutex=name+"-gate",width=width,height=height});
        }
        public void Send(WebCommand command){if(disposed)return;command.id=Id;service.Send(command);}
        public void Navigate(string url){Loading=true;Send(new WebCommand{type="navigate",url=url});}
        public void SetVisible(bool value){if(visible==value)return;visible=value;Send(new WebCommand{type="visible",flag=value});}
        public void Resize(int w,int h)
        {
            w=Mathf.Clamp(w,64,1920);h=Mathf.Clamp(h,64,1200);
            if(w==width&&h==height)return;width=w;height=h;Send(new WebCommand{type="resize",width=w,height=h});
        }
        internal void Receive(WebMessage message)
        {
            if(message.kind=="address")Url=message.url;
            if(message.kind=="loading"){Loading=message.flag;CanBack=message.back;CanForward=message.forward;}
            if(message.kind=="cursor")Cursor=message.text;
            Message?.Invoke(message);
        }
        public bool ReadFrame()
        {
            if(disposed)return false;bool held=false;
            try
            {
                try{held=gate.WaitOne(0);}catch(AbandonedMutexException){held=true;}
                if(!held)return false;int next=memory.ReadInt32(0);if(next==0||next==sequence)return false;
                int w=memory.ReadInt32(4),h=memory.ReadInt32(8);if(w<1||h<1||w>1920||h>1200)return false;
                int length=w*h*4;
                if(pixels==null||pixels.Length!=length){pixels=new byte[length];flipped=new byte[length];}
                memory.ReadArray(32,pixels,0,length);sequence=next;
                if(!Texture||Texture.width!=w||Texture.height!=h)
                {if(Texture)UnityEngine.Object.Destroy(Texture);Texture=new Texture2D(w,h,TextureFormat.BGRA32,false){name="Web page",filterMode=FilterMode.Bilinear};}
                for(int y=0;y<h;y++)Buffer.BlockCopy(pixels,y*w*4,flipped,(h-y-1)*w*4,w*4);
                Texture.LoadRawTextureData(flipped);Texture.Apply(false,false);return true;
            }
            finally{if(held)gate.ReleaseMutex();}
        }
        public void Dispose()
        {if(disposed)return;disposed=true;service.Remove(this);memory.Dispose();map.Dispose();gate.Dispose();if(Texture)UnityEngine.Object.Destroy(Texture);Texture=null;}
    }
}
