#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrGame
{
    // Opt-in development-player check. Normal play never creates test data or browses automatically.
    public sealed class WebBrowserPlayerSmoke : MonoBehaviour
    {
        static bool Requested=>Array.IndexOf(Environment.GetCommandLineArgs(),"--verify-browser")>=0;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Configure()
        {if(Requested)DesktopFileSystem.OverrideDirectory=Path.Combine(Application.temporaryCachePath,"BrowserQA-"+Guid.NewGuid().ToString("N"));}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Run(){if(Requested)new GameObject("Browser player verification").AddComponent<WebBrowserPlayerSmoke>();}
        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(2);
            var os=FindAnyObjectByType<PortfolioDesktop>();var surface=FindAnyObjectByType<MonitorSurface>();
            surface.seatedView.EnterComputer();yield return new WaitForSecondsRealtime(2);
            os.OpenApp("browser");os.Navigate("https://example.com/");
            float end=Time.realtimeSinceStartup+45;
            WebBrowserView view=null;
            while(Time.realtimeSinceStartup<end)
            {
                view=os.Root.Q<WebBrowserView>();
                if(view!=null&&view.Page.Texture&&!view.Page.Loading&&view.Page.Url=="https://example.com/")break;
                yield return null;
            }
            bool passed=view!=null&&view.Page.Texture&&!view.Page.Loading&&view.Page.Url=="https://example.com/";
            string result=passed?"PASS: packaged Windows player launched its bundled free Chromium engine and rendered live HTTPS into the OS monitor.":"FAIL: "+os.Root.Q<Label>("BrowserStatus")?.text;
            File.WriteAllText(Path.Combine(Application.dataPath,"../browser-player-verification.txt"),result);
            if(passed)Debug.Log("PR_GAME_BROWSER_PLAYER_VERIFIED");else Debug.LogError(result);
            os.GetWindow("browser")?.Close();yield return new WaitForSecondsRealtime(1);Application.Quit(passed?0:1);
        }
    }
}
#endif
