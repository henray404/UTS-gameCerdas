using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SneakHouse;

namespace SneakHouseEditor
{
    public static class GardenActivityBuilder
    {
        private const string Root = "Assets/SneakHouse/";
        private const string Group = "ACTIVITY / Moving garden props";
        [MenuItem("Sneak House/Enhance garden motion")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            GardenAmbience garden = UnityEngine.Object.FindFirstObjectByType<GardenAmbience>();
            if (garden == null) throw new InvalidOperationException("Apply the garden exterior first.");
            ApplyTo(garden);
            EditorSceneManager.MarkSceneDirty(garden.gameObject.scene); EditorSceneManager.SaveScene(garden.gameObject.scene); AssetDatabase.SaveAssets();
        }
        public static void ApplyTo(GardenAmbience garden)
        {
            foreach (string file in new[] { "garden_pinwheel", "garden_sprinkler", "garden_butterfly" })
                if (AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Models/Exterior/"+file+".fbx") == null) throw new InvalidOperationException("Missing activity model: "+file);
            foreach (Transform old in garden.transform.Cast<Transform>().Where(t=>t.name==Group).ToArray()) Undo.DestroyObjectImmediate(old.gameObject);
            GameObject group=new GameObject(Group);group.transform.SetParent(garden.transform,false);Undo.RegisterCreatedObjectUndo(group,"Enhance garden motion");
            GardenActivity activity=group.AddComponent<GardenActivity>();activity.garden=garden;
            Undo.RecordObject(garden,"Increase readable garden motion");
            foreach(GardenAmbience.Sway s in garden.sways)
            {
                if(s.target==null)continue;
                s.degrees=s.target.name=="Canopy"?4.5f:s.target.name=="Cloth wind pivot"?12:s.target.name=="flower_cluster"?5:7;
                s.speed=s.target.name=="Cloth wind pivot"?1.8f:s.target.name=="Canopy"?.95f:s.speed;
            }
            GameObject pinwheel=Model("garden_pinwheel",group.transform,new Vector3(9.8f,.02f,-4.45f));
            activity.rotor=pinwheel.GetComponentsInChildren<Transform>().First(t=>t.name=="Rotor");
            GameObject proxy=new GameObject("Pinwheel / stable stake collider");proxy.transform.SetParent(group.transform,false);proxy.transform.position=pinwheel.transform.position+Vector3.up*.7f;proxy.layer=8;
            CapsuleCollider collider=proxy.AddComponent<CapsuleCollider>();collider.height=1.4f;collider.radius=.065f;

            GameObject nozzle=Model("garden_sprinkler",group.transform,new Vector3(-9.5f,.02f,1.65f));
            Transform head=nozzle.GetComponentsInChildren<Transform>().First(t=>t.name=="SprayHead");
            Transform rig=new GameObject("Sprinkler sweep").transform;rig.SetParent(group.transform,false);rig.position=new Vector3(-9.5f,.28f,1.65f);head.SetParent(rig,true);activity.sprinkler=rig;
            Material water=Material("SprinklerWater","9BD5DA",true);
            activity.jets=new LineRenderer[5];
            for(int i=0;i<5;i++)
            {
                GameObject obj=new GameObject("Water arc "+i);obj.transform.SetParent(rig,false);LineRenderer line=obj.AddComponent<LineRenderer>();
                line.useWorldSpace=false;line.positionCount=14;line.sharedMaterial=water;line.startWidth=.028f;line.endWidth=.012f;line.startColor=new Color(.63f,.87f,.9f,.65f);line.endColor=new Color(.72f,.91f,.92f,.08f);
                for(int j=0;j<14;j++)line.SetPosition(j,GardenActivity.WaterArc(j/13f,i));activity.jets[i]=line;
            }
            Mesh drop=DropMesh();activity.droplets=new Transform[15];
            for(int i=0;i<15;i++)
            {
                GameObject obj=new GameObject("Water droplet "+i);obj.transform.SetParent(rig,false);obj.AddComponent<MeshFilter>().sharedMesh=drop;obj.AddComponent<MeshRenderer>().sharedMaterial=water;activity.droplets[i]=obj.transform;
            }
            activity.butterflies=new GardenActivity.Butterfly[2];
            for(int i=0;i<2;i++)
            {
                Transform flight=new GameObject("Butterfly flight "+i).transform;flight.SetParent(group.transform,false);
                flight.position=i==0?new Vector3(-9.8f,1.05f,4.0f):new Vector3(9.65f,1.05f,5.0f);
                GameObject model=Model("garden_butterfly",flight,flight.position);model.transform.localScale*=1.2f;
                activity.butterflies[i]=new GardenActivity.Butterfly {flight=flight,leftWing=model.GetComponentsInChildren<Transform>().First(t=>t.name=="WingLeft"),rightWing=model.GetComponentsInChildren<Transform>().First(t=>t.name=="WingRight")};
            }
        }
        private static GameObject Model(string file,Transform parent,Vector3 position)
        {
            GameObject model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Models/Exterior/"+file+".fbx"),parent);
            model.name=file;model.transform.position=position;return model;
        }
        private static Material Material(string name,string hex,bool transparent)
        {
            string path=Root+"Materials/Exterior/"+name+".mat";Material mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find(transparent?"Sprites/Default":"Standard"));AssetDatabase.CreateAsset(mat,path);}
            ColorUtility.TryParseHtmlString("#"+hex,out Color colour);mat.color=colour;return mat;
        }
        private static Mesh DropMesh()
        {
            string path=Root+"Models/Exterior/Generated/water-droplet.asset";Mesh mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh!=null)return mesh;
            mesh=new Mesh{name="Faceted water droplet"};mesh.vertices=new[]{Vector3.up*.055f,Vector3.down*.055f,Vector3.left*.035f,Vector3.right*.035f,Vector3.forward*.035f,Vector3.back*.035f};
            mesh.triangles=new[]{0,4,3,0,3,5,0,5,2,0,2,4,1,3,4,1,5,3,1,2,5,1,4,2};mesh.RecalculateNormals();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
    }
}
