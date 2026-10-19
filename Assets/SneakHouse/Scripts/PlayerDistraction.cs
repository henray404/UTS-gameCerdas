using UnityEngine;

namespace SneakHouse
{
    public class PlayerDistraction : MonoBehaviour
    {
        [Range(1, 10)] public int startingCans = 3;
        [Min(.1f)] public float cooldown = .7f;
        [Min(1)] public float hearingRadius = 7;
        public GameObject canPrefab;
        public int Remaining { get; private set; }
        public DroppedCan LastDrop { get; private set; }
        private float nextDrop;
        private void Awake()
        {
            Remaining = startingCans;
            if (canPrefab == null) canPrefab = Resources.Load<GameObject>("SneakHouseGameplay/DistractionCan");
        }
        public bool TryDrop()
        {
            GameSession game = GameSession.Instance;
            if (game == null || game.Phase != GameSession.ScreenState.Playing || Time.time < nextDrop) return false;
            PlayerHiding hiding=GetComponent<PlayerHiding>();
            if(hiding!=null && hiding.IsHidden){game.Notify("Leave hiding to drop a can.");return false;}
            if (Remaining <= 0) { game.Notify("No cans left."); return false; }
            if (canPrefab == null) { game.Notify("Distraction item is unavailable."); return false; }
            Vector3 origin=transform.position+Vector3.up*.85f;
            if(Physics.SphereCast(origin,.14f,transform.forward,out RaycastHit hit,.48f,1<<8,QueryTriggerInteraction.Ignore))
            {game.Notify("Step back from the wall to drop a can.");return false;}
            GameObject obj=Instantiate(canPrefab,origin+transform.forward*.4f,Quaternion.identity);
            LastDrop=obj.GetComponent<DroppedCan>();LastDrop.hearingRadius=hearingRadius;
            Rigidbody body=obj.GetComponent<Rigidbody>();body.linearVelocity=transform.forward*3.0f+Vector3.up*1.1f;body.angularVelocity=new Vector3(5,2,4);
            foreach(Collider own in GetComponentsInChildren<Collider>())Physics.IgnoreCollision(obj.GetComponent<Collider>(),own);
            Remaining--;nextDrop=Time.time+cooldown;game.Notify("Can dropped. Move away from the sound.");return true;
        }
    }
}
