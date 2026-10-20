using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using SneakHouse;

namespace SneakHouseEditor
{
    public static class PrototypeBuilder
    {
        public const string Root = "Assets/SneakHouse/";
        public const string ScenePath = Root + "Scenes/PrototypeHouse.unity";
        private static Transform level;
        private static Material cream, timber, wood, olive, floor, brass;

        [MenuItem("Sneak House/Build prototype scene")]
        public static void Build()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Directory.CreateDirectory(Root + "Scenes");
            Directory.CreateDirectory(Root + "Materials");
            Directory.CreateDirectory(Root + "Prefabs");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GraphicsSettings.defaultRenderPipeline = null;
            QualitySettings.renderPipeline = null;
            PlayerSettings.companyName = "Game Cerdas";
            PlayerSettings.productName = "Sneak House Prototype";
            PlayerSettings.defaultScreenWidth = 1440; PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            SerializedObject settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            SerializedProperty input = settings.FindProperty("activeInputHandler");
            if (input != null) { input.intValue = 0; settings.ApplyModifiedPropertiesWithoutUndo(); }
            SetLayer(8, "Architecture"); SetLayer(9, "Actors");
            cream = Material("Plaster", "D9D1B6"); timber = Material("Timber", "633F2A");
            wood = Material("WarmWood", "99623D"); olive = Material("Garden", "73805A");
            floor = Material("Floor", "BE9664"); brass = Material("Marker", "D1A34B");
            level = new GameObject("HOUSE / Navigation & occlusion geometry").transform;
            Box("Courtyard ground", new Vector3(0, -.15f, 0), new Vector3(23, .3f, 17), olive, true);
            Box("Living floor", new Vector3(0, -.025f, -2.5f), new Vector3(16, .1f, 7), floor, true);
            Box("Office floor", new Vector3(-4, -.025f, 3.5f), new Vector3(8, .1f, 5), floor, true);
            Box("Collection floor", new Vector3(4, -.025f, 3.5f), new Vector3(8, .1f, 5), floor, true);
            Planks(-8, 8, -6, 1); Planks(-8, 0, 1, 6); Planks(0, 8, 1, 6);
            Wall("Back", new Vector3(0, 0, 6), 16, false, 1.8f);
            Wall("Front cutaway", new Vector3(0, 0, -6), 16, false, .65f);
            Wall("East", new Vector3(8, 0, 0), 12, true, 1.0f);
            Wall("West north", new Vector3(-8, 0, 1.35f), 9.3f, true, 1.4f);
            Wall("West south", new Vector3(-8, 0, -5.8f), .4f, true, 1.0f);
            Wall("Office west partition", new Vector3(-6, 0, 1), 4, false, .75f);
            Wall("Middle partition", new Vector3(.5f, 0, 1), 5, false, .75f);
            Wall("Collection east partition", new Vector3(6.5f, 0, 1), 3, false, .75f);
            Wall("Office / collection", new Vector3(0, 0, 3.5f), 5, true, 1.15f);
            Prop("door_frame", new Vector3(-8, .03f, -4.45f), 2.05f, 2.55f, 90, false);
            // Full-height collision remains behind cutaway wall art for stealth line of sight.
            Box("West border", new Vector3(-11.45f, .65f, 0), new Vector3(.12f, 1.3f, 17), timber, true);
            Box("East border", new Vector3(11.45f, .65f, 0), new Vector3(.12f, 1.3f, 17), timber, true);
            Box("Garden north", new Vector3(0, .65f, 8.4f), new Vector3(23, 1.3f, .12f), timber, true);
            Box("Garden south", new Vector3(0, .65f, -8.4f), new Vector3(23, 1.3f, .12f), timber, true);

            Prop("loungeSofa", new Vector3(-5.3f, .06f, -.7f), 2.8f, 1.0f, 180);
            Prop("loungeChair", new Vector3(-6.2f, .06f, -3.1f), 1.15f, 1.0f, 75);
            Prop("tableCoffee", new Vector3(-4.9f, .06f, -2.6f), 1.7f, .52f, 0);
            Prop("rugRectangle", new Vector3(-4.7f, .07f, -2.5f), 3.6f, .015f, 0, false);
            Prop("pottedPlant", new Vector3(-7.2f, .06f, -.1f), .8f, 1.6f, 0);
            Prop("grandfather_clock", new Vector3(7.45f, .06f, -.3f), .8f, 1.75f, 270);
            Prop("bookcaseOpen", new Vector3(7.55f, .06f, -4.45f), 1.45f, 1.7f, 270);
            Prop("clay_vase", new Vector3(-7.15f, .07f, 5.0f), .65f, .85f, 0);
            Bounds desk = Prop("desk", new Vector3(-5.65f, .06f, 4.3f), 2.1f, .9f, 180);
            Prop("chairDesk", new Vector3(-5.65f, .06f, 3.35f), .7f, 1.1f, 0);
            Prop("computerScreen", new Vector3(-5.25f, desk.max.y, 4.35f), .55f, .48f, 180, false);
            Prop("computerKeyboard", new Vector3(-5.25f, desk.max.y, 4.0f), .45f, .06f, 180, false);
            Prop("filing_cabinet", new Vector3(-1.3f, .06f, 5.35f), .9f, 1.45f, 180);
            Prop("books", new Vector3(-6.4f, desk.max.y, 4.4f), .30f, .25f, 0, false);
            Bounds plinth = Prop("display_plinth", new Vector3(5.85f, .06f, 4.6f), .85f, .9f, 0);
            Prop("vault_safe", new Vector3(1.5f, .06f, 5.05f), 1.1f, 1.35f, 180);
            Prop("pottedPlant", new Vector3(7.15f, .06f, 5.3f), .7f, 1.4f, 0);
            Bounds consoleTable = Prop("tableCoffee", new Vector3(5.25f, .06f, -3.6f), 1.5f, .60f, 0);
            Prop("loungeSofa", new Vector3(3.2f, .06f, -5.05f), 2.1f, .95f, 0);
            Prop("pottedPlant", new Vector3(-9.7f, .05f, -2.6f), .65f, 1.1f, 0);
            Prop("pottedPlant", new Vector3(-9.7f, .05f, -6.35f), .65f, 1.1f, 0);

            GameObject system = new GameObject("MISSION / Change Active Guard Count here");
            RuntimeNavigation navigation = system.AddComponent<RuntimeNavigation>(); navigation.geometry = level;
            GameSession session = system.AddComponent<GameSession>(); session.activeGuardCount = 3;
            session.bodyFont = AssetDatabase.LoadAssetAtPath<Font>(Root + "Fonts/PixelifySans-Variable.ttf");
            session.titleFont = AssetDatabase.LoadAssetAtPath<Font>(Root + "Fonts/PressStart2P-Regular.ttf");
            session.targets = new[]
            {
                Loot("confidential_drive", "Drive rahasia", new Vector3(-6.15f, desk.max.y + .03f, 4.10f), .36f),
                Loot("premium_watch", "Jam tangan", new Vector3(5.85f, plinth.max.y + .03f, 4.6f), .34f),
                Loot("nextgen_console", "Konsol game", new Vector3(5.25f, consoleTable.max.y + .03f, -3.6f), .65f)
            };
            session.player = Player();
            session.guards = new[]
            {
                Guard("Bogo / Patroli rumah", new Vector3(1.4f, .06f, -1.65f), new[] { V(1.4f,-1.65f),V(-2.9f,-.15f),V(-2.9f,3.1f),V(-6.2f,2.5f),V(-2.9f,3.1f),V(-2.9f,-.2f),V(4,-.1f),V(4,3.1f),V(6.55f,2.55f),V(4,3.1f),V(4,-.1f),V(5,-2),V(1.4f,-1.65f) }),
                Guard("Bogo / NPC opsional 2", new Vector3(-2.9f,.06f,3.1f), new[]{V(-2.9f,3.1f),V(-6.2f,2.5f),V(-2.9f,3.1f),V(-2.9f,-.2f)}),
                Guard("Bogo / NPC opsional 3", new Vector3(4,.06f,3.1f), new[]{V(4,3.1f),V(6.55f,2.55f),V(4,3.1f),V(4,-.1f)})
            };
            GameObject exit = new GameObject("EXTRACTION / Kembali ke pintu masuk");
            exit.transform.position = new Vector3(-9,.08f,-4.45f); exit.AddComponent<ExtractionZone>();
            Ring(exit.transform, .95f, new Color(.30f,.65f,.48f), .07f);
            Light point = new GameObject("Porch lantern").AddComponent<Light>(); point.type=LightType.Point;
            point.transform.position=new Vector3(-8.6f,2.1f,-4.5f);point.color=new Color(1,.80f,.44f);point.intensity=2.4f;point.range=6;
            Camera camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag="MainCamera";
            camera.transform.position=new Vector3(0,22,-16);camera.transform.LookAt(new Vector3(0,.25f,.2f));
            camera.orthographic=true;camera.orthographicSize=9.2f;camera.nearClipPlane=.1f;camera.farClipPlane=90;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Hex("C9C9A3");camera.gameObject.AddComponent<AudioListener>();
            ChaseCameraBuilder.Configure(camera, session, level);
            Light sun=new GameObject("Warm afternoon").AddComponent<Light>();sun.type=LightType.Directional;
            sun.transform.rotation=Quaternion.Euler(48,-32,0);sun.color=new Color(1,.91f,.75f);sun.intensity=1.25f;sun.shadows=LightShadows.Soft;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=Hex("E0DEC8");RenderSettings.ambientEquatorColor=Hex("B8B69B");RenderSettings.ambientGroundColor=Hex("6E785D");
            QualitySettings.shadowDistance=50;QualitySettings.antiAliasing=4;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),ScenePath);
            ThreeGuardBuilder.Apply();
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            AssetDatabase.SaveAssets();
            SceneView view=SceneView.lastActiveSceneView;
            if(view!=null){view.pivot=new Vector3(0,0,.2f);view.rotation=camera.transform.rotation;view.size=14;view.orthographic=true;}
            Debug.Log("SNEAK_HOUSE_SCENE_READY: " + ScenePath);
        }
        private static Vector3 V(float x,float z) => new Vector3(x,.06f,z);
        private static void SetLayer(int index,string name)
        {
            SerializedObject tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            tags.FindProperty("layers").GetArrayElementAtIndex(index).stringValue=name;tags.ApplyModifiedProperties();
        }
        private static Material Material(string name,string hex)
        {
            string path=Root+"Materials/"+name+".mat";Material m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}
            m.color=Hex(hex);m.SetFloat("_Glossiness",.08f);return m;
        }
        private static Color Hex(string hex){ColorUtility.TryParseHtmlString("#"+hex,out Color c);return c;}
        private static GameObject Box(string name,Vector3 p,Vector3 size,Material m,bool collision)
        {
            GameObject box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name=name;box.transform.SetParent(level);
            box.transform.position=p;box.transform.localScale=size;box.GetComponent<Renderer>().sharedMaterial=m;
            if(!collision) UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
            box.layer=collision?8:0;box.isStatic=true;return box;
        }
        private static void Wall(string name,Vector3 centre,float length,bool vertical,float visibleHeight)
        {
            Box(name+" / Cutaway art",centre+Vector3.up*visibleHeight/2,new Vector3(vertical?.18f:length,visibleHeight,vertical?length:.18f),cream,false);
            Box(name+" / Baseboard",centre+Vector3.up*.10f,new Vector3(vertical?.23f:length,.2f,vertical?length:.23f),timber,false);
            GameObject collision=new GameObject(name+" / Full wall collider");collision.transform.SetParent(level);collision.layer=8;collision.transform.position=centre+Vector3.up*1.45f;
            collision.AddComponent<BoxCollider>().size=new Vector3(vertical?.18f:length,2.9f,vertical?length:.18f);
        }
        private static void Planks(float minX,float maxX,float minZ,float maxZ)
        {
            for(float z=minZ+.25f;z<maxZ;z+=.55f)
                for(float x=minX+1.0f;x<maxX;x+=2.0f)
                    Box("Floorboard",new Vector3(x,.027f,z),new Vector3(1.96f,.012f,.52f),((int)(z*10)%3)==0?wood:floor,false);
        }
        private static Bounds BoundsOf(GameObject go)
        {
            Renderer[] renderers=go.GetComponentsInChildren<Renderer>();Bounds b=renderers[0].bounds;
            foreach(Renderer r in renderers)b.Encapsulate(r.bounds);return b;
        }
        private static GameObject Import(string path,Transform parent,string name,Vector3 position,float width,float maxHeight,float angle=0)
        {
            GameObject asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(asset==null)throw new FileNotFoundException("Missing model",path);
            GameObject go=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);go.name=name;go.transform.position=position;go.transform.rotation=Quaternion.Euler(0,angle,0);
            Bounds b=BoundsOf(go);float factor=Mathf.Min(width/Mathf.Max(b.size.x,b.size.z),maxHeight/Mathf.Max(.001f,b.size.y));
            go.transform.localScale*=factor;b=BoundsOf(go);go.transform.position+=Vector3.up*(position.y-b.min.y);
            return go;
        }
        private static Bounds Prop(string name,Vector3 position,float width,float height,float angle=0,bool collision=true)
        {
            GameObject go=Import(Root+"Models/Props/"+name+".fbx",level,name,position,width,height,angle);
            Bounds b=BoundsOf(go);
            if(collision)
            {
                GameObject c=new GameObject(name+" / Navigation proxy");c.transform.SetParent(level);c.transform.position=b.center;c.layer=8;
                c.AddComponent<BoxCollider>().size=b.size;
            }
            return b;
        }
        private static LootTarget Loot(string name,string label,Vector3 position,float size)
        {
            GameObject root=new GameObject("TARGET / "+label);root.transform.position=position;
            LootTarget loot=root.AddComponent<LootTarget>();loot.displayName=label;
            GameObject art=Import(Root+"Models/Loot/"+name+".fbx",root.transform,name,position,size,size);
            loot.visual=art.transform;Ring(root.transform,.30f,Hex("D1A34B"),.04f);return loot;
        }
        private static void Ring(Transform parent,float radius,Color colour,float width)
        {
            GameObject obj=new GameObject("Interaction marker");obj.transform.SetParent(parent,false);
            LineRenderer line=obj.AddComponent<LineRenderer>();line.useWorldSpace=false;line.loop=true;line.positionCount=48;line.widthMultiplier=width;
            Material m=Material("Ring"+ColorUtility.ToHtmlStringRGB(colour),ColorUtility.ToHtmlStringRGB(colour));m.shader=Shader.Find("Sprites/Default");line.sharedMaterial=m;
            line.startColor=line.endColor=colour;
            for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48;line.SetPosition(i,new Vector3(Mathf.Cos(a)*radius,.05f,Mathf.Sin(a)*radius));}
        }
        private static ActorAnimation Actor(Transform parent,string file,float height,bool player)
        {
            GameObject model=Import(Root+"Models/Actors/"+file+".fbx",parent,"Visual / "+file,parent.position,2,height);
            Transform eye=model.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name.StartsWith("face_eye"));
            Transform head=model.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="head");
            if(eye!=null&&head!=null){Vector3 facing=eye.position-head.position;facing.y=0;if(facing.sqrMagnitude>.0001f)model.transform.Rotate(0,Vector3.SignedAngle(facing,parent.forward,Vector3.up),0);}
            Animator animator=model.GetComponent<Animator>();if(animator==null)animator=model.AddComponent<Animator>();
            animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            string path=Root+"Prefabs/"+file+".controller";
            AnimatorController controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine machine=controller.layers[0].stateMachine;
            foreach(ChildAnimatorState s in machine.states)machine.RemoveState(s.state);
            AnimationClip[] clips=AssetDatabase.LoadAllAssetsAtPath(Root+"Models/Actors/"+file+".fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
            foreach(string name in new[]{"Idle","Walk","Run","LookAround","FaceActing"})
            {
                AnimationClip clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"Animations/"+file+"_"+name+".anim") ?? clips.FirstOrDefault(c=>c.name.EndsWith(name)||c.name.Contains("|"+name));
                if(clip==null)continue;AnimatorState state=machine.AddState(name);state.motion=clip;if(name=="Idle")machine.defaultState=state;
            }
            animator.runtimeAnimatorController=controller;
            foreach(Transform t in parent.GetComponentsInChildren<Transform>())t.gameObject.layer=9;
            if(player)
            {
                Material jacket=Material("PlayerJacket","405644");
                foreach(Renderer r in model.GetComponentsInChildren<Renderer>())
                {
                    Material[] mats=r.sharedMaterials;
                    for(int i=0;i<mats.Length;i++)if(mats[i]!=null&&mats[i].name.ToLowerInvariant().Contains("navy"))mats[i]=jacket;
                    r.sharedMaterials=mats;
                }
            }
            ActorAnimation acting=parent.gameObject.AddComponent<ActorAnimation>();acting.animator=animator;return acting;
        }
        private static PlayerMotor Player()
        {
            GameObject go=new GameObject("PLAYER / Sneaker stand-in");go.transform.position=new Vector3(-9,.10f,-4.45f);
            CharacterController c=go.AddComponent<CharacterController>();c.height=1.55f;c.radius=.29f;c.center=Vector3.up*.78f;c.stepOffset=.2f;c.skinWidth=.025f;
            ActorAnimation acting=Actor(go.transform,"08_stan_guard",1.65f,true);
            PlayerMotor player=go.AddComponent<PlayerMotor>();player.acting=acting;return player;
        }
        private static GuardBrain Guard(string name,Vector3 position,Vector3[] route)
        {
            GameObject go=new GameObject(name);go.transform.position=position;go.transform.rotation=Quaternion.Euler(0,180,0);
            NavMeshAgent agent=go.AddComponent<NavMeshAgent>();agent.enabled=false;agent.radius=.40f;agent.height=1.8f;agent.acceleration=8;agent.angularSpeed=280;agent.stoppingDistance=.15f;agent.baseOffset=0;
            ActorAnimation acting=Actor(go.transform,"01_bogo_guard",1.80f,false);
            GuardBrain brain=go.AddComponent<GuardBrain>();brain.acting=acting;
            Transform points=new GameObject(name+" / Patrol route").transform;brain.patrol=new Transform[route.Length];
            for(int i=0;i<route.Length;i++){Transform p=new GameObject("Waypoint "+i.ToString("00")).transform;p.SetParent(points);p.position=route[i];brain.patrol[i]=p;}
            VisionCone cone=go.AddComponent<VisionCone>();cone.guard=brain;return brain;
        }
    }
    public class PrototypeModelImport : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if(!assetPath.Contains("/SneakHouse/Models/"))return;
            ModelImporter importer=(ModelImporter)assetImporter;
            importer.globalScale=1;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            if(assetPath.Contains("/Actors/") || assetPath.Contains("/PortraitCast/"))
            {
                importer.animationType=ModelImporterAnimationType.Generic;importer.importAnimation=true;
                ModelImporterClipAnimation[] clips=importer.defaultClipAnimations;
                foreach(ModelImporterClipAnimation c in clips){c.loopTime=true;c.loopPose=false;c.lockRootRotation=true;c.lockRootHeightY=true;c.lockRootPositionXZ=true;}
                importer.clipAnimations=clips;
            }
        }
        private void OnPostprocessMaterial(Material material)
        {
            if(!assetPath.Contains("/SneakHouse/Models/"))return;
            string name=material.name.ToLowerInvariant();material.shader=Shader.Find("Standard");material.SetFloat("_Glossiness",.10f);
            if(assetPath.Contains("/Exterior/")) return;
            Dictionary<string,string> colours=new Dictionary<string,string>
            {
                {"navydark","182C37"},{"navylight","53747E"},{"navy","2B4856"},{"greyhi","A9B5AC"},{"greydark","596559"},
                {"paper","F2EEDC"},{"pink","BF8D7E"},{"purple","81759C"},{"silver","A5ABA1"},{"gold","D1A34B"},
                {"cream","E6DFC6"},{"timber","633F2A"},{"wood","99623D"},{"clay","B87755"},{"olive","73805A"},{"leaf","647445"},
                {"ochre","D1A34B"},{"slate","62757D"},{"metal","646B68"},{"steel","89928B"},{"white","F2EEDC"},{"black","2D3330"},
                {"soil","554331"},{"red","955E50"},{"glass","AEC4BD"}
            };
            foreach(KeyValuePair<string,string> pair in colours)
                if(name.Contains(pair.Key)){ColorUtility.TryParseHtmlString("#"+pair.Value,out Color c);material.color=c;break;}
        }
    }
}
