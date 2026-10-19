#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEngine;
using SneakHouse;

namespace SneakHouseEditor
{
    // Controlled enter/exit montage; witness and search behavior are tested separately.
    public class HidingDemoRecorder : MonoBehaviour
    {
        private IEnumerator Start()
        {
            GameSession game=GameSession.Instance;game.PersistRecords=false;game.ShowBriefing();game.BeginMission();PlayerMotor player=game.player;player.enabled=false;
            foreach(GuardBrain guard in game.guards)if(guard.isActiveAndEnabled){guard.Agent.isStopped=true;guard.enabled=false;guard.acting.Set("Idle");}
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp","work/hiding-preview-frames"));Directory.CreateDirectory(folder);int index=0;
            foreach(HidingSpot spot in player.Hiding.spots)
            {
                player.Teleport(spot.Approach);player.Hiding.RefreshNearest();game.Notify("[F] Hide: "+spot.displayName);float next=Time.realtimeSinceStartup;
                for(int frame=0;frame<32;frame++)
                {
                    if(frame==8)player.Hiding.TryEnter();if(frame==24)player.Hiding.TryExit();
                    yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,(index++).ToString("D3")+".png"));next+=1f/12;yield return new WaitForSecondsRealtime(Mathf.Max(0,next-Time.realtimeSinceStartup));
                }
            }
            Debug.Log("HIDING_PREVIEW_DONE");Destroy(gameObject);
        }
    }
}
#endif
