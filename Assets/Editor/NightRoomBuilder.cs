using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrGame.Editor
{
    public static class NightRoomBuilder
    {
        const string Art="Assets/Art/Room";
        public static void BakeAndVerify()
        {
            Bake();DesktopVerification.Run();
        }
        [Serializable] class Specs { public Spec[] materials; }
        [Serializable] class Spec
        {
            public string name,texture;
            public float[] color;
            public float roughness,metallic,emission,uvScale;
        }

        [MenuItem("PR Game/Apply furnished night room")]
        public static void Apply()
        {
            PlayerSettings.colorSpace=ColorSpace.Linear;
            ReferenceDeskAssets.Apply();
            var scene=EditorSceneManager.GetActiveScene();
            foreach(var root in scene.GetRootGameObjects())
            {
                if(root.name=="MONITOR" || root.name=="Monitor operating system" ||
                    root.name=="Seated camera" || root.name.StartsWith("Refined / ")) continue;
                UnityEngine.Object.DestroyImmediate(root);
            }
            PrepareMaterials();
            var room=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/NightRoom.fbx"));
            room.name="Furnished night room";
            room.transform.rotation=Quaternion.Euler(0,180,0);
            // These landmark checks catch mirrored imports before the scene is saved.
            CheckLandmark(room,"Fridge body",new Vector3(-1.70f,.51f,1.23f));
            CheckLandmark(room,"Wardrobe carcass",new Vector3(1.47f,1.11f,1.16f));
            CheckLandmark(room,"Bed timber frame",new Vector3(.97f,.255f,-1.55f));
            CheckLandmark(room,"Desk walnut top",new Vector3(-.20f,.753f,1.27f));
            CheckLandmark(room,"Window night pane",new Vector3(2.205f,1.60f,-.53f));
            CheckLandmark(room,"Door leaf",new Vector3(-2.06f,1.025f,-1.65f));
            var keyboard=GameObject.Find("Refined / Keyboard");
            Fit(keyboard,.405f,new Vector3(-.32f,.788f,1.075f));
            Fit(GameObject.Find("Refined / Mouse"),.071f,new Vector3(.16f,.788f,1.06f));
            var monitor=GameObject.Find("Refined / Monitor");
            var housing=monitor.GetComponentsInChildren<Renderer>().First(r=>r.name=="Monitor housing");
            monitor.transform.localScale*=.76f/housing.bounds.size.x;
            Align(monitor,new Vector3(-.20f,.777f,1.397f));
            var glass=monitor.GetComponentsInChildren<Renderer>().First(r=>r.name=="Screen glass");
            var b=glass.bounds;
            var screen=GameObject.Find("MONITOR").transform.Find("Interactive screen");
            screen.position=new Vector3(b.center.x,b.center.y,b.min.z-.0008f);
            screen.localScale=new Vector3(b.size.x,b.size.y,1);
            glass.enabled=false;
            UnityEngine.Object.FindAnyObjectByType<MonitorSurface>().FitToDisplay();
            foreach(var r in monitor.GetComponentsInChildren<MeshFilter>())
                if(r.name.Contains("light shield") || r.name=="Monitor housing")
                { var c=r.gameObject.GetComponent<MeshCollider>();if(!c)c=r.gameObject.AddComponent<MeshCollider>();c.sharedMesh=r.sharedMesh; }
            foreach(var r in room.GetComponentsInChildren<Renderer>())
            {
                if(r.name.Contains("Window night pane")) r.shadowCastingMode=ShadowCastingMode.Off;
                if(r.name.StartsWith("T50"))r.gameObject.layer=8;
            }
            var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            tags.FindProperty("layers").GetArrayElementAtIndex(8).stringValue="Seated chair";tags.ApplyModifiedProperties();

            var camera=Camera.main;
            camera.transform.position=new Vector3(-.20f,1.19f,.25f);
            camera.transform.LookAt(new Vector3(-.20f,1.13f,1.397f));
            camera.fieldOfView=58;camera.nearClipPlane=.025f;camera.allowHDR=true;
            camera.renderingPath=RenderingPath.DeferredShading;
            camera.cullingMask=~(1<<8); // The player's own chair must not intersect their first-person view.
            var grade=camera.GetComponent<NightLighting>();if(!grade)grade=camera.gameObject.AddComponent<NightLighting>();
            grade.shader=Shader.Find("Hidden/PRGame/NightLighting");grade.exposure=1.45f;
            var view=camera.GetComponent<SeatedView>();view.defaultFieldOfView=58;view.focusFieldOfView=48;view.monitorViewportFraction=.8f;view.atmosphere=.25f;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.35f,.40f,.48f);
            RenderSettings.ambientEquatorColor=new Color(.25f,.28f,.32f);
            RenderSettings.ambientGroundColor=new Color(.13f,.12f,.11f);
            RenderSettings.fog=false;
            var lamp=Light("Task lamp",new Vector3(-.77f,1.155f,1.29f),new Color(1,.84f,.68f),1.8f,2.8f,LightType.Spot);
            lamp.spotAngle=100;lamp.innerSpotAngle=55;lamp.transform.LookAt(new Vector3(-.47f,.76f,1.12f));view.deskLamp=lamp;
            var moon=Light("Window moonlight",new Vector3(3.0f,2.1f,-.55f),new Color(.65f,.76f,.92f),2f,7,LightType.Spot);
            moon.spotAngle=72;moon.innerSpotAngle=38;moon.transform.LookAt(new Vector3(-.9f,.65f,.38f));
            var glow=Light("Screen bounced light",screen.position+new Vector3(0,0,-.12f),new Color(.65f,.78f,.83f),.38f,1.35f,LightType.Point);glow.shadows=LightShadows.None;
            var fill=Light("Night ambient bounce",new Vector3(-.10f,2.28f,-.12f),new Color(.55f,.62f,.70f),.85f,7f,LightType.Point);fill.shadows=LightShadows.None;
            Light("Corridor under door",new Vector3(-2.40f,.35f,-1.65f),new Color(1,.50f,.19f),1.1f,1.75f,LightType.Point);
            var probe=new GameObject("Room reflection").AddComponent<ReflectionProbe>();
            probe.transform.position=new Vector3(0,1.3f,-.2f);probe.size=new Vector3(4.6f,2.7f,4.0f);
            probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.NoTimeSlicing;probe.resolution=128;probe.intensity=.55f;
            probe.clearFlags=ReflectionProbeClearFlags.SolidColor;probe.backgroundColor=new Color(.04f,.055f,.08f);
            QualitySettings.pixelLightCount=8;QualitySettings.shadowDistance=12;QualitySettings.shadowResolution=ShadowResolution.High;QualitySettings.antiAliasing=4;
            foreach(var prop in new[]{keyboard,monitor,GameObject.Find("Refined / Mouse")})
                PrefabUtility.SaveAsPrefabAsset(prop,"Assets/Prefabs/Refined"+prop.name.Replace("Refined / ","")+".prefab");
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("NIGHT_ROOM_APPLIED");
        }
        static void PrepareMaterials()
        {
            Directory.CreateDirectory(Art+"/Materials");AssetDatabase.Refresh();
            foreach(var path in Directory.GetFiles(Art+"/Textures","*.jpg",SearchOption.AllDirectories))
            {
                var t=(TextureImporter)AssetImporter.GetAtPath(path);
                t.textureType=path.Contains("normal")?TextureImporterType.NormalMap:TextureImporterType.Default;
                t.sRGBTexture=path.Contains("albedo");t.wrapMode=TextureWrapMode.Repeat;t.anisoLevel=4;t.maxTextureSize=1024;t.SaveAndReimport();
            }
            var importer=(ModelImporter)AssetImporter.GetAtPath(Art+"/NightRoom.fbx");
            var specs=JsonUtility.FromJson<Specs>(File.ReadAllText(Art+"/materials.json"));
            foreach(var s in specs.materials)
            {
                string path=Art+"/Materials/"+s.name+".mat";
                var shader=Shader.Find(string.IsNullOrEmpty(s.texture)?"Standard":"PRGame/Room PBR");
                if(!shader)throw new Exception("Missing room material shader");
                var m=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!m){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}else m.shader=shader;
                // Blender source colors are linear reflectance values; Unity's color property is sRGB.
                m.color=new Color(s.color[0],s.color[1],s.color[2]).gamma;m.SetFloat("_Metallic",s.metallic);
                if(string.IsNullOrEmpty(s.texture))m.SetFloat("_Glossiness",1-s.roughness);
                else
                {
                    string p=Art+"/Textures/"+s.texture+"/";
                    // Plain dyed fabric: retain the weave normal/roughness without a plaid print.
                    m.SetTexture("_MainTex",s.texture=="fabric_pattern_07" ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(p+"albedo.jpg"));
                    m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(p+"normal.jpg"));
                    m.SetTexture("_RoughnessMap",AssetDatabase.LoadAssetAtPath<Texture2D>(p+"roughness.jpg"));
                    m.SetTexture("_OcclusionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(p+"ao.jpg"));
                    m.SetFloat("_BumpScale",s.texture.Contains("plaster")?.12f:.22f);m.SetFloat("_SmoothnessScale",.70f);
                }
                if(s.emission>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",m.color*s.emission);}
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),s.name),m);EditorUtility.SetDirty(m);
            }
            importer.SaveAndReimport();AssetDatabase.SaveAssets();
        }
        [MenuItem("PR Game/Bake night room lighting")]
        public static void Bake()
        {
            Apply();
            var importer=(ModelImporter)AssetImporter.GetAtPath(Art+"/NightRoom.fbx");
            importer.generateSecondaryUV=true;importer.SaveAndReimport();
            var settings=AssetDatabase.LoadAssetAtPath<LightingSettings>(Art+"/NightRoomLighting.lighting");
            if(!settings){settings=new LightingSettings();AssetDatabase.CreateAsset(settings,Art+"/NightRoomLighting.lighting");}
            settings.bakedGI=true;settings.realtimeGI=false;settings.lightmapper=LightingSettings.Lightmapper.ProgressiveGPU;
            settings.mixedBakeMode=MixedLightingMode.IndirectOnly;settings.lightmapResolution=32;settings.lightmapMaxSize=1024;
            settings.directSampleCount=64;settings.indirectSampleCount=256;settings.environmentSampleCount=128;
            settings.maxBounces=4;settings.ao=true;settings.aoMaxDistance=.15f;settings.aoExponentIndirect=.5f;
            Lightmapping.lightingSettings=settings;
            foreach(var r in GameObject.Find("Furnished night room").GetComponentsInChildren<MeshRenderer>())
            {
                bool useProbes=r.name.StartsWith("T50") || r.name.Contains("Window night pane") ||
                    r.name.Contains("cable") || r.name.StartsWith("Pen") || r.name.StartsWith("Wardrobe pull") ||
                    r.name.StartsWith("PC fan rim") || r.name=="Task lamp arm" || r.name=="Mug handle";
                GameObjectUtility.SetStaticEditorFlags(r.gameObject,useProbes ? 0 : StaticEditorFlags.ContributeGI);
                r.receiveGI=useProbes ? ReceiveGI.LightProbes : ReceiveGI.Lightmaps;
            }
            foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                light.lightmapBakeType=LightmapBakeType.Mixed;
            var area=Light("Window diffuse light",new Vector3(2.14f,1.6f,-.53f),new Color(.65f,.76f,.92f),.85f,5,LightType.Rectangle);
            area.transform.LookAt(new Vector3(0,1.2f,-.53f));area.areaSize=new Vector2(1.5f,1.15f);area.lightmapBakeType=LightmapBakeType.Baked;
            var group=new GameObject("Room light probes").AddComponent<LightProbeGroup>();
            var probes=new System.Collections.Generic.List<Vector3>();
            foreach(float x in new[]{-1.8f,-.8f,0,.8f,1.8f})foreach(float y in new[]{.25f,.8f,1.25f,1.9f,2.4f})foreach(float z in new[]{-1.85f,-1,.0f,.6f,1.3f})
                probes.Add(new Vector3(x,y,z));
            group.probePositions=probes.ToArray();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
            if(!Lightmapping.Bake())throw new Exception("Night room lightmap bake failed");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
            Debug.Log("NIGHT_ROOM_LIGHTING_BAKED");
        }
        static void CheckLandmark(GameObject room,string name,Vector3 expected)
        {
            var renderer=room.GetComponentsInChildren<Renderer>().First(r=>r.name==name);
            if(Vector3.Distance(renderer.bounds.center,expected)>.025f)throw new Exception("Room import orientation mismatch: "+name+" "+renderer.bounds.center);
        }
        static void Fit(GameObject go,float width,Vector3 position){go.transform.localScale*=width/Bounds(go).size.x;Align(go,position);}
        static void Align(GameObject go,Vector3 position){var b=Bounds(go);go.transform.position+=position-new Vector3(b.center.x,b.min.y,b.center.z);}
        static Bounds Bounds(GameObject go){var rr=go.GetComponentsInChildren<Renderer>();var b=rr[0].bounds;foreach(var r in rr.Skip(1))b.Encapsulate(r.bounds);return b;}
        static Light Light(string name,Vector3 position,Color color,float intensity,float range,LightType type)
        {
            var l=new GameObject(name).AddComponent<Light>();l.transform.position=position;l.type=type;l.color=color;l.intensity=intensity;l.range=range;l.shadows=LightShadows.Soft;l.shadowBias=.015f;l.shadowNormalBias=.025f;return l;
        }
    }
}
