using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrGame.Editor
{
    // Exercise the icon's pointer events, not just OpenVirtualFile directly.
    public static class DesktopFileActivationVerification
    {
        static int step;
        static string nestedId, desktopId, folderId, otherId;
        static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
        public static bool Tick(PortfolioDesktop os)
        {
            var root=os.Root;
            switch(step++)
            {
                case 0:
                    folderId=os.Files.Create("archive","저장 경로 검증",true).id;
                    nestedId=os.Files.Create(folderId,"같은 이름.txt").id;
                    desktopId=os.Files.Create("desktop","같은 이름.txt").id;
                    os.Files.Write(nestedId,"보관함의 원본 메모");os.Files.Write(desktopId,"바탕화면의 별도 메모");
                    os.OpenVirtualFile(folderId);os.GetWindow("notes")?.Close();return false;
                case 1:
                    var item=root.Q<Button>("File-"+nestedId);Assert(item!=null,"Saved icon missing");
                    Click(item,1,true);Assert(os.GetWindow("notes")==null,"Single click launched a file");
                    Click(item,2,true);
                    Assert(os.GetWindow("notes")!=null,"Double-click on saved file label did not launch Notes");
                    CheckNote(os,"보관함의 원본 메모",nestedId);
                    root.Q<TextField>("NoteBody").value="보관함에서 열어 수정한 메모";
                    Assert(os.Files.Get(nestedId).content=="보관함에서 열어 수정한 메모","Edits did not target original saved file");
                    Assert(os.Files.Get(desktopId).content=="바탕화면의 별도 메모","Same-name file in another folder was overwritten");
                    foreach(string app in new[]{"explorer","notes","browser","settings"})os.GetWindow(app)?.Minimize();return false;
                case 2:
                    var desktopButton=root.Q<Button>("DesktopFile-"+desktopId);int received=0;
                    desktopButton.clickable.clickedWithEventInfo+=_=>received++;
                    DoubleClick(desktopButton,true);
                    Assert(received==2,"Desktop completed clicks="+received+"; bounds="+desktopButton.worldBound+"; label="+desktopButton.Q<Label>().worldBound);
                    Assert(!os.GetWindow("notes").Minimized,"File activation did not restore minimized Notes");
                    CheckNote(os,"바탕화면의 별도 메모",desktopId);
                    os.Files.Rename(nestedId,"다시 찾기.txt");os.Files.Rename(folderId,"옮긴 자료");
                    os.Files.Paste(folderId,"downloads",true);Assert(os.Files.Save(),os.Files.LastError);
                    os.GetWindow("notes").Close();os.OpenVirtualFile("downloads");return false;
                case 3:
                    root.Q<TextField>("ExplorerSearch").value="옮긴 자료";return false;
                case 4:
                    DoubleClick(root.Q<Button>("File-"+folderId));
                    Assert(root.Q<TextField>("ExplorerSearch").value=="","Folder inherited stale filename filter");
                    Assert(root.Q<Button>("File-"+nestedId)!=null,"Folder double-click did not navigate into moved directory");return false;
                case 5:
                    DoubleClick(root.Q<Button>("File-"+nestedId));CheckNote(os,"보관함에서 열어 수정한 메모",nestedId);
                    Assert(root.Q<Label>("NoteStatus").tooltip.Contains("다운로드 › 옮긴 자료 › 다시 찾기.txt"),"Renamed/moved path not shown");
                    Assert(os.Files.Save(),os.Files.LastError);
                    // Re-create the OS from disk, preserving only its saved files.
                    os.enabled=false;os.enabled=true;
                    Assert(!os.IsPoweredOn&&os.IsLocked,"OS reload bypassed startup power/password gate");
                    os.PowerOn();step=16;return false;
                case 6:
                    Assert(os.Files.Get(folderId).parent=="downloads"&&os.Files.Get(nestedId).parent==folderId,"Reload lost stable file/folder location");
                    DoubleClick(os.Root.Q<Button>("File-"+folderId));return false;
                case 7:
                    DoubleClick(root.Q<Button>("File-"+nestedId),true);CheckNote(os,"보관함에서 열어 수정한 메모",nestedId);
                    root.Q<TextField>("NoteBody").value="다시 실행해도 같은 위치에 저장되는 메모";
                    Assert(os.Files.Get(nestedId).parent==folderId,"Reopening moved file changed its folder");
                    Assert(os.Files.Get(desktopId).content=="바탕화면의 별도 메모","Reload confused same-name files");
                    os.GetWindow("notes").Close();os.GetWindow("explorer").Minimize();return false;
                case 8:
                    RightClick(root.Q<Button>("DesktopFile-"+desktopId));
                    Assert(os.GetWindow("notes")==null,"Right-click launched a file");
                    Assert(root.Q("ContextMenu")!=null,"File context menu missing");return false;
                case 9:
                    Click(root.Q("ContextMenu").Query<Button>().ToList().Single(b=>b.Query<Label>().ToList().Any(l=>l.text=="열기")));
                    CheckNote(os,"바탕화면의 별도 메모",desktopId);os.GetWindow("notes").Close();
                    otherId=os.Files.Create(folderId,"다른 파일.txt").id;os.OpenVirtualFile(folderId);return false;
                case 10:
                    Click(os.GetWindow("explorer").Query<Button>().ToList().Single(b=>b.tooltip=="목록 보기"));return false;
                case 11:
                    Click(root.Q<Button>("File-"+nestedId),1,true);Click(root.Q<Button>("File-"+otherId),1,true);
                    Click(root.Q<Button>("File-"+nestedId),1,true);
                    Assert(os.GetWindow("notes")==null,"Clicks on different file IDs were treated as a double-click");return false;
                case 12:
                    // The verification tick waits 650 ms between stages: two slow clicks must only select.
                    var slow=root.Q<Button>("File-"+nestedId);Click(slow,1,true);
                    Assert(os.GetWindow("notes")==null,"Slow clicks unexpectedly launched a file");
                    using(var key=KeyDownEvent.GetPooled('\n',KeyCode.Return,EventModifiers.None))slow.SendEvent(key);
                    CheckNote(os,"다시 실행해도 같은 위치에 저장되는 메모",nestedId);
                    os.Lock();os.OpenVirtualFile(desktopId);CheckNote(os,"다시 실행해도 같은 위치에 저장되는 메모",nestedId);
                    Assert(os.TryLogin("1114"),"Password unlock failed");return false;
                case 13:
                    os.ShowSearch("다시 찾기");return false;
                case 14:
                    Click(root.Query<Button>(className:"search-result").ToList().Single());
                    CheckNote(os,"다시 실행해도 같은 위치에 저장되는 메모",nestedId);
                    Assert(os.Files.Save(),os.Files.LastError);
                    var reload=new DesktopFileSystem();Assert(reload.Get(nestedId).content=="다시 실행해도 같은 위치에 저장되는 메모"&&reload.Get(nestedId).parent==folderId,"Reopened edits did not persist at original path");
                    return false;
                case 15:
                    File.WriteAllText("Logs/OSQA/file-activation-results.txt","PASS: explorer label/icon and desktop double-click; single/different/slow clicks only select; runtime clicks without native double-click count; restore minimized app; folders and stale-filter reset; same-name file isolation; edit original file; folder/file rename and move; OS reload from disk and reopen; right-click Open; list-view Enter; search activation; locked-state guard; visible current path; persisted edits.\n");
                    Debug.Log("PR_GAME_FILE_ACTIVATION_VERIFIED");return true;
                case 16:
                    if(os.PowerState==ComputerSessionState.Booting){step=16;return false;}
                    Assert(os.TryLogin("1114"),"OS reload password login failed");
                    os.OpenVirtualFile("downloads");step=6;return false;
            }
            return true;
        }
        static void CheckNote(PortfolioDesktop os,string content,string id)
        {
            Assert(os.GetWindow("notes")!=null,"Notes was not launched");
            Assert(os.Root.Q<TextField>("NoteBody").value==content,"Wrong file content opened: "+id);
            var file=os.Files.Get(id);
            Assert(os.Root.Q<Label>("NoteStatus").tooltip==os.Files.PathOf(file)+" › "+file.name,"Opened file path is stale");
        }
        static void DoubleClick(Button button,bool caption=false)
        {
            // Both events deliberately have clickCount=1, as some runtime panels do.
            Click(button,1,caption);Click(button,1,caption);
        }
        static void RightClick(Button button)
        {
            var input=new Event{button=1,mousePosition=button.worldBound.center,type=EventType.MouseDown,clickCount=1};
            using(var evt=PointerDownEvent.GetPooled(input))button.SendEvent(evt);
            input.type=EventType.MouseUp;using(var evt=PointerUpEvent.GetPooled(input))button.SendEvent(evt);
        }
        static void Click(Button button,int count=1,bool caption=false)
        {
            Assert(button!=null,"Missing file button");
            VisualElement target=caption ? button.Q<Label>() : button;
            Assert(target!=null,"Missing file caption");var point=target.worldBound.center;
            var hit=button.panel.Pick(point);
            Assert(hit==button||hit?.GetFirstAncestorOfType<Button>()==button,"Button is covered at click point: "+button.name+" by "+hit?.name);
            var input=new Event{button=0,mousePosition=point,type=EventType.MouseDown,clickCount=count};
            using(var evt=PointerDownEvent.GetPooled(input))target.SendEvent(evt);
            input.type=EventType.MouseUp;
            using(var evt=PointerUpEvent.GetPooled(input))button.SendEvent(evt);
        }
    }
}
