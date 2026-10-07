using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrGame
{
    public sealed partial class PortfolioDesktop
    {
        VisualElement fileGrid, previewPane, noteList, dialogShade;
        Label folderPath, explorerStatus;
        TextField explorerSearch, noteTitle, noteBody, notesSearch;
        bool listView, showPreview;
        string editingNote;

        void BuildExplorer(VisualElement parent)
        {
            var toolbar=El(parent,"toolbar");
            IconBtn(toolbar,"arrow-left","상위 폴더",GoUp);IconBtn(toolbar,"arrow-up","상위 폴더",GoUp);
            folderPath=Text(toolbar,"문서","breadcrumb grow");
            explorerSearch=InputField(toolbar,"ExplorerSearch","이 폴더에서 검색");explorerSearch.AddToClassList("folder-search");explorerSearch.RegisterValueChangedCallback(_=>RefreshExplorer());
            var layout=El(parent,"app-layout grow");var sidebar=El(layout,"sidebar explorer-sidebar");Text(sidebar,"바로가기","section-label");
            foreach(var info in new[]{new[]{"desktop","monitor","바탕화면"},new[]{"documents","folder","문서"},new[]{"downloads","download","다운로드"},new[]{"pictures","image","사진"},new[]{"trash","trash-2","휴지통"}})
            {
                string id=info[0];var btn=Btn(sidebar,"",()=>{currentFolder=id;selectedFile=null;explorerSearch.value="";RefreshExplorer();},"sidebar-item","Folder-"+id);Icon(btn,info[1]);Text(btn,info[2]);
            }
            Text(sidebar,"이 컴퓨터","section-label lower-label");var disk=El(sidebar,"row disk-label");Icon(disk,"hard-drive");Text(disk,"로컬 디스크");
            var center=El(layout,"explorer-center grow");var controls=El(center,"file-controls row");
            IconBtn(controls,"folder-plus","새 폴더",()=>CreateInFolder(true),"","NewFolder");
            IconBtn(controls,"file-text","새 텍스트 파일",()=>CreateInFolder(false),"","NewTextFile");
            El(controls,"spacer");IconBtn(controls,"layout-grid","아이콘 보기",()=>{listView=false;RefreshExplorer();});IconBtn(controls,"list","목록 보기",()=>{listView=true;RefreshExplorer();});
            var preview=Btn(controls,"",()=>{showPreview=!showPreview;RefreshExplorer();},"soft-button","TogglePreview");Icon(preview,"panel-right");Text(preview,"미리보기");
            var scroll=new ScrollView();scroll.AddToClassList("file-scroll");center.Add(scroll);fileGrid=El(scroll,"file-grid","FileGrid");
            scroll.RegisterCallback<PointerDownEvent>(evt=>{if(evt.button==1){ShowFileContext(evt.position,null);evt.StopPropagation();}});
            previewPane=El(layout,"preview-pane");
            explorerStatus=Text(parent,"","statusbar");RefreshExplorer();
        }
        void GoUp()
        {
            var file=Files.Get(currentFolder);currentFolder=file?.parent=="root" || file==null ? "documents" : file.parent;selectedFile=null;RefreshExplorer();
        }
        void RefreshExplorer()
        {
            if(fileGrid==null || !windows.ContainsKey("explorer"))return;
            fileGrid.Clear();fileGrid.EnableInClassList("list-view",listView);
            bool trash=currentFolder=="trash";
            if(!trash && (Files.Get(currentFolder)==null || Files.IsDeleted(Files.Get(currentFolder))))currentFolder="documents";
            var folder=Files.Get(currentFolder);folderPath.text=trash ? "휴지통" : Files.PathOf(folder)+" › "+folder.name;
            var files=(trash ? Files.Data.files.Where(f=>f.deleted) : Files.Children(currentFolder)).Where(f=>f.name.IndexOf(explorerSearch?.value??"",StringComparison.OrdinalIgnoreCase)>=0).ToArray();
            foreach(var file in files)
            {
                var item=Btn(fileGrid,"",()=>SelectExplorerFile(file.id),"file-item", "File-"+file.id);
                Icon(item,file.folder?"folder":"file-text","file-icon");var text=El(item,"file-caption");Text(text,file.name);Text(text,file.folder?"폴더":"텍스트 문서","muted small");
                item.EnableInClassList("selected",selectedFile==file.id);
                item.RegisterCallback<PointerDownEvent>(evt=>
                {
                    if(evt.button==0 && evt.clickCount==2){if(trash)Files.Restore(file.id);else OpenVirtualFile(file.id);evt.StopPropagation();}
                    if(evt.button==1){selectedFile=file.id;ShowFileContext(evt.position,file);evt.StopPropagation();}
                });
            }
            if(files.Length==0)Text(fileGrid,trash?"휴지통이 비어 있어요.":"파일이 없어요.","empty-caption");
            explorerStatus.text=files.Length+"개 항목"+(selectedFile!=null&&Files.Get(selectedFile)!=null?" · "+Files.Get(selectedFile).name+" 선택됨":"");
            var win=windows["explorer"];win.Query<Button>(className:"sidebar-item").ForEach(b=>b.EnableInClassList("selected",b.name=="Folder-"+currentFolder));
            RefreshPreview();
        }
        void SelectExplorerFile(string id)
        {
            selectedFile=id;
            fileGrid.Query<Button>(className:"file-item").ForEach(b=>b.EnableInClassList("selected",b.name=="File-"+id));
            if(Files.Get(id)!=null)explorerStatus.text=Files.Get(id).name+" 선택됨";
            RefreshPreview();
        }
        void RefreshPreview()
        {
            Visible(previewPane,showPreview);previewPane.Clear();
            var header=El(previewPane,"row");Text(header,"미리보기","grow");IconBtn(header,"x","미리보기 닫기",()=>{showPreview=false;RefreshExplorer();});
            var selected=Files.Get(selectedFile);
            if(selected==null){Text(previewPane,"파일을 선택하세요.","muted wrap");return;}
            Icon(previewPane,selected.folder?"folder":"file-text","preview-icon");Text(previewPane,selected.name,"heading wrap");Text(previewPane,Files.PathOf(selected),"muted small wrap");
            var previewScroll=new ScrollView();previewScroll.style.flexGrow=1;previewPane.Add(previewScroll);Text(previewScroll,selected.folder?Files.Children(selected.id).Count()+"개 항목":selected.content,"wrap preview-text");
        }
        void CreateInFolder(bool folder)
        {
            if(currentFolder=="trash")currentFolder="documents";
            var file=Files.Create(currentFolder,folder?"새 폴더":"제목 없는 메모.txt",folder);selectedFile=file.id;RefreshExplorer();
            if(!folder)OpenVirtualFile(file.id);else RenameSelected();
        }
        void ShowFileContext(Vector2 pos,DesktopFile file)
        {
            var menu=NewContext(pos);
            if(file==null)
            {
                ContextItem(menu,"folder-plus","새 폴더",()=>CreateInFolder(true));ContextItem(menu,"file-text","새 메모",()=>CreateInFolder(false));ContextItem(menu,"copy","붙여넣기",PasteFile,"Ctrl+V",clipboard!=null);return;
            }
            if(Files.IsDeleted(file)){ContextItem(menu,"rotate-cw","복원",()=>Files.Restore(file.id));return;}
            ContextItem(menu,"folder","열기",()=>OpenVirtualFile(file.id),"Enter");
            ContextItem(menu,"panel-right","미리보기",()=>{showPreview=true;RefreshExplorer();});
            ContextItem(menu,"scissors","잘라내기",()=>{clipboard=file.id;cutClipboard=true;},"Ctrl+X");
            ContextItem(menu,"copy","복사",()=>{clipboard=file.id;cutClipboard=false;},"Ctrl+C");
            ContextItem(menu,"pencil","이름 바꾸기",RenameSelected,"F2");ContextItem(menu,"trash-2","삭제",DeleteSelected,"Del");
            ContextItem(menu,"info","속성",()=>ShowDialog("속성",file.name+"\n"+Files.PathOf(file),null,null));
        }
        void RenameSelected()
        {
            var file=Files.Get(selectedFile);if(file==null)return;
            ShowDialog("이름 바꾸기","새 이름",file.name,name=>Files.Rename(file.id,name));
        }
        void DeleteSelected(){if(selectedFile==null)return;Files.Delete(selectedFile);selectedFile=null;RefreshExplorer();}
        void PasteFile()
        {
            if(clipboard==null)return;Safely(()=>{var f=Files.Paste(clipboard,currentFolder,cutClipboard);if(cutClipboard)clipboard=null;selectedFile=f.id;RefreshExplorer();Notify("파일 작업 완료",f.name+" · "+Files.Get(currentFolder).name);});
        }
        void ShowDialog(string title,string caption,string value,Action<string> action)
        {
            CloseDialog();dialogShade=El(overlayLayer,"dialog-shade fill");var box=El(dialogShade,"popup dialog");Text(box,title,"heading");Text(box,caption,"muted wrap");
            TextField input=null;if(value!=null){input=InputField(box,"DialogInput","");input.value=value;}
            var error=Text(box,"","error wrap");var buttons=El(box,"dialog-buttons row");
            Action confirm=()=>{try{action?.Invoke(input?.value);CloseDialog();}catch(Exception e){error.text=e.Message;}};
            Btn(buttons,"취소",CloseDialog,"soft-button");Btn(buttons,"확인",confirm,"primary-button","ConfirmDialog");
            input?.Focus();input?.SelectAll();box.RegisterCallback<KeyDownEvent>(evt=>{if(evt.keyCode==KeyCode.Return){confirm();evt.StopPropagation();}});
        }
        void CloseDialog(){dialogShade?.RemoveFromHierarchy();dialogShade=null;}
        void BuildNotes(VisualElement parent)
        {
            var toolbar=El(parent,"toolbar");
            IconBtn(toolbar,"list","메모 목록",()=>parent.EnableInClassList("show-note-list",!parent.ClassListContains("show-note-list")),"","NotesListToggle");
            IconBtn(toolbar,"plus","새 메모",NewNote,"","NewNote");
            var save=Btn(toolbar,"",ShowSaveNoteDialog,"soft-button","SaveNoteFile");Icon(save,"hard-drive");Text(save,"파일로 저장");save.tooltip="이름과 폴더 선택 · Ctrl+Shift+S";
            El(toolbar,"spacer");IconBtn(toolbar,"trash-2","이 메모 삭제",()=>{if(noteId!=null)Files.Delete(noteId);});
            var layout=El(parent,"app-layout grow");var sidebar=El(layout,"notes-sidebar");
            notesSearch=InputField(sidebar,"NotesSearch","메모 검색");notesSearch.RegisterValueChangedCallback(_=>RefreshNoteList());
            var scroll=new ScrollView();scroll.style.flexGrow=1;sidebar.Add(scroll);noteList=El(scroll,"note-list");
            var editor=El(layout,"note-editor grow");noteTitle=InputField(editor,"NoteTitle","제목 없는 메모");noteTitle.AddToClassList("note-title");noteTitle.isDelayed=true;
            noteTitle.RegisterValueChangedCallback(_=>SaveNoteTitle());
            noteBody=InputField(editor,"NoteBody","여기에 내용을 입력하세요.",true);noteBody.AddToClassList("note-body");
            noteBody.verticalScrollerVisibility=ScrollerVisibility.Auto;
            noteBody.RegisterValueChangedCallback(evt=>{if(editingNote!=null)Files.Write(editingNote,evt.newValue);});
            noteStatus=Text(parent,"자동 저장됨","statusbar");SelectNote(noteId??Files.Notes.FirstOrDefault()?.id);RefreshNoteList();
        }
        public void NewNote()
        {
            OpenApp("notes");var file=Files.Create("documents","제목 없는 메모.txt");SelectNote(file.id);noteTitle.Focus();noteTitle.SelectAll();
        }
        void SelectNote(string id)
        {
            if(noteBody==null)return;
            var file=Files.Get(id);noteId=file?.id;editingNote=noteId;
            noteTitle.SetValueWithoutNotify(file==null?"":Path.GetFileNameWithoutExtension(file.name));noteBody.SetValueWithoutNotify(file?.content??"");
            noteTitle.SetEnabled(file!=null);noteBody.SetEnabled(file!=null);RefreshNoteList();
        }
        void RefreshNoteList()
        {
            if(noteList==null || !windows.ContainsKey("notes"))return;noteList.Clear();
            foreach(var file in Files.Notes.Where(f=>f.name.IndexOf(notesSearch?.value??"",StringComparison.OrdinalIgnoreCase)>=0))
            {
                var btn=Btn(noteList,"",()=>SelectNote(file.id),"note-entry");Text(btn,Path.GetFileNameWithoutExtension(file.name));Text(btn,string.IsNullOrWhiteSpace(file.content)?"내용 없음":file.content.Split('\n')[0],"muted small");btn.EnableInClassList("selected",noteId==file.id);
            }
        }
        void SaveNoteTitle()
        {
            if(noteId==null || noteTitle==null)return;
            string title=noteTitle.text.Trim();if(title.Length==0)return;
            if(!title.EndsWith(".txt",StringComparison.OrdinalIgnoreCase))title+=".txt";
            if(Files.Get(noteId)?.name!=title)Safely(()=>Files.Rename(noteId,title));
        }
        public void ShowSaveNoteDialog()
        {
            var source=Files.Get(noteId);if(source==null){NewNote();source=Files.Get(noteId);}
            if(source==null)return;
            CloseDialog();ClosePopups();
            dialogShade=El(overlayLayer,"dialog-shade fill");var box=El(dialogShade,"popup dialog save-dialog");Text(box,"파일로 저장","heading");
            Text(box,"파일 이름","muted");var filename=InputField(box,"SaveFileName","");filename.value=source.name;
            Text(box,"저장 위치","muted");
            var folders=Files.Data.files.Where(f=>f.folder&&!Files.IsDeleted(f)).ToArray();
            var choices=folders.Select(f=>Files.PathOf(f)+" › "+f.name).ToList();
            int initial=Array.FindIndex(folders,f=>f.id==source.parent);
            var location=new DropdownField(choices,Math.Max(0,initial)){name="SaveFileFolder"};location.AddToClassList("save-location");box.Add(location);
            Text(box,"텍스트 문서 (.txt)","muted small section-heading");var error=Text(box,"","error wrap");
            var actions=El(box,"dialog-buttons row");Btn(actions,"취소",CloseDialog,"soft-button");
            Action save=()=>
            {
                try
                {
                    string name=filename.value.Trim();if(!name.EndsWith(".txt",StringComparison.OrdinalIgnoreCase))name+=".txt";
                    if(name==".txt")throw new ArgumentException("파일 이름을 입력해 주세요.");
                    string parent=folders[Math.Max(0,location.index)].id;
                    var existing=Files.Children(parent).FirstOrDefault(f=>string.Equals(f.name,name,StringComparison.OrdinalIgnoreCase));
                    if(existing!=null&&existing.id!=source.id)throw new ArgumentException("같은 이름의 파일이 있어요. 다른 이름을 입력해 주세요.");
                    var saved=existing??Files.Create(parent,name);Files.Write(saved.id,source.content);
                    if(!Files.Save())throw new IOException(Files.LastError);
                    SelectNote(saved.id);CloseDialog();Notify("파일 저장 완료",saved.name+" · "+Files.Get(parent).name);
                    noteStatus.text="저장됨 · "+Files.PathOf(saved)+" › "+saved.name;
                }
                catch(Exception e){error.text=e.Message;}
            };
            Btn(actions,"저장",save,"primary-button","ConfirmSaveFile");filename.Focus();filename.SelectAll();
            box.RegisterCallback<KeyDownEvent>(evt=>{if(evt.keyCode==KeyCode.Return){save();evt.StopPropagation();}});
        }
        void BuildSettings(VisualElement parent)
        {
            var scroll=new ScrollView();scroll.AddToClassList("settings-page");parent.Add(scroll);
            var profile=El(scroll,"profile row");Icon(profile,"user-round","app-plate");var text=El(profile,"grow");Text(text,"조건희","heading");Text(text,"이 컴퓨터의 사용자","muted");
            Text(scroll,"배경 및 테마","heading section-heading");Text(scroll,"편안한 쪽으로 골라 보세요.","muted");
            var cards=El(scroll,"theme-cards row");
            foreach(var pair in new[]{new[]{"silver","실버 + 블루"},new[]{"sage","차콜 + 세이지"}})
            {
                string theme=pair[0];var btn=Btn(cards,"",()=>SetTheme(theme),"theme-card "+theme+"-swatch","Theme-"+theme);
                var sample=El(btn,"theme-sample");El(sample,"sample-island");El(sample,"sample-window");Text(btn,pair[1]);btn.EnableInClassList("selected",Files.Data.theme==theme);
            }
            Text(scroll,"알림","heading section-heading");var dnd=new Toggle("방해 금지"){value=Files.Data.doNotDisturb};dnd.RegisterValueChangedCallback(evt=>{Files.Data.doNotDisturb=evt.newValue;Files.Touch();notificationPanel.Q<Toggle>().SetValueWithoutNotify(evt.newValue);});scroll.Add(dnd);
            Text(scroll,"창 조작","heading section-heading");Text(scroll,"제목 표시줄을 끌어 이동 · 가장자리와 모서리로 크기 조절\n제목 표시줄을 두 번 클릭해 최대화 · Ctrl+Alt+← / →로 좌우 정렬","muted wrap");
            Text(scroll,"메모와 파일은 자동으로 저장돼요.","muted section-heading");
            Btn(scroll,"지금 저장",()=>{SaveNoteTitle();if(Files.Save())Notify("저장 완료","메모와 설정을 저장했어요.");else Notify("저장 실패",Files.LastError);},"soft-button");
        }
        public void SetTheme(string theme)
        {
            Files.Data.theme=theme=="sage"?"sage":"silver";desktop.EnableInClassList("sage",theme=="sage");desktop.EnableInClassList("silver",theme!="sage");Files.Touch();
            if(windows.TryGetValue("settings",out var settings))foreach(string id in new[]{"sage","silver"})settings.Q<Button>("Theme-"+id)?.EnableInClassList("selected",Files.Data.theme==id);
        }
    }
}
