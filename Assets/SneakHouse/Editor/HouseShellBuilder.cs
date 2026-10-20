using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using SneakHouse;

namespace SneakHouseEditor
{
    public static class HouseShellBuilder
    {
        private const string Root = "Assets/SneakHouse/";
        private static Transform group;
        private static Material plaster, timber, floor, wood;
        [MenuItem("Sneak House/Set up house shapes")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            GameSession game = UnityEngine.Object.FindFirstObjectByType<GameSession>();
            if (game == null || game.GetComponent<InteriorLayout>() == null) throw new InvalidOperationException("Open the configured PrototypeHouse scene.");
            plaster = Mat("Plaster"); timber = Mat("Timber"); floor = Mat("Floor"); wood = Mat("WarmWood");
            if (!AssetDatabase.IsValidFolder(Root+"Prefabs/Houses")) AssetDatabase.CreateFolder(Root+"Prefabs","Houses");
            GuardEncounter encounter = game.GetComponent<GuardEncounter>();
            if (game.guards.Length == 3)
            {
                GuardBrain extra = UnityEngine.Object.Instantiate(game.guards[1],game.guards[1].transform.parent);
                extra.name = "Guard 04 / Connecting hall"; extra.patrolZone = "Connecting hall";
                extra.GetComponent<NavMeshAgent>().avoidancePriority = 75;
                Transform route = new GameObject("Connecting hall / Patrol route").transform;
                route.SetParent(encounter.zones[0].route[0].parent.parent,false);
                Vector2[] points = { new Vector2(-6,2.4f),new Vector2(-3,2.4f),new Vector2(-3,4.1f),new Vector2(-6,4.1f) };
                extra.patrol = points.Select((p,i) => {
                    Transform node = new GameObject("Waypoint "+(i+1)).transform; node.SetParent(route,false);
                    node.position = new Vector3(p.x,.06f,p.y); return node;
                }).ToArray();
                extra.transform.position = extra.patrol[0].position;
                game.guards = game.guards.Concat(new[] {extra}).ToArray();
                encounter.zones = encounter.zones.Concat(new[] {new GuardEncounter.Zone {guard=extra,route=extra.patrol.ToArray()}}).ToArray();
                extra.gameObject.SetActive(false);
            }
            HouseShell shell = game.GetComponent<HouseShell>();
            if (shell == null) shell = Undo.AddComponent<HouseShell>(game.gameObject);
            shell.variants = Enumerable.Range(0,4).Select(Build).ToArray();
            if (game.GetComponent<FloorCutaway>() == null) Undo.AddComponent<FloorCutaway>(game.gameObject);
            shell.useFixedSeed = false; shell.areaPerGuard = 60;
            EditorUtility.SetDirty(shell); EditorUtility.SetDirty(game); EditorUtility.SetDirty(encounter);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene); EditorSceneManager.SaveScene(game.gameObject.scene); AssetDatabase.SaveAssets();
        }
        private static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>(Root+"Materials/"+name+".mat");
        private static HouseShellVariant Build(int shape)
        {
            group = new GameObject("House shape / temporary").transform;
            try
            {
                HouseShellVariant definition = group.gameObject.AddComponent<HouseShellVariant>();
                definition.label = new[] {"Collector's House","Library Villa","Courtyard Estate","Loft Villa"}[shape];
                definition.officeWing = shape > 0; definition.collectionWing = shape >= 2;
                definition.footprint = shape == 0 ? new[] {new Rect(-8,-6,16,12)} : shape == 1 ?
                    new[] {new Rect(-8,-6,16,12),new Rect(-8,6,8,12)} :
                    new[] {new Rect(-8,-6,16,12),new Rect(-8,6,6,12),new Rect(2,6,6,12)};
                if (shape > 0)
                {
                    Wing("Library",-8,shape == 1 ? 0 : -2,shape >= 2);
                    if (shape >= 2) Wing("Gallery",2,8,true);
                    Wall("Back left pier",new Vector3(-7.7f,0,6),.6f,false);
                    Wall("Back right",new Vector3(shape == 1 ? 2.7f : 7.7f,0,6),shape == 1 ? 10.6f : .6f,false);
                    if (shape >= 2) Wall("Courtyard frontage",new Vector3(0,0,6),5.2f,false);
                    Arch(-5,6,4.8f,false);
                    if (shape >= 2) Arch(5,6,4.8f,false);
                    Prop("bookcaseOpen",new Vector3(-7.15f,.06f,5.15f),1.2f,1.6f,90);
                    Prop("bookcaseClosedDoors",new Vector3(-.85f,.06f,4.8f),1,1.3f,270);
                    Prop("rugRectangle",new Vector3(-4,.045f,3.4f),2.5f,.015f,0,false);
                    if (shape >= 2)
                    {
                        Prop("bookcaseClosedDoors",new Vector3(7.1f,.06f,5.1f),1.1f,1.3f,270);
                        Prop("display_plinth",new Vector3(1.25f,.06f,4.8f),.7f,.8f);
                        Prop("rugRectangle",new Vector3(4,.045f,3.4f),2.6f,.015f,0,false);
                        // Garden props keep courtyard outdoors; neither flooring nor ceiling spans this gap.
                        if (shape == 2)
                        {
                            Exterior("birdbath",new Vector3(0,.02f,12.8f),.9f);
                            Exterior("flower_cluster",new Vector3(-.9f,.02f,15),.7f);
                            Exterior("flower_cluster",new Vector3(.85f,.02f,15.5f),.8f);
                            for (int i = 0; i < 6; i++) Exterior("stepping_stone",new Vector3(0,.025f,7.2f+i*.68f),.65f);
                        }
                    }
                    for (int i = 0; i < 6; i++)
                        foreach (float x in new[] {-11.38f,11.38f}) Exterior("fence_section",new Vector3(x,.015f,9.06f+i*2.28f),1,90);
                }
                if (shape == 3) UpperStorey(definition);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(group.gameObject,Root+"Prefabs/Houses/house-"+shape+".prefab");
                return prefab.GetComponent<HouseShellVariant>();
            }
            finally { UnityEngine.Object.DestroyImmediate(group.gameObject); }
        }
        private static void UpperStorey(HouseShellVariant definition)
        {
            const float height = 3.6f;
            definition.upperHeight = height; definition.upperFootprint = new[] {new Rect(-8,6,6,12)};
            // Furnish the library below the relocated office, keeping its central passage open.
            Prop("bookcaseOpen",new Vector3(-7.3f,.06f,11),1.4f,1.7f,90);
            Prop("bookcaseOpen",new Vector3(-7.3f,.06f,14),1.4f,1.7f,90);
            Prop("loungeChair",new Vector3(-5.8f,.06f,12.4f),.8f,.85f,65);
            Prop("tableCoffee",new Vector3(-6.4f,.06f,11.2f),.9f,.55f);
            Prop("rugRectangle",new Vector3(-5.7f,.045f,11.7f),2.2f,.015f,0,false);
            Transform ground = group;
            group = new GameObject("UPPER / Architecture").transform; group.SetParent(ground,false);
            definition.upperArchitecture = group;
            foreach (float x in new[] {-8f,-2f})
                foreach (float z in new[] {6f,18f})
                    Box("Loft support post",new Vector3(x,height*.5f,z),new Vector3(.20f,height,.20f),timber,true);
            Box("Loft west beam",new Vector3(-8,height-.13f,12),new Vector3(.20f,.24f,12),timber,false);
            Box("Loft east beam",new Vector3(-2,height-.13f,12),new Vector3(.20f,.24f,12),timber,false);
            GameObject slab = Box("Upper office floor",new Vector3(-5,height-.025f,12),new Vector3(6,.1f,12),floor,true);
            WoodSurface(slab);
            for (float z = 6.3f; z < 18; z += .55f)
                for (float x = -7; x < -2; x += 2)
                    Box("Upper floorboard",new Vector3(x,height+.027f,z),new Vector3(1.96f,.012f,.52f),floor,false);
            Wall("Upper west",new Vector3(-8,height,12),12,true);
            Wall("Upper north",new Vector3(-5,height,18),6,false);
            Wall("Upper south",new Vector3(-5,height,6),6,false);
            Wall("Upper east south",new Vector3(-2,height,10.55f),9.1f,true);
            Wall("Upper east north",new Vector3(-2,height,17.55f),.9f,true);
            foreach (float z in new[] {15.1f,17.1f})
                Box("Upper door jamb",new Vector3(-2,height+.7f,z),new Vector3(.13f,1.4f,.13f),timber,false);
            GameObject landing = Box("Upper landing",new Vector3(-.55f,height-.025f,16.1f),new Vector3(3.1f,.1f,2),floor,true);
            WoodSurface(landing);
            Rail(new Vector3(-2,height,17.1f),new Vector3(1,height,17.1f));
            Rail(new Vector3(1,height,15.2f),new Vector3(1,height,17.1f));
            Rail(new Vector3(-2,height,15.1f),new Vector3(-.8f,height,15.1f));
            group = ground;
            for (int i = 0; i < 20; i++)
            {
                float top = .025f+(i+1)*.18f;
                GameObject step = Box("Stair / Step "+(i+1),new Vector3(0,(top-.025f)*.5f,7.4f+i*.4f),new Vector3(1.6f,top+.025f,.405f),wood,true);
                WoodSurface(step);
            }
            foreach (float x in new[] {-.8f,.8f}) Rail(new Vector3(x,.025f,7.2f),new Vector3(x,height+.025f,15.2f));
            Sign("UP",new Vector3(0,.08f,6.65f));
            group = definition.upperArchitecture; Sign("DOWN",new Vector3(-.5f,height+.08f,16.25f)); group = ground;
        }
        private static void WoodSurface(GameObject surface)
        {
            FootstepSurface source = UnityEngine.Object.FindFirstObjectByType<InteriorLayout>().geometry.Find("Office floor").GetComponent<FootstepSurface>();
            if (source != null) surface.AddComponent<FootstepSurface>().clips = source.clips;
        }
        private static void Rail(Vector3 start, Vector3 end)
        {
            int posts = Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(start,end)/1.5f));
            for (int i = 0; i <= posts; i++)
            {
                Vector3 point = Vector3.Lerp(start,end,(float)i/posts);
                Box("Balustrade post",point+Vector3.up*.5f,new Vector3(.10f,1,.10f),timber,true);
            }
            Vector3 a = start+Vector3.up*.94f,b = end+Vector3.up*.94f;
            GameObject beam = Box("Balustrade rail",(a+b)*.5f,new Vector3(.1f,.1f,Vector3.Distance(a,b)),timber,true);
            beam.transform.rotation = Quaternion.LookRotation(b-a);
        }
        private static void Sign(string text, Vector3 position)
        {
            GameObject sign = new GameObject("Stairs / "+text); sign.transform.SetParent(group,false);
            sign.transform.SetPositionAndRotation(position,Quaternion.Euler(90,0,0));
            TextMesh mesh = sign.AddComponent<TextMesh>(); mesh.text = text; mesh.fontSize = 40; mesh.characterSize = .09f;
            mesh.anchor = TextAnchor.MiddleCenter; mesh.color = new Color(.28f,.2f,.12f);
            mesh.font = UnityEngine.Object.FindFirstObjectByType<GameSession>().bodyFont;
            sign.GetComponent<MeshRenderer>().sharedMaterial = mesh.font.material;
        }
        private static void Wing(string name, float left, float right, bool courtyardDoor)
        {
            GameObject slab = Box(name+" floor",new Vector3((left+right)*.5f,-.025f,12),new Vector3(right-left,.1f,12),floor,true);
            FootstepSurface source = UnityEngine.Object.FindFirstObjectByType<InteriorLayout>().geometry.Find("Office floor").GetComponent<FootstepSurface>();
            if (source != null) slab.AddComponent<FootstepSurface>().clips = source.clips;
            int board = 0;
            for (float z = 6.3f; z < 18; z += .55f)
                for (float x = left+1; x < right; x += 2)
                    Box("Wing floorboard",new Vector3(x,.027f,z),new Vector3(1.96f,.012f,.52f),(board++ % 11)==0?wood:floor,false);
            Wall(name+" north",new Vector3((left+right)*.5f,0,18),right-left,false);
            foreach (float x in new[] {left,right})
            {
                bool inner = x == -2 || x == 2;
                if (courtyardDoor && inner)
                {
                    Wall(name+" courtyard south",new Vector3(x,0,8.45f),4.9f,true);
                    Wall(name+" courtyard north",new Vector3(x,0,15.55f),4.9f,true);
                    Arch(x,12,2.2f,true);
                }
                else Wall(name+(x==left?" west":" east"),new Vector3(x,0,12),12,true);
            }
            Prop("bookcaseOpen",new Vector3((left+right)*.5f,.06f,17.35f),2.2f,1.65f);
            Prop("pottedPlant",new Vector3(left+.7f,.06f,7.1f),.65f,1.3f);
            Prop("loungeChair",new Vector3(left+.6f,.06f,8.7f),.7f,.8f,90);
            Prop("grandfather_clock",new Vector3(right-.6f,.06f,7.2f),.6f,1.55f,270);
            Prop("pottedPlant",new Vector3(right-.7f,.06f,17.1f),.65f,1.2f);
        }
        private static void Wall(string name, Vector3 centre, float length, bool vertical)
        {
            float height = 1.15f;
            Box(name+" / Cutaway art",centre+Vector3.up*height*.5f,new Vector3(vertical?.18f:length,height,vertical?length:.18f),plaster,false);
            Box(name+" / Baseboard",centre+Vector3.up*.1f,new Vector3(vertical?.23f:length,.2f,vertical?length:.23f),timber,false);
            GameObject collider = new GameObject(name+" / Full wall collider"); collider.transform.SetParent(group,false); collider.layer = 8;
            collider.transform.position = centre+Vector3.up*1.45f;
            collider.AddComponent<BoxCollider>().size = new Vector3(vertical?.18f:length,2.9f,vertical?length:.18f);
        }
        private static void Arch(float x, float z, float width, bool vertical)
        {
            foreach (float side in new[] {-1f,1f})
                Box("Timber arch jamb",new Vector3(x+(vertical?0:side*width*.5f),.7f,z+(vertical?side*width*.5f:0)),new Vector3(.13f,1.4f,.13f),timber,false);
        }
        private static GameObject Box(string name, Vector3 position, Vector3 size, Material material, bool collision)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name; box.transform.SetParent(group,false);
            box.transform.position = position; box.transform.localScale = size; box.GetComponent<Renderer>().sharedMaterial = material;
            if (collision) box.layer = 8; else UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
            return box;
        }
        private static void Prop(string name, Vector3 position, float width, float height, float yaw = 0, bool collision = true)
        {
            GameObject art = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Models/Props/"+name+".fbx"),group);
            art.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));
            Bounds bounds = BoundsOf(art); art.transform.localScale *= Mathf.Min(width/Mathf.Max(bounds.size.x,bounds.size.z),height/bounds.size.y);
            art.transform.position += Vector3.up*(position.y-BoundsOf(art).min.y); bounds = BoundsOf(art);
            if (collision)
            {
                GameObject proxy = new GameObject(name+" / Navigation proxy"); proxy.transform.SetParent(group,false); proxy.layer = 8;
                proxy.transform.position = bounds.center; proxy.AddComponent<BoxCollider>().size = bounds.size;
            }
        }
        private static void Exterior(string name, Vector3 position, float scale, float yaw = 0)
        {
            GameObject art = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Models/Exterior/"+name+".fbx"),group);
            art.transform.position = position; art.transform.rotation = Quaternion.Euler(0,yaw,0)*art.transform.rotation; art.transform.localScale *= scale;
            if (name == "birdbath")
            {
                Bounds bounds = BoundsOf(art); GameObject proxy = new GameObject("Birdbath / Navigation proxy");
                proxy.transform.SetParent(group,false); proxy.layer = 8; proxy.transform.position = bounds.center; proxy.AddComponent<BoxCollider>().size = bounds.size;
            }
        }
        private static Bounds BoundsOf(GameObject obj)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(); Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds); return bounds;
        }
    }
}
