using System;
using BlackoutProtocol.Core;
using UnityEngine;

namespace BlackoutProtocol.Body.Testing
{
    /// <summary>Owned visual evidence only: hand-placed routes, no pathfinding or enemy brain implementation.</summary>
    public sealed class GroundingSeparationSandbox : MonoBehaviour
    {
        [SerializeField] private AgentBody first, second, slopeAgent, ledgeAgent;
        [SerializeField] private Transform leftWall, rightWall;
        [SerializeField] private Camera viewCamera;
        private AgentBody[] agents;
        private Vector3[] starts;
        private Action[] stuckHandlers;
        private int focus;
        private bool tight;
        public int Arrivals { get; private set; }
        public int StuckEvents { get; private set; }

        private void Awake()
        {
            agents = new[]{first,second,slopeAgent,ledgeAgent};
            starts = new Vector3[agents.Length];
            stuckHandlers = new Action[agents.Length];
            for(int i=0;i<agents.Length;i++)
            {
                if(agents[i]==null) { Debug.LogError("Assign all four AgentBody sandbox actors.",this); enabled=false; return; }
                starts[i]=agents[i].Position;
                AgentBody captured=agents[i];
                stuckHandlers[i]=()=> { StuckEvents++; captured.Stop(); };
            }
        }

        private void OnEnable()
        {
            if(agents==null||stuckHandlers==null) return;
            for(int i=0;i<agents.Length;i++) if(agents[i]!=null)
            { agents[i].Arrived+=OnArrival; agents[i].Stuck+=stuckHandlers[i]; }
        }

        private void OnDisable()
        {
            if(agents==null||stuckHandlers==null) return;
            for(int i=0;i<agents.Length;i++) if(agents[i]!=null)
            { agents[i].Arrived-=OnArrival; agents[i].Stuck-=stuckHandlers[i]; agents[i].Stop(); }
        }

        private void Start() => ResetAndRun();
        private void OnArrival() => Arrivals++;

        public void ResetAndRun()
        {
            if(agents==null) return;
            Arrivals=StuckEvents=0;
            for(int i=0;i<agents.Length;i++)
            {
                agents[i].Stop();
                agents[i].GetComponent<AgentMotor>().Warp(starts[i]); // Explicit sandbox reset, never gameplay steering.
                agents[i].transform.rotation=Quaternion.Euler(0,i==1?180:0,0);
            }
            Physics.SyncTransforms();
            first.Execute(new MoveCommand(new[]{new Vector3(0,0,4)},MoveStyle.Walk));
            second.Execute(new MoveCommand(new[]{new Vector3(0,0,-4)},MoveStyle.Walk));
            slopeAgent.Execute(new MoveCommand(new[]{new Vector3(-8,-3,4)},MoveStyle.Run));
            ledgeAgent.Execute(new MoveCommand(new[]{new Vector3(8,0,6)},MoveStyle.Walk));
        }

        public void TightCorridor(bool value)
        {
            tight=value;
            if(leftWall!=null) leftWall.position=new Vector3(value?-0.55f:-1.6f,1,0);
            if(rightWall!=null) rightWall.position=new Vector3(value?0.55f:1.6f,1,0);
            ResetAndRun();
        }

        public void Focus(int lane) => focus=Mathf.Clamp(lane,0,2);

        private void LateUpdate()
        {
            if(viewCamera==null||agents==null) return;
            AgentBody target=focus==0?first:focus==1?slopeAgent:ledgeAgent;
            if(target==null) return;
            viewCamera.transform.position=target.Position+new Vector3(5,8,-7);
            viewCamera.transform.LookAt(target.Position+Vector3.up);
        }

        private void OnGUI()
        {
            if(agents==null) return;
            GUILayout.BeginArea(new Rect(15,15,640,180),GUI.skin.box);
            GUILayout.Label("GV ground contact / local separation — hand-placed routes, no AI brain changes");
            GUILayout.Label($"Arrivals {Arrivals} | Stuck {StuckEvents} | separation {first.IsSeparating}/{second.IsSeparating} | slope snaps {slopeAgent.GetComponent<AgentMotor>().GroundSnapCount}");
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Passing pair")) Focus(0);
            if(GUILayout.Button("35° descent")) Focus(1);
            if(GUILayout.Button("Ledge / gravity")) Focus(2);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Reset and run")) ResetAndRun();
            if(GUILayout.Button(tight?"Use wide corridor":"Use tight corridor")) TightCorridor(!tight);
            if(GUILayout.Button("Stop all")) foreach(var agent in agents) agent.Stop();
            GUILayout.EndHorizontal();
            GUILayout.Label(tight?"Tight lane intentionally cannot fit both capsules: Stuck → sandbox Stop is expected.":"Pair passes locally and arrives; slope actor stays grounded; ledge actor falls to lower floor.");
            GUILayout.EndArea();
        }
    }
}
