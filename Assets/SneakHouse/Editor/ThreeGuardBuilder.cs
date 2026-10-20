using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using SneakHouse;

namespace SneakHouseEditor
{
    public static class ThreeGuardBuilder
    {
        [MenuItem("Sneak House/Set up three patrol zones")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            GameSession game = UnityEngine.Object.FindFirstObjectByType<GameSession>();
            if (game == null || game.guards.Length != 3) throw new InvalidOperationException("Open the three-guard PrototypeHouse scene first.");
            Vector2[][] routes =
            {
                new[] { new Vector2(1.4f,-1.65f), new Vector2(1.4f,-4), new Vector2(4.8f,-2),
                    new Vector2(4,-.2f), new Vector2(.25f,-.2f), new Vector2(.25f,-1.65f) },
                new[] { new Vector2(-2.9f,3.1f), new Vector2(-6.2f,2.5f), new Vector2(-4.2f,2.5f), new Vector2(-2.9f,2.8f) },
                new[] { new Vector2(4,3.1f), new Vector2(6.55f,2.55f), new Vector2(4,2.2f), new Vector2(4,4.2f) }
            };
            string[] zones = { "Living room", "Office", "Collection room" };
            float[] facing = { 90, 270, 90 };
            for (int i = 0; i < 3; i++)
            {
                GuardBrain guard = game.guards[i];
                Transform root = guard.patrol[0].parent;
                Undo.RecordObject(root.gameObject, "Name patrol zone");
                root.name = zones[i] + " / Patrol route";
                Undo.RecordObject(guard, "Assign patrol zone");
                Undo.RecordObject(guard.gameObject, "Activate guard");
                Undo.RecordObject(guard.transform, "Place guard");
                guard.gameObject.SetActive(true);
                guard.name = "Guard 0" + (i + 1) + " / " + zones[i];
                guard.patrolZone = zones[i]; guard.patrolPause = .55f + i * .25f;
                foreach (Transform point in guard.patrol) Undo.DestroyObjectImmediate(point.gameObject);
                guard.patrol = new Transform[routes[i].Length];
                for (int j = 0; j < routes[i].Length; j++)
                {
                    GameObject point = new GameObject("Waypoint " + (j + 1));
                    Undo.RegisterCreatedObjectUndo(point, "Create patrol waypoint");
                    point.transform.SetParent(root);
                    point.transform.position = new Vector3(routes[i][j].x, .06f, routes[i][j].y);
                    guard.patrol[j] = point.transform;
                }
                guard.transform.SetPositionAndRotation(guard.patrol[0].position, Quaternion.Euler(0, facing[i], 0));
                NavMeshAgent agent = guard.GetComponent<NavMeshAgent>();
                Undo.RecordObject(agent, "Separate guard avoidance priorities");
                agent.avoidancePriority = 30 + i * 15;
                EditorUtility.SetDirty(agent); EditorUtility.SetDirty(guard);
            }
            Undo.RecordObject(game, "Enable three guards");
            game.activeGuardCount = 3; EditorUtility.SetDirty(game);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene);
        }
    }
}
