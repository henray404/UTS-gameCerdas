using UnityEngine;

namespace SneakHouse
{
    [DefaultExecutionOrder(-20)]
    public class PlayerHiding : MonoBehaviour
    {
        public HidingSpot[] spots=new HidingSpot[0];
        public HidingSpot CurrentSpot { get; private set; }
        public HidingSpot NearestSpot { get; private set; }
        public bool IsHidden => CurrentSpot!=null;
        public bool Compromised { get; private set; }
        private PlayerMotor player;
        private CharacterController controller;
        private Renderer[] renderers;
        private bool[] visible;
        private Vector3 entry;
        private Quaternion facing;
        private void Awake()
        {
            player=GetComponent<PlayerMotor>();controller=GetComponent<CharacterController>();
            renderers=player.acting.animator.GetComponentsInChildren<Renderer>(true);visible=new bool[renderers.Length];
            if(spots==null || spots.Length==0)spots=FindObjectsByType<HidingSpot>(FindObjectsSortMode.None);
        }
        private void Update()
        {
            GameSession game=GameSession.Instance;if(game==null || game.Phase!=GameSession.ScreenState.Playing)return;
            RefreshNearest();if(Input.GetKeyDown(KeyCode.F)){if(IsHidden)TryExit();else TryEnter();}
        }
        public void RefreshNearest()
        {
            NearestSpot=null;if(IsHidden)return;float best=float.PositiveInfinity;
            foreach(HidingSpot spot in spots)
            {
                if(spot==null || !spot.isActiveAndEnabled || spot.Occupant!=null)continue;
                Vector3 offset=spot.Approach-transform.position;if(Mathf.Abs(offset.y)>.75f)continue;offset.y=0;
                if(offset.sqrMagnitude>spot.interactionRadius*spot.interactionRadius || offset.sqrMagnitude>=best)continue;
                if(Physics.Linecast(transform.position+Vector3.up*.7f,spot.Approach+Vector3.up*.7f,1<<8,QueryTriggerInteraction.Ignore))continue;
                best=offset.sqrMagnitude;NearestSpot=spot;
            }
        }
        public bool TryEnter()
        {
            GameSession game=GameSession.Instance;
            if(game==null || game.Phase!=GameSession.ScreenState.Playing || IsHidden)return false;
            RefreshNearest();if(NearestSpot==null)return false;
            HidingSpot spot=NearestSpot;entry=transform.position;facing=transform.rotation;Compromised=false;
            // Evaluate witnesses at the entry location before concealing the player.
            foreach(GuardBrain guard in game.guards)
                if(guard!=null && guard.isActiveAndEnabled && guard.Sees(entry)){guard.NoticeHidingEntry(spot);Compromised=true;}
            CurrentSpot=spot;spot.Occupy(this);controller.enabled=false;
            transform.position=spot.anchor!=null?spot.anchor.position:spot.transform.position;
            for(int i=0;i<renderers.Length;i++){visible[i]=renderers[i].enabled;renderers[i].enabled=false;}
            player.Move(Vector3.zero,false,false,Time.deltaTime);player.acting.Set("Idle");
            player.RefreshLoot();
            game.Notify(Compromised?"Spotted entering! This hiding spot is compromised.":spot.HiddenText+". [F] Leave");return true;
        }
        public bool TryExit()
        {
            GameSession game=GameSession.Instance;
            if(game==null || game.Phase!=GameSession.ScreenState.Playing || !IsHidden)return false;
            float radius=controller.radius*.9f;Vector3 center=entry+controller.center;float half=controller.height*.5f-radius;
            if(Physics.CheckCapsule(center+Vector3.up*half,center-Vector3.up*half,radius,1<<8,QueryTriggerInteraction.Ignore))
            {game.Notify("Exit blocked. Try again.");return false;}
            CurrentSpot.Vacate(this);CurrentSpot=null;Compromised=false;transform.rotation=facing;player.Teleport(entry);
            for(int i=0;i<renderers.Length;i++)renderers[i].enabled=visible[i];
            game.Notify("Out of hiding.");RefreshNearest();return true;
        }
        private void OnDestroy(){if(CurrentSpot!=null)CurrentSpot.Vacate(this);}
    }
}
