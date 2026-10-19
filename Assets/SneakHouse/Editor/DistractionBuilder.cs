using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SneakHouse;

namespace SneakHouseEditor
{
    public static class DistractionBuilder
    {
        [MenuItem("Sneak House/Set up distraction cans")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
            const string root="Assets/SneakHouse/";
            GameObject model=AssetDatabase.LoadAssetAtPath<GameObject>(root+"Models/Props/distraction_can.fbx");
            AudioClip clip=AssetDatabase.LoadAssetAtPath<AudioClip>(root+"Audio/can-impact.wav");
            if(model==null || clip==null)throw new InvalidOperationException("Missing can model or impact clip.");
            if(!AssetDatabase.IsValidFolder(root+"Resources/SneakHouseGameplay"))AssetDatabase.CreateFolder(root+"Resources","SneakHouseGameplay");
            GameObject go=new GameObject("DistractionCan");
            try
            {
                GameObject art=(GameObject)PrefabUtility.InstantiatePrefab(model,go.transform);art.name="Can visual";
                CapsuleCollider collider=go.AddComponent<CapsuleCollider>();collider.height=.23f;collider.radius=.095f;collider.center=Vector3.up*.115f;
                Rigidbody body=go.AddComponent<Rigidbody>();body.mass=.12f;body.linearDamping=.12f;body.angularDamping=.25f;body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
                AudioSource source=go.AddComponent<AudioSource>();source.clip=clip;source.playOnAwake=false;source.volume=.65f;source.spatialBlend=.3f;source.minDistance=8;source.maxDistance=45;
                go.AddComponent<DroppedCan>();
                GameObject prefab=PrefabUtility.SaveAsPrefabAsset(go,root+"Resources/SneakHouseGameplay/DistractionCan.prefab");
                PlayerMotor player=UnityEngine.Object.FindFirstObjectByType<PlayerMotor>();
                PlayerDistraction drop=player.GetComponent<PlayerDistraction>();if(drop==null)drop=Undo.AddComponent<PlayerDistraction>(player.gameObject);
                Undo.RecordObject(drop,"Assign distraction can");drop.canPrefab=prefab;EditorUtility.SetDirty(drop);
                EditorSceneManager.MarkSceneDirty(player.gameObject.scene);EditorSceneManager.SaveScene(player.gameObject.scene);AssetDatabase.SaveAssets();
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}
