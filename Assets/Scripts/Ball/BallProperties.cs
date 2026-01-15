using Unity.Netcode;
using UnityEngine;

public class BallProperties : NetworkBehaviour
{
    [SerializeField] private int maxDamage = 20;
    [SerializeField] private float maxKnockback = 15f;

    private float currentPower = 0f;
    private ulong lastOwnerId;
    private bool isHeld = false;

    public void SetHeldState(bool held, ulong ownerId)
    {
        isHeld = held;
        lastOwnerId = ownerId;
    }

    public void SetThrown(float power)
    {
        isHeld = false;
        currentPower = power;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer || isHeld) return;

        if (collision.gameObject.TryGetComponent(out PlayerController targetPlayer))
        {
            // Don't hit the person who just threw it immediately
            if (targetPlayer.OwnerClientId == lastOwnerId) return;

            // Calculate relative damage/knockback
            int damage = Mathf.RoundToInt(maxDamage * currentPower);
            Vector3 knockback = (collision.transform.position - transform.position).normalized * (maxKnockback * currentPower);

            targetPlayer.TakeDamage(damage, knockback);

            // Reset power after hit to prevent double-counting
            currentPower = 0;
        }
    }
}