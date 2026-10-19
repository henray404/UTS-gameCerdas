#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using SneakHouse;

namespace SneakHouseEditor
{
    public class PrototypePlayChecks : MonoBehaviour
    {
        [Serializable] private class Report { public bool passed; public string unity; public string[] checks; public string error; public int defaultGuards; public int targets; }
        private readonly List<string> checks=new List<string>();
        private IEnumerator Start()
        {
            GameSession session = GameSession.Instance;
            session.PersistRecords = false;
            int phase=SessionState.GetInt("SneakHouse.VerifyPhase",0);
            if (phase == 0)
            {
                Check(session.Phase == GameSession.ScreenState.MainMenu && Time.timeScale == 0, "Starts at main menu with simulation frozen");
                Vector3 start = session.player.transform.position;
                yield return new WaitForSecondsRealtime(.2f);
                Check(session.Elapsed == 0 && Vector3.Distance(start, session.player.transform.position) < .001f, "Menu does not advance mission time or player");
                session.ShowBriefing();
                Check(session.Phase == GameSession.ScreenState.Briefing && session.Paused, "Main menu opens briefing");
                session.AddLoot(session.targets[0]);
                Check(session.Collected == 0, "Briefing blocks objective collection");
            }
            else session.ShowBriefing();
            session.BeginMission();
            if (phase == 0)
            {
                Check(session.Phase == GameSession.ScreenState.Playing && !session.Paused && Time.timeScale == 1, "Briefing starts playable mission");
                session.TogglePause();
                Check(session.Phase == GameSession.ScreenState.Paused && Time.timeScale == 0, "Pause freezes simulation");
                session.TogglePause();
                Check(session.Phase == GameSession.ScreenState.Playing && Time.timeScale == 1, "Resume restores simulation");
            }
            yield return new WaitForSeconds(.35f);
            if(phase==0)
            {
                Exception error=null;
                try { RunMainChecks(); } catch(Exception ex) { error=ex; }
                if(error!=null){Finish(false,error.ToString());yield break;}
                DontDestroyOnLoad(gameObject);
                session.Restart();
                yield return null;
                yield return new WaitForSecondsRealtime(.15f);
                session = GameSession.Instance;
                session.PersistRecords = false;
                Check(session.Phase == GameSession.ScreenState.Briefing && Time.timeScale == 0, "Retry reloads level into frozen briefing");
                Check(session.Collected == 0 && !session.Finished && session.Elapsed == 0, "Retry resets objectives, outcome, and timer");
                foreach (LootTarget target in session.targets) Check(!target.Collected, "Retry restores target " + target.displayName);
                session.BeginMission();
                session.End(false);
                session.ShowMainMenu();
                yield return null;
                yield return new WaitForSecondsRealtime(.15f);
                Check(GameSession.Instance.Phase == GameSession.ScreenState.MainMenu && Time.timeScale == 0, "Results can reload clean main menu");
                SessionState.SetString("SneakHouse.Passed",string.Join("\n",checks));
                SessionState.SetInt("SneakHouse.VerifyPhase",1);
                Time.timeScale=1;EditorApplication.isPlaying=false;
            }
            else
            {
                Exception error=null;
                try
                {
                    checks.AddRange(SessionState.GetString("SneakHouse.Passed","").Split('\n'));
                    GameSession game=GameSession.Instance;GuardBrain guard=game.guards[0];
                    game.player.enabled=false;guard.enabled=false;
                    guard.Agent.Warp(new Vector3(-3,.05f,-.6f));guard.transform.rotation=Quaternion.identity;
                    game.player.Teleport(new Vector3(-3,.05f,-.13f));
                    guard.Step(.1f,Time.time);
                    Check(game.Finished&&!game.Won,"Contact capture triggers lose");
                    Check(game.Phase==GameSession.ScreenState.Results && game.ResultScore==0 && !game.NewRecord,"Failed mission shows results without score or record");
                    game.BeginMission();
                    Check(game.Phase==GameSession.ScreenState.Results,"Results cannot resume completed mission");
                }
                catch(Exception ex){error=ex;}
                Finish(error==null,error?.ToString());
            }
        }
        private void RunMainChecks()
        {
            GameSession game=GameSession.Instance;GuardBrain guard=game.guards[0];PlayerMotor player=game.player;
            player.enabled=false;guard.enabled=false;
            Check(game.guards[0].gameObject.activeInHierarchy&&!game.guards[1].gameObject.activeInHierarchy&&!game.guards[2].gameObject.activeInHierarchy,"Default scope: one active NPC");
            Check(game.targets.Length==3,"Three distinct targets");
            Check(NavMesh.CalculateTriangulation().vertices.Length>0&&guard.Agent.isOnNavMesh,"Runtime NavMesh and guard placement");
            foreach(Vector3 p in new[]{new Vector3(-6.35f,.05f,3.05f),new Vector3(5.85f,.05f,3.35f),new Vector3(5.25f,.05f,-2.6f)})
            {
                NavMeshPath path=new NavMeshPath();
                bool found=NavMesh.CalculatePath(player.transform.position,p,NavMesh.AllAreas,path);
                Check(found&&path.status==NavMeshPathStatus.PathComplete,"Reachable objective approach "+p);
            }
            foreach(string state in new[]{"Idle","Walk","Run"})
                Check(guard.acting.animator.HasState(0,Animator.StringToHash(state))&&player.acting.animator.HasState(0,Animator.StringToHash(state)),"Imported actor animation "+state);
            Capture();
            guard.Agent.Warp(new Vector3(-5,.05f,-.6f));guard.transform.rotation=Quaternion.identity;
            player.Teleport(new Vector3(-5,.05f,3.0f));
            Check(!guard.Sees(player.transform.position),"Wall blocks vision");
            guard.Agent.Warp(new Vector3(-3,.05f,-.6f));guard.transform.rotation=Quaternion.identity;player.Teleport(new Vector3(-3,.05f,2.8f));
            Check(guard.Sees(player.transform.position),"Open doorway allows vision");
            player.Teleport(new Vector3(-3,.05f,-3.2f));Check(!guard.Sees(player.transform.position),"Outside FOV is not visible");
            player.Teleport(new Vector3(-3,.05f,2.8f));
            float now=Time.time;guard.Step(.5f,now);guard.Step(.5f,now+.5f);
            Check(guard.State==GuardBrain.Mode.Chase,"Visible exposure builds suspicion and triggers Chase");
            Vector3 observed=guard.LastKnownPosition;
            player.Teleport(new Vector3(-7,.05f,4.8f));guard.Step(.1f,now+1.1f);
            Check(!guard.CanSeePlayer&&Vector3.Distance(guard.LastKnownPosition,observed)<.01f,"Hidden player does not overwrite last known position");
            guard.Step(.1f,now+1.3f);Check(guard.State==GuardBrain.Mode.Search,"Losing sight triggers Search");
            Vector3 sound=new Vector3(1.4f,.05f,-2.3f);guard.Hear(sound,20,"Test distraction");
            Check(guard.State==GuardBrain.Mode.Investigate&&Vector3.Distance(guard.LastKnownPosition,sound)<.01f,"Hearing investigates stimulus position");
            guard.Agent.Warp(sound);guard.Agent.ResetPath();guard.Step(.1f,now+2);
            Check(guard.State==GuardBrain.Mode.Search,"Arriving at stimulus starts systematic search");
            guard.Step(.1f,now+10);Check(guard.State==GuardBrain.Mode.Return,"Search timeout returns to patrol route");
            ExtractionZone extraction=FindFirstObjectByType<ExtractionZone>();
            Check(!extraction.Check(extraction.transform.position)&&!game.Finished,"Exit blocked until all targets collected");
            Vector3[] approaches={new Vector3(-6.35f,.05f,3.05f),new Vector3(5.85f,.05f,3.35f),new Vector3(5.25f,.05f,-2.6f)};
            for(int i=0;i<approaches.Length;i++)
            {
                player.Teleport(approaches[i]);Check(player.TryInteract(),"Actual pickup interaction "+game.targets[i].displayName);
            }
            Check(game.Collected==3,"All three pickups recorded");
            Check(extraction.Check(extraction.transform.position)&&game.Finished&&game.Won,"Objective plus extraction triggers win");
            Check(game.Phase==GameSession.ScreenState.Results && Time.timeScale==0,"Win opens results and freezes simulation");
            Check(game.ResultScore==game.ObjectivePoints+game.StealthPoints+game.TimePoints && game.ResultScore>0 && game.ResultScore<=10000,"Score breakdown totals correctly within limit");
            int points=game.ResultScore;
            game.End(true);
            Check(game.ResultScore==points,"Repeated completion does not change score");
        }
        private void Check(bool condition,string name)
        {
            if(!condition)
            {
                Finish(false,"FAILED: "+name);
                throw new InvalidOperationException("FAILED: "+name);
            }
            checks.Add(name);Debug.Log("SNEAK_HOUSE_CHECK_PASS: "+name);
        }
        private static string WorkspaceOutput(string name)
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath,"../Documentation/Verification"));
            Directory.CreateDirectory(folder);
            return Path.Combine(folder,name);
        }
        private static void Capture()
        {
            Camera camera=Camera.main;RenderTexture target=RenderTexture.GetTemporary(1440,900,24);
            RenderTexture previous=RenderTexture.active;camera.targetTexture=target;
            camera.Render();RenderTexture.active=target;
            Texture2D image=new Texture2D(1440,900,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1440,900),0,0);image.Apply();
            File.WriteAllBytes(WorkspaceOutput("unity-prototype-preview.png"),image.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Destroy(image);
        }
        private void Finish(bool success,string error)
        {
            Report report=new Report{passed=success,unity=Application.unityVersion,checks=checks.ToArray(),error=error,defaultGuards=1,targets=3};
            File.WriteAllText(WorkspaceOutput("unity-prototype-validation.json"),JsonUtility.ToJson(report,true));
            Debug.Log(success?"SNEAK_HOUSE_PLAY_CHECKS_PASS":"SNEAK_HOUSE_PLAY_CHECKS_FAILED: "+error);
            SessionState.SetInt("SneakHouse.VerifyPhase",2);SessionState.SetInt("SneakHouse.VerifyExit",success?0:2);
            Time.timeScale=1;EditorApplication.isPlaying=false;
        }
    }
}

#endif
