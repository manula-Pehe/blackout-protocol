using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BlackoutProtocol.Core;
using UnityEngine;
using UnityEngine.AI;

namespace BlackoutProtocol.Body.Testing
{
    /// <summary>Opt-in, Play Mode only movement/rendering workload in the actual Main scene.
    /// Original four enemies remain active. Eight additional ground bodies use safe short
    /// NavMesh chords, not additional AI brains. Nothing is persisted in the scene.</summary>
    public sealed class MainAgentStressProbe : MonoBehaviour
    {
        private const float Warmup = 5f, Sample = 15f;
        private readonly List<MonoBehaviour> originals = new List<MonoBehaviour>();
        private readonly List<AgentBody> extras = new List<AgentBody>();
        private readonly List<Vector3> starts = new List<Vector3>();
        private readonly Vector3[] directions = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
        private GameObject group;
        private AgentBody template;
        private float phaseStart, nextRoutes;
        private int phase, frames, frames4, frames12;
        private double duration4, duration12;
        private double elapsed;
        private long movingSum;
        private double moving4, moving12;
        private bool finished;
        private string status = "Preparing";
        private static bool Live(MonoBehaviour b)
        {
            if (b == null || !b.isActiveAndEnabled) return false;
            if (b is IDamageable d && d.IsDead) return false;
            foreach (var component in b.GetComponents<MonoBehaviour>())
                if (component is IDamageable damageable && damageable.IsDead) return false;
            return true;
        }
        private void Start()
        {
            foreach (var b in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (!(b is IAgentBody) || !Live(b)) continue;
                originals.Add(b);
                if (b is AgentBody ground && ground.GetComponentInChildren<AgentActionAnimation>() != null)
                    template = ground;
            }
            if (originals.Count != 4 || template == null) { Abort("Expected exactly four live agent bodies, including an animated ground body."); return; }
            phaseStart = Time.realtimeSinceStartup;
            status = "4 agents: warmup";
        }
        private bool AddExtras()
        {
            group = new GameObject("TEMP - stress agents (not saved)");
            group.SetActive(false);
            var mesh = NavMesh.CalculateTriangulation();
            var cc = template.GetComponent<CharacterController>();
            if (cc == null) return false;
            float lift = cc.height * 0.5f - cc.center.y + cc.skinWidth;
            Vector3 origin = template.Position;
            // Bounded selection of non-overlapping walkable sites near the existing robot.
            for (int k = 0; k + 2 < mesh.indices.Length && extras.Count < 8; k += 3)
            {
                Vector3 p = (mesh.vertices[mesh.indices[k]] + mesh.vertices[mesh.indices[k+1]] + mesh.vertices[mesh.indices[k+2]]) / 3f;
                if ((p-origin).sqrMagnitude > 144f || (p-origin).sqrMagnitude < 4f) continue;
                bool close = false;
                foreach (var b in originals) if ((b.transform.position-p).sqrMagnitude < 4f) { close = true; break; }
                foreach (var s in starts) if ((s-p).sqrMagnitude < 4f) { close = true; break; }
                if (close || Physics.CheckCapsule(p + Vector3.up*0.5f, p + Vector3.up*1.6f, 0.38f, ~0, QueryTriggerInteraction.Ignore)) continue;
                // Require room for an actual movement command, not an idle clone.
                if (!TryGoal(p, extras.Count, out _)) continue;
                var clone = Instantiate(template.gameObject, p + Vector3.up*lift, template.transform.rotation, group.transform);
                clone.name = "Stress ground body " + (extras.Count+1);
                foreach (var b in clone.GetComponentsInChildren<MonoBehaviour>(true))
                    if (!(b is AgentBody || b is AgentMotor || b is AgentRotation || b is AgentActionAnimation || b is AgentAnimatorDriver)) b.enabled = false;
                foreach (var r in clone.GetComponentsInChildren<LineRenderer>(true)) r.enabled = false;
                foreach (var audio in clone.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
                extras.Add(clone.GetComponent<AgentBody>()); starts.Add(p);
            }
            if (extras.Count != 8) return false;
            group.SetActive(true);
            return true;
        }
        private bool TryGoal(Vector3 p, int offset, out Vector3 goal)
        {
            for (int i=0;i<directions.Length;i++)
            {
                Vector3 desired = p + directions[(i+offset)%directions.Length]*2f;
                if (NavMesh.SamplePosition(desired,out NavMeshHit h,0.2f,NavMesh.AllAreas) &&
                    !NavMesh.Raycast(p,h.position,out _,NavMesh.AllAreas)) { goal=h.position; return true; }
            }
            goal=p; return false;
        }
        private void DriveExtras()
        {
            if (Time.realtimeSinceStartup < nextRoutes) return;
            nextRoutes = Time.realtimeSinceStartup + 0.5f;
            for(int i=0;i<extras.Count;i++)
            {
                var b=extras[i];
                if (b.IsMoving) continue;
                if (!NavMesh.SamplePosition(b.Position,out NavMeshHit hit,2f,NavMesh.AllAreas)) continue;
                Vector3 goal;
                if ((hit.position-starts[i]).sqrMagnitude > 0.7f && !NavMesh.Raycast(hit.position,starts[i],out _,NavMesh.AllAreas)) goal=starts[i];
                else if (!TryGoal(hit.position,i,out goal)) continue;
                b.Execute(new MoveCommand(new[]{hit.position,goal},MoveStyle.Walk));
            }
        }
        private void Update()
        {
            if (finished) return;
            float now=Time.realtimeSinceStartup;
            if (Time.timeScale <= 0f) { Abort("Time scale paused; restart benchmark."); return; }
            // Count only lightweight movement state each sampled frame; health check at boundaries.
            if (phase >= 2) DriveExtras();
            if (phase == 0 || phase == 2)
            {
                if (now-phaseStart < Warmup) return;
                phase++; phaseStart=now; frames=0; elapsed=0; movingSum=0;
                status = phase==1 ? "4 agents: recording" : "12 agents: recording";
                return;
            }
            frames++; elapsed+=Time.unscaledDeltaTime;
            foreach (var b in originals) if (b != null && ((IAgentBody)b).IsMoving) movingSum++;
            foreach (var b in extras) if (b != null && b.IsMoving) movingSum++;
            if (now-phaseStart < Sample) return;
            foreach (var b in originals) if (!Live(b)) { Abort("An original enemy died or was disabled during measurement."); return; }
            if (phase==1)
            {
                frames4=frames; duration4=elapsed; moving4=(double)movingSum/frames;
                if (!AddExtras()) { Abort("Could not find eight safe nearby walkable spawn sites; start in another Main area."); return; }
                phase=2; phaseStart=now; status="12 agents: warmup";
                return;
            }
            foreach(var b in extras) if (b==null || !b.isActiveAndEnabled) { Abort("An extra agent was disabled."); return; }
            frames12=frames; duration12=elapsed; moving12=(double)movingSum/frames;
            try
            {
                string dir=Path.Combine(Application.dataPath,"../docs/evidence/agent-controller"); Directory.CreateDirectory(dir);
                string file=Path.Combine(dir,"main-stress-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".csv");
                string Row(int count,int n,double seconds,double moving) => string.Format(CultureInfo.InvariantCulture,"{0},{1},{2:F4},{3:F2},{4:F2},{5:F2}\n",count,n,seconds,n/seconds,1000*seconds/n,moving);
                File.WriteAllText(file,"agent_count,frames,seconds,mean_fps,mean_frame_ms,mean_moving_agents\n"+Row(4,frames4,duration4,moving4)+Row(12,frames12,duration12,moving12));
                Debug.Log("Agent stress evidence: "+file+". 4 originals vs 4 originals + 8 movement-only ground bodies.");
                status="Completed. CSV saved; see Console."; finished=true;
                if(group!=null) Destroy(group);
            }
            catch(Exception ex) { Abort(ex.Message); }
        }
        private void Abort(string reason) { finished=true; status="Aborted: "+reason; Debug.LogWarning(status); if(group!=null) Destroy(group); }
        private void OnDestroy() { if(group!=null) Destroy(group); }
        private void OnGUI() { GUI.Label(new Rect(12,Screen.height-32,Screen.width-24,25),"Agent benchmark: "+status); }
    }
}
