using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrGame
{
    public sealed partial class PortfolioDesktop
    {
        sealed class BrowserTab
        {
            public readonly List<string> entries;
            public BrowserTab(string initialUrl="local://newtab"){entries=new List<string>{initialUrl};}
            public int index;public string Url=>entries[index];
            public string title="",error="";
            public bool navigating;
            public WebBrowserPage web;public WebBrowserView view;
        }
        readonly List<BrowserTab> browserTabs=new List<BrowserTab>();
        int browserTabIndex;
        VisualElement tabStrip,browserPage,browserSide,browserViewport,browserBookmarks;
        ScrollView browserScroll;
        Label browserStatus;
        Button browserBack,browserForward;
        WebBrowserService webBrowser;
        TextField address;
        string sideMode;
        void BuildBrowser(VisualElement parent)
        {
            if(browserTabs.Count==0)browserTabs.Add(new BrowserTab(DesktopBrowserDefaults.HomeUrl));
            tabStrip=El(parent,"browser-tabs");
            var nav=El(parent,"toolbar browser-nav");
            browserBack=IconBtn(nav,"arrow-left","뒤로",()=>BrowseHistory(-1),"","BrowserBack");
            browserForward=IconBtn(nav,"arrow-right","앞으로",()=>BrowseHistory(1),"","BrowserForward");
            IconBtn(nav,"rotate-cw","새로 고침",ReloadBrowser,"","BrowserReload");IconBtn(nav,"house","홈",()=>Navigate(DesktopBrowserDefaults.HomeUrl),"","BrowserHome");
            address=InputField(nav,"BrowserAddress","검색 또는 주소 입력");address.AddToClassList("omnibox");
            address.RegisterCallback<KeyDownEvent>(evt=>{if(evt.keyCode==UnityEngine.KeyCode.Return){Navigate(address.value);evt.StopPropagation();}});
            IconBtn(nav,"star","북마크",()=>{string url=browserTabs[browserTabIndex].Url;if(Files.Data.bookmarks.Contains(url))Files.Data.bookmarks.Remove(url);else Files.Data.bookmarks.Add(url);Files.Touch();RenderBrowserBookmarks();RenderBrowserSide();});
            IconBtn(nav,"notebook-pen","메모 열기",()=>OpenApp("notes"));
            browserBookmarks=El(parent,"bookmarks row","BrowserBookmarks");browserBookmarks.style.overflow=Overflow.Hidden;RenderBrowserBookmarks();
            var body=El(parent,"app-layout grow");browserViewport=El(body,"browser-viewport grow");
            browserScroll=new ScrollView();browserScroll.style.flexGrow=1;browserViewport.Add(browserScroll);browserPage=El(browserScroll,"browser-page");
            browserSide=El(body,"browser-side");Visible(browserSide,false);
            var rail=El(body,"browser-rail");
            IconBtn(rail,"search","통합 검색",()=>ShowSearch());
            IconBtn(rail,"history","방문 기록",()=>ToggleBrowserSide("history"),"","BrowserHistory");IconBtn(rail,"star","북마크",()=>ToggleBrowserSide("bookmarks"));
            IconBtn(rail,"download","다운로드",()=>{currentFolder="downloads";OpenApp("explorer");});IconBtn(rail,"notebook-pen","메모",()=>OpenApp("notes"));
            El(rail,"spacer");IconBtn(rail,"settings-2","설정",()=>OpenApp("settings"));
            browserStatus=Text(parent,"","statusbar note-file-status");browserStatus.name="BrowserStatus";RenderBrowser();
        }
        public static string NormalizeBrowserAddress(string input)
        {
            string value=(input??"").Trim();if(value.Length==0)return "local://newtab";
            if(new[]{"local://newtab","local://documents","local://archive","local://history"}.Contains(value))return value;
            if(Uri.TryCreate(value,UriKind.Absolute,out var absolute)&&(absolute.Scheme=="http"||absolute.Scheme=="https"))return absolute.AbsoluteUri;
            if(!value.Any(char.IsWhiteSpace)&&value.Contains(".")&&!value.Contains("://")&&Uri.TryCreate("https://"+value,UriKind.Absolute,out var domain))return domain.AbsoluteUri;
            return "https://www.google.com/search?q="+Uri.EscapeDataString(value);
        }
        static bool IsWebAddress(string url)=>url.StartsWith("https://",StringComparison.OrdinalIgnoreCase)||url.StartsWith("http://",StringComparison.OrdinalIgnoreCase);
        public void Navigate(string url)
        {
            if(locked)return;if(!windows.ContainsKey("browser"))OpenApp("browser");
            url=NormalizeBrowserAddress(url);
            var tab=browserTabs[browserTabIndex];if(tab.index<tab.entries.Count-1)tab.entries.RemoveRange(tab.index+1,tab.entries.Count-tab.index-1);
            tab.entries.Add(url);tab.index=tab.entries.Count-1;tab.title="";tab.error="";
            RecordBrowserHistory(url);RenderBrowser();
        }
        void RecordBrowserHistory(string url)
        {
            if(url=="local://newtab"||url=="about:blank")return;
            Files.Data.history.Remove(url);Files.Data.history.Insert(0,url);if(Files.Data.history.Count>100)Files.Data.history.RemoveAt(100);Files.Touch();
        }
        void BrowseHistory(int direction)
        {
            var tab=browserTabs[browserTabIndex];int next=tab.index+direction;if(next<0||next>=tab.entries.Count)return;
            bool native=IsWebAddress(tab.Url)&&IsWebAddress(tab.entries[next])&&tab.web!=null&&
                (direction<0?tab.web.CanBack:tab.web.CanForward);
            tab.index=next;tab.error="";
            if(native){tab.navigating=true;tab.web.Send(new WebCommand{type=direction<0?"back":"forward"});}
            RenderBrowser(!native);
        }
        void ReloadBrowser()
        {
            var tab=browserTabs[browserTabIndex];tab.error="";
            if(IsWebAddress(tab.Url)&&tab.web!=null){tab.navigating=true;tab.web.Send(new WebCommand{type="reload"});UpdateBrowserStatus();}
            else RenderBrowser();
        }
        static string PageTitle(string url)
        {switch(url.TrimEnd('/')){case "https://www.naver.com":return "네이버";case DesktopBrowserDefaults.VarcoUrl:return "VARCO 3D";case "local://newtab":return "새 탭";case "local://documents":return "문서";case "local://archive":return "보관함";case "local://history":return "방문 기록";default:return url;}}
        void RenderBrowserBookmarks()
        {
            if(browserBookmarks==null)return;browserBookmarks.Clear();
            foreach(string entry in Files.Data.bookmarks.ToArray())
            {
                string url=entry;var button=Btn(browserBookmarks,PageTitle(url),()=>Navigate(url),"text-button");button.tooltip=url;
            }
        }
        void RenderBrowser(bool loadWeb=true)
        {
            if(browserPage==null)return;RenderBrowserTabs();browserPage.Clear();
            foreach(var existing in browserTabs)existing.view?.RemoveFromHierarchy();
            var current=browserTabs[browserTabIndex];string url=current.Url;
            address.SetValueWithoutNotify(url=="local://newtab"?"":url);
            Visible(browserScroll,!IsWebAddress(url));
            if(IsWebAddress(url))
            {
                try
                {
                    if(webBrowser==null)webBrowser=new WebBrowserService();
                    if(webBrowser.Error!=null)throw new InvalidOperationException(webBrowser.Error);
                    if(current.web==null)
                    {
                        current.web=webBrowser.CreatePage();current.view=new WebBrowserView(current.web,CanUseWebBrowser);
                        current.web.Message+=message=>OnWebMessage(current,message);
                    }
                    browserViewport.Add(current.view);current.view.Resize();
                    if(loadWeb&&current.web.Url!=url){current.navigating=true;current.web.Navigate(url);}
                }
                catch(Exception e){current.error=e.Message;Visible(browserScroll,true);Text(browserPage,"웹페이지를 열지 못했어요.","heading section-heading");Text(browserPage,e.Message,"muted wrap");}
            }
            else RenderLocalBrowser(url);
            RenderBrowserSide();UpdateBrowserStatus();
        }
        void RenderBrowserTabs()
        {
            tabStrip.Clear();
            for(int i=0;i<browserTabs.Count;i++)
            {
                int index=i;var tab=El(tabStrip,"browser-tab");tab.EnableInClassList("selected",index==browserTabIndex);
                var select=Btn(tab,"",()=>{browserTabIndex=index;RenderBrowser(false);},"browser-tab-title","BrowserTab-"+i);Icon(select,"globe");
                Text(select,string.IsNullOrEmpty(browserTabs[i].title)||!IsWebAddress(browserTabs[i].Url)?PageTitle(browserTabs[i].Url):browserTabs[i].title);
                IconBtn(tab,"x","탭 닫기",()=>CloseBrowserTab(index),"","CloseBrowserTab-"+i);
            }
            IconBtn(tabStrip,"plus","새 탭",NewBrowserTab,"","NewBrowserTab");
        }
        void NewBrowserTab()
        {
            if(browserTabs.Count>=8){Notify("브라우저","탭은 최대 8개까지 열 수 있어요.");return;}
            browserTabs.Add(new BrowserTab());browserTabIndex=browserTabs.Count-1;RenderBrowser();
        }
        void CloseBrowserTab(int index)
        {
            var tab=browserTabs[index];tab.view?.RemoveFromHierarchy();tab.web?.Dispose();
            if(browserTabs.Count==1){browserTabs[0]=new BrowserTab();browserTabIndex=0;}
            else{browserTabs.RemoveAt(index);if(index<browserTabIndex)browserTabIndex--;else if(index==browserTabIndex)browserTabIndex=Math.Min(index,browserTabs.Count-1);}
            RenderBrowser(false);
        }
        void RenderLocalBrowser(string url)
        {
            if(url=="local://newtab")
            {
                var welcome=El(browserPage,"new-tab");Text(welcome,DateTime.Now.ToString("HH:mm"),"browser-time");Text(welcome,DateTime.Now.ToString("M월 d일 dddd",new System.Globalization.CultureInfo("ko-KR")),"muted");
                var search=InputField(welcome,"BrowserNewTabSearch","검색하거나 주소를 입력하세요");search.AddToClassList("newtab-search");
                search.RegisterCallback<KeyDownEvent>(evt=>{if(evt.keyCode==UnityEngine.KeyCode.Return){Navigate(search.value);evt.StopPropagation();}});
                var shortcuts=El(welcome,"row");
                foreach(string route in new[]{"local://documents","local://archive","local://history"})
                {string path=route;var shortcut=Btn(shortcuts,"",()=>Navigate(path),"web-shortcut");Icon(shortcut,route.EndsWith("history")?"history":"folder","app-plate");Text(shortcut,PageTitle(route));}
            }
            else if(url=="local://documents" || url=="local://archive")
            {
                Text(browserPage,PageTitle(url),"heading section-heading");
                foreach(var file in Files.Children(url.EndsWith("documents")?"documents":"archive"))
                {string id=file.id;var link=Btn(browserPage,"",()=>OpenVirtualFile(id),"web-link");Icon(link,file.folder?"folder":"file-text");Text(link,file.name);}
                if(url.EndsWith("archive")&&!Files.Children("archive").Any())Text(browserPage,"보관함이 비어 있어요.","muted");
            }
            else if(url=="local://history")
            {
                Text(browserPage,"방문 기록","heading section-heading");
                foreach(string entry in Files.Data.history.ToArray()){string path=entry;Btn(browserPage,PageTitle(path),()=>Navigate(path),"web-link");}
            }
            else
            {
                Text(browserPage,"페이지를 찾을 수 없어요.","heading section-heading");
            }
        }
        bool CanUseWebBrowser()
        {
            var surface=GetComponent<MonitorSurface>();
            return !locked&&(Application.isFocused||Application.isBatchMode)&&(!surface||!surface.seatedView||surface.seatedView.CanInteractWithComputer);
        }
        void TickWebBrowser()
        {
            webBrowser?.Tick();var window=GetWindow("browser");
            for(int i=0;i<browserTabs.Count;i++)
            {
                var tab=browserTabs[i];bool visible=window!=null&&!window.Minimized&&i==browserTabIndex&&IsWebAddress(tab.Url)&&CanUseWebBrowser();
                tab.view?.Tick(visible);
            }
            if(window!=null)UpdateBrowserStatus();
        }
        void OnWebMessage(BrowserTab tab,WebMessage message)
        {
            if(!browserTabs.Contains(tab))return;
            if(message.kind=="address"&&IsWebAddress(message.url))
            {
                if(!IsWebAddress(tab.Url))return;
                if(tab.navigating)tab.entries[tab.index]=message.url;
                else if(tab.Url!=message.url)
                {
                    if(tab.index<tab.entries.Count-1)tab.entries.RemoveRange(tab.index+1,tab.entries.Count-tab.index-1);
                    tab.entries.Add(message.url);tab.index=tab.entries.Count-1;tab.navigating=tab.web.Loading;
                }
                RecordBrowserHistory(message.url);
                if(tab==browserTabs[browserTabIndex])address.SetValueWithoutNotify(message.url);
            }
            if(message.kind=="title"){tab.title=message.text;RenderBrowserTabs();}
            if(message.kind=="loading"){if(!message.flag)tab.navigating=false;else tab.error="";}
            if(message.kind=="loadError"||message.kind=="error")tab.error="페이지를 불러오지 못했어요. 연결을 확인하고 새로 고침해 주세요. ("+message.text+")";
            if(message.kind=="notice")Notify("브라우저",message.text);
            if(message.kind=="popup"&&browserTabs.Count<8){NewBrowserTab();Navigate(message.url);}
            UpdateBrowserStatus();
        }
        void UpdateBrowserStatus()
        {
            if(browserStatus==null||browserTabs.Count==0)return;var tab=browserTabs[browserTabIndex];
            string error=IsWebAddress(tab.Url)?webBrowser?.Error??tab.error:null;
            browserStatus.text=!string.IsNullOrEmpty(error)?error:IsWebAddress(tab.Url)?(tab.web?.Loading==true?"불러오는 중… · ":"연결됨 · ")+tab.Url:"게임 안의 페이지";
            browserStatus.tooltip=browserStatus.text;browserBack?.SetEnabled(tab.index>0);browserForward?.SetEnabled(tab.index<tab.entries.Count-1);
        }
        void DisposeWebBrowser()
        {
            foreach(var tab in browserTabs)tab.view?.ReleaseInput();webBrowser?.Dispose();webBrowser=null;
            browserTabs.Clear();browserTabIndex=0;
        }
        void ToggleBrowserSide(string mode){sideMode=sideMode==mode?null:mode;RenderBrowserSide();}
        void RenderBrowserSide()
        {
            if(browserSide==null)return;Visible(browserSide,sideMode!=null);browserSide.Clear();if(sideMode==null)return;
            var header=El(browserSide,"row");Text(header,sideMode=="history"?"방문 기록":"북마크","heading grow");IconBtn(header,"x","사이드바 닫기",()=>{sideMode=null;RenderBrowserSide();});
            var scroll=new ScrollView();scroll.style.flexGrow=1;browserSide.Add(scroll);
            var entries=sideMode=="history"?Files.Data.history:Files.Data.bookmarks;
            if(entries.Count==0)Text(scroll,"아직 기록이 없어요.","muted wrap");
            foreach(string entry in entries.ToArray())
            {
                string route=entry;var row=El(scroll,"history-entry row");Btn(row,PageTitle(route),()=>Navigate(route),"grow text-button");
                IconBtn(row,"x","기록 삭제",()=>{entries.Remove(route);Files.Touch();RenderBrowserBookmarks();RenderBrowserSide();});
            }
            if(sideMode=="history")Btn(browserSide,"전체 기록 보기",()=>Navigate("local://history"),"soft-button");
        }
    }
}
