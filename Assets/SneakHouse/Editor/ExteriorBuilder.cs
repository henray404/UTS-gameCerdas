using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SneakHouse;

namespace SneakHouseEditor
{
    public static class ExteriorBuilder
    {
        private const string Root = "Assets/SneakHouse/";
        private const string Group = "EXTERIOR / Living garden";
        private static Transform parent;
        private static readonly List<GardenAmbience.Sway> sways = new List<GardenAmbience.Sway>();
        private static System.Random random;

        [MenuItem("Sneak House/Apply garden exterior")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before updating the exterior.");
            GameSession session = UnityEngine.Object.FindFirstObjectByType<GameSession>();
            RuntimeNavigation nav = UnityEngine.Object.FindFirstObjectByType<RuntimeNavigation>();
            if (session == null || nav == null || nav.geometry == null) throw new InvalidOperationException("Open PrototypeHouse first.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            // Validate before replacing the previous garden; keep all user-edited interior objects.
            foreach (string asset in new[] { "garden_tree", "hedge_section", "fence_section", "garden_gate", "mailbox", "stepping_stone", "flower_cluster", "grass_clump", "wooden_crate", "watering_can", "hose_coil", "garden_seat", "garden_table", "birdbath", "laundry_rack", "porch_lantern" })
                if (AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Models/Exterior/" + asset + ".fbx") == null) throw new InvalidOperationException("Missing exterior model: " + asset);
            // Transform.Find treats '/' as a hierarchy path, while our display name contains '/'.
            foreach (Transform old in nav.geometry.Cast<Transform>().Where(t => t.name == Group).ToArray())
                Undo.DestroyObjectImmediate(old.gameObject);
            GameObject garden = new GameObject(Group); Undo.RegisterCreatedObjectUndo(garden, "Add garden exterior");
            parent = garden.transform; parent.SetParent(nav.geometry, false); sways.Clear(); random = new System.Random(274);
            foreach (string name in new[] { "West border", "East border", "Garden north", "Garden south" })
            {
                Transform edge = nav.geometry.Find(name);
                if (edge != null && edge.TryGetComponent(out MeshRenderer renderer)) { Undo.RecordObject(renderer, "Replace border art"); renderer.enabled = false; }
            }
            foreach (Transform child in nav.geometry)
                if (child.name == "pottedPlant" && child.position.x < -8)
                { Undo.RecordObject(child.gameObject, "Replace entrance planters"); child.gameObject.SetActive(false); }

            Material lawn = Mat("Lawn", "7C8954"), lightGrass = Mat("GrassLight", "879459"), darkGrass = Mat("GrassDark", "71804C");
            Transform ground = nav.geometry.Find("Courtyard ground");
            if (ground != null) { Renderer r = ground.GetComponent<Renderer>(); Undo.RecordObject(r, "Garden lawn palette"); r.sharedMaterial = lawn; }
            GroundPatch("Lawn west", new Vector3(-9.8f, .009f, 1), new Vector2(1.3f, 5.5f), lightGrass, 17);
            GroundPatch("Lawn east", new Vector3(9.8f, .01f, .5f), new Vector2(1.35f, 6.9f), lightGrass, 12);
            GroundPatch("Lawn north", new Vector3(0, .009f, 7.35f), new Vector2(8.4f, .8f), darkGrass, 15);
            GroundPatch("Lawn foreground", new Vector3(-3.3f, .01f, -7.2f), new Vector2(4.0f, .8f), darkGrass, 13);

            // Background ground has no collider. Existing lot boundary still confines play.
            Box("Neighborhood verge", new Vector3(0, -.16f, 0), new Vector3(38, .25f, 27), Mat("Verge", "90996A"), false);
            Box("Quiet street", new Vector3(0, -.04f, -10.1f), new Vector3(38, .10f, 1.65f), Mat("Road", "8E8C7E"), false);
            for (int i = 0; i < 19; i++)
            {
                Box("Pavement slab", new Vector3(-17.1f + i * 1.9f, .008f, -8.95f), new Vector3(1.86f, .07f, .85f), Mat("Pavement", "BDB39A"), false);
                Box("Curb", new Vector3(-17.1f + i * 1.9f, .015f, -9.42f), new Vector3(1.86f, .12f, .14f), Mat("Stone", "B7AD90"), false);
            }
            for (int i = 0; i < 10; i++)
            {
                float x = -10.3f + i * 2.28f;
                Place("fence_section", new Vector3(x, .015f, 8.35f));
                if (i > 0) Place("fence_section", new Vector3(x, .015f, -8.35f));
            }
            for (int i = 0; i < 7; i++)
            {
                float z = -6.9f + i * 2.28f;
                Place("fence_section", new Vector3(-11.38f, .015f, z), 90);
                Place("fence_section", new Vector3(11.38f, .015f, z), 90);
            }
            Place("garden_gate", new Vector3(-10.4f, .015f, -8.29f), -66);
            Place("mailbox", new Vector3(-11.0f, .015f, -7.55f), 0, .92f, true);
            Vector3[] path = { new Vector3(-10.2f,.025f,-7.75f), new Vector3(-10.0f,.025f,-7.12f), new Vector3(-9.75f,.025f,-6.48f), new Vector3(-9.45f,.025f,-5.86f), new Vector3(-9.2f,.025f,-5.25f), new Vector3(-8.9f,.025f,-4.56f), new Vector3(-8.36f,.025f,-4.4f) };
            path[5] = new Vector3(-9.05f,.025f,-4.52f); path[6] = new Vector3(-8.27f,.025f,-4.40f);
            for (int i = 0; i < path.Length; i++) Place("stepping_stone", path[i], i * 43, .78f);
            Place("porch_lantern", new Vector3(-8.17f, .55f, -3.42f), -90, 1.5f);

            Tree(new Vector3(-10.15f, .02f, 6.5f), .85f);
            Tree(new Vector3(10.13f, .02f, 6.5f), .86f);
            // Background planting fills the wider left camera margin without expanding play bounds.
            foreach(Vector3 p in new[] {new Vector3(-14.6f,-.03f,5.3f),new Vector3(-15.8f,-.03f,-3.2f)})
            {
                GameObject distant=Place("garden_tree",p,Range(0,360),1.12f);
                Transform canopy=distant.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Canopy");if(canopy!=null)Sway(canopy,1.0f,.55f);
            }
            Place("hedge_section",new Vector3(-14.2f,-.03f,1.2f),-25,1.35f);
            Place("hedge_section",new Vector3(-15.1f,-.03f,-5.8f),15,1.2f);
            for (int i = 0; i < 7; i++) Place("hedge_section", new Vector3(-6.7f + i * 2.15f, .015f, 7.98f), 0, .9f);
            foreach (float z in new[] { -2.0f, 1.2f, 4.2f }) Place("hedge_section", new Vector3(-10.97f, .015f, z), 90, .85f);
            foreach (float x in new[] { -5.6f, -3.4f, 5.9f, 8.0f }) Place("hedge_section", new Vector3(x, .015f, -8.06f), 0, .9f);
            for (int i = 0; i < 15; i++) Flowers(new Vector3(-9.4f + i * 1.30f, .02f, 7.14f + Range(-.22f,.22f)));
            foreach (Vector3 p in new[] { new Vector3(-10.7f,0,-6.8f),new Vector3(-10.65f,0,-1.1f),new Vector3(-10.5f,0,2.7f),new Vector3(-9.7f,0,5.5f),new Vector3(10.25f,0,5.2f),new Vector3(10.5f,0,-5.6f),new Vector3(6.6f,0,-7.4f),new Vector3(-5.8f,0,-7.4f) })
            { Flowers(p + Vector3.up * .02f); Flowers(p + new Vector3(.48f,.02f,.28f)); }
            for (int i = 0; i < 25; i++)
            {
                Vector3 p = i < 13 ? new Vector3(-10.9f + Range(0,.6f), .02f, -2.4f + (i % 13) * .7f) : new Vector3(9.5f + Range(0,1), .02f, -5.5f + (i - 13) * .85f);
                GameObject grass = Place("grass_clump", p, Range(0,360), Range(.8f,1.3f)); Sway(grass.transform, 2, 1.15f);
            }
            Place("wooden_crate", new Vector3(10.45f,.02f,2.7f), 8, 1, true);
            Place("wooden_crate", new Vector3(10.44f,.65f,2.7f), -7, .8f);
            Place("wooden_crate", new Vector3(9.6f,.02f,2.85f), -12, .83f, true);
            Place("watering_can", new Vector3(10.4f,.03f,1.55f), 35, 1.2f);
            Place("hose_coil", new Vector3(10.0f,.03f,.75f), 0, 1.2f);
            GameObject rack = Place("laundry_rack", new Vector3(10.6f,.025f,-1.2f), 90, .9f, true);
            Transform cloth = rack.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Cloth");
            if (cloth != null)
            {
                Transform pivot = new GameObject("Cloth wind pivot").transform;
                pivot.SetParent(rack.transform); pivot.position = cloth.GetComponent<Renderer>().bounds.center + Vector3.up * .34f;
                cloth.SetParent(pivot, true); Sway(pivot, 2.8f, 1.0f);
            }
            Box("Terrace foundation", new Vector3(1.9f,.025f,-7.17f), new Vector3(3.7f,.055f,1.55f), Mat("Stone", "B7AD90"), false);
            for (int x = 0; x < 5; x++) for (int z = 0; z < 2; z++)
                Box("Terrace paver", new Vector3(.46f + x*.72f,.06f,-7.55f+z*.74f), new Vector3(.69f,.03f,.70f), Mat((x+z)%2==0?"PaverLight":"PaverDark",(x+z)%2==0?"C5BDA1":"B8AF91"), false);
            Place("garden_seat", new Vector3(.8f,.08f,-7.25f), -90, .9f, true);
            Place("garden_seat", new Vector3(3.0f,.08f,-7.25f), 90, .9f, true);
            Place("garden_table", new Vector3(1.9f,.08f,-7.25f), 0, 1, true);
            GameObject bath = Place("birdbath", new Vector3(9.6f,.02f,5.3f), 0, 1, true);

            GardenAmbience ambience = garden.AddComponent<GardenAmbience>(); ambience.sways = sways.ToArray();
            ambience.leaves = new Transform[4];
            Material leafMat = Mat("DriftingLeaf", "B7A563");
            for (int i = 0; i < 4; i++)
            {
                GameObject leaf = new GameObject("Drifting leaf " + i); leaf.transform.SetParent(parent, false);
                leaf.transform.localPosition = new Vector3(i < 2 ? -10.1f : 9.8f, 2.15f, 6.7f + i*.12f);
                Mesh mesh = new Mesh { name = "Leaf silhouette" }; mesh.vertices = new[] { new Vector3(-.1f,0,0), new Vector3(0,.025f,-.045f),new Vector3(.1f,0,0),new Vector3(0,-.015f,.045f) };
                mesh.triangles = new[] { 0,1,2,0,2,3,2,1,0,3,2,0 }; mesh.RecalculateNormals(); mesh=SaveMesh(mesh,"drifting-leaf-"+i);
                leaf.AddComponent<MeshFilter>().sharedMesh = mesh; leaf.AddComponent<MeshRenderer>().sharedMaterial = leafMat; ambience.leaves[i] = leaf.transform;
            }
            ambience.ripples = new LineRenderer[2];
            Material rippleMat = Mat("Ripple", "CEDCC1"); rippleMat.shader = Shader.Find("Sprites/Default");
            for (int i = 0; i < 2; i++)
            {
                GameObject obj = new GameObject("Birdbath ripple " + i); obj.transform.SetParent(parent, false); obj.transform.position = bath.transform.position + Vector3.up * .762f;
                LineRenderer line = obj.AddComponent<LineRenderer>(); line.useWorldSpace=false; line.loop=true; line.positionCount=24;line.widthMultiplier=.018f;line.sharedMaterial=rippleMat;
                for(int j=0;j<24;j++){float a=j*Mathf.PI*2/24;line.SetPosition(j,new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)));}
                ambience.ripples[i]=line;
            }
            ambience.wind = Audio("Garden wind", "garden-wind", true, 0);
            ambience.birds = Audio("Distant birds", "garden-birds", false, 0);
            ambience.water = Audio("Birdbath water", "garden-water", true, .8f); ambience.water.transform.position = bath.transform.position;
            GardenActivityBuilder.ApplyTo(ambience);
            EditorSceneManager.MarkSceneDirty(garden.scene); EditorSceneManager.SaveScene(garden.scene); AssetDatabase.SaveAssets();
            Debug.Log("GARDEN_READY: 16 modular assets, " + ambience.sways.Length + " wind groups. Interior and camera preserved.");
        }
        private static float Range(float low,float high) { return low+(float)random.NextDouble()*(high-low); }
        private static void Sway(Transform t,float angle,float speed) { sways.Add(new GardenAmbience.Sway { target=t,degrees=angle,speed=speed }); }
        private static void Tree(Vector3 p,float scale)
        {
            GameObject tree=Place("garden_tree",p,Range(-20,20),scale);
            Transform canopy=tree.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Canopy");
            if(canopy!=null)Sway(canopy,1.6f,.65f);
            // Stable narrow trunk proxy; moving canopy is decorative.
            GameObject proxy=new GameObject("Tree trunk collision");proxy.transform.SetParent(parent,false);proxy.transform.position=p+Vector3.up*.85f;proxy.layer=8;
            CapsuleCollider c=proxy.AddComponent<CapsuleCollider>();c.radius=.17f;c.height=1.7f;
        }
        private static void Flowers(Vector3 p) { GameObject go=Place("flower_cluster",p,Range(0,360),Range(.85f,1.15f));Sway(go.transform,1.9f,Range(.8f,1.2f)); }
        private static GameObject Place(string asset,Vector3 p,float angle=0,float scale=1,bool solid=false)
        {
            GameObject go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Models/Exterior/"+asset+".fbx"),parent);
            go.name=asset;go.transform.position=p;go.transform.rotation=Quaternion.Euler(0,angle,0)*go.transform.rotation;go.transform.localScale*=scale;
            if(asset!="garden_tree" && asset!="flower_cluster" && asset!="grass_clump" && asset!="laundry_rack")
                foreach(Transform t in go.GetComponentsInChildren<Transform>()) GameObjectUtility.SetStaticEditorFlags(t.gameObject,StaticEditorFlags.BatchingStatic);
            if(solid)
            {
                Renderer[] renderers=go.GetComponentsInChildren<Renderer>();Bounds b=renderers[0].bounds;foreach(Renderer r in renderers)b.Encapsulate(r.bounds);
                GameObject proxy=new GameObject(asset+" / Stable collision");proxy.transform.SetParent(parent,false);proxy.transform.position=b.center;proxy.layer=8;proxy.AddComponent<BoxCollider>().size=b.size;
            }
            return go;
        }
        private static Material Mat(string name,string hex)
        {
            const string folder=Root+"Materials/Exterior";if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder(Root+"Materials","Exterior");
            string path=folder+"/"+name+".mat";Material m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}
            ColorUtility.TryParseHtmlString("#"+hex,out Color colour);m.color=colour;m.SetFloat("_Glossiness",.05f);return m;
        }
        private static void Box(string name,Vector3 p,Vector3 size,Material m,bool solid)
        {
            GameObject go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.position=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=m;
            if(!solid)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());else go.layer=8;
        }
        private static Mesh SaveMesh(Mesh mesh,string name)
        {
            const string folder=Root+"Models/Exterior/Generated";
            if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder(Root+"Models/Exterior","Generated");
            string path=folder+"/"+name+".asset";Mesh existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing!=null){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);return existing;}else AssetDatabase.CreateAsset(mesh,path);
            return mesh;
        }
        private static void GroundPatch(string name,Vector3 p,Vector2 size,Material mat,int sides)
        {
            Mesh mesh=new Mesh {name=name};Vector3[] v=new Vector3[sides+1];int[] tris=new int[sides*3];
            for(int i=0;i<sides;i++)
            {float a=i*Mathf.PI*2/sides;float r=Range(.86f,1.08f);v[i+1]=new Vector3(Mathf.Cos(a)*size.x*r,0,Mathf.Sin(a)*size.y*r);tris[i*3]=0;tris[i*3+1]=(i+1)%sides+1;tris[i*3+2]=i+1;}
            mesh.vertices=v;mesh.triangles=tris;mesh.RecalculateNormals();SaveMesh(mesh,name.Replace(' ','-'));
            GameObject go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.position=p;go.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"Models/Exterior/Generated/"+name.Replace(' ','-')+".asset");go.AddComponent<MeshRenderer>().sharedMaterial=mat;
        }
        private static AudioSource Audio(string name,string clip,bool loop,float spatial)
        {
            AudioClip asset=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"Audio/"+clip+".wav");if(asset==null)throw new InvalidOperationException("Missing garden audio: "+clip);
            GameObject obj=new GameObject(name);obj.transform.SetParent(parent,false);AudioSource source=obj.AddComponent<AudioSource>();source.clip=asset;source.loop=loop;source.playOnAwake=false;source.spatialBlend=spatial;source.minDistance=8;source.maxDistance=45;source.volume=0;return source;
        }
    }
}
