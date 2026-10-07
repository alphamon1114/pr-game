using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace PrGame
{
    public sealed partial class PortfolioDesktop
    {
        sealed class BrowserTab { public readonly List<string> entries=new List<string>{"local://newtab"};public int index;public string Url=>entries[index]; }
        readonly List<BrowserTab> browserTabs=new List<BrowserTab>();
        int browserTabIndex;
        VisualElement tabStrip,browserPage,browserSide;
        TextField address;
        string sideMode;
        void BuildBrowser(VisualElement parent)
        {
            if(browserTabs.Count==0)browserTabs.Add(new BrowserTab());
            tabStrip=El(parent,"browser-tabs");
            var nav=El(parent,"toolbar browser-nav");
            IconBtn(nav,"arrow-left","뒤로",()=>{var tab=browserTabs[browserTabIndex];if(tab.index>0){tab.index--;RenderBrowser();}});
            IconBtn(nav,"arrow-right","앞으로",()=>{var tab=browserTabs[browserTabIndex];if(tab.index<tab.entries.Count-1){tab.index++;RenderBrowser();}});
            IconBtn(nav,"rotate-cw","새로 고침",RenderBrowser);IconBtn(nav,"house","홈",()=>Navigate("local://newtab"));
            address=InputField(nav,"BrowserAddress","검색 또는 주소 입력");address.AddToClassList("omnibox");
            address.RegisterCallback<KeyDownEvent>(evt=>{if(evt.keyCode==UnityEngine.KeyCode.Return){Navigate(address.value);evt.StopPropagation();}});
            IconBtn(nav,"star","북마크",()=>{string url=browserTabs[browserTabIndex].Url;if(Files.Data.bookmarks.Contains(url))Files.Data.bookmarks.Remove(url);else Files.Data.bookmarks.Add(url);Files.Touch();RenderBrowserSide();});
            IconBtn(nav,"notebook-pen","메모 열기",()=>OpenApp("notes"));
            var bookmarks=El(parent,"bookmarks row");Btn(bookmarks,"문서",()=>Navigate("local://documents"),"text-button");Btn(bookmarks,"보관함",()=>Navigate("local://archive"),"text-button");
            var body=El(parent,"app-layout grow");var scroll=new ScrollView();scroll.style.flexGrow=1;body.Add(scroll);browserPage=El(scroll,"browser-page");
            browserSide=El(body,"browser-side");Visible(browserSide,false);
            var rail=El(body,"browser-rail");
            IconBtn(rail,"search","통합 검색",()=>ShowSearch());
            IconBtn(rail,"history","방문 기록",()=>ToggleBrowserSide("history"),"","BrowserHistory");IconBtn(rail,"star","북마크",()=>ToggleBrowserSide("bookmarks"));
            IconBtn(rail,"download","다운로드",()=>{currentFolder="downloads";OpenApp("explorer");});IconBtn(rail,"notebook-pen","메모",()=>OpenApp("notes"));
            El(rail,"spacer");IconBtn(rail,"settings-2","설정",()=>OpenApp("settings"));RenderBrowser();
        }
        public void Navigate(string url)
        {
            url=(url??"").Trim();if(url.Length==0)url="local://newtab";
            var tab=browserTabs[browserTabIndex];if(tab.index<tab.entries.Count-1)tab.entries.RemoveRange(tab.index+1,tab.entries.Count-tab.index-1);
            tab.entries.Add(url);tab.index=tab.entries.Count-1;
            if(url!="local://newtab"){Files.Data.history.Remove(url);Files.Data.history.Insert(0,url);if(Files.Data.history.Count>100)Files.Data.history.RemoveAt(100);Files.Touch();}
            RenderBrowser();
        }
        static string PageTitle(string url)
        {switch(url){case "local://newtab":return "새 탭";case "local://documents":return "문서";case "local://archive":return "보관함";case "local://history":return "방문 기록";default:return url;}}
        void RenderBrowser()
        {
            if(browserPage==null)return;tabStrip.Clear();browserPage.Clear();
            for(int i=0;i<browserTabs.Count;i++)
            {
                int index=i;var tab=El(tabStrip,"browser-tab");tab.EnableInClassList("selected",index==browserTabIndex);
                var select=Btn(tab,"",()=>{browserTabIndex=index;RenderBrowser();},"browser-tab-title");Icon(select,"globe");Text(select,PageTitle(browserTabs[i].Url));
                IconBtn(tab,"x","탭 닫기",()=>{if(browserTabs.Count==1){browserTabs[0]=new BrowserTab();}else{browserTabs.RemoveAt(index);browserTabIndex=Math.Min(browserTabIndex,browserTabs.Count-1);}RenderBrowser();});
            }
            IconBtn(tabStrip,"plus","새 탭",()=>{browserTabs.Add(new BrowserTab());browserTabIndex=browserTabs.Count-1;RenderBrowser();},"","NewBrowserTab");
            string url=browserTabs[browserTabIndex].Url;address.SetValueWithoutNotify(url=="local://newtab"?"":url);
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
                Text(browserPage,"검색 결과","heading section-heading");Text(browserPage,url,"muted wrap");
                var matches=Files.Search(url).ToArray();
                foreach(var file in matches){string id=file.id;Btn(browserPage,file.name,()=>OpenVirtualFile(id),"web-link");}
                if(matches.Length==0){Text(browserPage,"표시할 페이지가 없어요.","heading section-heading");Text(browserPage,"이 브라우저에서는 컴퓨터 안의 문서와 기록을 찾아볼 수 있어요.","muted wrap");}
            }
            RenderBrowserSide();
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
                IconBtn(row,"x","기록 삭제",()=>{entries.Remove(route);Files.Touch();RenderBrowserSide();});
            }
            if(sideMode=="history")Btn(browserSide,"전체 기록 보기",()=>Navigate("local://history"),"soft-button");
        }
    }
}
