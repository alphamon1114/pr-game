using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrGame
{
    public sealed partial class PortfolioDesktop
    {
        TextField searchInput;
        ScrollView searchResults, notificationList;
        string searchFilter="전체";
        int searchSelection;
        readonly List<Action> searchActions=new List<Action>();
        readonly List<Button> searchRows=new List<Button>();
        sealed class Notification { public string title,body,time; }
        readonly List<Notification> notifications=new List<Notification>();
        VisualElement notificationDot;

        void BuildIsland()
        {
            islandWrap=El(desktop,"island-wrap","IslandWrap");islandWrap.pickingMode=PickingMode.Ignore;
            island=El(islandWrap,"island","Island");
            IconBtn(island,"panels-top-left","시작",()=>TogglePopup(startMenu),"start-button","StartButton");
            IconBtn(island,"search","검색 · Ctrl+Space",()=>ShowSearch(),"","SearchButton");
            El(island,"vertical-divider");
            for(int i=0;i<3;i++)
            {
                string id=AppIds[i];var btn=Btn(island,"",()=>OpenApp(id),"island-app",id+"-dock");
                Icon(btn,AppIcons[i]);Text(btn,AppNames[i],"island-app-label");El(btn,"running-mark");appButtons[id]=btn;
            }
            El(island,"spacer");
            Icon(island,"wifi");Icon(island,"volume-2");
            var bell=IconBtn(island,"bell","알림",()=>{TogglePopup(notificationPanel);Visible(notificationDot,false);},"","NotificationsButton");
            notificationDot=El(bell,"notification-dot");Visible(notificationDot,false);
            clockLabel=Text(island,"","clock");
            islandHandle=Btn(islandWrap,"",()=>{islandPinned=!islandPinned;UpdateIsland();},"island-handle","IslandHandle");
            El(islandHandle,"handle-line");
            Vector2 origin=Vector2.zero;
            islandHandle.RegisterCallback<PointerDownEvent>(evt=>{origin=evt.position;islandHandle.CapturePointer(evt.pointerId);});
            islandHandle.RegisterCallback<PointerUpEvent>(evt=>
            {
                if(Mathf.Abs(evt.position.y-origin.y)>8){islandPinned=evt.position.y>origin.y;UpdateIsland();evt.StopImmediatePropagation();}
                islandHandle.ReleasePointer(evt.pointerId);
            });
        }
        void UpdateIsland()
        {
            bool hasMax=windows.Values.Any(w=>w.Maximized&&!w.Minimized);
            if(!hasMax)islandPinned=false;
            islandHidden=hasMax&&!islandPinned;
            islandWrap.EnableInClassList("island-hidden",islandHidden);
            foreach(var pair in appButtons)
            {
                pair.Value.EnableInClassList("selected",activeApp==pair.Key && windows.ContainsKey(pair.Key));
                pair.Value.EnableInClassList("running",windows.ContainsKey(pair.Key));
            }
        }
        void BuildStartMenu()
        {
            startMenu=El(overlayLayer,"popup start-menu","StartMenu");
            var search=Btn(startMenu,"",()=>ShowSearch(),"start-search");Icon(search,"search");Text(search,"앱과 파일 검색","muted");
            Text(startMenu,"앱","section-label");var grid=El(startMenu,"app-grid");
            for(int i=0;i<AppIds.Length;i++)
            {
                string id=AppIds[i];var btn=Btn(grid,"",()=>OpenApp(id),"app-tile","Start-"+id);Icon(btn,AppIcons[i],"app-plate");Text(btn,AppNames[i]);
            }
            var footer=El(startMenu,"start-footer");Icon(footer,"user-round");Text(footer,"조건희","account-name");El(footer,"spacer");
            var lockBtn=Btn(footer,"",()=>Lock(),"text-button","LockButton");Icon(lockBtn,"lock-keyhole");Text(lockBtn,"잠금");
            var power=Btn(footer,"",()=>{ClosePopups();ShowPower();},"text-button");Icon(power,"power");Text(power,"전원");
            Visible(startMenu,false);
        }
        void TogglePopup(VisualElement popup)
        {
            if(locked)return;bool show=!Shown(popup);ClosePopups();Visible(popup,show);popup?.BringToFront();
        }
        void ClosePopups()
        {
            Visible(startMenu,false);Visible(searchPanel,false);Visible(notificationPanel,false);
            contextMenu?.RemoveFromHierarchy();contextMenu=null;
        }
        void BuildSearch()
        {
            searchPanel=El(overlayLayer,"popup search-panel","SearchPanel");
            var header=El(searchPanel,"search-header");Icon(header,"search");
            searchInput=InputField(header,"GlobalSearchInput","앱과 파일 검색");searchInput.AddToClassList("search-input");
            searchInput.RegisterValueChangedCallback(_=>{searchSelection=0;RefreshSearch();});
            IconBtn(header,"x","검색어 지우기",()=>{searchInput.value="";searchInput.Focus();});
            Btn(header,"Esc",()=>ClosePopups(),"keycap");
            var filters=El(searchPanel,"search-filters");
            foreach(string item in new[]{"전체","앱","파일"})
            {
                string filter=item;Btn(filters,filter,()=>{searchFilter=filter;searchSelection=0;RefreshSearch();},"filter",filter+"Filter");
            }
            searchResults=new ScrollView();searchResults.AddToClassList("search-results");searchPanel.Add(searchResults);
            Text(searchPanel,"↑ ↓  선택     Enter  열기     Esc  닫기","panel-footer");Visible(searchPanel,false);
        }
        public void ShowSearch(string query="")
        {
            if(locked)return;ClosePopups();Visible(searchPanel,true);searchPanel.BringToFront();searchInput.SetValueWithoutNotify(query);searchSelection=0;RefreshSearch();searchInput.Focus();
        }
        void RefreshSearch()
        {
            if(searchResults==null)return;searchResults.Clear();searchActions.Clear();searchRows.Clear();
            foreach(string filter in new[]{"전체","앱","파일"})searchPanel.Q<Button>(filter+"Filter").EnableInClassList("selected",filter==searchFilter);
            string query=searchInput.value.Trim();
            if(searchFilter!="파일")
            {
                var matches=Enumerable.Range(0,4).Where(i=>AppNames[i].IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0).ToArray();
                if(matches.Length>0)Text(searchResults,"앱","section-label");
                foreach(int i in matches){string id=AppIds[i];SearchRow(AppIcons[i],AppNames[i],i==2?"메모 작성 및 확인":"앱",()=>OpenApp(id));}
            }
            if(searchFilter!="앱")
            {
                var files=Files.Search(query).Take(100).ToArray();if(files.Length>0)Text(searchResults,"파일","section-label");
                foreach(var file in files){string id=file.id;SearchRow(file.folder?"folder":"file-text",file.name,Files.PathOf(file),()=>OpenVirtualFile(id));}
            }
            if(searchRows.Count==0){var empty=El(searchResults,"empty-state");Icon(empty,"search");Text(empty,"검색 결과가 없어요","heading");Text(empty,"다른 이름으로 검색해 보세요.","muted");Btn(empty,"검색어 지우기",()=>searchInput.value="","soft-button");}
            searchSelection=Mathf.Clamp(searchSelection,0,Mathf.Max(0,searchRows.Count-1));HighlightSearch();
        }
        void SearchRow(string icon,string title,string subtitle,Action action)
        {
            var row=Btn(searchResults,"",action,"search-result");Icon(row,icon,"result-plate");var text=El(row,"grow");Text(text,title);Text(text,subtitle,"muted small");
            searchRows.Add(row);searchActions.Add(action);
        }
        void HighlightSearch(){for(int i=0;i<searchRows.Count;i++)searchRows[i].EnableInClassList("selected",i==searchSelection);}
        void BuildNotifications()
        {
            notificationPanel=El(overlayLayer,"popup notification-panel","NotificationCenter");
            var header=El(notificationPanel,"panel-heading");Text(header,"알림","heading grow");
            Btn(header,"모두 지우기",()=>{notifications.Clear();RefreshNotifications();},"text-button","ClearNotifications");IconBtn(header,"x","닫기",()=>ClosePopups());
            var dnd=new Toggle("방해 금지"){value=Files.Data.doNotDisturb};dnd.AddToClassList("dnd");dnd.RegisterValueChangedCallback(evt=>{Files.Data.doNotDisturb=evt.newValue;Files.Touch();});notificationPanel.Add(dnd);
            notificationList=new ScrollView();notificationList.AddToClassList("notification-list");notificationPanel.Add(notificationList);
            Btn(notificationPanel,"알림 설정",()=>OpenApp("settings"),"panel-footer");Visible(notificationPanel,false);RefreshNotifications();
            toast=El(overlayLayer,"popup toast","NotificationToast");toastText=Text(toast,"","grow");IconBtn(toast,"x","알림 닫기",()=>Visible(toast,false));Visible(toast,false);
        }
        public void Notify(string title,string body)
        {
            notifications.Insert(0,new Notification{title=title,body=body,time=DateTime.Now.ToString("HH:mm")});
            if(notifications.Count>50)notifications.RemoveAt(50);RefreshNotifications();Visible(notificationDot,true);
            if(!Files.Data.doNotDisturb && !locked && toast!=null)
            {
                toastText.text=title+"\n"+body;Visible(toast,true);toast.BringToFront();
                int count=notifications.Count;toast.schedule.Execute(()=>{if(notifications.Count==count)Visible(toast,false);}).StartingIn(4500);
            }
        }
        void RefreshNotifications()
        {
            if(notificationList==null)return;notificationList.Clear();
            Visible(notificationPanel.Q<Button>("ClearNotifications"),notifications.Count>0);
            if(notifications.Count==0){var empty=El(notificationList,"empty-state");Icon(empty,"bell");Text(empty,"새로운 알림이 없어요.","muted");Visible(notificationDot,false);}
            foreach(var notification in notifications.ToArray())
            {
                var card=El(notificationList,"notification-card");var row=El(card,"row");Text(row,notification.title,"grow");Text(row,notification.time,"muted small");
                IconBtn(row,"x","이 알림 지우기",()=>{notifications.Remove(notification);RefreshNotifications();},"dismiss-notification");
                Text(card,notification.body,"muted wrap");
            }
        }
        void BuildLockScreen()
        {
            lockScreen=El(desktop,"lock-screen fill","LockScreen");lockScreen.focusable=true;
            var wallpaper=El(lockScreen,"wallpaper lock-wallpaper");wallpaper.pickingMode=PickingMode.Ignore;
            El(wallpaper,"ribbon ribbon-one").pickingMode=PickingMode.Ignore;El(wallpaper,"ribbon ribbon-two").pickingMode=PickingMode.Ignore;
            Icon(lockScreen,"lock-keyhole","lock-icon");Text(lockScreen,"","lock-time").name="LockTime";Text(lockScreen,"","lock-date").name="LockDate";
            Icon(lockScreen,"user-round","lock-avatar");Text(lockScreen,"조건희","lock-user");
            var login=El(lockScreen,"login-form");
            var row=El(login,"login-row row");
            loginPassword=InputField(row,"LoginPassword","비밀번호");loginPassword.isPasswordField=true;loginPassword.maxLength=32;
            loginPassword.AddToClassList("login-password");
            IconBtn(row,"arrow-right","로그인",SubmitLogin,"login-submit","LoginSubmit");
            loginError=Text(login,"","login-error");loginError.name="LoginError";
            loginPassword.RegisterValueChangedCallback(_=>loginError.text="");
            Text(lockScreen,"비밀번호를 입력하여 로그인","lock-hint");
            var status=El(lockScreen,"lock-status row");Icon(status,"wifi");IconBtn(status,"power","전원",ShowPower);
            Visible(lockScreen,false);
        }
        void ShowPower()
        {
            if(!AcceptsScreenInput)return;
            var menu=NewContext(new Vector2(desktop.resolvedStyle.width/2-100,desktop.resolvedStyle.height/2-70));
            if(locked){lockScreen.Add(menu);menu.BringToFront();}
            if(!locked)ContextItem(menu,"lock-keyhole","잠금",()=>Lock());
            ContextItem(menu,"power","컴퓨터 끄기",PowerOff);
        }
        void OnKeyDown(KeyDownEvent evt)
        {
            if(!AcceptsScreenInput){evt.StopImmediatePropagation();return;}
            if(locked)
            {
                if(evt.keyCode==KeyCode.Return||evt.keyCode==KeyCode.KeypadEnter){SubmitLogin();evt.StopImmediatePropagation();}
                return; // Let the password field receive typed characters; never dispatch desktop shortcuts.
            }
            if(evt.keyCode==KeyCode.Escape){ClosePopups();CloseDialog();evt.StopPropagation();return;}
            if(desktop.panel?.focusController?.focusedElement is WebBrowserView)return;
            if(evt.ctrlKey && evt.keyCode==KeyCode.Space){ShowSearch();evt.StopPropagation();return;}
            if(Shown(searchPanel))
            {
                if(evt.keyCode==KeyCode.DownArrow || evt.keyCode==KeyCode.UpArrow)
                {searchSelection=Mathf.Clamp(searchSelection+(evt.keyCode==KeyCode.DownArrow?1:-1),0,Mathf.Max(0,searchRows.Count-1));HighlightSearch();if(searchRows.Count>0)searchResults.ScrollTo(searchRows[searchSelection]);evt.StopPropagation();}
                if((evt.keyCode==KeyCode.Return || evt.keyCode==KeyCode.KeypadEnter)&&searchActions.Count>0){searchActions[searchSelection]();evt.StopPropagation();}
                return;
            }
            if(evt.ctrlKey && evt.keyCode==KeyCode.S)
            {
                if(evt.shiftKey)ShowSaveNoteDialog();
                else{SaveNoteTitle();Files.Save();RefreshNoteStatus(Files.LastError??"자동 저장됨");}
                evt.StopPropagation();return;
            }
            if(IsTyping)return;
            var fileButton=FileButtonAt(evt.target as VisualElement);
            if((evt.keyCode==KeyCode.Return||evt.keyCode==KeyCode.KeypadEnter)&&fileButton!=null)
            {
                ActivateFile(fileButton.userData as string);evt.StopImmediatePropagation();return;
            }
            if(evt.ctrlKey && evt.altKey && windows.TryGetValue(activeApp,out var w))
            {if(evt.keyCode==KeyCode.LeftArrow)w.Snap(false);if(evt.keyCode==KeyCode.RightArrow)w.Snap(true);}
            if(activeApp=="explorer")
            {
                if(evt.keyCode==KeyCode.F2)RenameSelected();
                if(evt.keyCode==KeyCode.Delete)DeleteSelected();
                if(evt.keyCode==KeyCode.Return && selectedFile!=null)ActivateFile(selectedFile);
                if(evt.ctrlKey && evt.keyCode==KeyCode.C){clipboard=selectedFile;cutClipboard=false;}
                if(evt.ctrlKey && evt.keyCode==KeyCode.X){clipboard=selectedFile;cutClipboard=true;}
                if(evt.ctrlKey && evt.keyCode==KeyCode.V)PasteFile();
            }
        }
        VisualElement NewContext(Vector2 position)
        {
            contextMenu?.RemoveFromHierarchy();contextMenu=El(overlayLayer,"popup context-menu","ContextMenu");
            contextMenu.style.left=Mathf.Clamp(position.x,4,desktop.resolvedStyle.width-254);contextMenu.style.top=Mathf.Clamp(position.y,90,desktop.resolvedStyle.height-340);contextMenu.BringToFront();return contextMenu;
        }
        void ContextItem(VisualElement menu,string icon,string label,Action action,string shortcut="",bool enabled=true)
        {
            var button=Btn(menu,"",()=>{contextMenu?.RemoveFromHierarchy();contextMenu=null;Safely(action);},"context-item");Icon(button,icon);Text(button,label,"grow");Text(button,shortcut,"muted small");button.SetEnabled(enabled);
        }
        void ShowDesktopContext(Vector2 position)
        {
            var menu=NewContext(position);
            ContextItem(menu,"folder-plus","새 폴더",()=>{Files.Create("desktop","새 폴더",true);currentFolder="desktop";OpenApp("explorer");});
            ContextItem(menu,"file-text","새 메모",NewNote);
            ContextItem(menu,"copy","붙여넣기",()=>{currentFolder="desktop";PasteFile();},"Ctrl+V",clipboard!=null);
            ContextItem(menu,"rotate-cw","새로 고침",RefreshExplorer,"F5");
            ContextItem(menu,"settings-2","배경 및 테마",()=>OpenApp("settings"));
        }
    }
}
