using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace PrGame.Editor
{
    public static class DeskRoomBuilder
    {
        public const string ScenePath = "Assets/Scenes/Desktop3D.unity";
        private const string Art = "Assets/Art/DeskRoom";

        [MenuItem("PR Game/Create 3D desk room")]
        public static void Create()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Desktop3D already exists. Edit the scene directly; no overwrite performed.");
            Directory.CreateDirectory(Art);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var wood = Material("Walnut", "#80583C");
            var floor = Material("Floor", "#514338");
            var wall = Material("Wall", "#535F60");
            var dark = Material("Graphite", "#222B31");
            var black = Material("Rubber", "#11181E");
            var cream = Material("Ivory", "#C6C1AA");
            var brass = Material("Brass", "#AC8552");
            var sage = Material("Sage", "#526F62");
            var orange = Material("Terracotta", "#B7764C");
            var blue = Material("WindowNight", "#29465F", true);
            var glow = Material("WarmGlow", "#FFCE83", true);

            var room = new GameObject("ROOM").transform;
            Cube(room, "Floor", new Vector3(0,-.07f,0), new Vector3(5,.12f,5), floor);
            Cube(room, "Back wall", new Vector3(0,1.5f,1.9f), new Vector3(5,3,.12f), wall);
            Cube(room, "Left wall", new Vector3(-2.5f,1.5f,0), new Vector3(.12f,3,4), wall);
            Cube(room, "Right wall", new Vector3(2.5f,1.5f,0), new Vector3(.12f,3,4), wall);
            Cube(room, "Baseboard", new Vector3(0,.09f,1.80f), new Vector3(5,.18f,.07f), dark);
            for (int i = -8; i <= 8; i++)
                Cube(room, "Floor seam", new Vector3(i*.3f,.001f,0), new Vector3(.007f,.004f,5), dark);

            var window = new GameObject("WINDOW / night").transform;
            Cube(window, "Glass", new Vector3(-1.5f,1.75f,1.815f), new Vector3(.9f,1.25f,.018f), blue);
            foreach (float x in new[] {-1.99f,-1.01f})
                Cube(window, "Frame", new Vector3(x,1.75f,1.76f), new Vector3(.065f,1.4f,.09f), cream);
            foreach (float y in new[] {1.08f,1.75f,2.42f})
                Cube(window, "Frame", new Vector3(-1.5f,y,1.75f), new Vector3(1.03f,.055f,.1f), cream);
            Cube(window, "Center mullion", new Vector3(-1.5f,1.75f,1.74f), new Vector3(.04f,1.4f,.1f), cream);
            Cube(window, "Window sill", new Vector3(-1.5f,1.03f,1.68f), new Vector3(1.12f,.08f,.3f), wood);

            var desk = new GameObject("DESK").transform;
            Cube(desk, "Solid wood desktop", new Vector3(0,.76f,.55f), new Vector3(2.7f,.10f,1.22f), wood);
            foreach (float x in new[] {-1.19f,1.19f})
                foreach (float z in new[] {.07f,1.03f})
                    Cube(desk, "Leg", new Vector3(x,.36f,z), new Vector3(.07f,.72f,.07f), dark);
            Cube(desk, "Desk mat", new Vector3(.05f,.817f,.36f), new Vector3(1.53f,.009f,.64f), sage);

            var monitor = new GameObject("MONITOR").transform;
            Cube(monitor, "Housing", new Vector3(0,1.335f,.85f), new Vector3(1.50f,.895f,.16f), dark);
            Cube(monitor, "Inset bezel", new Vector3(0,1.35f,.762f), new Vector3(1.438f,.819f,.02f), black);
            Cube(monitor, "Stem", new Vector3(0,.90f,.89f), new Vector3(.08f,.18f,.08f), dark);
            Cube(monitor, "Stand", new Vector3(0,.832f,.86f), new Vector3(.42f,.045f,.28f), dark);
            Cube(monitor, "Power indicator", new Vector3(.64f,.916f,.759f), new Vector3(.018f,.008f,.007f), glow);

            var texture = new RenderTexture(1600,900,24, RenderTextureFormat.ARGB32)
            { name = "MonitorDesktop", antiAliasing = 1, filterMode = FilterMode.Bilinear };
            AssetDatabase.CreateAsset(texture, Art + "/MonitorDesktop.renderTexture");
            var screenMat = new Material(Shader.Find("Unlit/Texture")) { name = "MonitorDisplay", mainTexture = texture };
            AssetDatabase.CreateAsset(screenMat, Art + "/MonitorDisplay.mat");
            var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screen.name = "Interactive screen";
            screen.transform.SetParent(monitor);
            screen.transform.position = new Vector3(0,1.35f,.746f);
            screen.transform.localScale = new Vector3(1.4f,.7875f,1);
            screen.GetComponent<MeshRenderer>().sharedMaterial = screenMat;

            var keyboard = new GameObject("KEYBOARD").transform;
            Cube(keyboard, "Case", new Vector3(-.13f,.846f,.16f), new Vector3(.74f,.05f,.25f), cream);
            for (int row=0; row<4; row++) for (int col=0; col<13; col++)
                Cube(keyboard, "Key", new Vector3(-.463f + col*.055f,.881f,.074f + row*.055f), new Vector3(.043f,.019f,.044f), row==0 && col==0 ? orange : dark);
            Cube(keyboard, "Space", new Vector3(-.13f,.881f,.035f), new Vector3(.28f,.019f,.025f), dark);
            var mouse = Part(desk,"Mouse",PrimitiveType.Sphere,new Vector3(.56f,.85f,.16f),new Vector3(.11f,.052f,.18f),cream);
            Cube(desk, "Mouse seam", new Vector3(.56f,.877f,.20f), new Vector3(.003f,.003f,.056f), black);

            foreach (float x in new[] {-.90f,.90f})
            {
                Cube(desk, "Speaker cabinet", new Vector3(x,.955f,.77f),new Vector3(.19f,.28f,.17f),wood);
                var speaker = Part(desk,"Speaker cone",PrimitiveType.Cylinder,new Vector3(x,.99f,.677f),new Vector3(.115f,.008f,.115f),black);
                speaker.transform.rotation = Quaternion.Euler(90,0,0);
                var tweeter = Part(desk,"Tweeter",PrimitiveType.Cylinder,new Vector3(x,.88f,.677f),new Vector3(.05f,.008f,.05f),dark);
                tweeter.transform.rotation = Quaternion.Euler(90,0,0);
            }

            var lamp = new GameObject("DESK LAMP").transform;
            Part(lamp,"Base",PrimitiveType.Cylinder,new Vector3(-1.08f,.84f,.75f),new Vector3(.23f,.025f,.23f),brass);
            Cube(lamp,"Stem",new Vector3(-1.08f,1.15f,.75f),new Vector3(.027f,.61f,.027f),brass);
            Cube(lamp,"Arm",new Vector3(-.96f,1.45f,.75f),new Vector3(.28f,.025f,.025f),brass);
            Part(lamp,"Shade",PrimitiveType.Cylinder,new Vector3(-.85f,1.425f,.75f),new Vector3(.23f,.065f,.23f),orange);
            Part(lamp,"Bulb",PrimitiveType.Sphere,new Vector3(-.85f,1.36f,.75f),new Vector3(.07f,.03f,.07f),glow);
            var lampLight = Light("Desk light",new Vector3(-.85f,1.34f,.70f),new Color(1,.74f,.43f),2.5f,LightType.Point);
            lampLight.range = 3.4f;
            lampLight.shadows = LightShadows.Soft;

            var cup = Part(desk,"Cup",PrimitiveType.Cylinder,new Vector3(-.88f,.894f,.12f),new Vector3(.13f,.075f,.13f),cream);
            Part(desk,"Coffee",PrimitiveType.Cylinder,new Vector3(-.88f,.969f,.12f),new Vector3(.113f,.002f,.113f),black);
            var handle = Part(desk,"Cup handle",PrimitiveType.Cube,new Vector3(-.966f,.91f,.12f),new Vector3(.065f,.065f,.036f),cream);
            Cube(desk,"Notebook",new Vector3(1.02f,.825f,.27f),new Vector3(.27f,.03f,.36f),orange);
            Cube(desk,"Notebook pages",new Vector3(1.02f,.838f,.27f),new Vector3(.25f,.009f,.34f),cream);
            Cube(desk,"Pen",new Vector3(1.08f,.849f,.28f),new Vector3(.009f,.01f,.21f),brass);

            var shelf = new GameObject("SHELF / personal objects").transform;
            Cube(shelf,"Shelf",new Vector3(1.32f,1.8f,1.65f),new Vector3(1.2f,.065f,.36f),wood);
            for (int i=0;i<7;i++)
                Cube(shelf,"Book",new Vector3(.88f+i*.075f,1.985f,1.66f),new Vector3(.058f,.28f+(i%3)*.025f,.20f),i%2==0?sage:orange);
            Part(shelf,"Plant pot",PrimitiveType.Cylinder,new Vector3(1.68f,1.91f,1.63f),new Vector3(.18f,.10f,.18f),cream);
            for (int i=0;i<5;i++)
            {
                var leaf = Part(shelf,"Leaf",PrimitiveType.Sphere,new Vector3(1.68f+(i-2)*.034f,2.10f,1.63f),new Vector3(.055f,.27f,.055f),sage);
                leaf.transform.rotation = Quaternion.Euler(i*9,0,(i-2)*22);
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.29f,.36f,.45f);
            RenderSettings.ambientEquatorColor = new Color(.20f,.23f,.25f);
            RenderSettings.ambientGroundColor = new Color(.12f,.10f,.08f);
            RenderSettings.fog = false;
            var moon = Light("Window fill",new Vector3(-1.7f,2.2f,.7f),new Color(.48f,.65f,1),.7f,LightType.Directional);
            moon.transform.rotation = Quaternion.Euler(35,-30,0);
            moon.shadows = LightShadows.Soft;
            var screenLight = Light("Monitor glow",new Vector3(0,1.32f,.63f),new Color(.4f,.7f,.78f),.45f,LightType.Point);
            screenLight.range=1.5f;
            QualitySettings.shadows=ShadowQuality.All;
            QualitySettings.shadowResolution=ShadowResolution.High;
            QualitySettings.antiAliasing=4;

            var camera = new GameObject("Seated camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0,1.52f,-.95f);
            camera.transform.LookAt(new Vector3(0,1.16f,.65f));
            camera.fieldOfView = 47;
            camera.nearClipPlane = .03f;
            camera.farClipPlane = 30;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.04f,.06f,.08f);
            camera.gameObject.AddComponent<AudioListener>();
            var view = camera.gameObject.AddComponent<SeatedView>();
            view.deskLamp = lampLight;

            var ui = new GameObject("Monitor operating system");
            var document = ui.AddComponent<UIDocument>();
            var panel = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Settings/DesktopPanel.asset"));
            panel.name = "WorldMonitorPanel";
            panel.targetTexture = texture;
            panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            panel.scale = 1;
            AssetDatabase.CreateAsset(panel, Art + "/WorldMonitorPanel.asset");
            document.panelSettings = panel;
            var surface = ui.AddComponent<MonitorSurface>();
            surface.viewCamera = camera;
            surface.screenCollider = screen.GetComponent<MeshCollider>();
            surface.screenTexture = texture;
            surface.seatedView = view;
            ui.AddComponent<PortfolioDesktop>();
            EditorSceneManager.SaveScene(scene,ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath,true) };
            AssetDatabase.SaveAssets();
            Debug.Log("PR_GAME_3D_SCENE_OK");
        }

        private static Material Material(string name,string hex,bool unlit=false)
        {
            ColorUtility.TryParseHtmlString(hex,out var color);
            var material = new Material(Shader.Find(unlit?"Unlit/Color":"Standard")) { name=name, color=color };
            if(!unlit) material.SetFloat("_Glossiness",.23f);
            AssetDatabase.CreateAsset(material,Art+"/"+name+".mat");
            return material;
        }
        private static GameObject Cube(Transform parent,string name,Vector3 position,Vector3 size,Material material)
            => Part(parent,name,PrimitiveType.Cube,position,size,material);
        private static GameObject Part(Transform parent,string name,PrimitiveType primitive,Vector3 position,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(primitive);
            go.name=name;
            go.transform.SetParent(parent);
            go.transform.position=position;
            go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=material;
            return go;
        }
        private static Light Light(string name,Vector3 position,Color color,float intensity,LightType type)
        {
            var light=new GameObject(name).AddComponent<Light>();
            light.transform.position=position; light.type=type; light.color=color; light.intensity=intensity;
            return light;
        }
    }
}
