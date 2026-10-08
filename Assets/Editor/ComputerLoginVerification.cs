using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrGame.Editor
{
    public static class ComputerLoginVerification
    {
        static int step;
        static string savedId;
        static Vector3 seatedPosition;
        static Quaternion seatedRotation;
        static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
        public static bool Tick(PortfolioDesktop os,MonitorSurface surface)
        {
            var camera=surface.viewCamera;var power=UnityEngine.Object.FindAnyObjectByType<DeskComputerPower>();
            var field=os.Root.Q<TextField>("LoginPassword");
            switch(step++)
            {
                case 0:
                    seatedPosition=camera.transform.position;seatedRotation=camera.transform.rotation;
                    Assert(power&&os.PowerState==ComputerSessionState.Off&&os.IsLocked&&!power.PoweredVisualsActive,"Computer must start off with lights off");
                    var indicators=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r=>r.name.StartsWith("Status indicator")||r.name.StartsWith("RGB underkey")||r.name=="Power LED"||r.name.StartsWith("PC fan rim")).ToArray();
                    Assert(indicators.Length>10&&indicators.All(r=>!r.enabled),"Some computer indicator lights remain visible while off");
                    Assert(!os.TryLogin("1114"),"Password bypassed power-on");os.OpenApp("notes");os.ShowSearch();
                    Assert(os.GetWindow("notes")==null,"Off computer opened an app");BlackScreen(surface);
                    DesktopVerification.Capture(surface,"power-01-off");
                    var note=UnityEngine.Object.FindAnyObjectByType<FridgePasswordNote>();Assert(note,"Handwritten fridge clue missing");
                    var ink=note.GetComponent<Renderer>().bounds;var paper=note.Paper.bounds;
                    Assert(ink.min.x>paper.min.x&&ink.max.x<paper.max.x&&ink.min.y>paper.min.y&&ink.max.y<paper.max.y&&ink.max.z<paper.min.z,"Ink clips paper or sits inside fridge door");
                    camera.transform.LookAt(paper.center);camera.fieldOfView=32;RoomViewVerification.Capture(camera,"fridge-1114-from-seat");
                    camera.transform.position=paper.center+new Vector3(.12f,.05f,-.35f);camera.transform.LookAt(paper.center);camera.fieldOfView=42;
                    RoomViewVerification.Capture(camera,"fridge-1114-detail");
                    camera.transform.SetPositionAndRotation(seatedPosition,seatedRotation);camera.fieldOfView=58;
                    surface.seatedView.EnterComputer();return false;
                case 1:
                    Assert(!os.IsPoweredOn&&!surface.seatedView.CanInteractWithComputer,"Approaching off monitor powered on or enabled input");
                    Assert(surface.ScreenToPanel(new Vector2(Screen.width*.5f,Screen.height*.5f)).x<0,"Off monitor accepted pointer");
                    os.Pointer.UpdateFromInput();Assert(!os.Pointer.IsDisplayed,"Off monitor displayed cursor");
                    surface.seatedView.LeaveComputer();return false;
                case 2:
                    camera.transform.SetPositionAndRotation(seatedPosition,seatedRotation);camera.transform.LookAt(power.ButtonPosition);Physics.SyncTransforms();
                    RoomViewVerification.Capture(camera,"computer-power-button");
                    Assert(DeskComputerPower.TryInteract(camera),"Seated player cannot reach physical power switch");
                    Assert(os.PowerState==ComputerSessionState.Booting&&power.PoweredVisualsActive,"Physical switch failed to boot computer");
                    Assert(!os.TryLogin("1114"),"Login bypassed boot phase");os.PowerOn();
                    camera.transform.SetPositionAndRotation(seatedPosition,seatedRotation);surface.seatedView.EnterComputer();return false;
                case 3:
                    if(os.PowerState==ComputerSessionState.Booting){DesktopVerification.Capture(surface,"power-02-boot");step=3;return false;}
                    Assert(os.IsLocked&&os.PowerState==ComputerSessionState.Locked,"Boot exposed unlocked desktop");return false;
                case 4:
                    Assert(surface.seatedView.CanInteractWithComputer,"Login input is unavailable after approach");
                    Assert(field.isPasswordField&&string.IsNullOrEmpty(field.value),"Password must start empty and masked");
                    DesktopVerification.Capture(surface,"power-03-login");
                    Click(os.Root.Q("LockScreen"));Assert(os.IsLocked,"Background click bypassed password");
                    Key(field,'\n',KeyCode.Return);Assert(os.IsLocked,"Empty Enter bypassed password");
                    field.Focus();foreach(char c in "1124")Key(field,c,(KeyCode)((int)KeyCode.Alpha0+c-'0'));
                    Assert(field.value=="1124","Password field cannot receive typed key events: "+field.value);
                    Key(field,'\n',KeyCode.Return);
                    Assert(os.IsLocked&&field.value==""&&!string.IsNullOrEmpty(os.Root.Q<Label>("LoginError").text),"Wrong password was accepted or no error shown");
                    os.OpenApp("notes");os.OpenVirtualFile("documents");Assert(os.GetWindow("notes")==null&&os.GetWindow("explorer")==null,"Locked desktop opened files/apps");return false;
                case 5:
                    DesktopVerification.Capture(surface,"power-04-wrong-password");
                    foreach(char c in "1114")Key(field,c,(KeyCode)((int)KeyCode.Alpha0+c-'0'));
                    Click(os.Root.Q<Button>("LoginSubmit"));Assert(!os.IsLocked,"Correct password button failed");return false;
                case 6:
                    Assert(os.PowerState==ComputerSessionState.Desktop&&field.value=="","Login failed to clear password or show desktop");
                    DesktopVerification.Capture(surface,"power-05-desktop");
                    savedId=os.Files.Create("documents","전원 검증 메모.txt").id;os.Files.Write(savedId,"전원을 꺼도 보존되는 내용");os.OpenVirtualFile(savedId);
                    os.Lock();Assert(os.IsLocked&&os.GetWindow("notes")!=null,"Lock lost note window");
                    field.value="1114";Key(field,'\n',KeyCode.KeypadEnter);Assert(!os.IsLocked,"Keypad Enter login failed");
                    os.PowerOff();Assert(!os.IsPoweredOn&&os.GetWindow("notes")==null&&!power.PoweredVisualsActive,"Shutdown left computer or LEDs active");return false;
                case 7:
                    BlackScreen(surface);Assert(new DesktopFileSystem().Get(savedId)?.content=="전원을 꺼도 보존되는 내용","Shutdown lost saved note");
                    os.PowerOn();return false;
                case 8:
                    if(os.PowerState==ComputerSessionState.Booting){step=8;return false;}
                    Assert(os.IsLocked&&!os.TryLogin(""),"Power cycle retained authentication");
                    Assert(os.TryLogin("1114"),"Reboot password login failed");
                    os.Files.Delete(savedId);os.Files.Save();surface.seatedView.LeaveComputer();return false;
                case 9:
                    camera.transform.SetPositionAndRotation(seatedPosition,seatedRotation);
                    File.WriteAllText("Logs/OSQA/power-login-results.txt","PASS: handwritten clue stays on visible fridge paper; seated zoom render; cold start black display and LEDs off; app/password/monitor-approach cannot bypass power; physical power switch reachable from seat; timed boot; locked account after boot; masked empty password; real typed digits; background-click/empty/wrong-password rejection; correct 1114 login via button and keypad Enter; locked app/file guards; locking preserves windows; shutdown stops computer, clears input and saves notes; reboot requires password; saved file survives power cycle.\n");
                    Debug.Log("PR_GAME_POWER_LOGIN_VERIFIED");return true;
            }
            return false;
        }
        static void Key(TextField field,char character,KeyCode code)
        {
            field.Focus();var target=field.panel.focusController.focusedElement as VisualElement ?? field;
            using(var evt=KeyDownEvent.GetPooled(character,code,EventModifiers.None))target.SendEvent(evt);
        }
        static void Click(VisualElement element)
        {
            Assert(element!=null,"Login UI missing");var point=element.worldBound.center;
            var input=new Event{button=0,mousePosition=point,type=EventType.MouseDown};
            using(var evt=PointerDownEvent.GetPooled(input))element.SendEvent(evt);
            input.type=EventType.MouseUp;using(var evt=PointerUpEvent.GetPooled(input))element.SendEvent(evt);
        }
        static void BlackScreen(MonitorSurface surface)
        {
            var active=RenderTexture.active;var texture=new Texture2D(16,9,TextureFormat.RGB24,false);
            try
            {
                RenderTexture.active=surface.screenTexture;
                texture.ReadPixels(new Rect(100,100,16,9),0,0);texture.Apply();
                foreach(var pixel in texture.GetPixels())Assert(pixel.maxColorComponent<.01f,"Powered-off monitor is not black");
            }
            finally{RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(texture);}
        }
    }
}
