using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrGame
{
    // Original pen strokes for the clue 1114; no external font or image texture is needed.
    public sealed class FridgePasswordNote : MonoBehaviour
    {
        Mesh ink;Material material;
        public Renderer Paper { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var room=GameObject.Find("Furnished night room");if(!room||room.GetComponentInChildren<FridgePasswordNote>())return;
            var paper=room.GetComponentsInChildren<Renderer>().FirstOrDefault(r=>r.name=="Fridge note");if(!paper)return;
            var go=new GameObject("Fridge handwritten 1114");go.transform.SetParent(room.transform,false);
            go.transform.SetPositionAndRotation(new Vector3(paper.bounds.center.x-.044f,paper.bounds.center.y-.030f,paper.bounds.min.z-.0003f),Quaternion.identity);
            var note=go.AddComponent<FridgePasswordNote>();note.Paper=paper;
            var strokes=new List<Vector2[]>();
            for(int i=0;i<3;i++)
            {
                float x=i*.022f,y=i==1?.0015f:0;
                strokes.Add(new[]{new Vector2(x+.001f,y+.027f),new Vector2(x+.006f,y+.031f),new Vector2(x+.010f,y+.036f),
                    new Vector2(x+.008f,y+.026f),new Vector2(x+.005f,y+.008f),new Vector2(x+.005f,y+.002f),new Vector2(x+.013f,y+.003f)});
            }
            strokes.Add(new[]{new Vector2(.080f,.036f),new Vector2(.073f,.024f),new Vector2(.067f,.013f),new Vector2(.075f,.013f),new Vector2(.089f,.014f)});
            strokes.Add(new[]{new Vector2(.084f,.035f),new Vector2(.082f,.024f),new Vector2(.079f,.010f),new Vector2(.078f,-.002f)});
            note.ink=BuildInk(strokes,.00135f,true);note.ink.name="Handwritten 1114 pen strokes";
            note.material=new Material(paper.sharedMaterial){name="Dark blue ballpoint ink",color=new Color(.055f,.075f,.115f)};
            note.material.SetFloat("_Metallic",0);note.material.SetFloat("_Glossiness",.08f);
            go.AddComponent<MeshFilter>().sharedMesh=note.ink;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=note.material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.lightProbeUsage=LightProbeUsage.BlendProbes;
        }

        internal static Mesh BuildInk(IEnumerable<Vector2[]> strokes,float radius,bool smooth)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            void Triangle(Vector2 a,Vector2 b,Vector2 c)
            {
                int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);
                // The fridge faces the seated player along -Z.
                if(Vector3.Cross((Vector3)(b-a),(Vector3)(c-a)).z>0)triangles.AddRange(new[]{n,n+2,n+1});
                else triangles.AddRange(new[]{n,n+1,n+2});
            }
            foreach(var stroke in strokes)
            {
                var points=new List<Vector2>();
                for(int i=0;i<stroke.Length-1;i++)
                {
                    var a=stroke[Mathf.Max(0,i-1)];var b=stroke[i];var c=stroke[i+1];var d=stroke[Mathf.Min(stroke.Length-1,i+2)];
                    int samples=smooth?8:1;
                    for(int j=0;j<samples;j++)
                    {
                        float t=(float)j/samples;
                        points.Add(smooth?.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t):b);
                    }
                }
                points.Add(stroke[stroke.Length-1]);
                for(int i=0;i<points.Count;i++)
                {
                    float r=radius*(smooth?(.9f+.1f*Mathf.Sin(i*.63f)):1);
                    for(int k=0;k<8;k++)
                    {
                        float angle=k*Mathf.PI/4,next=(k+1)*Mathf.PI/4;
                        Triangle(points[i],points[i]+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*r,points[i]+new Vector2(Mathf.Cos(next),Mathf.Sin(next))*r);
                    }
                    if(i==0)continue;
                    var offset=Vector2.Perpendicular((points[i]-points[i-1]).normalized)*r;
                    Triangle(points[i-1]-offset,points[i-1]+offset,points[i]+offset);
                    Triangle(points[i-1]-offset,points[i]+offset,points[i]-offset);
                }
            }
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        void OnDestroy(){if(ink)Destroy(ink);if(material)Destroy(material);}
    }
}
