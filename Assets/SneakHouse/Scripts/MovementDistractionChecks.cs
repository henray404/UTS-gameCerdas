#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using SneakHouse;

namespace SneakHouseEditor
{
    public class MovementDistractionChecks : MonoBehaviour
    {
        [Serializable] private class Report { public bool passed; public string[] checks,failures; }
        private readonly List<string> checks=new List<string>(),failures=new List<string>();
        private int clangs;
        private void Noise(Vector3 p,float radius,string kind){if(kind=="Dropped can")clangs++;}
        private void Check(bool value,string label){if(value)checks.Add(label);else failures.Add(label);}
        private IEnumerator Start()
        {
            GameSession game=GameSession.Instance;game.PersistRecords=false;PlayerMotor player=game.player;PlayerDistraction drop=player.Distractions;GuardBrain guard=game.guards[0];
            Check(drop!=null && drop.Remaining==3 && drop.canPrefab!=null,"Fresh mission supplies three physical distraction cans");
            Check(!drop.TryDrop() && drop.Remaining==3,"Main menu blocks dropping");game.ShowBriefing();Check(!drop.TryDrop(),"Briefing blocks dropping");game.BeginMission();game.TogglePause();Check(!drop.TryDrop(),"Pause blocks dropping");game.TogglePause();
            player.enabled=false;guard.visionDistance=.01f;
            player.Teleport(new Vector3(-9.3f,.1f,-1));
            for(int i=0;i<24;i++){player.Move(Vector3.forward,false,false,Time.deltaTime);yield return null;}
            Check(player.Velocity.magnitude>2 && player.acting.CurrentState=="Walk" && Mathf.Abs(player.acting.PlaybackRate-player.Velocity.magnitude/1.4f)<.02f,"Actual walking speed drives playback rate");
            for(int i=0;i<12;i++){player.Move(Vector3.back,true,false,Time.deltaTime);yield return null;}
            Check(player.Velocity.magnitude>3.5f && player.acting.CurrentState=="Run" && Mathf.Abs(player.acting.PlaybackRate-player.Velocity.magnitude/3.2f)<.02f,"Actual running speed drives playback rate");
            float heard=game.LastNoiseTime;
            for(int i=0;i<20;i++){player.Move(Vector3.forward,false,true,Time.deltaTime);yield return null;}
            Check(player.Sneaking && Mathf.Abs(player.Velocity.magnitude-player.sneakSpeed)<.15f && Mathf.Abs(player.acting.PlaybackRate-player.sneakSpeed/1.4f)<.12f,"Sneaking uses measured speed, not fixed animation multiplier");
            Check(game.LastNoiseTime==heard,"Sneaking produces no footstep noise");
            player.Teleport(new Vector3(-8.65f,.1f,-1));
            for(int i=0;i<25;i++){player.Move(Vector3.right,false,false,Time.deltaTime);yield return null;}
            heard=game.LastNoiseTime;
            for(int i=0;i<8;i++){player.Move(Vector3.right,false,false,Time.deltaTime);yield return null;}
            Check(player.Velocity.magnitude<.08f && player.acting.CurrentState=="Idle","Wall contact stops walking animation when displacement stops");
            Check(game.LastNoiseTime==heard,"Pushing into wall produces no phantom steps");
            player.Teleport(new Vector3(-1,.1f,-2));player.transform.rotation=Quaternion.Euler(0,90,0);guard.Agent.Warp(new Vector3(3,.05f,-2));guard.Step(.01f,Time.time);
            GameSession.Noise+=Noise;
            Check(drop.TryDrop() && drop.Remaining==2,"Q action spawns can and spends exactly one charge");DroppedCan can=drop.LastDrop;
            Check(!can.Impacted && clangs==0,"Can is silent until physical impact");Check(!drop.TryDrop() && drop.Remaining==2,"Cooldown prevents duplicate drop");
            float deadline=Time.time+2;while(can!=null && !can.Impacted && Time.time<deadline)yield return null;
            Check(can!=null && can.Impacted && clangs==1,"Ground collision emits one distraction noise");
            Check(guard.MemorySource=="Dropped can" && Vector3.Distance(guard.LastKnownPosition,can.ImpactPoint)<.1f,"Guard remembers actual impact location");
            Check(guard.State==GuardBrain.Mode.Investigate || guard.State==GuardBrain.Mode.Search,"Noise switches nearby guard into investigate/search");
            Check(game.Collected==0 && Array.TrueForAll(game.targets,t=>!t.Collected),"Distraction does not spend mission objectives");
            yield return new WaitForSeconds(.8f);Check(clangs==1,"Resting or bouncing can does not spam hearing");
            player.Teleport(new Vector3(-8.45f,.1f,0));player.transform.rotation=Quaternion.Euler(0,90,0);
            Check(!drop.TryDrop() && drop.Remaining==2,"Wall prevents spawning a can through architecture");
            player.Teleport(new Vector3(-9.2f,.1f,-4.4f));player.transform.rotation=Quaternion.identity;Vector3 previous=guard.LastKnownPosition;
            Check(drop.TryDrop(),"Second can can be dropped after cooldown");DroppedCan distant=drop.LastDrop;deadline=Time.time+2;while(!distant.Impacted && Time.time<deadline)yield return null;
            Check(distant.Impacted && Vector3.Distance(guard.LastKnownPosition,distant.ImpactPoint)>3,"Distant guard ignores out-of-range impact");
            yield return new WaitForSeconds(.3f);Check(drop.TryDrop() && drop.Remaining==0,"Last can exhausts inventory");
            yield return new WaitForSeconds(.75f);Check(!drop.TryDrop() && drop.Remaining==0,"Empty inventory cannot create more cans");
            game.End(false);Check(!drop.TryDrop(),"Results screen blocks dropping");
            GameSession.Noise-=Noise;DontDestroyOnLoad(gameObject);game.Restart();yield return null;yield return new WaitForSecondsRealtime(.2f);
            game=GameSession.Instance;game.PersistRecords=false;
            Check(game.player.Distractions.Remaining==3 && FindObjectsByType<DroppedCan>(FindObjectsSortMode.None).Length==0,"Retry restores cans and removes previous physical props");
            var report=new Report{passed=failures.Count==0,checks=checks.ToArray(),failures=failures.ToArray()};
            File.WriteAllText(Path.Combine(Application.dataPath,"../Documentation/Verification/movement-distraction-validation.json"),JsonUtility.ToJson(report,true));
            if(report.passed)Debug.Log("MOVEMENT_DISTRACTION_PASSED: "+checks.Count);else Debug.LogError("MOVEMENT_DISTRACTION_FAILED: "+string.Join("; ",failures));EditorApplication.isPlaying=false;
        }
        private void OnDestroy(){GameSession.Noise-=Noise;}
    }
}
#endif
