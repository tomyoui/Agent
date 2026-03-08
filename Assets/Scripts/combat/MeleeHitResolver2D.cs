using System.Collections.Generic;
using UnityEngine;

public static class MeleeHitResolver2D
{
    // Shared helper so Player and Enemy can use the same broad-phase + damage pipeline.
    public static int DealDamageInRange(
        Vector2 origin,
        float range,
        int damage,
        LayerMask targetLayer,
        Object debugContext = null,
        string debugLabel = "Melee")
    {
        float safeRange = Mathf.Max(0f, range);
        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, safeRange, targetLayer);
        HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();

        int damagedCount = 0;

        for (int i = 0; i < hits.Length; i++)
        {
            IDamageable damageable = hits[i].GetComponentInParent<IDamageable>();
            if (damageable == null || !damagedTargets.Add(damageable))
            {
                continue;
            }

            damageable.TakeDamage(damage);
            damagedCount++;
        }

        if (debugContext != null)
        {
            Debug.Log($"[{debugLabel}] Damaged {damagedCount} target(s) in range.", debugContext);
        }

        return damagedCount;
    }

    // Uses circle overlap as broad-phase, then applies cone filtering for directional melee attacks.
    public static int DealDamageInCone(
        Vector2 origin,
        Vector2 forwardDirection,
        float attackPointDistance,
        float attackRange,
        float attackAngle,
        int damage,
        LayerMask targetLayer,
        Object debugContext = null,
        string debugLabel = "MeleeCone")
    {
        Vector2 safeForward = forwardDirection.sqrMagnitude > 0.0001f
            ? forwardDirection.normalized
            : Vector2.right;

        float coneRange = GetConeRange(attackPointDistance, attackRange);
        float halfAngle = Mathf.Clamp(attackAngle * 0.5f, 0f, 180f);
        float minDot = Mathf.Cos(Mathf.Deg2Rad * halfAngle);

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, coneRange, targetLayer);
        HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();

        int damagedCount = 0;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null || !damagedTargets.Add(damageable))
            {
                continue;
            }

            Vector2 targetPoint = hit.ClosestPoint(origin);
            Vector2 toTarget = targetPoint - origin;

            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                toTarget = (Vector2)hit.transform.position - origin;
            }

            if (toTarget.sqrMagnitude > coneRange * coneRange)
            {
                continue;
            }

            float dot = Vector2.Dot(safeForward, toTarget.normalized);
            if (dot < minDot)
            {
                continue;
            }

            damageable.TakeDamage(damage);
            damagedCount++;
        }

        if (debugContext != null)
        {
            Debug.Log($"[{debugLabel}] Damaged {damagedCount} target(s) in cone.", debugContext);
        }

        return damagedCount;
    }

    public static float GetConeRange(float attackPointDistance, float attackRange)
    {
        return Mathf.Max(0f, attackPointDistance + attackRange);
    }
}
