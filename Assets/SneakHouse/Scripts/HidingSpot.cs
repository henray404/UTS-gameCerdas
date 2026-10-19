using UnityEngine;

namespace SneakHouse
{
    public class HidingSpot : MonoBehaviour
    {
        public string displayName="bush";
        public Transform anchor, inspectionPoint;
        [Min(.3f)] public float interactionRadius=1.25f;
        public PlayerHiding Occupant { get; private set; }
        public Vector3 Approach => inspectionPoint!=null?inspectionPoint.position:transform.position;
        public string HiddenText => displayName=="under desk"?"Hidden under desk":"Hidden in "+displayName;
        public void Occupy(PlayerHiding player) { Occupant=player; }
        public void Vacate(PlayerHiding player) { if(Occupant==player)Occupant=null; }
        public bool Inspect(GuardBrain guard)
        {
            if(Occupant==null || Vector3.Distance(guard.transform.position,Approach)>1.15f)return false;
            GameSession.Instance.End(false);return true;
        }
    }
}
