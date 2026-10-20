using UnityEngine;

namespace SneakHouse
{
    public class HouseShellVariant : MonoBehaviour
    {
        public string label;
        public Rect[] footprint;
        public bool officeWing, collectionWing;
        public float upperHeight;
        public Rect[] upperFootprint = new Rect[0];
        public Transform upperArchitecture;
    }
}
