using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PrGame
{
    public sealed class DeskComputerPower : MonoBehaviour
    {
        PortfolioDesktop desktop;
        Renderer[] poweredRenderers;
        Light screenGlow;
        Material buttonMaterial,inkMaterial;
        Mesh symbol;
        bool previousPower;
        public bool PoweredVisualsActive { get; private set; }
        public Vector3 ButtonPosition=>transform.position;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var room=GameObject.Find("Furnished night room");var os=FindAnyObjectByType<PortfolioDesktop>();
            if(!room||!os||room.GetComponentInChildren<DeskComputerPower>())return;
            var chassis=room.GetComponentsInChildren<Renderer>().FirstOrDefault(r=>r.name=="PC chassis");if(!chassis)return;
            var root=new GameObject("Computer power button");root.transform.SetParent(room.transform,false);
            root.transform.SetPositionAndRotation(new Vector3(chassis.bounds.center.x,chassis.bounds.max.y-.055f,chassis.bounds.min.z-.004f),Quaternion.identity);
            var power=root.AddComponent<DeskComputerPower>();power.desktop=os;
            var collider=root.AddComponent<BoxCollider>();collider.size=new Vector3(.085f,.075f,.028f);
            var button=GameObject.CreatePrimitive(PrimitiveType.Cylinder);button.name="Power switch face";button.transform.SetParent(root.transform,false);
            button.transform.localRotation=Quaternion.Euler(90,0,0);button.transform.localScale=new Vector3(.041f,.002f,.041f);
            button.GetComponent<Collider>().enabled=false;Destroy(button.GetComponent<Collider>());
            power.buttonMaterial=new Material(chassis.sharedMaterial){name="Power switch metal",color=new Color(.22f,.26f,.30f)};
            power.buttonMaterial.SetFloat("_Metallic",.6f);power.buttonMaterial.SetFloat("_Glossiness",.35f);
            button.GetComponent<Renderer>().sharedMaterial=power.buttonMaterial;
            var arc=new List<Vector2>();
            for(int i=0;i<=30;i++){float a=(50+i*260f/30)*Mathf.Deg2Rad;arc.Add(new Vector2(Mathf.Sin(a),Mathf.Cos(a))*.010f);}
            power.symbol=FridgePasswordNote.BuildInk(new[]{arc.ToArray(),new[]{new Vector2(0,.014f),new Vector2(0,.002f)}},.001f,false);
            power.symbol.name="Power button symbol";
            var icon=new GameObject("Power switch icon");icon.transform.SetParent(root.transform,false);icon.transform.localPosition=new Vector3(0,0,-.0022f);
            icon.AddComponent<MeshFilter>().sharedMesh=power.symbol;
            power.inkMaterial=new Material(chassis.sharedMaterial){name="Power switch indicator"};
            icon.AddComponent<MeshRenderer>().sharedMaterial=power.inkMaterial;
            power.poweredRenderers=FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r=>r.name.StartsWith("PC fan rim")||r.name=="Power LED"||r.name.StartsWith("RGB underkey")||r.name.StartsWith("Status indicator")).ToArray();
            power.screenGlow=GameObject.Find("Screen bounced light")?.GetComponent<Light>();
            power.ApplyPower();
        }
        public static bool IsAimedAt(Camera camera)
        {return FindAimedAt(camera)!=null;}
        static DeskComputerPower FindAimedAt(Camera camera)
        {
            if(!camera||!Physics.Raycast(camera.ViewportPointToRay(new Vector3(.5f,.5f)),out var hit,4.5f))return null;
            return hit.collider.GetComponentInParent<DeskComputerPower>();
        }
        public static bool TryInteract(Camera camera)
        {
            var power=FindAimedAt(camera);if(!power)return false;
            power.desktop.PowerOn();return true;
        }
        public static void SyncNow(PortfolioDesktop os)
        {
            foreach(var power in FindObjectsByType<DeskComputerPower>(FindObjectsSortMode.None))
                if(power.desktop==os)power.ApplyPower();
        }
        void Update(){if(desktop&&desktop.IsPoweredOn!=previousPower)ApplyPower();}
        void ApplyPower()
        {
            previousPower=desktop.IsPoweredOn;PoweredVisualsActive=previousPower;
            foreach(var renderer in poweredRenderers)if(renderer)renderer.enabled=previousPower;
            if(screenGlow)screenGlow.enabled=previousPower;
            inkMaterial.color=previousPower?new Color(.25f,.70f,.8f):new Color(.60f,.65f,.68f);
            inkMaterial.EnableKeyword("_EMISSION");inkMaterial.SetColor("_EmissionColor",previousPower?new Color(.04f,.19f,.25f):Color.black);
        }
        void OnDestroy(){if(symbol)Destroy(symbol);if(buttonMaterial)Destroy(buttonMaterial);if(inkMaterial)Destroy(inkMaterial);}
    }
}
