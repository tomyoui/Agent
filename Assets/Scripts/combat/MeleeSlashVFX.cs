using UnityEngine;

// 근접 공격 슬래시 VFX를 AttackPoint 위치에 스폰하는 컴포넌트.
//
// [연결 방법]
//   1. AttackPoint GameObject에 이 컴포넌트를 추가.
//   2. Inspector에서 slashPrefab 할당.
//   3. Character combat onComboAttack  → MeleeSlashVFX.PlayComboSlash (int 파라미터)
//   4. Character combat onHeavyAttack  → MeleeSlashVFX.PlayHeavySlash
//
// [VFX 방향]
//   AttackPoint의 rotation이 조준 방향으로 매 프레임 갱신되므로
//   (character combat UpdateAttackPointFromMouse 참조)
//   Instantiate 시 transform.rotation을 그대로 사용하면 슬래시가 공격 방향을 가리킴.
[DisallowMultipleComponent]
public class MeleeSlashVFX : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("콤보 슬래시 프리팹.\n" +
             "미할당 시 모든 슬래시 스킵.")]
    [SerializeField] private GameObject slashPrefab;

    [Tooltip("강공격 전용 슬래시 프리팹.\n" +
             "미할당 시 slashPrefab을 대신 사용.")]
    [SerializeField] private GameObject heavySlashPrefab;

    [Header("Lifetime")]
    [Tooltip("스폰 후 자동 제거까지의 시간 (초). 권장: 0.08~0.15")]
    [SerializeField] private float vfxLifetime = 0.12f;

    [Header("Scale — 콤보 단계별")]
    [Tooltip("콤보 1타 크기 배율. 가볍고 작게.")]
    [SerializeField] private float combo1Scale = 0.7f;
    [Tooltip("콤보 2타 크기 배율.")]
    [SerializeField] private float combo2Scale = 1.0f;
    [Tooltip("콤보 3타 크기 배율. 마무리타 — 크게.")]
    [SerializeField] private float combo3Scale = 1.3f;
    [Tooltip("강공격 크기 배율.")]
    [SerializeField] private float heavyScale = 1.8f;

    // Character combat onComboAttack (UnityEvent<int>) 에 Inspector에서 연결.
    // comboStep: 1=1타, 2=2타, 3=3타
    public void PlayComboSlash(int comboStep)
    {
        SpawnSlash(slashPrefab, GetComboScale(comboStep));
    }

    // Character combat onHeavyAttack (UnityEvent) 에 Inspector에서 연결.
    public void PlayHeavySlash()
    {
        GameObject prefab = heavySlashPrefab != null ? heavySlashPrefab : slashPrefab;
        SpawnSlash(prefab, heavyScale);
    }

    private void SpawnSlash(GameObject prefab, float scale)
    {
        if (prefab == null)
        {
            return;
        }

        // AttackPoint의 위치(= 공격 기준점)와 rotation(= 조준 방향)으로 스폰
        GameObject fx = Instantiate(prefab, transform.position, transform.rotation);
        fx.transform.localScale = Vector3.one * scale;
        Destroy(fx, vfxLifetime);
    }

    private float GetComboScale(int comboStep)
    {
        switch (comboStep)
        {
            case 1:  return combo1Scale;
            case 2:  return combo2Scale;
            default: return combo3Scale;
        }
    }
}
