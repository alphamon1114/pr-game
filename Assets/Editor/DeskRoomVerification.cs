using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrGame.Editor
{
    [InitializeOnLoad]
    public static class DeskRoomVerification
    {
        private static double started;
        private static int frames;
        private static int testStep;
        private static Camera ledCamera;
        private static double ledStart;
        static DeskRoomVerification()
        {
            if (SessionState.GetBool("PrGame.Verify", false)) EditorApplication.update += Tick;
        }

        public static void Run()
        {
            EditorSceneManager.OpenScene(DeskRoomBuilder.ScenePath);
            SessionState.SetBool("PrGame.Verify", true);
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            if (started == 0) started = EditorApplication.timeSinceStartup;
            if (++frames < 120 || EditorApplication.timeSinceStartup - started < 8) return;
            try
            {
                var surface = UnityEngine.Object.FindFirstObjectByType<MonitorSurface>();
                if (!surface) throw new Exception("Missing physical monitor");
                var center = surface.viewCamera.WorldToScreenPoint(surface.screenCollider.transform.position);
                var panelPoint = surface.ScreenToPanel(new Vector2(center.x, Screen.height - center.y));
                if (Vector2.Distance(panelPoint, new Vector2(800,450)) > 3)
                    throw new Exception("Monitor pointer mapping mismatch: " + panelPoint);
                var outside = surface.ScreenToPanel(new Vector2(1,1));
                if (outside.x >= 0) throw new Exception("Off-monitor input was accepted");
                var root = surface.GetComponent<UIDocument>().rootVisualElement;
                Directory.CreateDirectory("Logs/QA");
                if(testStep == 0)
                {
                    Capture(surface.viewCamera,"Logs/QA/desk-room.png");
                    var keyboard=GameObject.Find("Refined / Keyboard");
                    var renderers=keyboard.GetComponentsInChildren<Renderer>();
                    var diffusers=renderers.Where(r=>r.name.StartsWith("RGB underkey")).ToArray();
                    if(diffusers.Length!=67 || diffusers.Any(r=>r.sharedMaterial.shader.name!="PRGame/Keyboard RGB Wave" || !r.sharedMaterial.shader.isSupported))
                        throw new Exception("Keyboard LED material or count mismatch");
                    if(renderers.Any(r=>r.name.StartsWith("Legend ")))throw new Exception("Expected blank keycaps");
                    var bounds=renderers[0].bounds;
                    foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
                    ledCamera=new GameObject("LED verification camera").AddComponent<Camera>();
                    ledCamera.enabled=false;ledCamera.orthographic=true;ledCamera.orthographicSize=bounds.size.x*.34f;
                    ledCamera.transform.position=bounds.center+new Vector3(0,1,-.4f);
                    ledCamera.transform.LookAt(bounds.center);ledCamera.clearFlags=CameraClearFlags.SolidColor;
                    ledCamera.backgroundColor=Color.black;
                    Capture(ledCamera,"Logs/QA/keyboard-wave-a.png");
                    ledStart=EditorApplication.timeSinceStartup;
                }
                var folders = new[] {"Aboutme", "Music", "Games", "Myprojects"};
                if(testStep < 16)
                {
                    var folder=folders[testStep/4];
                    var button=root.Q<Button>(testStep%4 < 2 ? folder : "CloseWindow");
                    if(button == null) throw new Exception("Missing button " + folder);
                    if(testStep%4 == 2 && root.Q("FileWindow").style.display.value == DisplayStyle.None)
                        throw new Exception("Folder did not open " + folder);
                    var input = new Event { button=0, mousePosition=button.worldBound.center,
                        type=testStep%2 == 0 ? EventType.MouseDown : EventType.MouseUp };
                    if(testStep%2 == 0) { using(var evt=PointerDownEvent.GetPooled(input)) button.SendEvent(evt); }
                    else { using(var evt=PointerUpEvent.GetPooled(input)) button.SendEvent(evt); }
                    testStep++;
                    return;
                }
                if(root.Q("FileWindow").style.display.value != DisplayStyle.None) throw new Exception("Close failed");
                if(EditorApplication.timeSinceStartup-ledStart<2) return;
                Capture(ledCamera,"Logs/QA/keyboard-wave-b.png");
                var a=new Texture2D(2,2);var b=new Texture2D(2,2);
                a.LoadImage(File.ReadAllBytes("Logs/QA/keyboard-wave-a.png"));b.LoadImage(File.ReadAllBytes("Logs/QA/keyboard-wave-b.png"));
                var pa=a.GetPixels32();var pb=b.GetPixels32();int changed=0;
                for(int i=0;i<pa.Length;i++)if(Math.Abs(pa[i].r-pb[i].r)+Math.Abs(pa[i].g-pb[i].g)+Math.Abs(pa[i].b-pb[i].b)>30)changed++;
                UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);
                if(changed<50)throw new Exception("RGB wave did not visibly animate");
                File.WriteAllText("Logs/QA/verification.txt","PASS: physical monitor ray mapping, off-screen rejection, all four folders open and close, scene rendering, 67 unmarked keycaps with RGB diffusers; animated pixels="+changed+".\n");
                Debug.Log("PR_GAME_3D_VERIFIED");
                Finish(0);
            }
            catch(Exception error)
            {
                Debug.LogException(error);
                Finish(1);
            }
        }

        private static void Capture(Camera camera,string path)
        {
            var target = new RenderTexture(1600,900,24);
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            camera.targetTexture=target;
            camera.Render();
            RenderTexture.active=target;
            var texture=new Texture2D(1600,900,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,1600,900),0,0);
            texture.Apply();
            File.WriteAllBytes(path,texture.EncodeToPNG());
            camera.targetTexture=oldTarget;
            RenderTexture.active=oldActive;
            UnityEngine.Object.DestroyImmediate(texture);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }
        private static void Finish(int code)
        {
            SessionState.SetBool("PrGame.Verify",false);
            EditorApplication.update-=Tick;
            EditorApplication.Exit(code);
        }
    }
}
