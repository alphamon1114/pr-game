using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PrGame.Editor
{
    public static class ReferenceDeskAssets
    {
        const string Root="Assets/Art/ReferenceProps";
        [MenuItem("PR Game/Apply reference desk props")]
        public static void Apply()
        {
            PrepareMaterials();
            EditorSceneManager.OpenScene(DeskRoomBuilder.ScenePath);
            foreach(var old in GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t=>t.name.StartsWith("Refined / ")).ToArray())
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            var keyboard=Place("Keyboard",.74f,new Vector3(-.13f,.827f,.15f));
            var mouse=Place("Mouse",.13f,new Vector3(.56f,.827f,.17f));
            var monitor=Place("Monitor",1.30f,new Vector3(0,.82f,.85f));
            var oldKeyboard=GameObject.Find("KEYBOARD");if(oldKeyboard)oldKeyboard.SetActive(false);
            foreach(string name in new[]{"Mouse","Mouse seam"}) {var o=GameObject.Find(name);if(o)o.SetActive(false);}
            var physical=GameObject.Find("MONITOR");
            foreach(Transform child in physical.transform)if(child.name!="Interactive screen")child.gameObject.SetActive(false);

            var glass=monitor.GetComponentsInChildren<Renderer>().First(r=>r.name=="Screen glass");
            var b=glass.bounds;
            // Align the imported monitor's front toward the seated player using its glass center.
            var housing=monitor.GetComponentsInChildren<Renderer>().First(r=>r.name=="Monitor housing");
            if(glass.bounds.center.z>housing.bounds.center.z)
            {
                monitor.transform.Rotate(0,180,0,Space.World);
                AlignBottom(monitor,new Vector3(0,.82f,.85f));
                b=glass.bounds;
            }
            var screen=physical.transform.Find("Interactive screen");
            screen.position=new Vector3(b.center.x,b.center.y,b.min.z-.0015f);
            screen.localScale=new Vector3(b.size.x,b.size.y,1);
            glass.enabled=false;
            // A slight upward framing adjustment keeps the real stand and the desk visible.
            var camera=Camera.main;
            camera.transform.position=new Vector3(0,1.57f,-1.23f);
            camera.transform.LookAt(new Vector3(0,1.28f,.64f));
            var reflection=GameObject.Find("Desk material reflection");
            if(!reflection)reflection=new GameObject("Desk material reflection");
            reflection.transform.position=new Vector3(0,1.3f,.5f);
            var probe=reflection.GetComponent<ReflectionProbe>();
            if(!probe)probe=reflection.AddComponent<ReflectionProbe>();
            probe.mode=UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode=UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode=UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size=new Vector3(5,3,5);probe.resolution=128;probe.intensity=.7f;
            probe.clearFlags=UnityEngine.Rendering.ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor=new Color(.20f,.24f,.29f);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
            Debug.Log("REFERENCE_PROPS_APPLIED");
        }

        public static void PrepareMaterials()
        {
            Directory.CreateDirectory(Root+"/Materials");AssetDatabase.Refresh();
            foreach(string key in new[]{"Keyboard","Mouse","Monitor"})
            {
                string path=Root+"/"+key+"/"+key+"_Clean.fbx";
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                foreach(var src in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                {
                    string name=src.name;
                    string dest=Root+"/Materials/"+(name.StartsWith("RGB wave diffuser") ? "Keyboard RGB Wave" : name.Replace('/','_'))+".mat";
                    var m=AssetDatabase.LoadAssetAtPath<Material>(dest);
                    if(!m){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,dest);}
                    m.name=name.StartsWith("RGB wave diffuser") ? "Keyboard RGB Wave" : name;
                    if(name.StartsWith("RGB wave diffuser"))
                    {
                        var wave=Shader.Find("PRGame/Keyboard RGB Wave");
                        if(!wave)throw new Exception("Missing keyboard RGB wave shader");
                        m.shader=wave;
                        m.SetFloat("_Intensity",1.6f);m.SetFloat("_Speed",.12f);
                        m.SetFloat("_Frequency",1.35f);m.SetFloat("_Saturation",.9f);
                        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),name),m);
                        EditorUtility.SetDirty(m);
                        continue;
                    }
                    Color c=new Color(.035f,.04f,.05f);float metal=.1f,smooth=.4f;
                    if(name.Contains("aluminum")){c=new Color(.10f,.115f,.13f);metal=.48f;smooth=.58f;}
                    else if(name.Contains("Machined")){c=new Color(.08f,.09f,.105f);metal=.38f;smooth=.45f;}
                    else if(name.Contains("PBT")){c=new Color(.07f,.077f,.09f);smooth=.35f;metal=0;}
                    else if(name.Contains("legends")){c=new Color(.76f,.77f,.79f);smooth=.2f;metal=0;}
                    else if(name.Contains("diffuser")){c=new Color(.55f,.74f,.78f);smooth=.35f;}
                    else if(name.Contains("metallic blue")){c=new Color(.025f,.43f,.59f);metal=.48f;smooth=.60f;}
                    else if(name.Contains("aperture walls")){c=new Color(.018f,.28f,.39f);metal=.4f;smooth=.5f;}
                    else if(name.Contains("engraving")){c=new Color(.014f,.18f,.24f);metal=.05f;smooth=.2f;}
                    else if(name.Contains("rubber")){c=new Color(.022f,.028f,.035f);metal=0;smooth=.18f;}
                    else if(name.Contains("satin stand")){c=new Color(.10f,.11f,.13f);metal=.65f;smooth=.62f;}
                    else if(name.Contains("red accent")){c=new Color(.54f,.025f,.038f);metal=.3f;smooth=.5f;}
                    else if(name.Contains("etched markings")){c=new Color(.42f,.44f,.46f);smooth=.2f;metal=0;}
                    else if(name.Contains("Power light")){c=new Color(.45f,.7f,.8f);smooth=.5f;}
                    m.color=c.gamma;m.SetFloat("_Metallic",metal);m.SetFloat("_Glossiness",smooth);
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),name),m);
                    EditorUtility.SetDirty(m);
                }
                importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
        }

        static GameObject Place(string key,float width,Vector3 position)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/"+key+"/"+key+"_Clean.fbx");
            if(!asset)throw new Exception("Missing refined "+key);
            var wrapper=new GameObject("Refined / "+key);
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(asset);obj.transform.SetParent(wrapper.transform,false);
            var bounds=BoundsOf(wrapper);wrapper.transform.localScale=Vector3.one*(width/bounds.size.x);
            var renderers=wrapper.GetComponentsInChildren<Renderer>();
            if(key=="Keyboard")
            {
                var space=renderers.First(r=>r.name=="Keycap Space");
                var esc=renderers.First(r=>r.name=="Keycap Esc");
                if(space.bounds.center.z>esc.bounds.center.z)wrapper.transform.Rotate(0,180,0);
            }
            if(key=="Mouse")
            {
                var wheel=renderers.First(r=>r.name=="Single rubber scroll wheel");
                if(wheel.bounds.center.z<BoundsOf(wrapper).center.z)wrapper.transform.Rotate(0,180,0);
            }
            if(key=="Monitor")
            {
                var glass=renderers.First(r=>r.name=="Screen glass");
                var housing=renderers.First(r=>r.name=="Monitor housing");
                if(glass.bounds.center.z>housing.bounds.center.z)wrapper.transform.Rotate(0,180,0);
            }
            AlignBottom(wrapper,position);
            Directory.CreateDirectory("Assets/Prefabs");
            PrefabUtility.SaveAsPrefabAsset(wrapper,"Assets/Prefabs/Refined"+key+".prefab");
            Debug.Log("REFINED_BOUNDS "+key+" "+BoundsOf(wrapper));return wrapper;
        }
        static void AlignBottom(GameObject obj,Vector3 position)
        {
            var b=BoundsOf(obj);obj.transform.position+=position-new Vector3(b.center.x,b.min.y,b.center.z);
        }
        static Bounds BoundsOf(GameObject obj)
        {
            var renderers=obj.GetComponentsInChildren<Renderer>();var b=renderers[0].bounds;
            foreach(var r in renderers.Skip(1))b.Encapsulate(r.bounds);return b;
        }
    }
}
