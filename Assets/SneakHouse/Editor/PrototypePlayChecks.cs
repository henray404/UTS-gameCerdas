using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using SneakHouse;

namespace SneakHouseEditor
{
    [InitializeOnLoad]
    public static class PrototypeVerification
    {
        static PrototypeVerification() { EditorApplication.playModeStateChanged += Changed; }
        [MenuItem("Sneak House/Verify prototype gameplay")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);
            SessionState.SetBool("SneakHouse.Verify",true);SessionState.SetInt("SneakHouse.VerifyPhase",0);
            SessionState.SetString("SneakHouse.Passed","");
            EditorApplication.isPlaying=true;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool("SneakHouse.Verify",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Verification / transient").AddComponent<PrototypePlayChecks>();
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                if(SessionState.GetInt("SneakHouse.VerifyPhase",0)==1)
                    EditorApplication.delayCall+=()=>EditorApplication.isPlaying=true;
                else
                {
                    SessionState.SetBool("SneakHouse.Verify",false);
                    if(Application.isBatchMode)EditorApplication.Exit(SessionState.GetInt("SneakHouse.VerifyExit",1));
                }
            }
        }
    }
}
