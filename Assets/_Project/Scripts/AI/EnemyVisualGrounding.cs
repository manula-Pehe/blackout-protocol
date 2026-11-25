using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyVisualGrounding : MonoBehaviour
{
    [SerializeField] private float verticalOffset = -0.86f;

    private Vector3 originalLocalPosition;
    private bool hasOriginalPosition;
    private BlackoutProtocol.Body.AgentActionAnimation completedAnimation;

    private void OnEnable()
    {
        completedAnimation = GetComponentInParent<BlackoutProtocol.Body.AgentActionAnimation>();
        originalLocalPosition = transform.localPosition;
        hasOriginalPosition = true;
    }

    private void LateUpdate()
    {
        if (completedAnimation != null && completedAnimation.IsReady) return;
        if (!hasOriginalPosition)
        {
            originalLocalPosition = transform.localPosition;
            hasOriginalPosition = true;
        }

        Vector3 groundedPosition = originalLocalPosition;
        groundedPosition.y += verticalOffset;
        transform.localPosition = groundedPosition;
    }
}
