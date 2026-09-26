using System;
using System.Collections;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Unity.XR.CoreUtils;
using JuegoAAA.VR;
namespace ForestVR.Editor
{
    // Batch entry point for a disposable project only. Does not run in the user's session.
    [InitializeOnLoad]
    public static class ForestCombatValidation
    {
        const string Flag = "ForestVR.CombatValidation";
        static IEnumerator sequence;
        static int frame = -1;
        static double deadline;
        static ForestCombatValidation() { EditorApplication.update += Tick; }
        public static void RunBatch()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Flag, true); EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying || frame == Time.frameCount) return;
            frame = Time.frameCount;
            try
            {
                if (sequence == null) { sequence = Tests(); deadline = EditorApplication.timeSinceStartup + 120; }
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Combat test timeout");
                if (!sequence.MoveNext()) { SessionState.SetBool(Flag, false); Debug.Log("FOREST_COMBAT_TESTS_OK"); EditorApplication.Exit(0); }
            }
            catch (Exception ex) { SessionState.SetBool(Flag, false); Debug.LogException(ex); EditorApplication.Exit(1); }
        }
        static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Debug.Log("COMBAT PASS: " + label); }
        static T Load<T>(string path) where T:UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path);
        static Health Target(Vector3 position)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name="Damage target"; go.transform.position=position;
            go.transform.localScale=new Vector3(.5f,1,.5f); return go.AddComponent<Health>();
        }
        static XRDirectInteractor Hand(Transform player,string name)
        {
            var go=new GameObject(name); go.transform.SetParent(player,false);
            var sphere=go.AddComponent<SphereCollider>(); sphere.isTrigger=true; sphere.radius=.03f;
            var body=go.AddComponent<Rigidbody>(); body.isKinematic=true; body.useGravity=false;
            return go.AddComponent<XRDirectInteractor>();
        }
        static IEnumerator Tests()
        {
            new GameObject("Interaction Manager").AddComponent<XRInteractionManager>();
            var player=new GameObject("Player"); var hp=player.AddComponent<Health>();
            var head=new GameObject("Head").transform; head.SetParent(player.transform,false); head.localPosition=Vector3.up*1.6f;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.position=Vector3.down*.5f; floor.transform.localScale=new Vector3(200,1,200);
            var config=UnityEngine.Object.Instantiate(Load<GoblinSettings>("Assets/SO_/ForestGoblinSettings.asset"));
            var actor=UnityEngine.Object.Instantiate(Load<GameObject>("Assets/02_Prefabs/Enemy/ForestGoblin.prefab")).GetComponent<GoblinActor>();
            player.transform.position=new Vector3(0,0,6);
            actor.Initialize(config,head,hp);
            Check(actor.CurrentState==GoblinActor.State.Resting,"Goblin starts resting");
            for(int i=0;i<3;i++) yield return null;
            Check(actor.CurrentState==GoblinActor.State.Resting,"Sleeping goblin ignores a player outside the wake radius");
            player.transform.position=new Vector3(0,0,config.wakeRadius*0.6f);
            for(int i=0;i<3;i++) yield return null;
            Check(actor.CurrentState==GoblinActor.State.GettingUp,"Very close player wakes goblin");
            player.transform.position=new Vector3(0,0,6);
            float until=Time.time+config.gettingUp.length+.3f; while(Time.time<until) yield return null;
            Check(actor.CanSeeTarget()&&actor.CurrentState==GoblinActor.State.Walking,"Front target is pursued after getting up");
            player.transform.position=actor.transform.position-actor.transform.forward*6;
            yield return null;
            Check(!actor.CanSeeTarget(),"Front vision cannot see behind");
            actor.Health.TakeHit(5,player.transform.position);
            Check(actor.CurrentState==GoblinActor.State.TurningFromHit,"Back hit triggers turn reaction");
            until=Time.time+config.attackedFromBack.length+.3f; while(Time.time<until) yield return null;
            Check(actor.CanSeeTarget(),"Goblin turns toward attacker");
            Check(hp.Current==100,"No melee damage at long range");
            player.transform.position=actor.transform.position+actor.transform.forward*.8f;
            until=Time.time+4; while(actor.CurrentState!=GoblinActor.State.Attacking&&Time.time<until) yield return null;
            Check(actor.CurrentState==GoblinActor.State.Attacking,"Close target triggers attack");
            player.transform.position=actor.transform.position+actor.transform.forward*10;
            until=Time.time+3; while(Time.time<until) yield return null;
            Check(hp.Current==100,"Moving out of melee range avoids impact");
            actor.Health.TakeHit(1000,player.transform.position);
            Check(actor.CurrentState==GoblinActor.State.Dead,"Lethal damage selects death state");
            UnityEngine.Object.Destroy(actor.gameObject);

            var left=Hand(player.transform,"Left hand"); var right=Hand(player.transform,"Right hand");
            player.transform.position=new Vector3(10,0,0); right.transform.position=new Vector3(10,1.4f,0); right.transform.rotation=Quaternion.identity;
            var gun=UnityEngine.Object.Instantiate(Load<GameObject>("Assets/02_Prefabs/Weapons/VRRevolver.prefab")).GetComponent<VRRevolver>();
            var gunGrip=gun.GetComponent<WeaponGrip>(); gunGrip.Configure(hp,null);
            yield return null;
            right.StartManualInteraction((IXRSelectInteractable)gunGrip.Grab);
            for(int i=0;i<4;i++) yield return null;
            var gunTarget=Target(gun.muzzle.position+gun.muzzle.forward*3); Physics.SyncTransforms();
            Check(gun.TryFire(),"Held revolver fires"); Check(!gun.TryFire(),"Revolver cooldown blocks repeated activation");
            until=Time.time+1; while(Time.time<until&&!gunTarget.IsDead) yield return null;
            Check(gunTarget.IsDead,"Bullet sweep deals 100 damage");
            right.EndManualInteraction(); yield return null; Check(!gun.TryFire(),"Unheld revolver cannot fire");
            UnityEngine.Object.Destroy(gun.gameObject); UnityEngine.Object.Destroy(gunTarget.gameObject);

            player.transform.position=new Vector3(20,0,0); left.transform.position=new Vector3(20,1.4f,0); left.transform.rotation=Quaternion.identity;
            var bow=UnityEngine.Object.Instantiate(Load<GameObject>("Assets/02_Prefabs/Weapons/VRBow.prefab")).GetComponent<VRBow>();
            var bowGrip=bow.GetComponent<WeaponGrip>(); bowGrip.Configure(hp,null); yield return null;
            left.StartManualInteraction((IXRSelectInteractable)bowGrip.Grab);
            until=Time.time+.4f; while(Time.time<until) yield return null;
            Check(bowGrip.CanUse,"Bow is held by living player");
            Physics.SyncTransforms();
            Check(System.Array.Exists(Physics.OverlapSphere(bow.stringGrip.transform.position,.08f,1<<8,QueryTriggerInteraction.Ignore), c=>c.GetComponent<BowStringInteractable>()!=null),"String detectable by non-trigger XR casts");
            Check(!bow.CanDraw(left),"Bow holding hand cannot draw the string");
            right.transform.position=bow.restingNock.position; yield return null;
            Check(bow.stringGrip.IsSelectableBy((IXRSelectInteractor)right),"Second hand can grab the string nearby: distance="+Vector3.Distance(right.transform.position,bow.stringGrip.transform.position)+" canDraw="+bow.CanDraw(right));
            right.StartManualInteraction((IXRSelectInteractable)bow.stringGrip); yield return null;
            right.transform.position=bow.pullEnd.position;
            for(int i=0;i<4;i++) yield return null;
            Check(bow.PullAmount>.98f,"String tracks full draw; pull="+bow.PullAmount);
            var bowTarget=Target(bow.restingNock.position+bow.transform.forward*4); Physics.SyncTransforms();
            right.EndManualInteraction();
            until=Time.time+1; while(Time.time<until&&bowTarget.Current==100) yield return null;
            Check(Mathf.Abs(bowTarget.Current-45)<.1f,"Released arrow deals 55 damage at full draw");
            Check(bow.DrawDistance==0,"String resets after release");
            until=Time.time+.6f; while(Time.time<until) yield return null;
            right.transform.position=bow.restingNock.position; right.StartManualInteraction((IXRSelectInteractable)bow.stringGrip);
            yield return null; right.EndManualInteraction();
            until=Time.time+.3f; while(Time.time<until) yield return null;
            Check(Mathf.Abs(bowTarget.Current-45)<.1f,"Releasing without draw does not shoot");
            left.EndManualInteraction(); UnityEngine.Object.Destroy(bow.gameObject); UnityEngine.Object.Destroy(bowTarget.gameObject);

            player.transform.position=new Vector3(30,0,0); right.transform.position=new Vector3(30,1,0); right.transform.rotation=Quaternion.identity;
            var axe=UnityEngine.Object.Instantiate(Load<GameObject>("Assets/02_Prefabs/Weapons/VRAxe.prefab")).GetComponent<VRAxe>();
            var axeGrip=axe.GetComponent<WeaponGrip>(); axeGrip.Configure(hp,null); yield return null;
            right.StartManualInteraction((IXRSelectInteractable)axeGrip.Grab);
            until=Time.time+.4f; while(Time.time<until) yield return null;
            Check(axeGrip.CanUse,"Axe is held by living player");
            var axeTarget=Target(axe.blade.position+Vector3.forward*.5f); Physics.SyncTransforms();
            for(int i=0;i<5;i++) yield return null;
            Check(axeTarget.Current==100,"Stationary axe deals no damage");
            until=Time.time+.25f; while(Time.time<until) { right.transform.position+=Vector3.forward*(2.5f*Time.deltaTime); yield return null; }
            float afterSwing=axeTarget.Current;
            Check(afterSwing>=75 && afterSwing<=85,"2.5 m/s axe swing deals ~21 damage once; health="+afterSwing);
            until=Time.time+.8f; while(Time.time<until) yield return null;
            Check(axeTarget.Current==afterSwing,"Holding axe against target does not repeat damage");
            right.EndManualInteraction(); UnityEngine.Object.Destroy(axe.gameObject); UnityEngine.Object.Destroy(axeTarget.gameObject);

            player.transform.position=new Vector3(40,0,0);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position=new Vector3(40,1,2); wall.transform.localScale=new Vector3(2,2,.05f);
            var behind=Target(new Vector3(40,1,4));
            var bullet=UnityEngine.Object.Instantiate(Load<GameObject>("Assets/02_Prefabs/Weapons/Bullet.prefab"),new Vector3(40,1,0),Quaternion.identity).GetComponent<WeaponProjectile>();
            Physics.SyncTransforms(); bullet.Launch(Load<WeaponSettings>("Assets/SO_/Weapons/Revolver.asset"),hp,null,Vector3.forward,1,new Vector3(40,1,0));
            until=Time.time+.3f; while(Time.time<until) yield return null;
            Check(behind.Current==100,"Thin wall blocks high speed bullet");

            var ramp=new GameObject("55 degree ramp"); ramp.transform.position=new Vector3(50,0,0);
            var mesh=new Mesh(); float height=Mathf.Tan(55*Mathf.Deg2Rad)*3;
            mesh.vertices=new[]{new Vector3(-2,0,0),new Vector3(2,0,0),new Vector3(-2,height,3),new Vector3(2,height,3)};
            mesh.triangles=new[]{0,2,1,1,2,3}; mesh.RecalculateNormals(); ramp.AddComponent<MeshCollider>().sharedMesh=mesh;
            var walker=new GameObject("Slope player"); walker.SetActive(false); walker.transform.position=new Vector3(50,.05f,-.6f);
            var cc=walker.AddComponent<CharacterController>(); cc.height=1.7f; cc.center=Vector3.up*.85f; cc.radius=.2f;
            var xr=walker.AddComponent<XROrigin>(); var camera=new GameObject("Camera").AddComponent<Camera>(); camera.transform.SetParent(walker.transform,false); camera.transform.localPosition=Vector3.up*1.7f; xr.Camera=camera;
            var probe=walker.AddComponent<VRGroundProbe>(); var serialized=new SerializedObject(probe);
            serialized.FindProperty("locomotionSettings").objectReferenceValue=Load<VRLocomotionSettings>("Assets/SO_/ForestLocomotionSettings.asset"); serialized.ApplyModifiedPropertiesWithoutUndo();
            walker.SetActive(true); Physics.SyncTransforms(); yield return null;
            Check(cc.slopeLimit==60&&Mathf.Abs(cc.stepOffset-.4f)<.001f&&cc.minMoveDistance==0,"Locomotion settings applied");
            for(int i=0;i<120;i++) { cc.Move(new Vector3(0,-.03f,.025f)); yield return null; }
            Check(walker.transform.position.y>1&&walker.transform.position.z>.7f,"Character controller climbs 55 degree ramp");
        }
    }
}

