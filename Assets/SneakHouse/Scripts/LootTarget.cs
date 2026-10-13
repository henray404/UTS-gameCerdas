using UnityEngine;

namespace SneakHouse
{
    public class LootTarget : MonoBehaviour
    {
        public string displayName;
        public Transform visual;
        public bool Collected { get; private set; }
        private Vector3 rest;
        private void Awake() { if (visual != null) rest = visual.localPosition; }
        private void Update()
        {
            if (!Collected && visual != null) visual.localPosition = rest + Vector3.up * Mathf.Sin(Time.time * 2.4f) * .025f;
        }
        public void MarkCollected()
        {
            Collected = true;
            if (visual != null) visual.gameObject.SetActive(false);
            foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        }
    }
}
