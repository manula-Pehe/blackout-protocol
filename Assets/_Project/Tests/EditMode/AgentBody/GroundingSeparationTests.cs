using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using BlackoutProtocol.Core;
using Object = UnityEngine.Object;

namespace BlackoutProtocol.Body.Tests
{
    public sealed class GroundingSeparationTests
    {
        [Test]
        public void SeparationScoring_IsDeterministicBoundedAndRejectsInvalidOrBehindInputs()
        {
            Assert.That(SeparationMath.Threat(Vector3.forward,Vector3.forward,0.75f,2.2f),Is.InRange(0f,1f));
            Assert.AreEqual(0,SeparationMath.Threat(Vector3.forward,Vector3.back,0.75f,2.2f));
            Assert.AreEqual(0,SeparationMath.Threat(Vector3.forward,Vector3.right*4,0.75f,2.2f));
            Assert.AreEqual(0,SeparationMath.Threat(Vector3.zero,Vector3.one,0.75f,2.2f));
            Assert.AreEqual(0,SeparationMath.Threat(Vector3.forward,new Vector3(float.NaN,0,0),0.75f,2.2f));
            Assert.AreEqual(1,SeparationMath.PassingSide(Vector3.forward,Vector3.forward,Vector3.back,1,2));
            Assert.AreEqual(1,SeparationMath.PassingSide(Vector3.back,Vector3.back,Vector3.forward,2,1));
            Assert.AreEqual(-SeparationMath.PassingSide(Vector3.forward,Vector3.zero,Vector3.forward,2,1),
                SeparationMath.PassingSide(Vector3.forward,Vector3.zero,Vector3.forward,1,2));
        }
        [UnityTest]
        public IEnumerator FlatAndDescendingSlope_KeepContact_AndLedgeStillFalls()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return VerifyGround();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyGround()
        {
            const float dt = 1f / 60;
            var flat = Box("Flat ground", new Vector3(-10,-0.2f,0), new Vector3(8,0.4f,16));
            var body = Agent(new Vector3(-10,0.1f,-4));
            body.enabled = false; // Only the explicit motor ticks below own movement in this deterministic check.
            var motor = body.GetComponent<AgentMotor>();
            for (int i = 0; i < 60; i++) motor.Move(Vector3.zero, dt);
            float rest = body.Position.y;
            for (int i = 0; i < 120; i++)
            {
                motor.Move(Vector3.forward, dt);
                Assert.IsTrue(motor.IsGrounded);
                Assert.AreEqual(rest, body.Position.y, 0.002f, "Flat contact must not jitter.");
            }
            var slope = Box("35 degree slope", Vector3.zero, new Vector3(6,0.4f,18));
            slope.transform.rotation = Quaternion.Euler(35,0,0);
            Physics.SyncTransforms();
            float height = 0.2f / Mathf.Cos(35*Mathf.Deg2Rad) + 5*Mathf.Tan(35*Mathf.Deg2Rad);
            motor.Warp(new Vector3(0,height+0.15f,-5));
            for (int i = 0; i < 60; i++) motor.Move(Vector3.zero, dt);
            int grounded = 0;
            float maxGap = 0;
            for (int i = 0; i < 150; i++)
            {
                motor.Move(Vector3.forward*4, dt);
                if (motor.IsGrounded) grounded++;
                if (Physics.Raycast(body.Position + Vector3.up, Vector3.down, out var hit, 3))
                    maxGap = Mathf.Max(maxGap, body.Position.y-hit.point.y);
            }
            Debug.Log($"Ground contact audit: descending grounded={grounded}/150 max bottom gap={maxGap:F4}");
            Assert.GreaterOrEqual(grounded, 145, "Moderate descending slope should not repeatedly become airborne.");
            Assert.Less(maxGap, 0.14f);

            motor.Warp(new Vector3(0,height+0.15f,-5));
            for (int i=0;i<60;i++) motor.Move(Vector3.zero,dt);
            int highRateContact=0;
            for(int i=0;i<1000;i++)
            {
                motor.Move(Vector3.forward*4,0.001f);
                if(motor.IsGrounded) highRateContact++;
            }
            Debug.Log($"High-rate descent audit: grounded={highRateContact}/1000");
            Assert.GreaterOrEqual(highRateContact,950,"Short editor frames must not introduce repeated contact loss.");

            motor.Warp(new Vector3(0,height+0.15f,-5));
            for(int i=0;i<60;i++) motor.Move(Vector3.zero,dt);
            for(int i=0;i<10;i++)
            {
                motor.Move(Vector3.forward*4,0.2f);
                Assert.IsTrue(motor.IsGrounded,"A bounded long-frame substep must retain slope contact.");
            }

            // A large drop must not be converted into a snap onto the lower floor.
            var ledge = Box("Upper ledge", new Vector3(10,-0.2f,-1), new Vector3(5,0.4f,4));
            Box("Lower floor", new Vector3(10,-3.2f,3), new Vector3(6,0.4f,12));
            Physics.SyncTransforms();
            motor.Warp(new Vector3(10,0.1f,0));
            for (int i = 0; i < 60; i++) motor.Move(Vector3.zero,dt);
            bool sawAir = false, sawIntermediateHeight = false;
            for (int i = 0; i < 100; i++)
            {
                motor.Move(Vector3.forward*2,dt);
                sawAir |= !motor.IsGrounded;
                sawIntermediateHeight |= body.Position.y < -0.5f && body.Position.y > -2.5f;
            }
            Assert.IsTrue(sawAir);
            Assert.IsTrue(sawIntermediateHeight, "Gravity, not a teleport to the floor below, must cross the drop.");
            Assert.That(body.Position.y, Is.InRange(-3.05f,-2.85f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThreeAgents_PassSuccessiveEncountersNearWallsWithoutOverlap()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return VerifyThreeAgents();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyThreeAgents()
        {
            Box("Corridor floor",new Vector3(0,-0.2f,0),new Vector3(8,0.4f,24));
            Box("Left wall",new Vector3(-1.6f,1,0),new Vector3(0.2f,2,24));
            Box("Right wall",new Vector3(1.6f,1,0),new Vector3(0.2f,2,24));
            var agents = new[]{ Agent(new Vector3(-0.1f,0.05f,-4)), Agent(new Vector3(0.1f,0.05f,4)), Agent(new Vector3(-0.1f,0.05f,-6.5f)) };
            agents[1].transform.rotation=Quaternion.Euler(0,180,0);
            Vector3[] goals={new Vector3(-0.1f,0,4),new Vector3(0.1f,0,-4),new Vector3(-0.1f,0,1)};
            int arrivals=0,stuck=0;
            for(int i=0;i<agents.Length;i++)
            {
                agents[i].Arrived+=()=>arrivals++;
                agents[i].Stuck+=()=>stuck++;
                agents[i].Execute(new MoveCommand(new[]{goals[i]},MoveStyle.Walk));
            }
            float deadline=Time.realtimeSinceStartup+22, minimum=float.MaxValue;
            while(arrivals<3 && Time.realtimeSinceStartup<deadline)
            {
                for(int i=0;i<agents.Length;i++)
                {
                    Assert.Less(Mathf.Abs(agents[i].Position.x),1.24f,"Do not penetrate the corridor wall.");
                    for(int j=0;j<i;j++) minimum=Mathf.Min(minimum,PathFollower.HorizontalDistance(agents[i].Position,agents[j].Position));
                }
                yield return null;
            }
            Debug.Log($"Three-agent corridor: arrivals={arrivals}, stuck={stuck}, minimum={minimum:F3}");
            Assert.AreEqual(3,arrivals);
            Assert.AreEqual(0,stuck);
            Assert.Greater(minimum,0.52f);
            for(int i=0;i<agents.Length;i++) Assert.AreEqual(goals[i],agents[i].Waypoints[agents[i].Waypoints.Count-1]);
        }

        [UnityTest]
        public IEnumerator TightCorridor_BlocksSafelyReportsStuckOnceAndNewRouteRecovers()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return VerifyTightCorridor();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyTightCorridor()
        {
            Box("Floor",new Vector3(0,-0.2f,0),new Vector3(4,0.4f,16));
            Box("Left wall",new Vector3(-0.55f,1,0),new Vector3(0.2f,2,16));
            Box("Right wall",new Vector3(0.55f,1,0),new Vector3(0.2f,2,16));
            var a=Agent(new Vector3(0,0.05f,-2)); var b=Agent(new Vector3(0,0.05f,2));
            b.transform.rotation=Quaternion.Euler(0,180,0);
            int stuckA=0,stuckB=0,arrivals=0;
            a.Stuck+=()=>stuckA++; b.Stuck+=()=>stuckB++;
            a.Arrived+=()=>arrivals++; b.Arrived+=()=>arrivals++;
            a.Execute(new MoveCommand(new[]{new Vector3(0,0,3)},MoveStyle.Walk));
            b.Execute(new MoveCommand(new[]{new Vector3(0,0,-3)},MoveStyle.Walk));
            yield return Until(()=>stuckA>0&&stuckB>0,10);
            yield return Elapse(0.5f);
            Assert.AreEqual(1,stuckA); Assert.AreEqual(1,stuckB); Assert.AreEqual(0,arrivals);
            Assert.Less(Mathf.Abs(a.Position.x),0.2f); Assert.Less(Mathf.Abs(b.Position.x),0.2f);
            Assert.Greater(PathFollower.HorizontalDistance(a.Position,b.Position),0.52f);
            b.Stop(); a.Stop();
            Vector3 newGoal=new Vector3(0,0,-3);
            a.Execute(new MoveCommand(new[]{newGoal},MoveStyle.Walk));
            yield return Until(()=>a.HasArrived,10);
            Assert.AreEqual(1,arrivals); Assert.AreEqual(1,stuckA); Assert.AreEqual(1,stuckB);
        }

        [UnityTest]
        public IEnumerator StopAndReplacement_ClearLocalSteeringWithoutObsoleteEvents()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return VerifyInterruptions();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyInterruptions()
        {
            Box("Floor",new Vector3(0,-0.2f,0),new Vector3(14,0.4f,20));
            var a=Agent(new Vector3(0,0.05f,-3)); var b=Agent(new Vector3(0,0.05f,3));
            b.transform.rotation=Quaternion.Euler(0,180,0);
            int arrivalsA=0,stuckA=0;
            a.Arrived+=()=>arrivalsA++; a.Stuck+=()=>stuckA++;
            a.Execute(new MoveCommand(new[]{new Vector3(0,0,4)},MoveStyle.Walk));
            b.Execute(new MoveCommand(new[]{new Vector3(0,0,-4)},MoveStyle.Walk));
            yield return Until(()=>a.IsSeparating,5);
            a.Stop(); Vector3 stopped=a.Position;
            Assert.IsFalse(a.IsSeparating);
            yield return Until(()=>b.HasArrived,12);
            Assert.Less(PathFollower.HorizontalDistance(a.Position,stopped),0.005f);
            Assert.AreEqual(0,arrivalsA); Assert.AreEqual(0,stuckA); Assert.AreEqual(Vector3.zero,a.Velocity);
            Vector3 goal=a.Position+Vector3.left*2;
            a.Execute(new MoveCommand(new[]{a.Position,goal},MoveStyle.Walk));
            yield return Until(()=>a.HasArrived,10);
            Assert.AreEqual(1,arrivalsA); Assert.AreEqual(0,stuckA);
            a.Execute(default); Assert.IsFalse(a.IsMoving); Assert.IsFalse(a.HasArrived);
        }

        [UnityTest]
        public IEnumerator Separation_DoesNotChooseAnUnsupportedSidestepOffTheFloor()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return VerifyEdgeSafety();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyEdgeSafety()
        {
            Box("Narrow elevated platform",new Vector3(0,-0.2f,0),new Vector3(0.9f,0.4f,12));
            var a=Agent(new Vector3(0,0.05f,-2)); var b=Agent(new Vector3(0,0.05f,2));
            b.transform.rotation=Quaternion.Euler(0,180,0);
            int stuck=0;
            a.Stuck+=()=>stuck++; b.Stuck+=()=>stuck++;
            a.Execute(new MoveCommand(new[]{new Vector3(0,0,3)},MoveStyle.Walk));
            b.Execute(new MoveCommand(new[]{new Vector3(0,0,-3)},MoveStyle.Walk));
            yield return Until(()=>stuck>=2,10);
            Assert.Greater(a.Position.y,-0.1f); Assert.Greater(b.Position.y,-0.1f);
            Assert.Less(Mathf.Abs(a.Position.x),0.4f); Assert.Less(Mathf.Abs(b.Position.x),0.4f);
            a.Stop(); b.Stop();
        }

        private static IEnumerator Until(Func<bool> condition,float seconds)
        {
            float end=Time.realtimeSinceStartup+seconds;
            while(!condition()&&Time.realtimeSinceStartup<end) yield return null;
            Assert.IsTrue(condition(),"Timed out waiting for the ground/crowd scenario.");
        }

        private static IEnumerator Elapse(float seconds)
        {
            float end=Time.time+seconds;
            while(Time.time<end) yield return null;
        }

        [UnityTest]
        public IEnumerator HeadOnAgents_PassAndArriveWithoutOverlappingOrStuck()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return VerifyPassing();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyPassing()
        {
            Box("Crowd floor", new Vector3(0,-0.2f,0), new Vector3(12,0.4f,20));
            AgentBody a = Agent(new Vector3(0,0.05f,-4)), b = Agent(new Vector3(0,0.05f,4));
            b.transform.rotation = Quaternion.Euler(0,180,0);
            int arrivals = 0, stuck = 0;
            a.Arrived += () => arrivals++; b.Arrived += () => arrivals++;
            a.Stuck += () => stuck++; b.Stuck += () => stuck++;
            a.Execute(new MoveCommand(new[]{new Vector3(0,0,4)},MoveStyle.Walk));
            b.Execute(new MoveCommand(new[]{new Vector3(0,0,-4)},MoveStyle.Walk));
            float deadline = Time.realtimeSinceStartup+18, minimum = float.MaxValue, deviation = 0;
            while (arrivals < 2 && Time.realtimeSinceStartup < deadline)
            {
                minimum = Mathf.Min(minimum, PathFollower.HorizontalDistance(a.Position,b.Position));
                deviation = Mathf.Max(deviation, Mathf.Abs(a.Position.x), Mathf.Abs(b.Position.x));
                yield return null;
            }
            Debug.Log($"Head-on audit: arrivals={arrivals}, stuck={stuck}, min distance={minimum:F3}, max deviation={deviation:F3}");
            Assert.AreEqual(2,arrivals);
            Assert.AreEqual(0,stuck);
            Assert.Greater(minimum,0.52f,"Controller skin tolerance is not permission for substantial overlap.");
            Assert.Greater(deviation,0.25f,"Passing must be local steering, not agents passing through one another.");
            Assert.AreEqual(new Vector3(0,0,4),a.Waypoints[a.Waypoints.Count-1]);
        }

        private static AgentBody Agent(Vector3 position)
        {
            var go = new GameObject("GV ground test agent");
            go.transform.position = position;
            var body = go.AddComponent<AgentBody>();
            var cc = go.GetComponent<CharacterController>();
            cc.center=Vector3.up; cc.height=2; cc.radius=0.3f; cc.skinWidth=0.03f; cc.stepOffset=0.25f;
            Physics.SyncTransforms();
            return body;
        }

        private static GameObject Box(string name, Vector3 position, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name=name; go.transform.position=position; go.transform.localScale=size;
            Physics.SyncTransforms();
            return go;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }
    }
}
