using BlackoutProtocol.Core;
using UnityEngine;

public class ShootingTestTarget : MonoBehaviour, IDamageable
{
    [SerializeField] private float health = 100f;

    public bool IsDead => health <= 0f;

    public void TakeDamage(float amount, Vector3 hitPoint, GameObject source)
    {
        health -= amount;
        Debug.Log($"{name}: took {amount} damage; health = {health}");
    }
}