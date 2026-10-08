using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PrGame.Editor
{
    public static class RoomPropVerification
    {
        static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
        public static void Check(MonitorSurface surface)
        {
            var camera=surface.viewCamera;var position=camera.transform.position;
            var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
            var door=UnityEngine.Object.FindAnyObjectByType<RoomDoor>();
            Assert(door,"Interactive door missing");
            try
            {
                door.SetOpen(false,true);Physics.SyncTransforms();
                camera.transform.LookAt(new Vector3(-2.17f,1.04f,-1.63f));
                RoomViewVerification.Capture(camera,"door-closed-from-seat");
                Assert(RoomDoor.TryInteract(camera)&&door.IsOpen,"Seated center click failed to open door");
                door.Advance(1f/60);Assert(door.Angle>0&&door.Angle<10,"Door snapped instead of easing open");
                for(int i=0;i<180;i++)
                {
                    door.Advance(1f/60);
                    foreach(var rose in new[]{door.InsideRose,door.OutsideRose})
                    {
                        Assert(rose.parent==door.Panel,"Handle is not attached to moving panel");
                        float expected=rose==door.InsideRose ? .0025f : -.0465f;
                        var local=door.Panel.InverseTransformPoint(rose.position);
                        Assert(Mathf.Abs(local.x-expected)<.0001f&&Mathf.Abs(local.z-.82f)<.0001f,"Handle detached during swing");
                    }
                }
                Assert(Mathf.Abs(door.Angle-90)<.01f,"Door did not fully open");Physics.SyncTransforms();
                Assert(Physics.Raycast(camera.ViewportPointToRay(new Vector3(.5f,.5f)),out var hit,6),"Doorway has no visible outside geometry");
                Assert(hit.collider.name.StartsWith("Corridor"),"Open door blocks seated sightline: "+hit.collider.name);
                RoomViewVerification.Capture(camera,"door-open-from-seat");
                camera.fieldOfView=32;RoomViewVerification.Capture(camera,"door-open-inspection");
                camera.transform.LookAt(door.Panel.TransformPoint(door.PanelCollider.center));
                Assert(RoomDoor.TryInteract(camera)&&!door.IsOpen,"Open door cannot be clicked closed");
                for(int i=0;i<180;i++)door.Advance(1f/60);
                Assert(Mathf.Abs(door.Angle)<.01f,"Door failed to close");
                var panelBounds=door.Panel.GetComponentInChildren<MeshRenderer>().bounds;
                Assert(Mathf.Abs(panelBounds.min.x+2.192f)<.001f&&Mathf.Abs(panelBounds.max.x+2.148f)<.001f,"Door faces differ from handle mounting planes");
                var old=GameObject.Find("Furnished night room").GetComponentsInChildren<Transform>(true);
                Assert(!old.First(t=>t.name=="Door handle").gameObject.activeSelf,"Floating original handle still visible");
                Assert(!old.First(t=>t.name=="Door corridor darkness").gameObject.activeSelf,"Old plane blocks outside");

                var pad=UnityEngine.Object.FindAnyObjectByType<DeskMousePad>();Assert(pad,"Mousepad missing");
                var roomRenderers=GameObject.Find("Furnished night room").GetComponentsInChildren<Renderer>(true);
                var desk=roomRenderers.First(r=>r.name=="Desk walnut top").bounds;
                var p=pad.Surface.bounds;
                Assert(Mathf.Abs(p.size.x-.490f)<.0001f&&Mathf.Abs(p.size.z-.420f)<.0001f&&Mathf.Abs(p.size.y-.004f)<.0001f,"Mousepad dimensions incorrect");
                Assert(Mathf.Abs(p.min.y-desk.max.y)<.0001f&&p.min.x>desk.min.x&&p.max.x<desk.max.x&&p.min.z>desk.min.z&&p.max.z<desk.max.z,"Mousepad floats or hangs off desk");
                Assert(!roomRenderers.First(r=>r.name=="Desk mat").gameObject.activeSelf,"Extended deskmat remains active");
                var mouse=Bounds(GameObject.Find("Refined / Mouse"));var keyboard=Bounds(GameObject.Find("Refined / Keyboard"));
                Assert(Mathf.Abs(mouse.min.y-p.max.y-.0002f)<.0001f&&mouse.min.x>p.min.x&&mouse.max.x<p.max.x&&mouse.min.z>p.min.z&&mouse.max.z<p.max.z,"Mouse not resting on new pad");
                Assert(keyboard.max.x<p.min.x&&Mathf.Abs(keyboard.min.y-desk.max.y-.0002f)<.0001f,"Keyboard overlaps mousepad or floats after deskmat removal");
                var notebook=roomRenderers.First(r=>r.name=="Notebook").bounds;
                Assert(notebook.min.x>p.max.x&&notebook.max.x<desk.max.x&&notebook.min.z>desk.min.z&&notebook.max.z<desk.max.z,"Notebook overlaps pad or hangs off desk");
                var rug=roomRenderers.First(r=>r.name=="Rug");
                Assert(rug.gameObject.activeSelf&&rug.sharedMaterial!=pad.Surface.sharedMaterial,"Mousepad change altered rug material");
                camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=58;
                camera.transform.LookAt(new Vector3(.08f,.78f,1.12f));RoomViewVerification.Capture(camera,"desk-pad-from-seat");
                camera.transform.position=new Vector3(.08f,1.67f,.78f);camera.transform.LookAt(new Vector3(.05f,.78f,1.16f));camera.fieldOfView=55;
                RoomViewVerification.Capture(camera,"red-mousepad-detail");
                File.WriteAllText("Logs/OSQA/room-props-results.txt","PASS: seated center-click opens reversed rear-hinged door; eased 0-90 degree swing; handle roses remain attached throughout motion and overlap actual door faces; seated open sightline reaches corridor; click closes door; original floating handle and black blocker disabled. Mouse-only red pad is 490 x 420 x 4 mm, within desk bounds, resting on desk; mouse rests on pad; keyboard rests directly on desk with no pad overlap; old deskmat hidden; rug unchanged.\n");
                Debug.Log("PR_GAME_ROOM_PROPS_VERIFIED");
            }
            finally {door.SetOpen(false,true);Physics.SyncTransforms();camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;}
        }
        static Bounds Bounds(GameObject go)
        {
            var renderers=go.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
            foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
