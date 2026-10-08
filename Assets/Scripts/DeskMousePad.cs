using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrGame
{
    // Replace the imported deskmat at scene load, preserving the room's authored lighting and layout.
    public sealed class DeskMousePad : MonoBehaviour
    {
        public const float Width=.490f, Depth=.420f, Thickness=.004f;
        public MeshRenderer Surface { get; private set; }
        readonly List<Object> generated=new List<Object>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var room=GameObject.Find("Furnished night room");
            if(!room || room.GetComponentInChildren<DeskMousePad>())return;
            var renderers=room.GetComponentsInChildren<MeshRenderer>();
            var desk=renderers.FirstOrDefault(r=>r.name=="Desk walnut top");
            var old=renderers.FirstOrDefault(r=>r.name=="Desk mat");
            if(!desk || !old)return;
            float top=desk.bounds.max.y;
            var root=new GameObject("Saturn Pro red mousepad");root.transform.SetParent(room.transform,false);
            root.transform.SetPositionAndRotation(new Vector3(.22f,top,1.13f),Quaternion.identity);
            var pad=root.AddComponent<DeskMousePad>();pad.Build(old.sharedMaterial);
            old.gameObject.SetActive(false);
            RestOn(GameObject.Find("Refined / Keyboard"),top+.0002f);
            RestOn(GameObject.Find("Refined / Mouse"),top+Thickness+.0002f);
            // Leave a clear strip beside the pad for the existing notebook and its pen.
            var notebook=renderers.FirstOrDefault(r=>r.name=="Notebook");
            if(notebook)
            {
                var offset=new Vector3(.075f,top+.0002f-notebook.bounds.min.y,.070f);
                foreach(var r in renderers.Where(r=>r.name=="Notebook"||r.name=="Notebook pages"||r.name=="Pen"))
                {
                    r.transform.position+=offset;r.gameObject.isStatic=false;r.lightmapIndex=-1;
                    r.receiveGI=ReceiveGI.LightProbes;r.lightProbeUsage=LightProbeUsage.BlendProbes;
                }
            }
        }

        static void RestOn(GameObject prop,float height)
        {
            if(!prop)return;
            var renderers=prop.GetComponentsInChildren<Renderer>();if(renderers.Length==0)return;
            float bottom=renderers.Min(r=>r.bounds.min.y);
            prop.transform.position+=Vector3.up*(height-bottom);
            foreach(var r in renderers)
            {
                r.gameObject.isStatic=false;r.lightmapIndex=-1;r.lightProbeUsage=LightProbeUsage.BlendProbes;
            }
        }

        Material Cloth(Material source,string name,Color color)
        {
            var material=new Material(source){name=name,color=color};generated.Add(material);
            material.SetFloat("_Metallic",0);material.SetFloat("_BumpScale",.075f);
            material.SetFloat("_SmoothnessScale",.20f);material.mainTextureScale=new Vector2(7,6);
            return material;
        }

        void Build(Material source)
        {
            var cloth=Cloth(source,"Saturn Pro ruby cloth",new Color(.69f,.037f,.075f));
            var edge=Cloth(source,"Saturn Pro stitched red edge",new Color(.51f,.023f,.049f));
            var thread=Cloth(source,"Saturn Pro red thread",new Color(.62f,.035f,.064f));
            var points=Outline(Width,Depth,.009f);var inset=Outline(Width-.0016f,Depth-.0016f,.0082f);
            int n=points.Count;
            var vertices=new List<Vector3>();var uv=new List<Vector2>();
            void Vertex(Vector2 p,float y){vertices.Add(new Vector3(p.x,y,p.y));uv.Add(new Vector2(p.x/Width+.5f,p.y/Depth+.5f));}
            foreach(var p in points)Vertex(p,0);
            foreach(var p in points)Vertex(p,Thickness-.0007f);
            foreach(var p in inset)Vertex(p,Thickness);
            Vertex(Vector2.zero,Thickness);int center=vertices.Count-1;
            Vertex(Vector2.zero,0);int bottom=vertices.Count-1;
            var face=new List<int>();var sides=new List<int>();
            for(int i=0;i<n;i++)
            {
                int j=(i+1)%n;
                face.AddRange(new[]{center,n*2+j,n*2+i});
                sides.AddRange(new[]{bottom,i,j});
                for(int ring=0;ring<2;ring++)
                {
                    int a=ring*n+i,b=ring*n+j,c=(ring+1)*n+j,d=(ring+1)*n+i;
                    sides.AddRange(new[]{a,c,b,a,d,c});
                }
            }
            var mesh=new Mesh{name="Rounded 490 x 420 x 4 mm mousepad"};generated.Add(mesh);
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.subMeshCount=2;
            mesh.SetTriangles(face,0);mesh.SetTriangles(sides,1);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            Surface=gameObject.AddComponent<MeshRenderer>();Surface.sharedMaterials=new[]{cloth,edge};
            Surface.lightProbeUsage=LightProbeUsage.BlendProbes;
            AddStitching(thread);
            AddLabel("SATURN PRO",new Vector3(Width*.5f-.015f,Thickness+.00006f,-Depth*.5f+.031f),.046f);
            AddLabel("pulsar",new Vector3(Width*.5f-.015f,Thickness+.00006f,-Depth*.5f+.015f),.029f);
        }

        void AddLabel(string text,Vector3 position,float width)
        {
            var font=Resources.Load<Font>("Fonts/NotoSansKR-Regular");if(!font)return;
            var go=new GameObject("Mousepad printed "+text);go.transform.SetParent(transform,false);
            go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(90,0,0);
            var label=go.AddComponent<TextMesh>();label.font=font;label.fontSize=72;label.characterSize=.01f;
            label.text=text;label.anchor=TextAnchor.LowerRight;label.alignment=TextAlignment.Right;label.color=new Color(.62f,.59f,.56f);
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=font.material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            if(renderer.bounds.size.x>0)go.transform.localScale=Vector3.one*(width/renderer.bounds.size.x);
        }

        static List<Vector2> Outline(float width,float depth,float radius)
        {
            var points=new List<Vector2>();
            for(int corner=0;corner<4;corner++)
            {
                float start=corner*Mathf.PI*.5f;
                var center=new Vector2((corner==0||corner==3?1:-1)*(width*.5f-radius),(corner<2?1:-1)*(depth*.5f-radius));
                for(int i=0;i<=12;i++)
                {
                    float angle=start+i*Mathf.PI/24;
                    points.Add(center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius);
                }
            }
            return points;
        }

        void AddStitching(Material material)
        {
            var outline=Outline(Width-.003f,Depth-.003f,.0075f);
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int i=0;i<outline.Count;i++)
            {
                var a=outline[i];var b=outline[(i+1)%outline.Count];
                int count=Mathf.Max(1,Mathf.RoundToInt(Vector2.Distance(a,b)/.0018f));
                var inward=Vector2.Perpendicular((b-a).normalized)*.00065f;
                for(int j=0;j<count;j++)
                {
                    var p=Vector2.Lerp(a,b,(j+.25f)/count);var q=Vector2.Lerp(a,b,(j+.5f)/count);
                    int index=vertices.Count;
                    foreach(var v in new[]{p-inward,p+inward,q+inward,q-inward})
                    {vertices.Add(new Vector3(v.x,Thickness+.000035f,v.y));uv.Add(v*14);}
                    triangles.AddRange(new[]{index,index+1,index+2,index,index+2,index+3});
                }
            }
            var mesh=new Mesh{name="Low profile edge stitches"};generated.Add(mesh);
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            var go=new GameObject("Red edge stitching");go.transform.SetParent(transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        void OnDestroy(){foreach(var asset in generated)if(asset)Destroy(asset);}
    }
}
