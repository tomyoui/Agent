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
        Debug.Log($"[Resolver] range={range} mask={targetLayer.value}");
        float safeRange = Mathf.Max(0f, range);
        
        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = targetLayer;
        filter.useTriggers = true; // 무적 버그 방지: 설정과 무관하게 트리거 히트박스 강제 감지

        Collider2D[] hits = new Collider2D[32];
        int hitCount = Physics2D.OverlapCircle(origin, safeRange, filter, hits);
        
        HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();

        int damagedCount = 0;

        for (int i = 0; i < hitCount; i++)
        {
            IDamageable damageable = hits[i].GetComponentInParent<IDamageable>();
            
            // 만약 콜라이더의 부모 계층에 없다면, 프리팹 구조가 달라 형제 노드 등에 있을 수 있으므로 루트 기준 하위 전체 탐색
            if (damageable == null && hits[i].transform.root != null)
            {
                damageable = hits[i].transform.root.GetComponentInChildren<IDamageable>();
            }

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

        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = targetLayer;
        filter.useTriggers = true; // 강제 트리거 감지

        Collider2D[] hits = new Collider2D[32];
        int hitCount = Physics2D.OverlapCircle(origin, coneRange, filter, hits);
        
        HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();

        int damagedCount = 0;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            
            if (damageable == null && hit.transform.root != null)
            {
                damageable = hit.transform.root.GetComponentInChildren<IDamageable>();
            }
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
