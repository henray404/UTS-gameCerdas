using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SneakHouse;

namespace SneakHouseEditor
{
    public static class HidingBuilder
    {
        private const string Root="Assets/SneakHouse/";
        [MenuItem("Sneak House/Set up hiding spots")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
            foreach(string asset in new[]{"hiding_bush","hiding_tent"})if(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Models/Exterior/"+asset+".fbx")==null)throw new InvalidOperationException("Missing hiding asset: "+asset);
            RuntimeNavigation nav=UnityEngine.Object.FindFirstObjectByType<RuntimeNavigation>();PlayerMotor player=UnityEngine.Object.FindFirstObjectByType<PlayerMotor>();
            if(nav==null || player==null)throw new InvalidOperationException("Open PrototypeHouse first.");
            foreach(Transform old in nav.geometry.Cast<Transform>().Where(t=>t.name=="HIDING / Spots").ToArray())Undo.DestroyObjectImmediate(old.gameObject);
            GameObject group=new GameObject("HIDING / Spots");group.transform.SetParent(nav.geometry,false);Undo.RegisterCreatedObjectUndo(group,"Add hiding spots");
            HidingSpot bush=Spot(group.transform,"bush",new Vector3(-10.4f,.06f,3.2f),new Vector3(-9.15f,.06f,3.2f),"hiding_bush",0);
            Proxy(group.transform,"Bush occlusion",new Vector3(-10.4f,.52f,3.2f),new Vector3(1.45f,1.04f,1.1f));
            HidingSpot tent=Spot(group.transform,"tent",new Vector3(7.5f,.06f,-7.1f),new Vector3(9.2f,.06f,-7.1f),"hiding_tent",90);
            Proxy(group.transform,"Tent collision",new Vector3(7.5f,.64f,-7.1f),new Vector3(1.35f,1.28f,1.72f));
            HidingSpot desk=Spot(group.transform,"under desk",new Vector3(-5.65f,.06f,4.3f),new Vector3(-6.65f,.06f,3.05f),null,0);
            PlayerHiding hiding=player.GetComponent<PlayerHiding>();if(hiding==null)hiding=Undo.AddComponent<PlayerHiding>(player.gameObject);
            Undo.RecordObject(hiding,"Assign hiding spots");hiding.spots=new[]{bush,tent,desk};EditorUtility.SetDirty(hiding);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);EditorSceneManager.SaveScene(player.gameObject.scene);AssetDatabase.SaveAssets();
        }
        private static HidingSpot Spot(Transform group,string label,Vector3 anchor,Vector3 approach,string model,float angle)
        {
            GameObject go=new GameObject("Hide / "+label);go.transform.SetParent(group,false);go.transform.position=anchor;
            HidingSpot spot=go.AddComponent<HidingSpot>();spot.displayName=label;
            Transform inside=new GameObject("Hidden anchor").transform;inside.SetParent(go.transform,false);spot.anchor=inside;
            Transform entry=new GameObject("Approach and inspection").transform;entry.SetParent(go.transform,false);entry.position=approach;spot.inspectionPoint=entry;
            if(model!=null)
            {
                GameObject art=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Models/Exterior/"+model+".fbx"),go.transform);
                art.transform.position=anchor;art.transform.rotation=Quaternion.Euler(0,angle,0)*art.transform.rotation;
            }
            return spot;
        }
        private static void Proxy(Transform group,string name,Vector3 p,Vector3 size)
        {
            GameObject go=new GameObject(name);go.transform.SetParent(group,false);go.transform.position=p;go.layer=8;go.AddComponent<BoxCollider>().size=size;
        }
    }
}
