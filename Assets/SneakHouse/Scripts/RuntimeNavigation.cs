using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SneakHouse
{
    [DefaultExecutionOrder(-300)]
    public class RuntimeNavigation : MonoBehaviour
    {
        public Transform geometry;
        public Bounds bounds = new Bounds(new Vector3(0, 1, 0), new Vector3(24, 7, 18));
        private NavMeshData data;
        private NavMeshDataInstance handle;
        private void Awake()
        {
            List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(geometry, 1 << 8, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = .42f; settings.agentHeight = 1.8f; settings.agentClimb = .24f;
            settings.overrideVoxelSize = true; settings.voxelSize = .09f;
            data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null) { Debug.LogError("NavMesh could not be built."); return; }
            handle = NavMesh.AddNavMeshData(data);
            foreach (NavMeshAgent agent in FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
            {
                agent.enabled = false;
                if (NavMesh.SamplePosition(agent.transform.position, out NavMeshHit hit, 1, NavMesh.AllAreas)) agent.transform.position = hit.position;
                agent.enabled = true;
            }
        }
        private void OnDestroy() { if (handle.valid) handle.Remove(); if (data != null) Destroy(data); }
    }
}
