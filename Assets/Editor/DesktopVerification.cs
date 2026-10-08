using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrGame.Editor
{
    // Runs in an isolated project. Test saves never touch the player's save directory.
    [InitializeOnLoad]
    public static class DesktopVerification
    {
        static double next;
        static int step;
        static string createdId;
        static bool fileActivationVerified;
        static bool webBrowserVerified;
        static string SaveDirectory => Path.GetFullPath("Logs/OSQA/State-"+SessionState.GetString("PrGame.OSRun","default"));
        static DesktopVerification()
        {
            if(SessionState.GetBool("PrGame.OSVerify",false))
            { DesktopFileSystem.OverrideDirectory=SaveDirectory;EditorApplication.update+=Tick; }
        }
        public static void Run()
        {
            SessionState.SetString("PrGame.OSRun",DateTime.UtcNow.Ticks.ToString());
            Directory.CreateDirectory("Logs/OSQA");TestStorage();
            SessionState.SetBool("PrGame.OSVerify",true);DesktopFileSystem.OverrideDirectory=SaveDirectory;
            EditorApplication.isPlaying=true;
        }
        static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
        static void TestStorage()
        {
            string directory=Path.Combine(SaveDirectory,"storage-tests");var fs=new DesktopFileSystem(directory);
            var file=fs.Create("documents","검증 메모.txt");fs.Write(file.id,"한국어 입력 / English 123\n두 번째 줄");
            Assert(fs.Search("검증").Any(f=>f.id==file.id),"Created file missing from search");
            fs.Rename(file.id,"이름 변경.txt");Assert(!fs.Search("검증").Any(),"Rename left stale search result");
            var folder=fs.Create("documents","검증 폴더",true);fs.Paste(file.id,folder.id,true);fs.Delete(folder.id);
            Assert(!fs.Search("이름 변경").Any(),"Deleted parent left searchable child");fs.Restore(folder.id);
            Assert(fs.Search("이름 변경").Any(),"Restored child missing");
            bool cycleRejected=false;try{fs.Paste(folder.id,folder.id,true);}catch(InvalidOperationException){cycleRejected=true;}Assert(cycleRejected,"Folder cycle accepted");
            var copy=fs.Paste(folder.id,"desktop",false);Assert(fs.Children(copy.id).Single().content==file.content,"Recursive copy lost content");
            fs.Data.theme="sage";Assert(fs.Save(),fs.LastError);var reload=new DesktopFileSystem(directory);
            Assert(reload.Get(file.id).content==file.content&&reload.Data.theme=="sage","Persistence mismatch");
            reload.Write(file.id,"두 번째 저장");reload.Save();File.WriteAllText(Path.Combine(directory,"session-v1.json"),"{broken");
            var recovery=new DesktopFileSystem(directory);Assert(recovery.Get(file.id)?.content==file.content,"Backup recovery failed");
            // Simulate an older save with a personal bookmark, then persist a deliberate deletion.
            recovery.Data.browserDefaultsVersion=0;recovery.Data.bookmarks.Clear();recovery.Data.bookmarks.Add("https://example.com/");recovery.Save();
            var upgraded=new DesktopFileSystem(directory);
            Assert(upgraded.Data.bookmarks.SequenceEqual(new[]{"https://example.com/",DesktopBrowserDefaults.VarcoUrl}),"Bookmark upgrade lost existing data or omitted VARCO");
            Assert(upgraded.Get(file.id)?.content==file.content&&upgraded.Data.theme=="sage","Bookmark upgrade changed saved files or theme");
            upgraded.Data.browserDefaultsVersion=0;upgraded.Save();upgraded=new DesktopFileSystem(directory);
            Assert(upgraded.Data.bookmarks.Count(url=>url==DesktopBrowserDefaults.VarcoUrl)==1,"Bookmark upgrade duplicated VARCO");
            upgraded.Data.bookmarks.Remove(DesktopBrowserDefaults.VarcoUrl);upgraded.Save();
            Assert(!new DesktopFileSystem(directory).Data.bookmarks.Contains(DesktopBrowserDefaults.VarcoUrl),"Removed bookmark returned on reload");
            Debug.Log("OS_BROWSER_DEFAULTS_PASS: legacy bookmark migration, existing data preserved, no duplicates, deletion retained");
            Debug.Log("OS_STORAGE_PASS: create, rename, recursive copy/move, cycle rejection, trash, restore, Korean persistence, backup recovery");
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+.65;
            try
            {
                var os=UnityEngine.Object.FindAnyObjectByType<PortfolioDesktop>();var surface=UnityEngine.Object.FindAnyObjectByType<MonitorSurface>();
                if(!os || os.Root?.panel==null)return;
                if(step==0){next+=6;step=-1;return;}
                if(step==-1)
                {
                    RoomViewVerification.CheckFreeLook(surface);
                    DesktopCursorVerification.CheckInactive(os);
                    RoomViewVerification.CheckRoom(surface);surface.seatedView.EnterComputer();
                    DesktopCursorVerification.CheckInactive(os);
                    Assert(!surface.seatedView.CanInteractWithComputer,"OS accepted input during approach");next+=2;step=1;return;
                }
                var root=os.Root;
                if(step==28&&!fileActivationVerified)
                {
                    fileActivationVerified=DesktopFileActivationVerification.Tick(os);
                    if(fileActivationVerified)
                    {
                        Capture(surface,"21-file-opened");
                        os.OpenApp("explorer");DesktopCursorVerification.PointAt(os,surface,new Vector2(1190,625));
                    }
                    return;
                }
                if(step==28&&!webBrowserVerified)
                {
                    webBrowserVerified=WebBrowserVerification.Tick(os,surface,name=>Capture(surface,name));
                    if(webBrowserVerified){os.OpenApp("explorer");DesktopCursorVerification.PointAt(os,surface,new Vector2(1190,625));}
                    return;
                }
                switch(step)
                {
                    case 1:
                        RoomViewVerification.CheckFocused(surface);
                        CheckMonitor(surface);Capture(surface,"01-desktop");Click(root.Q<Button>("StartButton"));break;
                    case 2:
                        Assert(root.Q("StartMenu").resolvedStyle.display==DisplayStyle.Flex,"Start did not open via pointer");Capture(surface,"02-start");Click(root.Q<Button>("Start-explorer"));break;
                    case 3:
                        Assert(os.GetWindow("explorer")!=null,"Explorer did not open");Capture(surface,"03-explorer");os.OpenApp("notes");break;
                    case 4:Click(root.Q<Button>("NewNote"));break;
                    case 5:
                        root.Q<TextField>("NoteTitle").value="내 메모";
                        root.Q<TextField>("NoteBody").value="방 안에서 발견한 내용을 여기에 적어 두기.\n한국어 메모가 저장됩니다.";
                        createdId=os.Files.Search("내 메모.txt").Single().id;Assert(os.Files.Get(createdId).content.Contains("한국어"),"Editing note did not update file system");
                        Capture(surface,"04-notes");os.ShowSearch("내 메모");break;
                    case 6:
                        Assert(root.Query<Button>(className:"search-result").ToList().Count==1,"User file missing from UI search");Capture(surface,"05-search");
                        os.Files.Rename(createdId,"새 이름.txt");Assert(root.Query<Button>(className:"search-result").ToList().Count==0,"Search rename was stale");
                        os.ShowSearch("새 이름");Assert(root.Query<Button>(className:"search-result").ToList().Count==1,"Renamed file missing");
                        os.Files.Delete(createdId);Assert(root.Query<Button>(className:"search-result").ToList().Count==0,"Deleted file still shown");
                        os.Files.Restore(createdId);os.OpenApp("browser");break;
                    case 7:Capture(surface,"06-browser");Click(root.Q<Button>("NewBrowserTab"));os.Navigate("local://documents");break;
                    case 8:
                        Assert(root.Query(className:"browser-tab").ToList().Count==2,"New browser tab failed");Click(root.Q<Button>("BrowserHistory"));break;
                    case 9:Capture(surface,"07-browser-history");os.OpenApp("explorer");Click(root.Q<Button>("explorer-maximize"));break;
                    case 10:
                        Assert(os.GetWindow("explorer").Maximized,"Maximize failed");Assert(root.Q("IslandWrap").ClassListContains("island-hidden"),"Island did not hide");Capture(surface,"08-maximized");Click(root.Q<Button>("IslandHandle"));break;
                    case 11:
                        Assert(!root.Q("IslandWrap").ClassListContains("island-hidden"),"Island handle failed");Capture(surface,"09-island-revealed");Click(root.Q<Button>("explorer-maximize"));break;
                    case 12:
                        Assert(!os.GetWindow("explorer").Maximized && Math.Abs(os.GetWindow("explorer").resolvedStyle.width-980)<3,"Maximize restore did not preserve size");
                        var win=os.GetWindow("explorer");var before=win.resolvedStyle.width;var edge=win.Q("explorer-resize-10");var point=edge.worldBound.center;
                        SendPointer(edge,EventType.MouseDown,point);SendPointer(win,EventType.MouseDrag,point-new Vector2(120,60));SendPointer(win,EventType.MouseUp,point-new Vector2(120,60));
                        SessionState.SetFloat("PrGame.OSBeforeWidth",before);break;
                    case 13:
                        Assert(os.GetWindow("explorer").resolvedStyle.width<SessionState.GetFloat("PrGame.OSBeforeWidth",0)-60,"Window resize failed");os.OpenApp("settings");break;
                    case 14:Click(root.Q<Button>("Theme-sage"));break;
                    case 15:
                        Assert(root.ClassListContains("sage"),"Theme did not switch");Capture(surface,"10-sage");os.SetTheme("silver");
                        os.Notify("파일 작업 완료","내 메모.txt를 문서에 저장했어요.");os.Notify("복사 완료","파일 3개를 보관함에 복사했어요.");Click(root.Q<Button>("NotificationsButton"));break;
                    case 16:
                        Capture(surface,"11-notifications");Click(root.Query<Button>(className:"dismiss-notification").First());break;
                    case 17:
                        Assert(root.Query(className:"notification-card").ToList().Count==1,"Individual notification dismissal failed");Click(root.Q<Button>("ClearNotifications"));break;
                    case 18:
                        Assert(root.Query(className:"notification-card").ToList().Count==0,"Clear notifications failed");os.Lock();break;
                    case 19:
                        Assert(os.IsLocked,"Lock failed");Capture(surface,"12-lock");os.Unlock();break;
                    case 20:
                        Assert(!os.IsLocked&&os.GetWindow("settings")!=null,"Unlock lost window session");os.OpenVirtualFile(createdId);break;
                    case 21:Click(root.Q<Button>("SaveNoteFile"));break;
                    case 22:
                        root.Q<TextField>("SaveFileName").value="플레이 메모.txt";
                        var folders=root.Q<DropdownField>("SaveFileFolder");folders.index=folders.choices.FindIndex(p=>p.EndsWith(" › 보관함"));break;
                    case 23:Capture(surface,"13-save-file");Click(root.Q<Button>("ConfirmSaveFile"));break;
                    case 24:
                        var saved=os.Files.Search("플레이 메모.txt").Single();Assert(saved.parent=="archive"&&saved.content.Contains("한국어"),"Save as lost path or content");
                        os.ShowSearch("플레이 메모");break;
                    case 25:
                        Assert(root.Query<Button>(className:"search-result").ToList().Count==1,"Saved note missing from search");Capture(surface,"14-saved-file-search");
                        os.OpenVirtualFile("archive");break;
                    case 26:
                        Capture(surface,"15-saved-file-explorer");os.Files.Save();
                        var reload=new DesktopFileSystem(SaveDirectory);Assert(reload.Get(createdId)?.content.Contains("한국어")==true,"UI-created note lost on reload");
                        Assert(reload.Search("플레이 메모.txt").Single().parent=="archive","Saved-as file lost on reload");
                        var desktopFile=os.Files.Create("desktop","바탕화면 메모.txt");Assert(root.Q("DesktopFile-"+desktopFile.id)!=null,"Desktop file icon missing");
                        var moving=os.GetWindow("explorer");var bar=moving.Q(className:"titlebar");var barPoint=bar.worldBound.position+new Vector2(150,24);
                        SessionState.SetFloat("PrGame.OSBeforeX",moving.resolvedStyle.left);
                        SendPointer(bar,EventType.MouseDown,barPoint);SendPointer(moving,EventType.MouseDrag,barPoint+new Vector2(50,0));SendPointer(moving,EventType.MouseUp,barPoint+new Vector2(50,0));break;
                    case 27:
                        Assert(Math.Abs(os.GetWindow("explorer").resolvedStyle.left-SessionState.GetFloat("PrGame.OSBeforeX",0)-50)<3,"Window title drag failed");
                        File.WriteAllText("Logs/OSQA/results.txt","PASS: storage/recovery; monitor fit and 5 ray coordinates; off-screen input; pointer start/app launch; notes and live search; rename/delete/restore; browser tabs/history; window maximize/restore/resize; island hide/reveal; themes; per-card notification dismissal; lock/session restore; Save As filename/folder; saved-file search/explorer and persistence.\n");
                        Debug.Log("PR_GAME_OS_VERIFIED");os.OpenApp("explorer");
                        DesktopCursorVerification.PointAt(os,surface,new Vector2(1190,625));break;
                    case 28:
                        DesktopCursorVerification.CheckArrowHotspot(os);Capture(surface,"16-cursor-arrow");
                        DesktopCursorVerification.PointAt(os,surface,root.Q<Button>("StartButton"));DesktopCursorVerification.CheckShape(os,DesktopCursorShape.Link);break;
                    case 29:
                        Capture(surface,"17-cursor-link");os.OpenApp("notes");
                        DesktopCursorVerification.PointAt(os,surface,root.Q<TextField>("NoteBody"));DesktopCursorVerification.CheckShape(os,DesktopCursorShape.Text);break;
                    case 30:
                        Capture(surface,"18-cursor-text");os.SetTheme("sage");DesktopCursorVerification.PointAt(os,surface,new Vector2(1190,625));
                        Assert(os.Pointer.IsSage,"Cursor did not follow sage theme");break;
                    case 31:
                        Capture(surface,"19-cursor-sage");os.SetTheme("silver");os.OpenApp("explorer");
                        DesktopCursorVerification.PointAt(os,surface,os.GetWindow("explorer").Q("explorer-resize-10"));
                        DesktopCursorVerification.CheckShape(os,DesktopCursorShape.DiagonalDown);Assert(!os.Pointer.IsSage,"Cursor did not return to silver palette");break;
                    case 32:
                        Capture(surface,"20-cursor-resize");DesktopCursorVerification.CheckResizeAndHide(os,surface);
                        os.OpenApp("notes");root.Q<TextField>("NoteBody").Focus();surface.seatedView.LeaveComputer();next+=2;break;
                    case 33:
                        RoomViewVerification.CheckReturned(surface,os);DesktopCursorVerification.CheckReturned(os);Finish(0);return;
                }
                step++;
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        static void CheckMonitor(MonitorSurface surface)
        {
            Assert(surface!=null,"Missing monitor");var screen=surface.screenCollider.transform;
            var glass=GameObject.Find("Refined / Monitor").GetComponentsInChildren<Renderer>().First(r=>r.name=="Screen glass").bounds;
            var rect=screen.GetComponent<Renderer>().bounds;
            Assert(rect.min.x>=glass.min.x && rect.max.x<=glass.max.x && rect.min.y>=glass.min.y && rect.max.y<=glass.max.y,"Screen exceeds monitor glass");
            foreach(var uv in new[]{new Vector2(.5f,.5f),new Vector2(.05f,.05f),new Vector2(.95f,.05f),new Vector2(.05f,.95f),new Vector2(.95f,.95f)})
            {
                var world=screen.TransformPoint(new Vector3(uv.x-.5f,uv.y-.5f,0));var pixel=surface.viewCamera.WorldToScreenPoint(world);
                var hit=surface.ScreenToPanel(new Vector2(pixel.x,Screen.height-pixel.y));
                Assert(Vector2.Distance(hit,new Vector2(uv.x*surface.screenTexture.width,(1-uv.y)*surface.screenTexture.height))<3,"Monitor mapping failed "+uv+" => "+hit);
            }
            Assert(surface.ScreenToPanel(new Vector2(1,1)).x<0,"Off-screen pointer accepted");
            Debug.Log("OS_MONITOR_PASS: glass="+glass+" screen="+rect);
        }
        static void Click(Button button){Assert(button!=null,"Missing button");SendPointer(button,EventType.MouseDown,button.worldBound.center);SendPointer(button,EventType.MouseUp,button.worldBound.center);}
        static void SendPointer(VisualElement target,EventType type,Vector2 point)
        {
            var input=new Event{button=0,mousePosition=point,type=type};
            if(type==EventType.MouseDown){using(var e=PointerDownEvent.GetPooled(input))target.SendEvent(e);}
            else if(type==EventType.MouseUp){using(var e=PointerUpEvent.GetPooled(input))target.SendEvent(e);}
            else {using(var e=PointerMoveEvent.GetPooled(input))target.SendEvent(e);}
        }
        static void Capture(MonitorSurface surface,string name)
        {
            SaveTexture(surface.screenTexture,"Logs/OSQA/"+name+"-screen.png");
            var target=new RenderTexture(1600,900,24);var camera=surface.viewCamera;var previous=camera.targetTexture;
            float aspect=camera.aspect;var position=camera.transform.position;var rotation=camera.transform.rotation;
            try
            {
                camera.targetTexture=target;camera.aspect=1600f/900f;
                if(surface.seatedView.IsFocused && surface.seatedView.TryGetFocusPose(out var p,out var q))camera.transform.SetPositionAndRotation(p,q);
                camera.Render();SaveTexture(target,"Logs/OSQA/"+name+"-room.png");
                if(name=="03-explorer")
                {
                    var grade=camera.GetComponent<NightLighting>();grade.enabled=false;
                    camera.Render();SaveTexture(target,"Logs/OSQA/03-explorer-ungraded.png");grade.enabled=true;
                }
            }
            finally {camera.targetTexture=previous;camera.aspect=aspect;camera.transform.SetPositionAndRotation(position,rotation);target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
        static void SaveTexture(RenderTexture target,string path)
        {
            var previous=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(texture);
        }
        static void Finish(int code){SessionState.SetBool("PrGame.OSVerify",false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
    }
}
