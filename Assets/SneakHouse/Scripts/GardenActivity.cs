using System;
using UnityEngine;

namespace SneakHouse
{
    [DefaultExecutionOrder(20)]
    public class GardenActivity : MonoBehaviour
    {
        [Serializable] public class Butterfly
        {
            public Transform flight, leftWing, rightWing;
            [NonSerialized] public Vector3 center, leftAxis, rightAxis;
            [NonSerialized] public Quaternion leftRest, rightRest;
        }
        public GardenAmbience garden;
        public Transform rotor, sprinkler;
        [Range(20, 150)] public float pinwheelSpeed = 85;
        public LineRenderer[] jets = new LineRenderer[0];
        public Transform[] droplets = new Transform[0];
        public Butterfly[] butterflies = new Butterfly[0];
        private Quaternion rotorRest;
        private Vector3 rotorAxis;

        private void Awake()
        {
            if (rotor != null) { rotorRest = rotor.localRotation; rotorAxis = rotor.InverseTransformDirection(Vector3.forward); }
            foreach (Butterfly b in butterflies)
            {
                if (b.flight == null || b.leftWing == null || b.rightWing == null) continue;
                b.center = b.flight.localPosition;
                b.leftRest = b.leftWing.localRotation; b.rightRest = b.rightWing.localRotation;
                b.leftAxis = b.leftWing.InverseTransformDirection(Vector3.forward);
                b.rightAxis = b.rightWing.InverseTransformDirection(Vector3.forward);
            }
        }
        public static Vector3 WaterArc(float t, int stream)
        {
            float angle = (stream - 2) * 8 * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(angle) * t * 1.35f, Mathf.Sin(t * Mathf.PI) * .48f - t * .23f,
                Mathf.Cos(angle) * t * 1.35f);
        }
        private void Update()
        {
            if (garden == null) return;
            float t = garden.AnimationClock;
            bool moving = garden.ambientMotion;
            if (rotor != null) rotor.localRotation = rotorRest * Quaternion.AngleAxis(moving ? t * pinwheelSpeed : 0, rotorAxis);
            if (sprinkler != null) sprinkler.localRotation = Quaternion.Euler(0, -90 + (moving ? Mathf.Sin(t * .8f) * 47 : 0), 0);
            for (int i = 0; i < jets.Length; i++)
            {
                LineRenderer line = jets[i]; if (line == null) continue; line.enabled = moving;
                for (int j = 0; j < line.positionCount; j++) line.SetPosition(j, WaterArc(j / (float)(line.positionCount - 1), i));
            }
            for (int i = 0; i < droplets.Length; i++)
            {
                if (droplets[i] == null) continue;
                droplets[i].gameObject.SetActive(moving);
                float progress = Mathf.Repeat(t * 1.3f + i * .19f, 1);
                droplets[i].localPosition = WaterArc(progress, i % 5);
            }
            for (int i = 0; i < butterflies.Length; i++)
            {
                Butterfly b = butterflies[i]; if (b.flight == null || b.leftWing == null || b.rightWing == null) continue;
                b.flight.gameObject.SetActive(moving);
                float phase = t * .85f + i * 2.8f;
                b.flight.localPosition = b.center + new Vector3(Mathf.Sin(phase) * .68f, Mathf.Sin(phase * 2.1f) * .17f, Mathf.Sin(phase * .7f) * .85f);
                Vector3 direction = new Vector3(Mathf.Cos(phase) * .68f, 0, Mathf.Cos(phase * .7f) * .595f);
                b.flight.localRotation = Quaternion.LookRotation(direction);
                float flap = 20 + Mathf.Sin(t * 21 + i) * 48;
                b.leftWing.localRotation = b.leftRest * Quaternion.AngleAxis(flap, b.leftAxis);
                b.rightWing.localRotation = b.rightRest * Quaternion.AngleAxis(-flap, b.rightAxis);
            }
        }
    }
}
