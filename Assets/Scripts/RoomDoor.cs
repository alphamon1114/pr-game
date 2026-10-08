using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrGame
{
    // Assemble the imported panel around a real hinge without rebuilding the furnished room.
    public sealed class RoomDoor : MonoBehaviour
    {
        const float Width=.956f, Height=2.01f, Thickness=.044f;
        public bool IsOpen { get; private set; }
        public float Angle { get; private set; }
        public Transform Panel { get; private set; }
        public BoxCollider PanelCollider { get; private set; }
        public Transform InsideRose { get; private set; }
        public Transform OutsideRose { get; private set; }
        Quaternion closedRotation;
        float velocity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var room=GameObject.Find("Furnished night room");
            if(!room || room.GetComponentInChildren<RoomDoor>())return;
            var renderers=room.GetComponentsInChildren<MeshRenderer>();
            var leaf=renderers.FirstOrDefault(r=>r.name=="Door leaf");
            if(!leaf || !leaf.TryGetComponent<MeshFilter>(out var mesh))return;
            var steel=renderers.FirstOrDefault(r=>r.name=="Door handle")?.sharedMaterial ?? leaf.sharedMaterial;
            foreach(var old in renderers.Where(r=>r.name=="Door handle" || r.name=="Door corridor darkness"))
                old.gameObject.SetActive(false);

            var pivot=new GameObject("Door rear hinge");pivot.transform.SetParent(room.transform,false);
            // Far jamb, towards the back wall: opening leaves the seated player's sightline clear.
            pivot.transform.SetPositionAndRotation(new Vector3(-2.148f,.02f,-2.106f),Quaternion.identity);
            var door=pivot.AddComponent<RoomDoor>();door.closedRotation=pivot.transform.localRotation;
            var panel=new GameObject("Door panel").transform;panel.SetParent(pivot.transform,false);door.Panel=panel;

            // Imported FBX axes/scales vary; align the thin, wide and tall mesh axes explicitly.
            var bounds=mesh.sharedMesh.bounds;
            var axes=new[]{0,1,2}.OrderBy(axis=>bounds.size[axis]).ToArray();
            var tall=Vector3.zero;tall[axes[2]]=1;var wide=Vector3.zero;wide[axes[1]]=1;
            var rotation=Quaternion.Inverse(Quaternion.LookRotation(wide,tall));
            var scale=Vector3.one;scale[axes[0]]=Thickness/bounds.size[axes[0]];
            scale[axes[1]]=Width/bounds.size[axes[1]];scale[axes[2]]=Height/bounds.size[axes[2]];
            leaf.transform.SetParent(panel,false);leaf.transform.localRotation=rotation;leaf.transform.localScale=scale;
            leaf.transform.localPosition=new Vector3(-Thickness*.5f,Height*.5f,Width*.5f)-rotation*Vector3.Scale(bounds.center,scale);
            foreach(var collider in leaf.GetComponents<Collider>())collider.enabled=false;
            Dynamic(leaf);
            door.PanelCollider=panel.gameObject.AddComponent<BoxCollider>();
            door.PanelCollider.center=new Vector3(-Thickness*.5f,Height*.5f,Width*.5f);
            door.PanelCollider.size=new Vector3(Thickness,Height,Width);
            door.InsideRose=door.Handle("inside",0,1,steel);
            door.OutsideRose=door.Handle("outside",-Thickness,-1,steel);
            foreach(float y in new[]{.22f,1.03f,1.80f})
                Cylinder(panel,"Door hinge barrel",new Vector3(0,y-.045f,0),new Vector3(0,y+.045f,0),.010f,steel);
            AddCorridor(room.transform,renderers);
        }

        Transform Handle(string side,float face,float direction,Material material)
        {
            const float y=.99f,z=.82f;
            // Rose overlaps the door surface by 1 mm; spindle and lever intersect each other.
            var rose=Cylinder(Panel,"Door handle rose "+side,new Vector3(face-direction*.001f,y,z),new Vector3(face+direction*.006f,y,z),.029f,material);
            Cylinder(Panel,"Door handle spindle "+side,new Vector3(face+direction*.004f,y,z),new Vector3(face+direction*.050f,y,z),.012f,material);
            Cylinder(Panel,"Door handle lever "+side,new Vector3(face+direction*.046f,y,z+.012f),new Vector3(face+direction*.046f,y,z-.115f),.009f,material);
            return rose;
        }

        static Transform Cylinder(Transform parent,string name,Vector3 start,Vector3 end,float radius,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=(start+end)*.5f;
            go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,(end-start).normalized);
            go.transform.localScale=new Vector3(radius*2,(end-start).magnitude*.5f,radius*2);
            go.GetComponent<Collider>().enabled=false;Destroy(go.GetComponent<Collider>());
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;Dynamic(renderer);return go.transform;
        }

        static void Dynamic(MeshRenderer renderer)
        {
            renderer.gameObject.isStatic=false;renderer.lightmapIndex=-1;renderer.receiveGI=ReceiveGI.LightProbes;
            renderer.lightProbeUsage=LightProbeUsage.BlendProbes;renderer.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;
            renderer.shadowCastingMode=ShadowCastingMode.On;
        }

        static void AddCorridor(Transform room,MeshRenderer[] renderers)
        {
            var wall=renderers.First(r=>r.name.StartsWith("Architecture west")).sharedMaterial;
            var floor=renderers.First(r=>r.name=="Architecture floor").sharedMaterial;
            var root=new GameObject("Visible corridor").transform;root.SetParent(room,false);
            void Box(string name,Vector3 position,Vector3 size,Material material)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root,false);
                go.transform.SetPositionAndRotation(position,Quaternion.identity);go.transform.localScale=size;
                go.GetComponent<MeshRenderer>().sharedMaterial=material;
            }
            Box("Corridor floor",new Vector3(-3.10f,-.045f,-1.61f),new Vector3(1.88f,.09f,1.44f),floor);
            Box("Corridor far wall",new Vector3(-4.04f,1.24f,-1.61f),new Vector3(.10f,2.48f,1.54f),wall);
            Box("Corridor rear wall",new Vector3(-3.17f,1.24f,-2.33f),new Vector3(1.84f,2.48f,.10f),wall);
            Box("Corridor front wall",new Vector3(-3.17f,1.24f,-.89f),new Vector3(1.84f,2.48f,.10f),wall);
            Box("Corridor ceiling",new Vector3(-3.10f,2.49f,-1.61f),new Vector3(1.88f,.10f,1.44f),wall);
            var light=new GameObject("Corridor warm ceiling light").AddComponent<Light>();light.transform.SetParent(root,false);
            light.transform.position=new Vector3(-3.07f,1.94f,-1.55f);light.type=LightType.Point;
            light.color=new Color(1,.70f,.43f);light.intensity=.85f;light.range=3.3f;light.shadows=LightShadows.Soft;
            light.shadowBias=.01f;light.shadowNormalBias=.025f;
        }

        public static bool TryInteract(Camera camera)
        {
            if(!Physics.Raycast(camera.ViewportPointToRay(new Vector3(.5f,.5f)),out var hit,4.5f))return false;
            var door=hit.collider.GetComponentInParent<RoomDoor>();if(!door)return false;
            door.SetOpen(!door.IsOpen);return true;
        }
        public void SetOpen(bool open,bool immediate=false)
        {
            IsOpen=open;
            if(immediate){Angle=open?90:0;velocity=0;ApplyAngle();}
        }
        void Update(){Advance(Time.deltaTime);}
        public void Advance(float deltaTime)
        {
            Angle=Mathf.SmoothDampAngle(Angle,IsOpen?90:0,ref velocity,.28f,Mathf.Infinity,deltaTime);
            ApplyAngle();
        }
        void ApplyAngle(){transform.localRotation=closedRotation*Quaternion.Euler(0,Angle,0);}
    }
}
