using System.Collections.Generic;
using UnityEngine;

// 근접 공격의 원형 범위 및 콘 판정을 처리하는 정적 유틸리티 클래스
public static class MeleeHitResolver2D
{
    // 플레이어·적이 공통으로 사용하는 브로드페이즈 + 데미지 파이프라인 헬퍼
    public static int DealDamageInRange(
        Vector2 origin,
        float range,
        int damage,
        LayerMask targetLayer,
        Object debugContext = null,
        string debugLabel = "Melee",
        CombatAttribute attribute = CombatAttribute.Justice)
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

            damageable.TakeDamage(damage, attribute);
            damagedCount++;
        }

        if (debugContext != null)
        {
            Debug.Log($"[{debugLabel}] 범위 내 {damagedCount}개 대상에 데미지 적용.", debugContext);
        }

        return damagedCount;
    }

    // 원형 브로드페이즈 후 콘 필터링을 적용하는 방향성 근접 공격 처리
    public static int DealDamageInCone(
        Vector2 origin,
        Vector2 forwardDirection,
        float attackPointDistance,
        float attackRange,
        float attackAngle,
        int damage,
        LayerMask targetLayer,
        Object debugContext = null,
        string debugLabel = "MeleeCone",
        CombatAttribute attribute = CombatAttribute.Justice)
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

            damageable.TakeDamage(damage, attribute);
            damagedCount++;
        }

        if (debugContext != null)
        {
            Debug.Log($"[{debugLabel}] 콘 내 {damagedCount}개 대상에 데미지 적용.", debugContext);
        }

        return damagedCount;
    }

    public static float GetConeRange(float attackPointDistance, float attackRange)
    {
        return Mathf.Max(0f, attackPointDistance + attackRange);
    }
}
