using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrGame.Editor
{
    public static class WebBrowserVerification
    {
        static int step=-1;
        static double deadline;
        static TcpListener server;
        static string origin;
        static WebBrowserView first;
        static readonly Dictionary<string,string> answers=new Dictionary<string,string>();
        static void Assert(bool value,string message){if(!value)throw new Exception(message);}
        static void Advance(){step++;deadline=EditorApplication.timeSinceStartup+45;}
        static bool Loaded(WebBrowserView view,string url)=>view!=null&&view.Page.Texture&&view.Page.Url==url&&!view.Page.Loading;
        static void Observe(WebBrowserPage page)=>page.Message+=message=>{if(message.kind=="evaluation")answers[message.request]=message.flag?message.text:"ERROR";};
        static void Evaluate(WebBrowserPage page,string key,string script){answers.Remove(key);page.Send(new WebCommand{type="evaluate",request=key,text=script});}
        public static bool Tick(PortfolioDesktop os,MonitorSurface surface,Action<string> capture)
        {
            if(step>=0&&EditorApplication.timeSinceStartup>deadline)throw new Exception("Web QA timed out at "+step+": "+os.Root.Q<Label>("BrowserStatus")?.text);
            var root=os.Root;var view=root.Q<WebBrowserView>();
            switch(step)
            {
                case -1:
                    os.OpenApp("browser");Assert(root.Q<TextField>("BrowserAddress").value==DesktopBrowserDefaults.HomeUrl,"Browser did not start at Naver");Advance();break;
                case 0:
                    if(!Loaded(view,DesktopBrowserDefaults.HomeUrl))return false;
                    capture("24-naver-home");
                    Assert(PortfolioDesktop.NormalizeBrowserAddress("조건희 포트폴리오").StartsWith("https://www.google.com/search?q="),"Search did not become HTTPS URL");
                    Assert(PortfolioDesktop.NormalizeBrowserAddress("example.com")=="https://example.com/","Bare domain normalization failed");
                    Assert(PortfolioDesktop.NormalizeBrowserAddress("file:///C:/Windows").StartsWith("https://www.google.com/search?q="),"Local file scheme was allowed");
                    StartFixture();os.Navigate("https://example.com/");Advance();break;
                case 1:
                    if(!Loaded(view,"https://example.com/"))return false;
                    Assert(view.Page.Texture.width>600,"Web texture was not uploaded to Unity");
                    capture("22-live-internet");first=view;Observe(first.Page);os.Navigate(origin+"/fixture");Advance();break;
                case 2:
                    if(!Loaded(view,origin+"/fixture"))return false;
                    PointerClick(view,new Vector2(100,125));Assert(os.IsTyping&&view.HasKeyboardFocus,"Web input did not own keyboard focus");
                    view.CommitText("조건희 한글 검색");Advance();break;
                case 3:
                    Evaluate(view.Page,"input","document.querySelector('#q').value");Advance();break;
                case 4:
                    if(!answers.ContainsKey("input"))return false;
                    Assert(answers["input"]=="조건희 한글 검색","Korean text lost between Unity and Chromium");
                    PointerClick(view,new Vector2(80,205));Advance();break;
                case 5:
                    Evaluate(view.Page,"clicked","document.title");
                    var point=PixelPoint(view,new Vector2(700,350));var scroll=new Event{type=EventType.ScrollWheel,delta=new Vector2(0,12),mousePosition=point};
                    using(var evt=WheelEvent.GetPooled(scroll))view.SendEvent(evt);Advance();break;
                case 6:
                    if(!answers.ContainsKey("clicked"))return false;Assert(answers["clicked"]=="Clicked","Web button did not receive mapped click");
                    Evaluate(view.Page,"scroll","window.scrollY");Advance();break;
                case 7:
                    if(!answers.ContainsKey("scroll"))return false;Assert(double.Parse(answers["scroll"])>100,"Web scroll was not delivered");
                    Click(root.Q<Button>("NewBrowserTab"));os.Navigate(origin+"/second");Advance();break;
                case 8:
                    if(!Loaded(view,origin+"/second"))return false;
                    Assert(view!=first,"Tabs share one live page");Click(root.Q<Button>("BrowserTab-0"));Advance();break;
                case 9:
                    Assert(root.Q<WebBrowserView>()==first,"Tab switch recreated the browser page");
                    Evaluate(first.Page,"retained","document.querySelector('#q').value+'|'+window.scrollY");Advance();break;
                case 10:
                    if(!answers.ContainsKey("retained"))return false;
                    Assert(answers["retained"].StartsWith("조건희 한글 검색|")&&!answers["retained"].EndsWith("|0"),"Tab switch lost entered text or scroll");
                    os.Navigate(origin+"/second");Advance();break;
                case 11:
                    if(!Loaded(view,origin+"/second"))return false;Click(root.Q<Button>("BrowserBack"));Advance();break;
                case 12:
                    if(!Loaded(view,origin+"/fixture"))return false;Click(root.Q<Button>("BrowserForward"));Advance();break;
                case 13:
                    if(!Loaded(view,origin+"/second"))return false;Click(root.Q<Button>("browser-maximize"));Advance();break;
                case 14:
                    int expected=Mathf.Clamp(Mathf.RoundToInt(view.contentRect.width*1.25f),64,1920);
                    if(view.Page.Texture.width!=expected)return false;
                    Assert(Mathf.Abs(view.Page.Texture.height-view.contentRect.height*1.25f)<2,"Web surface did not resize with window");
                    capture("23-live-browser-maximized");Click(root.Q<Button>("browser-maximize"));view.Focus();os.SuspendInput();
                    Assert(!view.HasKeyboardFocus,"Leaving computer did not release browser typing");
                    os.Lock();Assert(!view.HasKeyboardFocus,"Lock retained browser focus");Assert(os.TryLogin("1114"),"Password unlock failed");Advance();break;
                case 15:
                    Click(root.Q<Button>("CloseBrowserTab-1"));Assert(root.Query(className:"browser-tab").ToList().Count==1,"Closing background web tab failed");
                    var bookmark=root.Q("BrowserBookmarks").Query<Button>().ToList().Find(button=>button.text=="VARCO 3D");
                    Click(bookmark);Assert(root.Q<TextField>("BrowserAddress").value==DesktopBrowserDefaults.VarcoUrl,"VARCO bookmark opened the wrong URL");Advance();break;
                case 16:
                    Click(root.Q<Button>("BrowserHome"));Assert(root.Q<TextField>("BrowserAddress").value==DesktopBrowserDefaults.HomeUrl,"Home button did not navigate to Naver");Advance();break;
                case 17:
                    if(!Loaded(view,DesktopBrowserDefaults.HomeUrl))return false;
                    os.GetWindow("browser").Close();Assert(root.Q<WebBrowserView>()==null,"Closed browser retained live view");
                    server.Stop();server=null;
                    File.WriteAllText("Logs/OSQA/web-browser-results.txt","PASS: free CEF 152 / CefSharp 152.0.100; Naver startup and home button; VARCO 3D bookmark navigation; live public HTTPS rendered to monitor; URL/search normalization; local file scheme exclusion; real UI pointer mapping; Unicode Korean text; web button click; scrolling; independent tabs retaining input/scroll; native back/forward; resize/maximize; keyboard focus release and lock; tab/window close. Host smoke test additionally covers IME composition/commit.\n");
                    Debug.Log("PR_GAME_LIVE_WEB_VERIFIED");return true;
            }
            return false;
        }
        static Vector2 PixelPoint(WebBrowserView view,Vector2 pixel)=>view.LocalToWorld(new Vector2(pixel.x/view.Page.Width*view.contentRect.width,pixel.y/view.Page.Height*view.contentRect.height));
        static void PointerClick(WebBrowserView view,Vector2 pixel)
        {
            var point=PixelPoint(view,pixel);Assert(view.panel.Pick(point)==view,"Web click was covered by OS chrome");
            SendClick(view,point);
        }
        static void Click(Button button){Assert(button!=null,"Missing browser button");SendClick(button,button.worldBound.center);}
        static void SendClick(VisualElement target,Vector2 point)
        {
            var input=new Event{button=0,type=EventType.MouseDown,mousePosition=point,clickCount=1};
            using(var e=PointerDownEvent.GetPooled(input))target.SendEvent(e);input.type=EventType.MouseUp;
            using(var e=PointerUpEvent.GetPooled(input))target.SendEvent(e);
        }
        static void StartFixture()
        {
            server=new TcpListener(IPAddress.Loopback,0);server.Start();origin="http://127.0.0.1:"+((IPEndPoint)server.LocalEndpoint).Port;
            var listener=server;
            new Thread(()=>
            {
                try
                {
                    while(true)
                    using(var client=listener.AcceptTcpClient())
                    using(var stream=client.GetStream())
                    using(var reader=new StreamReader(stream,Encoding.ASCII,false,1024,true))
                    {
                        try
                        {
                        string request=reader.ReadLine();string header;while(!string.IsNullOrEmpty(header=reader.ReadLine())){}
                        bool second=request?.Contains("/second")==true;
                        string html=second?"<!doctype html><title>Second page</title><h1>Second page</h1>":
                            "<!doctype html><meta charset='utf-8'><title>Web controls test</title><style>body{margin:0;background:#e9edf2;font:24px sans-serif}input{position:absolute;left:20px;top:100px;width:400px;height:45px}button{position:absolute;left:20px;top:180px;width:200px;height:50px}main{height:2200px}</style><main><h1>실제 웹 브라우저</h1><input id='q'><button onclick=\"document.title='Clicked'\">클릭 확인</button></main>";
                        byte[] body=Encoding.UTF8.GetBytes(html);byte[] head=Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: "+body.Length+"\r\nConnection: close\r\n\r\n");
                        stream.Write(head,0,head.Length);stream.Write(body,0,body.Length);
                        }
                        catch(IOException){} // Chromium may cancel favicon requests when a tab closes.
                    }
                }
                catch(SocketException){}catch(ObjectDisposedException){}
                catch(InvalidOperationException){} // A stopped listener may reject the next Accept call.
            }){IsBackground=true,Name="Web verification fixture"}.Start();
        }
    }
}
