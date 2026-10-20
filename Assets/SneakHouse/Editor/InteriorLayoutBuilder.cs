using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SneakHouse;

namespace SneakHouseEditor
{
    public static class InteriorLayoutBuilder
    {
        private const string Root = "Assets/SneakHouse/";
        private static Transform group;
        [MenuItem("Sneak House/Set up interior layout variants")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            GameSession game = UnityEngine.Object.FindFirstObjectByType<GameSession>();
            RuntimeNavigation navigation = game.GetComponent<RuntimeNavigation>();
            if (game.GetComponent<GuardEncounter>() == null) throw new InvalidOperationException("Set up guard encounters first.");
            if (!AssetDatabase.IsValidFolder(Root+"Prefabs/Interiors")) AssetDatabase.CreateFolder(Root+"Prefabs","Interiors");
            Transform fallback = navigation.geometry.Cast<Transform>().FirstOrDefault(t => t.name == "INTERIOR / Authored fallback");
            if (fallback == null)
            {
                fallback = new GameObject("INTERIOR / Authored fallback").transform;
                fallback.SetParent(navigation.geometry,false); Undo.RegisterCreatedObjectUndo(fallback.gameObject,"Group authored furniture");
                var names = new HashSet<string> { "loungeSofa","loungeChair","tableCoffee","rugRectangle","pottedPlant","grandfather_clock",
                    "bookcaseOpen","clay_vase","desk","chairDesk","computerScreen","computerKeyboard","filing_cabinet","books","display_plinth","vault_safe" };
                foreach (Transform child in navigation.geometry.Cast<Transform>().ToArray())
                {
                    string key = child.name.Split(new[] {" / "},StringSplitOptions.None)[0];
                    if (names.Contains(key) && Mathf.Abs(child.position.x) < 8 && child.position.z > -6 && child.position.z < 6)
                        Undo.SetTransformParent(child,fallback,"Preserve authored fallback");
                }
            }
            InteriorLayout layout = game.GetComponent<InteriorLayout>();
            if (layout == null) layout = Undo.AddComponent<InteriorLayout>(game.gameObject);
            Undo.RecordObject(layout,"Assign interior catalogue");
            layout.geometry = navigation.geometry; layout.authoredFurniture = fallback.gameObject;
            layout.deskHiding = UnityEngine.Object.FindObjectsByType<HidingSpot>(FindObjectsSortMode.None).First(s => s.displayName == "under desk");
            string[] namesByRoom = { "Living room", "Office", "Collection room" };
            Vector3[] approaches = { P(5.25f,-2.6f),P(-6.35f,3.05f),P(5.85f,3.35f) };
            LootTarget[] targets = { game.targets[2],game.targets[0],game.targets[1] };
            layout.rooms = new InteriorLayout.Room[3];
            for (int room = 0; room < 3; room++)
            {
                Transform approach = fallback.Cast<Transform>().FirstOrDefault(t => t.name == "Fallback approach " + room);
                if (approach == null) { approach = new GameObject("Fallback approach " + room).transform; approach.SetParent(fallback,false); }
                approach.position = approaches[room];
                layout.rooms[room] = new InteriorLayout.Room { label = namesByRoom[room],target = targets[room],fallbackApproach = approach,
                    variants = Enumerable.Range(0,3).Select(v => Build(room,v)).ToArray() };
            }
            EditorUtility.SetDirty(layout); EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene); AssetDatabase.SaveAssets();
        }
        private static Vector3 P(float x, float z, float y = .06f) => new Vector3(x,y,z);
        private static Transform Socket(string name, Vector3 point)
        {
            Transform result = new GameObject(name).transform; result.SetParent(group,false); result.position = point; return result;
        }
        private static RoomLayoutVariant Build(int room, int variant)
        {
            group = new GameObject("Interior group / temporary").transform;
            try
            {
                RoomLayoutVariant definition = group.gameObject.AddComponent<RoomLayoutVariant>();
                if (room == 0) Living(definition,variant);
                else if (room == 1) Office(definition,variant);
                else Collection(definition,variant);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(group.gameObject,Root+"Prefabs/Interiors/room-"+room+"-"+variant+".prefab");
                return prefab.GetComponent<RoomLayoutVariant>();
            }
            finally { UnityEngine.Object.DestroyImmediate(group.gameObject); }
        }
        private static void Living(RoomLayoutVariant room, int v)
        {
            room.label = new[] { "Reading lounge","Conversation lounge","Music corner" }[v];
            Prop("loungeSofa",P(-5.5f,-.4f),2.8f,1,180);
            if (v == 1) Prop("loungeSofa",P(-5.3f,-3.8f),2.6f,1,0);
            else Prop("loungeChair",P(-6.1f,-3.1f),1.05f,1,75);
            Bounds coffee = Prop("tableCoffee",P(-4.8f,-2.15f),1.65f,.52f);
            Prop("rugRectangle",P(-4.9f,-2.1f,.04f),3.7f,.015f,0,false);
            Prop("books",P(-4.45f,-2.1f,coffee.max.y+.01f),.35f,.18f,15,false);
            Prop("lampRoundTable",P(-5.25f,-2.15f,coffee.max.y+.01f),.36f,.43f,0,false);
            Prop("pottedPlant",P(-7.15f,-.4f),.65f,1.4f);
            if (v == 0) Prop("bookcaseOpen",P(-7.25f,-2.2f),1.3f,1.6f,90);
            if (v == 2)
            {
                Bounds cabinet = Prop("bookcaseClosedDoors",P(-2.2f,-5.1f),1.3f,1.05f);
                Prop("gramophone",P(-2.2f,-5.1f,cabinet.max.y+.01f),.55f,.65f,30,false);
            }
            Vector3 console = v == 0 ? P(5.3f,-3.8f) : v == 1 ? P(6.1f,-4.7f) : P(5.5f,-4.7f);
            Bounds table = Prop("tableCoffee",console,1.65f,.6f);
            Vector3 approach = console + (v == 1 ? Vector3.left*1.25f : Vector3.forward*1.1f);
            room.targetSocket = Socket("Console surface",new Vector3(console.x,table.max.y+.03f,console.z));
            room.targetApproach = Socket("Console approach",approach);
            Prop("radio",new Vector3(console.x+.48f,table.max.y+.01f,console.z),.3f,.25f,180,false);
            Prop("loungeSofa",P(2.7f,-5.2f),2.15f,.95f);
            Prop("grandfather_clock",v == 2 ? P(7.25f,-4.5f) : P(7.3f,-.3f),.7f,1.7f,270);
            if (v != 2) Prop("bookcaseOpen",P(7.35f,v == 0 ? -4.6f : -2.4f),1.25f,1.65f,270);
            else Prop("pottedPlant",P(7.2f,-.3f),.65f,1.4f);
            Prop("plantSmall1",P(-5.0f,-2.4f,coffee.max.y+.01f),.18f,.24f,0,false);
        }
        private static void Office(RoomLayoutVariant room, int v)
        {
            room.label = new[] { "Director office","Archive office","Private study" }[v];
            Vector3 desk = v == 0 ? P(-5.6f,4.45f) : v == 1 ? P(-2,4.65f) : P(-1.25f,4.2f);
            Bounds top = Prop("desk",desk,2.1f,.9f,v == 2 ? 90 : 180);
            Vector3 chair = v == 2 ? P(-2.55f,4) : desk + Vector3.back*.95f;
            Prop("chairDesk",chair,.65f,1.05f,v == 2 ? 90 : 0);
            Vector3 drive = v == 2 ? new Vector3(desk.x-.2f,top.max.y+.03f,desk.z+.45f) : new Vector3(desk.x-.5f,top.max.y+.03f,desk.z-.2f);
            Vector3 approach = v == 0 ? P(-6.65f,3.35f) : v == 1 ? P(-3.2f,3.6f) : P(-2.65f,4.9f);
            room.targetSocket = Socket("Drive surface",drive); room.targetApproach = Socket("Drive approach",approach);
            room.hidingAnchor = Socket("Under desk",desk); room.hidingApproach = Socket("Desk hiding approach",approach);
            Vector3 screen = v == 2 ? new Vector3(desk.x+.1f,top.max.y,desk.z-.2f) : new Vector3(desk.x+.35f,top.max.y,desk.z+.05f);
            Prop("computerScreen",screen,.52f,.42f,v == 2 ? 90 : 180,false);
            Prop("computerKeyboard",screen+(v == 2 ? Vector3.left : Vector3.back)*.3f,.4f,.05f,v == 2 ? 90 : 180,false);
            Prop("books",new Vector3(desk.x+(v == 2 ? 0 : -.65f),top.max.y,desk.z+(v == 2 ? -.5f : .18f)),.28f,.18f,0,false);
            Prop("rugRectangle",P(-4.2f,3.8f,.04f),3.1f,.015f,0,false);
            if (v == 0)
            {
                Prop("bookcaseOpen",P(-7.3f,4.6f),1.2f,1.7f,90);
                Prop("filing_cabinet",P(-1.3f,5.25f),.9f,1.45f,180);
            }
            else if (v == 1)
            {
                Prop("bookcaseOpen",P(-6.9f,5.15f),1.6f,1.65f);
                Prop("filing_cabinet",P(-5.15f,5.25f),.9f,1.45f,180);
                Prop("loungeChair",P(-7.05f,3.4f),.7f,.85f,70);
            }
            else
            {
                Prop("bookcaseOpen",P(-5.6f,5.4f),2.1f,1.7f);
                Prop("filing_cabinet",P(-7.05f,4.5f),.85f,1.3f,180);
                Prop("pottedPlant",P(-4,4.0f),.6f,1.1f);
            }
            Prop("pottedPlant",P(-1.1f,1.9f),.6f,1.25f);
            Prop("clay_vase",P(-7.2f,1.9f),.45f,.65f);
        }
        private static void Collection(RoomLayoutVariant room, int v)
        {
            room.label = new[] { "Gallery","Antique salon","Secure collection" }[v];
            Vector3 watch = v == 0 ? P(5.85f,4.7f) : v == 1 ? P(2,4.65f) : P(6.8f,4.3f);
            Bounds top = Prop("display_plinth",watch,.85f,.9f);
            room.targetSocket = Socket("Watch surface",new Vector3(watch.x,top.max.y+.03f,watch.z));
            room.targetApproach = Socket("Watch approach",watch+(v == 2 ? Vector3.left : Vector3.back)*1.15f);
            Prop("rugRectangle",P(4.5f,3.9f,.04f),3.1f,.015f,0,false);
            if (v == 0)
            {
                Prop("vault_safe",P(1.4f,5.1f),1.1f,1.35f,180);
                Bounds display = Prop("display_plinth",P(2.1f,2.5f),.75f,.8f);
                Prop("museum_mask",P(2.1f,2.5f,display.max.y+.01f),.35f,.5f,180,false);
                Prop("pottedPlant",P(7.15f,5.3f),.7f,1.4f);
                Bounds second = Prop("display_plinth",P(7.15f,3.7f),.6f,.75f);
                Prop("clay_vase",P(7.15f,3.7f,second.max.y),.35f,.5f,0,false);
            }
            else if (v == 1)
            {
                Bounds cabinet = Prop("bookcaseClosedDoors",P(6.0f,5.25f),2,1.15f);
                Prop("gramophone",P(5.6f,5.25f,cabinet.max.y),.55f,.65f,180,false);
                Prop("gold_tray",P(6.65f,5.25f,cabinet.max.y),.5f,.12f,0,false);
                Prop("vault_safe",P(1.1f,1.95f),.95f,1.25f,90);
                Prop("clay_vase",P(6.75f,3.9f),.65f,1.1f);
                Prop("pottedPlant",P(7.2f,1.9f),.6f,1.2f);
            }
            else
            {
                Prop("vault_safe",P(1.3f,5.0f),1.25f,1.55f,180);
                Prop("filing_cabinet",P(1.4f,2.4f),.9f,1.45f);
                Bounds display = Prop("display_plinth",P(3.1f,5.2f),.65f,.8f);
                Prop("museum_mask",P(3.1f,5.2f,display.max.y),.3f,.4f,180,false);
                Prop("grandfather_clock",P(5.1f,5.4f),.7f,1.6f,180);
                Prop("pottedPlant",P(7.1f,1.85f),.6f,1.2f);
            }
        }
        private static Bounds Prop(string name, Vector3 position, float width, float height, float yaw = 0, bool collision = true)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Models/Props/"+name+".fbx");
            if (asset == null) throw new InvalidOperationException("Missing interior prop " + name);
            GameObject art = (GameObject)PrefabUtility.InstantiatePrefab(asset,group); art.name = name;
            art.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));
            Bounds bounds = BoundsOf(art);
            art.transform.localScale *= Mathf.Min(width/Mathf.Max(bounds.size.x,bounds.size.z),height/Mathf.Max(.001f,bounds.size.y));
            art.transform.position += Vector3.up*(position.y-BoundsOf(art).min.y); bounds = BoundsOf(art);
            if (collision)
            {
                GameObject proxy = new GameObject(name+" / Navigation proxy"); proxy.transform.SetParent(group,false);
                proxy.transform.position = bounds.center; proxy.layer = 8; proxy.AddComponent<BoxCollider>().size = bounds.size;
            }
            return bounds;
        }
        private static Bounds BoundsOf(GameObject obj)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(); Bounds result = renderers[0].bounds;
            foreach (Renderer renderer in renderers) result.Encapsulate(renderer.bounds); return result;
        }
    }
}
