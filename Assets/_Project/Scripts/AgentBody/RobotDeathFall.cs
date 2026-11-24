using System.Collections.Generic;
using BlackoutProtocol.Core;
using UnityEngine;

namespace BlackoutProtocol.Body
{
    /// <summary>Collapses a defeated ground robot and clears its standing collision.</summary>
    [RequireComponent(typeof(AgentBody))]
    [DisallowMultipleComponent]
    public sealed class RobotDeathFall : MonoBehaviour
    {
        private struct Part
        {
            public Transform Transform;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        private readonly List<Part> parts = new List<Part>();
        private AgentBody body;
        private AgentActionAnimation humanoid;
        private CharacterController controller;
        private float deathStarted = -1f;
        private bool finished;

        private void Awake()
        {
            body = GetComponent<AgentBody>();
            humanoid = GetComponentInChildren<AgentActionAnimation>(true);
            controller = GetComponent<CharacterController>();
            foreach (Transform child in transform)
            {
                bool model = false;
                foreach (Renderer renderer in child.GetComponentsInChildren<Renderer>(true))
                    if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                    {
                        model = true;
                        break;
                    }
                if (model) parts.Add(new Part
                    { Transform = child, Position = child.localPosition, Rotation = child.localRotation });
            }
        }

        private void OnEnable()
        {
            if (body != null) body.ActionRequested += OnAction;
        }

        private void OnDisable()
        {
            if (body != null) body.ActionRequested -= OnAction;
        }

        private void OnAction(AgentAction action)
        {
            if (humanoid != null && humanoid.IsReady) return;
            if (action != AgentAction.Die || deathStarted >= 0f) return;
            deathStarted = Time.time;
            body.Stop();
        }

        private void LateUpdate()
        {
            if (deathStarted < 0f || finished) return;
            float progress = Mathf.Clamp01((Time.time - deathStarted) / 0.75f);
            float eased = progress * progress * (3f - 2f * progress);
            for (int i = 0; i < parts.Count; i++)
            {
                Part part = parts[i];
                if (part.Transform == null) continue;
                part.Transform.localRotation = part.Rotation * Quaternion.Euler(0, 0, 85f * eased);
                part.Transform.localPosition = part.Position + Vector3.down * (0.6f * eased);
            }
            if (progress < 1f) return;
            finished = true;
            if (body != null) body.enabled = false;
            if (controller != null) controller.enabled = false;
        }
    }
}
