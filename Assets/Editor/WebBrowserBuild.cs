using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace PrGame.Editor
{
    // Native browser files live outside Assets so Unity never imports CefSharp/.NET
    // assemblies into its own runtime. Standalone builds receive a separate folder.
    public sealed class WebBrowserBuild : IPreprocessBuildWithReport,IPostprocessBuildWithReport
    {
        public int callbackOrder=>100;
        public void OnPreprocessBuild(BuildReport report)
        {
            if(report.summary.platform!=BuildTarget.StandaloneWindows64)return;
            if(!File.Exists(Path.Combine(WebBrowserService.RuntimeDirectory,"PrGame.BrowserHost.exe")))
                throw new BuildFailedException("Run Tools/BrowserHost/build.ps1 before building the Windows player.");
        }
        public void OnPostprocessBuild(BuildReport report)
        {
            if(report.summary.platform!=BuildTarget.StandaloneWindows64)return;
            string output=Path.GetFullPath(report.summary.outputPath);
            string destination=Path.Combine(Path.GetDirectoryName(output),Path.GetFileNameWithoutExtension(output)+"_Data","BrowserRuntime");
            string source=Path.GetFullPath(WebBrowserService.RuntimeDirectory);
            foreach(string file in Directory.GetFiles(source,"*",SearchOption.AllDirectories))
            {
                string relative=file.Substring(source.TrimEnd(Path.DirectorySeparatorChar).Length+1);
                string target=Path.Combine(destination,relative);Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(file,target,true);
            }
        }
        public static void VerifyPlayerBuild()
        {
            try
            {
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes=new[]{DeskRoomBuilder.ScenePath},target=BuildTarget.StandaloneWindows64,
                    locationPathName="Builds/BrowserPlayerQA/PRGame.exe",options=BuildOptions.Development
                });
                if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Standalone build failed: "+report.summary.result);
                UnityEngine.Debug.Log("PR_GAME_BROWSER_PLAYER_BUILT");EditorApplication.Exit(0);
            }
            catch(Exception e){UnityEngine.Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
