#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEngine;
using SneakHouse;

namespace SneakHouseEditor
{
    // Staged input walkthrough: captures actual controller, animation, physics and hearing.
    public class MovementDemoRecorder : MonoBehaviour
    {
        private GameSession game;private PlayerMotor player;private GuardBrain guard;private float elapsed;private bool dropped;
        private IEnumerator Start()
        {
            game=GameSession.Instance;game.PersistRecords=false;game.ShowBriefing();game.BeginMission();player=game.player;guard=game.guards[0];
            player.enabled=false;player.Teleport(new Vector3(-6.2f,.1f,-1.75f));
            guard.enabled=false;guard.Agent.Warp(new Vector3(3.2f,.05f,-1.75f));guard.Agent.isStopped=true;guard.Agent.ResetPath();guard.transform.rotation=Quaternion.Euler(0,90,0);guard.acting.Set("Idle");
            typeof(GameSession).GetProperty("DebugMode").SetValue(game,true);
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp","work/movement-drop-frames"));Directory.CreateDirectory(folder);float next=Time.realtimeSinceStartup;
            for(int i=0;i<64;i++)
            {
                yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,i.ToString("D3")+".png"));next+=1f/12;yield return new WaitForSecondsRealtime(Mathf.Max(0,next-Time.realtimeSinceStartup));
            }
            Debug.Log("MOVEMENT_DROP_PREVIEW_DONE");enabled=false;Destroy(gameObject);
        }
        private void Update()
        {
            if(player==null || game.Phase!=GameSession.ScreenState.Playing)return;
            elapsed+=Time.deltaTime;
            if(elapsed<1.6f){player.Move(Vector3.right,false,false,Time.deltaTime);game.Notify("Walk");}
            else if(elapsed<2.6f){player.Move(Vector3.left,true,false,Time.deltaTime);game.Notify("Run");}
            else if(elapsed<4.2f){player.Move(Vector3.right,false,false,Time.deltaTime);game.Notify("Walk");}
            else
            {
                player.Move(Vector3.zero,false,false,Time.deltaTime);
                if(!dropped){dropped=true;guard.enabled=true;guard.Agent.isStopped=false;player.Distractions.TryDrop();}
            }
        }
    }
}
#endif
