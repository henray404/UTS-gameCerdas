using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using SneakHouse;

namespace SneakHouseEditor
{
    public static class LocomotionBuilder
    {
        private const string Root="Assets/SneakHouse/";
        [MenuItem("Sneak House/Calibrate locomotion")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
            if(!AssetDatabase.IsValidFolder(Root+"Animations"))AssetDatabase.CreateFolder("Assets/SneakHouse","Animations");
            var reports=new List<string>();var done=new HashSet<string>();
            foreach(ActorAnimation acting in UnityEngine.Object.FindObjectsByType<ActorAnimation>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                AnimatorController controller=acting.animator.runtimeAnimatorController as AnimatorController;
                if(controller==null)continue;
                Undo.RecordObject(acting,"Match locomotion speed");acting.walkReferenceSpeed=1.4f;acting.runReferenceSpeed=3.2f;
                if(!done.Add(controller.name))continue;
                // Always sample the imported originals, so rebuilding cannot compound edits.
                string model=controller.name;string assetPath=Root+"Models/Actors/"+model+".fbx";
                var clips=AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")).ToArray();
                foreach(string state in new[]{"Walk","Run"})
                {
                    AnimationClip original=clips.First(c=>c.name==state);
                    float duration=state=="Walk"?.65f:.44f, reference=state=="Walk"?1.4f:3.2f, duty=state=="Walk"?.56f:.36f;
                    float error;AnimationClip clip=Bake(acting,original,duration,reference,duty,out error);
                    string path=Root+"Animations/"+model+"_"+state+".anim";AnimationClip existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if(existing!=null){EditorUtility.CopySerialized(clip,existing);UnityEngine.Object.DestroyImmediate(clip);clip=existing;}else AssetDatabase.CreateAsset(clip,path);
                    foreach(var child in controller.layers[0].stateMachine.states)if(child.state.name==state){child.state.motion=clip;EditorUtility.SetDirty(child.state);}
                    EditorUtility.SetDirty(controller);reports.Add(model+" / "+state+": reference "+reference.ToString("F2")+" m/s, cycle "+duration.ToString("F2")+" s, max IK error "+error.ToString("F4")+" m");
                }
            }
            System.IO.Directory.CreateDirectory("Documentation/Verification");System.IO.File.WriteAllLines("Documentation/Verification/locomotion-calibration.txt",reports);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }
        internal static AnimationClip Bake(ActorAnimation acting,AnimationClip source,float duration,float reference,float duty,out float maxError)
        {
            GameObject dummy=UnityEngine.Object.Instantiate(acting.animator.gameObject);dummy.name="Locomotion bake / temporary";dummy.hideFlags=HideFlags.HideAndDontSave;dummy.transform.position=Vector3.zero;
            maxError=0;
            try
            {
                Transform[] transforms=dummy.GetComponentsInChildren<Transform>();var map=transforms.ToDictionary(t=>t.name,t=>t);
                Transform[] hips={map["thigh.L"],map["thigh.R"]},knees={map["shin.L"],map["shin.R"]},feet={map["foot.L"],map["foot.R"]};
                Vector3 forward=acting.transform.forward;Vector3[] centers=new Vector3[2];Quaternion[] soles=new Quaternion[2];float[] floor={100,100},near={100,100},far={-100,-100};
                for(int i=0;i<80;i++)
                {
                    source.SampleAnimation(dummy,source.length*i/80);
                    for(int side=0;side<2;side++){floor[side]=Mathf.Min(floor[side],feet[side].position.y);float z=Vector3.Dot(feet[side].position,forward);near[side]=Mathf.Min(near[side],z);far[side]=Mathf.Max(far[side],z);}
                }
                source.SampleAnimation(dummy,0);
                for(int side=0;side<2;side++){centers[side]=feet[side].position;centers[side]+=forward*((near[side]+far[side])*.5f-Vector3.Dot(centers[side],forward));centers[side].y=floor[side];soles[side]=feet[side].rotation;}
                var curves=new Dictionary<string,AnimationCurve[]>();
                foreach(Transform t in transforms)if(t!=dummy.transform)curves[AnimationUtility.CalculateTransformPath(t,dummy.transform)]=Enumerable.Range(0,7).Select(_=>new AnimationCurve()).ToArray();
                const int samples=72;
                for(int frame=0;frame<=samples;frame++)
                {
                    float u=frame/(float)samples,time=u*duration;source.SampleAnimation(dummy,source.length*u);
                    map["pelvis"].position-=Vector3.up*.10f;
                    Vector3[] targets=new Vector3[2];float lowerPelvis=0;
                    for(int side=0;side<2;side++)
                    {
                        float phase=Mathf.Repeat(u+side*.5f,1),span=reference*duration*duty,offset,height=0;
                        if(phase<duty)offset=span*(.5f-phase/duty);
                        else{float p=(phase-duty)/(1-duty);offset=Mathf.Lerp(-span*.5f,span*.5f,Mathf.SmoothStep(0,1,p));height=Mathf.Sin(p*Mathf.PI)*(source.name=="Run"?.11f:.07f);}
                        targets[side]=centers[side]+forward*offset+Vector3.up*height;
                        float reach=Vector3.Distance(hips[side].position,knees[side].position)+Vector3.Distance(knees[side].position,feet[side].position)-.006f;
                        Vector3 horizontal=Vector3.ProjectOnPlane(targets[side]-hips[side].position,Vector3.up);
                        float allowedHeight=Mathf.Sqrt(Mathf.Max(.001f,reach*reach-horizontal.sqrMagnitude));
                        lowerPelvis=Mathf.Max(lowerPelvis,hips[side].position.y-targets[side].y-allowedHeight);
                    }
                    map["pelvis"].position-=Vector3.up*lowerPelvis;
                    for(int side=0;side<2;side++){Solve(hips[side],knees[side],feet[side],targets[side],forward);feet[side].rotation=soles[side];maxError=Mathf.Max(maxError,Vector3.Distance(targets[side],feet[side].position));}
                    foreach(Transform t in transforms)
                    {
                        if(t==dummy.transform)continue;AnimationCurve[] c=curves[AnimationUtility.CalculateTransformPath(t,dummy.transform)];Vector3 p=t.localPosition;Quaternion q=t.localRotation;
                        float[] values={p.x,p.y,p.z,q.x,q.y,q.z,q.w};for(int k=0;k<7;k++)c[k].AddKey(time,values[k]);
                    }
                }
                AnimationClip clip=new AnimationClip{name=source.name,frameRate=60};string[] properties={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w"};
                foreach(var pair in curves)for(int k=0;k<7;k++)clip.SetCurve(pair.Key,typeof(Transform),properties[k],pair.Value[k]);
                clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);return clip;
            }
            finally{UnityEngine.Object.DestroyImmediate(dummy);}
        }
        private static void Solve(Transform hip,Transform knee,Transform foot,Vector3 target,Vector3 forward)
        {
            float a=Vector3.Distance(hip.position,knee.position),b=Vector3.Distance(knee.position,foot.position);Vector3 delta=target-hip.position;
            float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.001f,a+b-.001f);Vector3 direction=delta.normalized;
            Vector3 bend=Vector3.ProjectOnPlane(forward,direction).normalized;float x=(a*a+distance*distance-b*b)/(2*distance);
            Vector3 joint=hip.position+direction*x+bend*Mathf.Sqrt(Mathf.Max(0,a*a-x*x));
            hip.rotation=Quaternion.FromToRotation(knee.position-hip.position,joint-hip.position)*hip.rotation;
            knee.rotation=Quaternion.FromToRotation(foot.position-knee.position,target-knee.position)*knee.rotation;
        }
    }
}
