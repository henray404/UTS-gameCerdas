using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MCPForUnity.Editor.Services;

namespace SneakHouseEditor
{
    public static class PrototypeEditorBootstrap
    {
        [MenuItem("Sneak House/Connect Unity MCP")]
        public static async void Connect()
        {
            EditorPrefs.SetBool("MCPForUnity.UseHttpTransport",true);
            EditorPrefs.SetString("MCPForUnity.HttpUrl","http://127.0.0.1:8080");
            if(System.IO.File.Exists(PrototypeBuilder.ScenePath))EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);
            bool connected=await MCPServiceLocator.Bridge.StartAsync();
            Debug.Log("SNEAK_HOUSE_MCP_CONNECTED: "+connected);
        }
        [MenuItem("Sneak House/Open prototype")]
        public static void OpenPrototype()
        {
            EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);
            Connect();
        }
        [MenuItem("Sneak House/Close automation editor")]
        public static void CloseAutomation()
        {
            if(Application.isBatchMode)EditorApplication.Exit(0);
            else Debug.Log("This command only closes the background automation editor.");
        }
    }
}
