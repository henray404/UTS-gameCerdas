#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using SneakHouse;

namespace SneakHouseEditor
{
    public class HidingPlayChecks : MonoBehaviour
    {
        [Serializable] private class Report {public bool passed;public string[] checks,failures;}
        private readonly List<string> checks=new List<string>(),failures=new List<string>();
        private void Check(bool value,string label){if(value)checks.Add(label);else failures.Add(label);}
        private IEnumerator Start()
        {
            GameSession game=GameSession.Instance;game.PersistRecords=false;PlayerMotor player=game.player;PlayerHiding hide=player.Hiding;GuardBrain guard=game.guards[0];
            Check(hide!=null && hide.spots.Length==3,"Bush, tent and desk hiding spots assigned");
            HidingSpot bush=hide.spots.First(s=>s.displayName=="bush");
            player.Teleport(bush.Approach);Check(!hide.TryEnter(),"Main menu blocks entering hiding");game.ShowBriefing();Check(!hide.TryEnter(),"Briefing blocks entering hiding");game.BeginMission();
            player.enabled=false;guard.visionDistance=.01f;
            foreach(HidingSpot spot in hide.spots)
            {
                NavMeshPath path=new NavMeshPath();Check(NavMesh.CalculatePath(new Vector3(-9,.05f,-4.45f),spot.Approach,NavMesh.AllAreas,path) && path.status==NavMeshPathStatus.PathComplete,"Reachable hiding approach: "+spot.displayName);
                guard.Agent.Warp(new Vector3(2,.05f,-2));player.Teleport(spot.Approach);hide.RefreshNearest();
                Check(hide.NearestSpot==spot && hide.TryEnter() && hide.IsHidden && !hide.Compromised,"Unseen entry works: "+spot.displayName);
                Check(!player.GetComponent<CharacterController>().enabled && player.acting.animator.GetComponentsInChildren<Renderer>(true).All(r=>!r.enabled),"Concealed pose and collision: "+spot.displayName);
                Vector3 inside=player.transform.position;player.Move(Vector3.right,true,false,.1f);
                Check(Vector3.Distance(inside,player.transform.position)<.001f && player.Velocity==Vector3.zero,"Movement blocked while hidden: "+spot.displayName);
                Check(!player.Distractions.TryDrop() && !player.TryInteract(),"No drop or pickup from hiding: "+spot.displayName);
                guard.Step(.1f,Time.time);Check(!guard.CanSeePlayer && !game.Finished,"Hidden player is not visually detected: "+spot.displayName);
                game.TogglePause();Check(!hide.TryExit() && hide.IsHidden,"Pause blocks hiding exit: "+spot.displayName);game.TogglePause();
                Check(hide.TryExit() && !hide.IsHidden && Vector3.Distance(player.transform.position,spot.Approach)<.05f && player.GetComponent<CharacterController>().enabled,"Exit restores safe entry location: "+spot.displayName);
                Check(player.acting.animator.GetComponentsInChildren<Renderer>(true).Any(r=>r.enabled),"Exit restores character rendering: "+spot.displayName);
            }
            HidingSpot desk=hide.spots.First(s=>s.displayName=="under desk");
            player.Teleport(desk.Approach);guard.Agent.Warp(new Vector3(-6.65f,.05f,1.9f));guard.transform.rotation=Quaternion.identity;guard.visionDistance=8;
            Check(guard.Sees(player.transform.position),"Witness test has unobstructed sight of entry");
            Check(hide.TryEnter() && hide.Compromised && guard.MemorySource=="Saw hiding entry" && guard.State==GuardBrain.Mode.Investigate,"Seen entry records spot and sends guard to inspect");
            guard.Step(.1f,Time.time);Check(!guard.CanSeePlayer && guard.HasMemory && Vector3.Distance(guard.LastKnownPosition,desk.Approach)<.05f,"Concealment retains witnessed entry memory");
            float deadline=Time.time+4;while(!game.Finished && Time.time<deadline)yield return null;
            Check(game.Finished && !game.Won,"Guard catches player after checking witnessed hiding spot");
            DontDestroyOnLoad(gameObject);game.Restart();yield return null;yield return new WaitForSecondsRealtime(.2f);
            game=GameSession.Instance;game.PersistRecords=false;player=game.player;hide=player.Hiding;guard=game.guards[0];
            Check(!hide.IsHidden && hide.spots.All(s=>s.Occupant==null),"Retry clears hiding occupancy and state");
            game.BeginMission();player.enabled=false;guard.visionDistance=.01f;bush=hide.spots.First(s=>s.displayName=="bush");player.Teleport(bush.Approach);guard.Agent.Warp(bush.Approach+Vector3.forward*1.8f);
            Check(hide.TryEnter() && !hide.Compromised,"Unwitnessed bush entry remains uncompromised");
            guard.Hear(bush.Approach,20,"Dropped can");deadline=Time.time+4;while(!game.Finished && Time.time<deadline)yield return null;
            Check(game.Finished && !game.Won,"Investigating noise at occupied hiding spot can reveal player");
            var report=new Report{passed=failures.Count==0,checks=checks.ToArray(),failures=failures.ToArray()};File.WriteAllText(Path.Combine(Application.dataPath,"../Documentation/Verification/hiding-validation.json"),JsonUtility.ToJson(report,true));
            if(report.passed)Debug.Log("HIDING_CHECKS_PASSED: "+checks.Count);else Debug.LogError("HIDING_CHECKS_FAILED: "+string.Join("; ",failures));EditorApplication.isPlaying=false;
        }
    }
}
#endif
