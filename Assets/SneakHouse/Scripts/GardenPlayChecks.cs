#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using SneakHouse;

namespace SneakHouseEditor
{
    public class GardenPlayChecks : MonoBehaviour
    {
        [Serializable] private class Report { public bool passed; public string unity; public string[] checks; public string[] failures; public int renderers; }
        private readonly List<string> checks = new List<string>(), failures = new List<string>();
        private void Check(bool condition,string name) { if(condition)checks.Add(name);else failures.Add(name); }
        private IEnumerator Start()
        {
            GameSession game=GameSession.Instance;
            GardenAmbience garden=FindFirstObjectByType<GardenAmbience>();
            if(garden==null || game==null){Check(false,"Garden and mission exist");Finish(garden);yield break;}
            game.PersistRecords=false;
            Check(game.Phase==GameSession.ScreenState.MainMenu,"Fresh scene starts in main menu");
            Check(garden.sways.Length>=30 && Array.TrueForAll(garden.sways,s=>s.target!=null),"Wind targets resolve after scene reload");
            Check(garden.leaves.Length==4 && Array.TrueForAll(garden.leaves,t=>t!=null && t.GetComponentsInChildren<Collider>().Length==0),"Four decorative leaves have no collision");
            Check(garden.ripples.Length==2 && Array.TrueForAll(garden.ripples,r=>r!=null),"Birdbath ripple references resolve");
            Check(garden.wind!=null && garden.birds!=null && garden.water!=null && garden.wind.clip!=null && garden.birds.clip!=null && garden.water.clip!=null,"Three ambience clips assigned");
            Check(garden.wind.isPlaying && garden.water.isPlaying,"Looping wind and water started");
            float clock=garden.AnimationClock, noise=game.LastNoiseTime;
            Quaternion pose=garden.sways[0].target.localRotation;
            GardenActivity activity=FindFirstObjectByType<GardenActivity>();
            Check(activity!=null && activity.rotor!=null && activity.sprinkler!=null && activity.butterflies.Length==2,"Visible activity props and two butterflies assigned");
            if(activity==null){Finish(garden);yield break;}
            Quaternion rotor=activity.rotor.localRotation,spray=activity.sprinkler.localRotation;
            Vector3 butterfly=activity.butterflies[0].flight.localPosition,drop=activity.droplets[0].localPosition;
            Quaternion wing=activity.butterflies[0].leftWing.localRotation;
            float wingMotion=0,sprayMotion=0;
            for(int sample=0;sample<12;sample++)
            {
                yield return new WaitForSecondsRealtime(.1f);
                wingMotion=Mathf.Max(wingMotion,Quaternion.Angle(wing,activity.butterflies[0].leftWing.localRotation));
                sprayMotion=Mathf.Max(sprayMotion,Quaternion.Angle(spray,activity.sprinkler.localRotation));
            }
            Check(garden.AnimationClock>clock+.15f && Quaternion.Angle(pose,garden.sways[0].target.localRotation)>.01f,"Menu garden animates while mission simulation is frozen");
            Check(game.Elapsed==0,"Ambient clock does not advance mission timer");
            Check(Quaternion.Angle(rotor,activity.rotor.localRotation)>15,"Pinwheel turns visibly while characters are paused in menu");
            Check(sprayMotion>3,"Sprinkler sweeps garden visibly");
            Check(Vector3.Distance(butterfly,activity.butterflies[0].flight.localPosition)>.1f,"Butterfly moves through garden independently of NPCs");
            Check(wingMotion>2,"Butterfly wings flap");
            Check(Vector3.Distance(drop,activity.droplets[0].localPosition)>.15f,"Water droplets travel along spray arcs");
            game.ShowBriefing();game.BeginMission();game.TogglePause();
            yield return null;
            clock=garden.AnimationClock;pose=garden.sways[0].target.localRotation;
            rotor=activity.rotor.localRotation;spray=activity.sprinkler.localRotation;butterfly=activity.butterflies[0].flight.localPosition;
            yield return new WaitForSecondsRealtime(.25f);
            Check(Mathf.Abs(garden.AnimationClock-clock)<.001f && Quaternion.Angle(pose,garden.sways[0].target.localRotation)<.01f,"Gameplay pause freezes garden animation");
            Check(garden.wind.volume==0 && garden.birds.volume==0 && garden.water.volume==0,"Gameplay pause silences ambience");
            Check(Quaternion.Angle(rotor,activity.rotor.localRotation)<.01f && Quaternion.Angle(spray,activity.sprinkler.localRotation)<.01f && Vector3.Distance(butterfly,activity.butterflies[0].flight.localPosition)<.001f,"Pause freezes pinwheel, sprinkler and butterflies");
            game.TogglePause();yield return new WaitForSecondsRealtime(.2f);
            Check(garden.AnimationClock>clock+.05f && garden.wind.volume>0,"Resume restores garden clock and ambience");
            Collider[] solid=garden.GetComponentsInChildren<Collider>();Vector3[] positions=new Vector3[solid.Length];
            for(int i=0;i<solid.Length;i++)positions[i]=solid[i].transform.position;
            yield return new WaitForSecondsRealtime(.3f);
            bool stable=true;for(int i=0;i<solid.Length;i++)stable &= Vector3.Distance(positions[i],solid[i].transform.position)<.001f;
            Check(stable && solid.Length>0,"Garden collision remains fixed during wind animation");
            Check(Mathf.Approximately(game.LastNoiseTime,noise),"Ambient audio does not emit AI hearing stimuli");
            garden.ambientMotion=false;yield return null;yield return null;
            Check(Array.TrueForAll(garden.leaves,t=>!t.gameObject.activeSelf),"Reduced motion hides drifting leaves");
            Check(Array.TrueForAll(garden.sways,s=>Quaternion.Angle(s.rest,s.target.localRotation)<.01f),"Reduced motion restores plant and cloth rest poses");
            Check(Array.TrueForAll(activity.jets,j=>!j.enabled) && Array.TrueForAll(activity.butterflies,b=>!b.flight.gameObject.activeSelf),"Reduced motion hides spray and flying insects");
            garden.ambientMotion=true;
            Vector3 start=game.player.transform.position;
            foreach(Vector3 target in new[] {new Vector3(-10,0,-7),new Vector3(-9,0,4),new Vector3(9,0,-4),new Vector3(9,0,4),new Vector3(3,0,6.9f),new Vector3(-4,0,-7)})
            {
                NavMeshPath path=new NavMeshPath();
                bool reachable=NavMesh.SamplePosition(target,out NavMeshHit hit,.6f,NavMesh.AllAreas) && NavMesh.CalculatePath(start,hit.position,NavMesh.AllAreas,path) && path.status==NavMeshPathStatus.PathComplete;
                Check(reachable,"Exterior route reachable: "+target.ToString());
            }
            Check(Mathf.Abs(Camera.main.transform.position.x+2.43f)<.001f && Mathf.Abs(Camera.main.orthographicSize-9.2f)<.001f,"Manual camera position and framing retained");
            Finish(garden);
        }
        private void Finish(GardenAmbience garden)
        {
            Report report=new Report{passed=failures.Count==0,unity=Application.unityVersion,checks=checks.ToArray(),failures=failures.ToArray(),renderers=garden==null?0:garden.GetComponentsInChildren<Renderer>(true).Length};
            string folder=Path.Combine(Application.dataPath,"../Documentation/Verification");Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder,"garden-validation.json"),JsonUtility.ToJson(report,true));
            if(report.passed)Debug.Log("GARDEN_CHECKS_PASSED: "+checks.Count);else Debug.LogError("GARDEN_CHECKS_FAILED: "+string.Join("; ",failures));
            EditorApplication.isPlaying=false;
        }
    }
}
#endif
