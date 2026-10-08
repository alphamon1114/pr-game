using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrGame
{
    [RequireComponent(typeof(UIDocument))]
    public sealed partial class PortfolioDesktop : MonoBehaviour
    {
        public DesktopFileSystem Files { get; private set; }
        public VisualElement Root => desktop;
        public bool IsLocked => locked;
        public void SuspendInput()
        {
            // Preserve windows and notes, but release typing and active drags.
            var panel = desktop?.panel;
            panel?.focusController?.focusedElement?.Blur();
            var captured = panel?.GetCapturingElement(PointerId.mousePointerId);
            captured?.ReleasePointer(PointerId.mousePointerId);
            SaveNoteTitle();
            if (Files?.Dirty == true) Files.Save();
        }
        public bool IsTyping
        {
            get
            {
                var element=desktop?.panel?.focusController?.focusedElement as VisualElement;
                for(;element!=null;element=element.parent) if(element is TextField)return true;
                return false;
            }
        }
        VisualElement desktop, desktopFiles, windowLayer, overlayLayer, island, islandWrap, startMenu, searchPanel, notificationPanel, contextMenu, lockScreen, toast;
        Button islandHandle;
        Label clockLabel, noteStatus, toastText;
        readonly Dictionary<string, DesktopWindow> windows=new Dictionary<string, DesktopWindow>();
        readonly Dictionary<string, Button> appButtons=new Dictionary<string, Button>();
        bool locked, islandHidden, islandPinned;
        float saveAt;
        bool saveFailed;
        string activeApp="", currentFolder="documents", selectedFile, noteId, clipboard;
        bool cutClipboard;
        static readonly string[] AppIds={"explorer","browser","notes","settings"};
        static readonly string[] AppNames={"파일 탐색기","브라우저","메모","설정"};
        static readonly string[] AppIcons={"folder","globe","notebook-pen","settings-2"};

        void OnEnable()
        {
            Files=new DesktopFileSystem(); Files.Changed+=OnFilesChanged;
            var document=GetComponent<UIDocument>();
            if(!GetComponent<MonitorSurface>() && document.panelSettings) document.panelSettings.referenceResolution=new Vector2Int(1280,720);
            var root=document.rootVisualElement;root.Clear();root.style.flexGrow=1;
            root.RegisterCallback<KeyDownEvent>(evt=>
            {
                var surface=GetComponent<MonitorSurface>();
                if(surface && surface.seatedView && !surface.seatedView.CanInteractWithComputer) evt.StopImmediatePropagation();
            },TrickleDown.TrickleDown);
            var styles=Resources.Load<StyleSheet>("Desktop/Desktop");if(styles)root.styleSheets.Add(styles);
            var font=Resources.Load<Font>("Fonts/NotoSansKR-Regular");if(font)root.style.unityFontDefinition=FontDefinition.FromFont(font);
            desktop=El(root,"desktop","Desktop");desktop.focusable=true;
            desktop.AddToClassList(Files.Data.theme=="sage" ? "sage" : "silver");
            var wallpaper=El(desktop,"wallpaper");wallpaper.pickingMode=PickingMode.Ignore;
            El(wallpaper,"ribbon ribbon-one").pickingMode=PickingMode.Ignore;
            El(wallpaper,"ribbon ribbon-two").pickingMode=PickingMode.Ignore;
            Text(desktop,"UNTITLED","wordmark").pickingMode=PickingMode.Ignore;
            var shortcuts=El(desktop,"shortcuts");
            DesktopShortcut(shortcuts,"hard-drive","내 컴퓨터",()=>OpenApp("explorer"));
            DesktopShortcut(shortcuts,"notebook-pen","메모",()=>OpenApp("notes"));
            DesktopShortcut(shortcuts,"trash-2","휴지통",()=>{currentFolder="trash";OpenApp("explorer");});
            desktopFiles=El(desktop,"desktop-files");desktopFiles.pickingMode=PickingMode.Ignore;RefreshDesktopFiles();
            windowLayer=El(desktop,"fill","Windows");windowLayer.pickingMode=PickingMode.Ignore;
            BuildIsland();
            overlayLayer=El(desktop,"fill","Overlays");overlayLayer.pickingMode=PickingMode.Ignore;
            BuildStartMenu();BuildSearch();BuildNotifications();BuildLockScreen();
            desktop.RegisterCallback<PointerDownEvent>(evt=>
            {
                if(evt.button==0 && !IsInside(evt.target as VisualElement,overlayLayer) && !IsInside(evt.target as VisualElement,islandWrap)) ClosePopups();
                if(evt.button==1 && !locked && (evt.target==desktop || evt.target==windowLayer))
                { ShowDesktopContext(evt.position);evt.StopPropagation(); }
            },TrickleDown.TrickleDown);
            desktop.RegisterCallback<KeyDownEvent>(OnKeyDown,TrickleDown.TrickleDown);
            desktop.RegisterCallback<GeometryChangedEvent>(_=>{foreach(var w in windows.Values)w.ClampToDesktop();});
            desktop.schedule.Execute(UpdateClock).Every(1000);UpdateClock();
            if(!string.IsNullOrEmpty(Files.LastError)) Notify("저장 상태",Files.LastError);
        }
        void Update()
        {
            if(Files!=null && Files.Dirty && Time.unscaledTime>=saveAt && !saveFailed)
            {
                saveFailed=!Files.Save();
                if(noteStatus!=null)noteStatus.text=saveFailed ? "저장 실패 · 다시 저장해 주세요" : "자동 저장됨";
                if(saveFailed)Notify("저장 실패",Files.LastError);
            }
        }
        void OnApplicationPause(bool pause) { if(pause){SaveNoteTitle();if(Files?.Dirty==true)Files.Save();} }
        void OnApplicationQuit() { SaveNoteTitle();if(Files?.Dirty==true)Files.Save(); }
        void OnDisable()
        {
            if(Files!=null){SaveNoteTitle();Files.Changed-=OnFilesChanged;if(Files.Dirty)Files.Save();}
            windows.Clear();appButtons.Clear();notifications.Clear();browserTabs.Clear();
        }
        void OnFilesChanged()
        {
            saveFailed=false;saveAt=Time.unscaledTime+.6f;
            RefreshExplorer();RefreshSearch();RefreshNoteList();RefreshDesktopFiles();
            if(noteId!=null && (Files.Get(noteId)==null || Files.IsDeleted(Files.Get(noteId))))SelectNote(null);
            else if(noteId!=null && noteTitle!=null && !IsInside(desktop.panel?.focusController?.focusedElement as VisualElement,noteTitle))
                noteTitle.SetValueWithoutNotify(System.IO.Path.GetFileNameWithoutExtension(Files.Get(noteId).name));
            if(noteStatus!=null)noteStatus.text="저장 중…";
        }
        void UpdateClock()
        {
            var now=DateTime.Now;
            if(clockLabel!=null)clockLabel.text=now.ToString("tt h:mm",new System.Globalization.CultureInfo("ko-KR"))+"\n"+now.ToString("yyyy. MM. dd");
            if(lockScreen!=null){lockScreen.Q<Label>("LockTime").text=now.ToString("HH:mm");lockScreen.Q<Label>("LockDate").text=now.ToString("M월 d일 dddd",new System.Globalization.CultureInfo("ko-KR"));}
        }
        public void OpenApp(string id)
        {
            if(locked)return;
            ClosePopups();
            if(!windows.TryGetValue(id,out var window))
            {
                int index=Array.IndexOf(AppIds,id);if(index<0)return;
                var rect=id=="notes" ? new Rect(660,265,500,390) : id=="settings" ? new Rect(290,130,720,490) : new Rect(150,112,980,550);
                window=new DesktopWindow(id,AppNames[index],AppIcons[index],rect,()=>new Vector2(desktop.resolvedStyle.width,desktop.resolvedStyle.height));
                window.Focused+=()=>FocusApp(id);window.Changed+=UpdateIsland;
                window.Closed+=()=>{windows.Remove(id);if(activeApp==id)activeApp="";UpdateIsland();};
                windows.Add(id,window);windowLayer.Add(window);
                switch(id){case "explorer":BuildExplorer(window.Content);break;case "notes":BuildNotes(window.Content);break;case "browser":BuildBrowser(window.Content);break;case "settings":BuildSettings(window.Content);break;}
            }
            window.Show();FocusApp(id);window.ClampToDesktop();
        }
        public DesktopWindow GetWindow(string id) => windows.TryGetValue(id,out var w) ? w : null;
        public void OpenVirtualFile(string id)
        {
            var file=Files.Get(id);if(file==null || Files.IsDeleted(file))return;
            selectedFile=id;
            if(file.folder){selectedFile=null;currentFolder=id;OpenApp("explorer");RefreshExplorer();}
            else {OpenApp("notes");SelectNote(id);}
        }
        void FocusApp(string id)
        {
            activeApp=id;foreach(var entry in windows)entry.Value.EnableInClassList("active",entry.Key==id);
            UpdateIsland();
        }
        void DesktopShortcut(VisualElement parent,string icon,string title,Action action)
        {
            var button=Btn(parent,"",action,"desktop-shortcut");Icon(button,icon,"shortcut-plate");Text(button,title);
            button.name="Desktop-"+icon;
        }
        void RefreshDesktopFiles()
        {
            if(desktopFiles==null)return;desktopFiles.Clear();
            foreach(var file in Files.Children("desktop"))
            {
                var button=Btn(desktopFiles,"",()=>{selectedFile=file.id;desktopFiles.Query<Button>().ForEach(b=>b.EnableInClassList("selected",b.name=="DesktopFile-"+file.id));},"desktop-shortcut","DesktopFile-"+file.id);
                Icon(button,file.folder?"folder":"file-text","shortcut-plate");Text(button,file.name,"desktop-file-name");
                button.RegisterCallback<PointerDownEvent>(evt=>
                {
                    if(evt.button==0&&evt.clickCount==2){OpenVirtualFile(file.id);evt.StopPropagation();}
                    if(evt.button==1){selectedFile=file.id;ShowFileContext(evt.position,file);evt.StopPropagation();}
                });
            }
        }
        public static VisualElement El(VisualElement parent,string classes="",string name=null)
        {
            var element=new VisualElement();if(name!=null)element.name=name;
            foreach(var cls in classes.Split(' '))if(cls.Length>0)element.AddToClassList(cls);
            parent?.Add(element);return element;
        }
        public static Label Text(VisualElement parent,string text,string classes="")
        {
            var label=new Label(text);foreach(var cls in classes.Split(' '))if(cls.Length>0)label.AddToClassList(cls);parent?.Add(label);return label;
        }
        public static Button Btn(VisualElement parent,string text,Action action,string classes="",string name=null)
        {
            var button=new Button(action){text=text};if(name!=null)button.name=name;
            foreach(var cls in classes.Split(' '))if(cls.Length>0)button.AddToClassList(cls);parent?.Add(button);return button;
        }
        public static VisualElement Icon(VisualElement parent,string icon,string classes="")
        {
            if(classes.Contains("plate") || classes=="lock-avatar")
            {
                var plate=El(parent,classes);plate.pickingMode=PickingMode.Ignore;
                Icon(plate,icon);return plate;
            }
            var element=El(parent,"icon "+classes);element.pickingMode=PickingMode.Ignore;
            element.style.backgroundImage=Resources.Load<Texture2D>("Desktop/Icons/"+icon);return element;
        }
        public static Button IconBtn(VisualElement parent,string icon,string tip,Action action,string classes="",string name=null)
        {
            var button=Btn(parent,"",action,"icon-button "+classes,name);button.tooltip=tip;Icon(button,icon);return button;
        }
        static bool IsInside(VisualElement target,VisualElement container)
        {for(var t=target;t!=null;t=t.parent)if(t==container)return true;return false;}
        static void Visible(VisualElement el,bool show){if(el!=null)el.style.display=show?DisplayStyle.Flex:DisplayStyle.None;}
        static bool Shown(VisualElement el)=>el!=null && el.style.display.value!=DisplayStyle.None;
        static TextField InputField(VisualElement parent,string name,string placeholder,bool multiline=false)
        {
            var field=new TextField{ name=name, multiline=multiline };field.AddToClassList("field");field.textEdition.placeholder=placeholder;
            parent.Add(field);return field;
        }
        void Safely(Action action){try{action();}catch(Exception e){Notify("작업을 완료하지 못했어요",e.Message);}}
    }
}
