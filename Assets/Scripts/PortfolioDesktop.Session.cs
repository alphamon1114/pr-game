using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrGame
{
    public enum ComputerSessionState { Off, Booting, Locked, Desktop }

    public sealed partial class PortfolioDesktop
    {
        // A game-world puzzle credential, never an account on the player's real computer.
        const string UserPassword="1114";
        public ComputerSessionState PowerState { get; private set; }
        public bool IsPoweredOn=>PowerState!=ComputerSessionState.Off;
        public bool AcceptsScreenInput=>PowerState==ComputerSessionState.Locked||PowerState==ComputerSessionState.Desktop;
        VisualElement powerScreen,bootContent;
        TextField loginPassword;
        Label loginError;
        float bootFinishesAt;

        void InitializePower()
        {
            powerScreen=El(desktop,"power-screen fill","PowerScreen");
            bootContent=El(powerScreen,"boot-content");
            Text(bootContent,"UNTITLED","boot-wordmark");
            Text(bootContent,"시작하는 중","boot-caption");
            var track=El(bootContent,"boot-track");El(track,"boot-progress");
            PowerState=ComputerSessionState.Off;locked=true;
            Visible(lockScreen,false);Visible(bootContent,false);Visible(powerScreen,true);
            SetShellEnabled(false);
            DeskComputerPower.SyncNow(this);
        }
        void SetShellEnabled(bool enabled)
        {
            windowLayer.SetEnabled(enabled);islandWrap.SetEnabled(enabled);overlayLayer.SetEnabled(enabled);
            desktopFiles.SetEnabled(enabled);desktop.Q(className:"shortcuts")?.SetEnabled(enabled);
        }
        public void PowerOn()
        {
            if(IsPoweredOn)return;
            PowerState=ComputerSessionState.Booting;locked=true;
            bootFinishesAt=Time.unscaledTime+1.8f;
            Visible(bootContent,true);Visible(powerScreen,true);powerScreen.BringToFront();
            DeskComputerPower.SyncNow(this);
        }
        void UpdatePower()
        {
            if(PowerState!=ComputerSessionState.Booting||Time.unscaledTime<bootFinishesAt)return;
            PowerState=ComputerSessionState.Locked;Visible(powerScreen,false);Lock();
        }
        public void PowerOff()
        {
            SuspendInput();ClosePopups();CloseDialog();
            foreach(var window in windows.Values.ToArray())window.Close();
            DisposeWebBrowser();
            PowerState=ComputerSessionState.Off;locked=true;SetShellEnabled(false);
            loginPassword.SetValueWithoutNotify("");loginError.text="";
            Visible(lockScreen,false);Visible(toast,false);Visible(bootContent,false);Visible(powerScreen,true);powerScreen.BringToFront();
            GetComponent<MonitorSurface>()?.seatedView?.LeaveComputer();
            DeskComputerPower.SyncNow(this);
        }
        public void Lock()
        {
            if(!AcceptsScreenInput)return;
            SuspendInput();ClosePopups();CloseDialog();
            PowerState=ComputerSessionState.Locked;locked=true;SetShellEnabled(false);
            loginPassword.SetValueWithoutNotify("");loginError.text="";
            lockScreen.RemoveFromClassList("unlocking");Visible(toast,false);Visible(lockScreen,true);
            lockScreen.BringToFront();UpdateClock();
            if(GetComponent<MonitorSurface>()?.seatedView?.CanInteractWithComputer!=false)FocusLogin();
        }
        public void FocusLogin(){if(PowerState==ComputerSessionState.Locked)loginPassword?.Focus();}
        public bool TryLogin(string password)
        {
            if(PowerState!=ComputerSessionState.Locked)return false;
            if(password!=UserPassword)
            {
                loginPassword.SetValueWithoutNotify("");loginError.text="비밀번호가 맞지 않아요. 다시 입력해 주세요.";
                FocusLogin();return false;
            }
            loginPassword.SetValueWithoutNotify("");loginError.text="";
            PowerState=ComputerSessionState.Desktop;locked=false;SetShellEnabled(true);
            lockScreen.AddToClassList("unlocking");
            lockScreen.schedule.Execute(()=>{if(!locked)Visible(lockScreen,false);}).StartingIn(250);
            desktop.Focus();return true;
        }
        void SubmitLogin(){TryLogin(loginPassword.value);}
    }
}
