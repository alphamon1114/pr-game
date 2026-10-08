using UnityEngine;

namespace PrGame
{
    [RequireComponent(typeof(Camera))]
    public sealed class NightLighting : MonoBehaviour
    {
        public Shader shader;
        [Range(.02f,.4f)] public float contactRadius=.16f;
        [Range(0,2)] public float contactStrength=.65f;
        [Range(.5f,3)] public float exposure=1.45f;
        Material material;
        Camera view;
        MonitorSurface monitor;
        void OnEnable()
        {
            view=GetComponent<Camera>();view.depthTextureMode|=DepthTextureMode.Depth|DepthTextureMode.DepthNormals;
            monitor=FindAnyObjectByType<MonitorSurface>();
            if(shader && shader.isSupported)material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
        }
        void OnRenderImage(RenderTexture source,RenderTexture destination)
        {
            if(!material){Graphics.Blit(source,destination);return;}
            var ao=RenderTexture.GetTemporary(Mathf.Max(1,source.width/2),Mathf.Max(1,source.height/2),0,RenderTextureFormat.R8,RenderTextureReadWrite.Linear);
            ao.filterMode=FilterMode.Bilinear;
            float tangent=Mathf.Tan(view.fieldOfView*Mathf.Deg2Rad*.5f);
            material.SetVector("_ProjectionScale",new Vector4(tangent*view.aspect,tangent,view.farClipPlane,view.nearClipPlane));
            material.SetFloat("_Radius",contactRadius);material.SetFloat("_Strength",contactStrength);material.SetFloat("_Exposure",exposure);
            material.SetFloat("_HasMonitor",monitor && monitor.screenCollider ? 1 : 0);
            material.SetMatrix("_ViewToWorld",view.cameraToWorldMatrix);
            if(monitor && monitor.screenCollider)material.SetMatrix("_WorldToMonitor",monitor.screenCollider.transform.worldToLocalMatrix);
            Graphics.Blit(source,ao,material,0);material.SetTexture("_OcclusionTex",ao);Graphics.Blit(source,destination,material,1);
            RenderTexture.ReleaseTemporary(ao);
        }
        void OnDisable(){if(material)Destroy(material);}
    }
}
