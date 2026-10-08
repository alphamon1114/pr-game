using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using CefSharp;
using CefSharp.Enums;
using CefSharp.Handler;
using CefSharp.OffScreen;
using CefSharp.Structs;
using Size=System.Drawing.Size;

namespace PrGame.BrowserHost;

// Native Chromium stays outside Unity. Only inherited pipes and randomly named
// per-tab shared memory are exposed to the parent, with no listening network port.
internal static class Program
{
    static readonly JsonSerializerOptions Json=new(){IncludeFields=true};
    static readonly object OutputLock=new();
    static readonly Dictionary<string,BrowserPage> Pages=new();
    static bool exiting;
    [STAThread]
    static int Main(string[] args)
    {
        Console.InputEncoding=new UTF8Encoding(false);Console.OutputEncoding=new UTF8Encoding(false);
        try
        {
            if(args.Length!=2||!int.TryParse(args[0],out int parentId))return 2;
            var parent=Process.GetProcessById(parentId);
            _=Task.Run(()=>{parent.WaitForExit();if(!exiting)Environment.Exit(0);});
            string cache=Path.GetFullPath(args[1]);Directory.CreateDirectory(cache);
            Cef.EnableWaitForBrowsersToClose();
            var settings=new CefSettings
            {
                WindowlessRenderingEnabled=true,MultiThreadedMessageLoop=true,
                RootCachePath=cache,CachePath=Path.Combine(cache,"Profile"),
                LogFile=Path.Combine(cache,"cef.log"),LogSeverity=LogSeverity.Error,
                Locale="ko",AcceptLanguageList="ko-KR,ko,en-US,en"
            };
            if(!Cef.Initialize(settings,performDependencyCheck:true,browserProcessHandler:null))
                throw new InvalidOperationException("Chromium initialization failed: "+Cef.GetExitCode());
            Emit(new {kind="ready",text=Cef.CefSharpVersion});
            string? line;
            while((line=Console.ReadLine())!=null)
            {
                var command=JsonSerializer.Deserialize<Command>(line,Json);
                if(command==null)continue;if(command.type=="quit")break;
                try{Handle(command);}catch(Exception e){Emit(new {kind="error",id=command.id,text=e.Message});}
            }
            exiting=true;foreach(var page in Pages.Values)page.Dispose();Pages.Clear();
            Cef.WaitForBrowsersToClose();Cef.Shutdown();return 0;
        }
        catch(Exception e){Emit(new {kind="fatal",text=e.Message});return 1;}
    }
    internal static bool WebUrl(string? url)=>Uri.TryCreate(url,UriKind.Absolute,out var uri)&&(uri.Scheme=="https"||uri.Scheme=="http");
    internal static void Emit(object message)
    {lock(OutputLock){Console.WriteLine("PRWEB:"+JsonSerializer.Serialize(message,Json));Console.Out.Flush();}}
    static void Handle(Command c)
    {
        if(c.type=="create")
        {if(Pages.ContainsKey(c.id)||Pages.Count>=8)return;Pages[c.id]=new BrowserPage(c);return;}
        if(!Pages.TryGetValue(c.id,out var page))return;
        if(c.type=="close"){Pages.Remove(c.id);page.Dispose();return;}page.Handle(c);
    }
}
internal sealed class Command
{
    public string type="",id="",url="",text="",map="",mutex="",request="";
    public int width=960,height=600,x,y,button,count=1,dx,dy,key,modifiers;
    public bool flag;
}
internal sealed class BrowserPage : IDisposable
{
    readonly string id;
    readonly ChromiumWebBrowser browser;
    readonly SharedRenderer renderer;
    readonly ConcurrentQueue<Command> pending=new();
    volatile bool initialized,disposed;
    public BrowserPage(Command c)
    {
        id=c.id;
        browser=new ChromiumWebBrowser("about:blank",new BrowserSettings{WindowlessFrameRate=24},automaticallyCreateBrowser:false);
        renderer=new SharedRenderer(browser,c);browser.RenderHandler=renderer;
        browser.RequestHandler=new NavigationPolicy(id);browser.LifeSpanHandler=new PopupPolicy(id);
        browser.MenuHandler=new NoNativeMenu();browser.DownloadHandler=new NoNativeDownload(id);browser.DialogHandler=new NoNativeFilePicker(id);
        browser.AddressChanged+=(_,e)=>Program.Emit(new {kind="address",id,url=e.Address});
        browser.TitleChanged+=(_,e)=>Program.Emit(new {kind="title",id,text=e.Title});
        browser.LoadingStateChanged+=(_,e)=>Program.Emit(new {kind="loading",id,flag=e.IsLoading,back=e.CanGoBack,forward=e.CanGoForward});
        browser.LoadError+=(_,e)=>{if(e.Frame.IsMain&&e.ErrorCode!=CefErrorCode.Aborted)Program.Emit(new {kind="loadError",id,text=e.ErrorText,url=e.FailedUrl});};
        browser.BrowserInitialized+=(_,_)=>
        {
            if(!browser.IsBrowserInitialized||disposed)return;initialized=true;
            Program.Emit(new {kind="created",id});while(pending.TryDequeue(out var next))Handle(next);
        };
        browser.Size=new Size(Math.Clamp(c.width,64,1920),Math.Clamp(c.height,64,1200));browser.CreateBrowser();
    }
    public void Handle(Command c)
    {
        if(disposed)return;if(!initialized){pending.Enqueue(c);return;}
        var host=browser.GetBrowserHost();var flags=(CefEventFlags)c.modifiers;
        switch(c.type)
        {
            case "navigate":if(Program.WebUrl(c.url))browser.Load(c.url);break;
            case "back":browser.Back();break;
            case "forward":browser.Forward();break;
            case "reload":browser.Reload();break;
            case "stop":browser.Stop();break;
            case "resize":browser.Size=new Size(Math.Clamp(c.width,64,1920),Math.Clamp(c.height,64,1200));break;
            case "visible":host.WasHidden(!c.flag);if(c.flag)host.Invalidate(PaintElementType.View);break;
            case "focus":host.SetFocus(c.flag);if(!c.flag)host.ImeCancelComposition();break;
            case "move":host.SendMouseMoveEvent(new MouseEvent(c.x,c.y,flags),c.flag);break;
            case "down":case "up":host.SendMouseClickEvent(new MouseEvent(c.x,c.y,flags),(MouseButtonType)c.button,c.type=="up",Math.Clamp(c.count,1,3));break;
            case "wheel":host.SendMouseWheelEvent(new MouseEvent(c.x,c.y,flags),c.dx,c.dy);break;
            case "keyDown":case "keyUp":
                host.SendKeyEvent(new KeyEvent{WindowsKeyCode=c.key,Type=c.type=="keyDown"?KeyEventType.RawKeyDown:KeyEventType.KeyUp,Modifiers=flags,IsSystemKey=(flags&CefEventFlags.AltDown)!=0});
                if(c.type=="keyDown"&&c.key==13)host.SendKeyEvent(new KeyEvent{WindowsKeyCode=13,Type=KeyEventType.Char,Modifiers=flags});break;
            case "text":
                // Unity supplies committed Unicode separately from the IME preview.
                // Remove that preview, then deliver character events exactly once.
                host.ImeCancelComposition();
                foreach(char character in c.text)host.SendKeyEvent(new KeyEvent{WindowsKeyCode=character,Type=KeyEventType.Char});break;
            case "composition":
                if(c.text.Length==0)host.ImeCancelComposition();
                else host.ImeSetComposition(c.text,Array.Empty<CompositionUnderline>(),null,new CefSharp.Structs.Range(c.text.Length,c.text.Length));break;
            case "evaluate":
                _=browser.EvaluateScriptAsync(c.text).ContinueWith(task=>
                {if(task.IsCompletedSuccessfully)Program.Emit(new {kind="evaluation",id,request=c.request,text=Convert.ToString(task.Result.Result),flag=task.Result.Success});});break;
        }
    }
    public void Dispose(){if(disposed)return;disposed=true;renderer.ReleaseStorage();browser.Dispose();}
}
internal sealed class SharedRenderer : DefaultRenderHandler
{
    readonly string id;
    readonly MemoryMappedFile map;
    readonly MemoryMappedViewAccessor view;
    readonly Mutex gate;
    readonly object paintLock=new();
    byte[] pixels=Array.Empty<byte>(),popup=Array.Empty<byte>(),composite=Array.Empty<byte>();
    int width,height,popupWidth,popupHeight,sequence;
    CefSharp.Structs.Rect popupRect;
    bool popupVisible,disposed;
    public SharedRenderer(ChromiumWebBrowser browser,Command c):base(browser)
    {id=c.id;map=MemoryMappedFile.OpenExisting(c.map);view=map.CreateViewAccessor();gate=Mutex.OpenExisting(c.mutex);}
    public override void OnPaint(PaintElementType type,CefSharp.Structs.Rect dirtyRect,IntPtr buffer,int w,int h)
    {
        lock(paintLock)
        {
            if(disposed||w<=0||h<=0||w>1920||h>1200)return;int length=w*h*4;
            if(type==PaintElementType.View)
            {width=w;height=h;if(pixels.Length!=length){pixels=new byte[length];composite=new byte[length];}Marshal.Copy(buffer,pixels,0,length);}
            else{popupWidth=w;popupHeight=h;if(popup.Length!=length)popup=new byte[length];Marshal.Copy(buffer,popup,0,length);}
            Publish();
        }
    }
    public override void OnPopupShow(bool show){lock(paintLock){popupVisible=show;if(!show)Publish();}}
    public override void OnPopupSize(CefSharp.Structs.Rect rect){lock(paintLock){popupRect=rect;}}
    public override void OnCursorChange(IntPtr cursor,CursorType type,CursorInfo info)=>Program.Emit(new {kind="cursor",id,text=type.ToString()});
    void Publish()
    {
        if(disposed||pixels.Length==0)return;bool held=false;
        try
        {
            try{held=gate.WaitOne(8);}catch(AbandonedMutexException){held=true;}if(!held)return;
            Buffer.BlockCopy(pixels,0,composite,0,pixels.Length);
            if(popupVisible&&popup.Length==popupWidth*popupHeight*4)
            {
                int left=Math.Max(0,popupRect.X),top=Math.Max(0,popupRect.Y);
                int right=Math.Min(width,popupRect.X+popupWidth),bottom=Math.Min(height,popupRect.Y+popupHeight);
                if(right>left)for(int y=top;y<bottom;y++)Buffer.BlockCopy(popup,((y-popupRect.Y)*popupWidth+left-popupRect.X)*4,composite,(y*width+left)*4,(right-left)*4);
            }
            view.Write(4,width);view.Write(8,height);view.WriteArray(32,composite,0,composite.Length);view.Write(0,++sequence);
        }
        finally{if(held)gate.ReleaseMutex();}
    }
    public void ReleaseStorage(){lock(paintLock){if(disposed)return;disposed=true;view.Dispose();map.Dispose();gate.Dispose();}}
}
internal sealed class NavigationPolicy(string id) : RequestHandler
{
    protected override bool OnBeforeBrowse(IWebBrowser control,IBrowser browser,IFrame frame,IRequest request,bool gesture,bool redirect)
        =>!Program.WebUrl(request.Url)&&request.Url!="about:blank";
    protected override bool OnOpenUrlFromTab(IWebBrowser control,IBrowser browser,IFrame frame,string url,WindowOpenDisposition disposition,bool gesture)
    {if(gesture&&Program.WebUrl(url))Program.Emit(new {kind="popup",id,url});return true;}
}
internal sealed class PopupPolicy(string id) : LifeSpanHandler
{
    protected override bool OnBeforePopup(IWebBrowser control,IBrowser browser,IFrame frame,string url,string target,WindowOpenDisposition disposition,bool gesture,IPopupFeatures features,IWindowInfo info,IBrowserSettings settings,ref bool noJavascriptAccess,out IWebBrowser newBrowser)
    {newBrowser=null!;if(gesture&&Program.WebUrl(url))Program.Emit(new {kind="popup",id,url});return true;}
}
internal sealed class NoNativeMenu : ContextMenuHandler
{
    protected override void OnBeforeContextMenu(IWebBrowser control,IBrowser browser,IFrame frame,IContextMenuParams parameters,IMenuModel model)=>model.Clear();
}
internal sealed class NoNativeDownload(string id) : DownloadHandler
{
    protected override bool CanDownload(IWebBrowser control,IBrowser browser,string url,string method)
    {Program.Emit(new {kind="notice",id,text="웹 파일 다운로드는 아직 지원하지 않아요."});return false;}
}
internal sealed class NoNativeFilePicker(string id) : DialogHandler
{
    protected override bool OnFileDialog(IWebBrowser control,IBrowser browser,CefFileDialogMode mode,string title,string path,IReadOnlyCollection<string> filters,IReadOnlyCollection<string> extensions,IReadOnlyCollection<string> descriptions,IFileDialogCallback callback)
    {callback.Cancel();Program.Emit(new {kind="notice",id,text="PC 파일 업로드는 아직 지원하지 않아요."});return true;}
}
