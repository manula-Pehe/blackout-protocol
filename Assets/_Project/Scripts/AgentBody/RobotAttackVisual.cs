using System.Collections.Generic;
using BlackoutProtocol.Core;
using UnityEngine;

namespace BlackoutProtocol.Body
{
    /// <summary>Short visible windup and lunge for ground robot attack actions.</summary>
    [RequireComponent(typeof(AgentBody))]
    [DisallowMultipleComponent]
    public sealed class RobotAttackVisual : MonoBehaviour
    {
        private struct Part
        {
            public Transform Transform;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        private readonly List<Part> parts = new List<Part>();
        private AgentBody body;
        private float attackStarted = float.NegativeInfinity;
        private bool attacking;
        private const float Duration = 0.35f;

        private void Awake()
        {
            body = GetComponent<AgentBody>();
            foreach (Transform child in transform)
            {
                if (!child.gameObject.activeInHierarchy) continue;
                bool hasModel = false;
                foreach (Renderer renderer in child.GetComponentsInChildren<Renderer>(true))
                    if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                    {
                        hasModel = true;
                        break;
                    }
                if (hasModel)
                    parts.Add(new Part { Transform = child, Position = child.localPosition, Rotation = child.localRotation });
            }
        }

        private void OnEnable()
        {
            if (body != null) body.ActionRequested += OnAction;
        }

        private void OnDisable()
        {
            if (body != null) body.ActionRequested -= OnAction;
            Restore();
        }

        private void OnAction(AgentAction action)
        {
            if (action == AgentAction.Attack) { attackStarted = Time.time; attacking = true; }
            else if (action == AgentAction.Die) { attacking = false; Restore(); }
        }

        private void LateUpdate()
        {
            if (!attacking) return;
            if (Time.time - attackStarted >= Duration) { attacking = false; Restore(); return; }
            float phase = Mathf.Clamp01((Time.time - attackStarted) / Duration);
            float extension = Mathf.Sin(phase * Mathf.PI);
            float windup = Mathf.Sin(phase * Mathf.PI * 2f);
            for (int i = 0; i < parts.Count; i++)
            {
                Part part = parts[i];
                if (part.Transform == null) continue;
                part.Transform.localPosition = part.Position + new Vector3(0, 0.06f * extension, 0.28f * extension);
                part.Transform.localRotation = part.Rotation * Quaternion.Euler(-16f * windup, 0, 0);
            }
        }

        private void Restore()
        {
            for (int i = 0; i < parts.Count; i++)
            {
                Part part = parts[i];
                if (part.Transform == null) continue;
                part.Transform.localPosition = part.Position;
                part.Transform.localRotation = part.Rotation;
            }
        }
    }
}
