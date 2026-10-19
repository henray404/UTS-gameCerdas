using System;
using UnityEngine;

namespace SneakHouse
{
    public class GardenAmbience : MonoBehaviour
    {
        [Serializable] public class Sway
        {
            public Transform target;
            public float degrees = 1.5f, speed = .8f;
            [NonSerialized] public Quaternion rest;
            [NonSerialized] public Vector3 tiltAxis, crossAxis;
        }
        [Tooltip("Disable drifting leaves and plant motion. Water keeps a gentle ripple.")]
        public bool ambientMotion = true;
        [Range(0, 1)] public float ambienceVolume = .65f;
        public Sway[] sways = new Sway[0];
        public Transform[] leaves = new Transform[0];
        public LineRenderer[] ripples = new LineRenderer[0];
        public AudioSource wind, birds, water;
        private Vector3[] leafOrigins;
        private float clock, nextBird = 6;
        private ChaseAtmosphere chaseAtmosphere;
        public float AnimationClock => clock;

        private void Awake()
        {
            if (Camera.main != null) chaseAtmosphere = Camera.main.GetComponent<ChaseAtmosphere>();
            foreach (Sway sway in sways) if (sway.target != null)
            {
                sway.rest = sway.target.localRotation;
                sway.tiltAxis = sway.target.InverseTransformDirection(Vector3.forward);
                sway.crossAxis = sway.target.InverseTransformDirection(Vector3.right);
            }
            leafOrigins = new Vector3[leaves.Length];
            for (int i = 0; i < leaves.Length; i++) if (leaves[i] != null) leafOrigins[i] = leaves[i].localPosition;
            if (wind != null) wind.Play();
            if (water != null) water.Play();
        }
        private void Update()
        {
            GameSession game = GameSession.Instance;
            bool paused = game != null && game.Phase == GameSession.ScreenState.Paused;
            if (!paused) clock += Time.unscaledDeltaTime;
            for (int i = 0; i < sways.Length; i++)
            {
                Sway sway = sways[i];
                if (sway.target == null) continue;
                float t = clock * sway.speed + i * 1.73f;
                sway.target.localRotation = sway.rest * Quaternion.AngleAxis(ambientMotion ? Mathf.Sin(t) * sway.degrees : 0, sway.tiltAxis)
                    * Quaternion.AngleAxis(ambientMotion ? Mathf.Sin(t * .7f) * sway.degrees * .35f : 0, sway.crossAxis);
            }
            for (int i = 0; i < leaves.Length; i++)
            {
                if (leaves[i] == null) continue;
                leaves[i].gameObject.SetActive(ambientMotion);
                float t = Mathf.Repeat(clock * (.065f + i * .003f) + i * .23f, 1);
                leaves[i].localPosition = leafOrigins[i] + new Vector3(t * .9f + Mathf.Sin(t * 10 + i) * .18f, -t * 1.65f, Mathf.Sin(t * 6 + i) * .28f);
                leaves[i].localRotation = Quaternion.Euler(25 + t * 140, t * 280 + i * 40, Mathf.Sin(t * 8) * 35);
            }
            for (int i = 0; i < ripples.Length; i++)
            {
                if (ripples[i] == null) continue;
                float t = Mathf.Repeat(clock * .28f + i * .5f, 1);
                ripples[i].transform.localScale = Vector3.one * Mathf.Lerp(.05f, .31f, t);
                Color c = new Color(.82f, .89f, .76f, (1 - t) * .42f);
                ripples[i].startColor = ripples[i].endColor = c;
            }
            float duck = 1;
            if (game != null && game.guards != null)
                foreach (GuardBrain guard in game.guards)
                    if (guard != null && guard.isActiveAndEnabled && guard.Suspicion > .4f) { duck = .4f; break; }
            float gain = ambienceVolume * duck * (paused ? 0 : 1);
            if (chaseAtmosphere != null && chaseAtmosphere.isActiveAndEnabled)
                gain *= Mathf.Lerp(1, .18f, chaseAtmosphere.Intensity);
            if (wind != null) wind.volume = .13f * gain;
            if (water != null) water.volume = .10f * gain;
            if (birds != null)
            {
                birds.volume = .12f * gain;
                if (!paused && clock >= nextBird) { birds.Play(); nextBird = clock + 14 + Mathf.Sin(clock) * 3; }
            }
        }
        private void OnDestroy()
        {
            // Restore poses when domain reload is disabled in Editor play settings.
            foreach (Sway sway in sways) if (sway.target != null) sway.target.localRotation = sway.rest;
        }
    }
}
