using UnityEngine;

/// <summary>
/// 파멸 속성 원거리 투사체.
/// - PlayerRangedAttack2D가 Instantiate 후 Initialize()를 호출해 방향·데미지·레이어를 주입합니다.
/// - Rigidbody2D가 붙어 있으면 velocity로 이동, 없으면 Transform.Translate 폴백.
/// - 적 레이어 충돌 → IDamageable.TakeDamage → Destroy
/// - lifetime 초과 시 자동 Destroy
///
/// [확장 포인트]
///   속성별 이펙트: HandleCollision 하단 TODO 주석 참조
///   새 속성 추가:  ProjectileAttribute 열거형에 항목 추가
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DoomProjectile2D : MonoBehaviour
{
    // ─────────────────────────────────────────────
    // 속성 열거형 — 나중에 Fire, Ice 등 추가 가능
    // ─────────────────────────────────────────────
    public enum ProjectileAttribute { None, Doom }

    // ─────────────────────────────────────────────
    // Inspector 필드 (프리팹 기본값)
    // ─────────────────────────────────────────────
    [Header("Movement")]
    [Tooltip("투사체 이동 속도 (유닛/초)")]
    [SerializeField] private float speed = 10f;

    [Tooltip("이 시간(초) 이후 자동 소멸")]
    [SerializeField] private float lifetime = 3f;

    [Header("Damage")]
    [Tooltip("기본 데미지 (Initialize()로 덮어쓸 수 있음)")]
    [SerializeField] private int damage = 20;

    [Tooltip("데미지를 줄 대상 레이어 (Initialize()로 덮어쓸 수 있음)")]
    [SerializeField] private LayerMask targetLayer;

    [Header("Attribute")]
    [Tooltip("이 투사체의 속성 — 이펙트 분기 등에 활용")]
    [SerializeField] private ProjectileAttribute attribute = ProjectileAttribute.Doom;

    // ─────────────────────────────────────────────
    // 런타임 상태
    // ─────────────────────────────────────────────
    private Vector2 _direction;
    private Rigidbody2D _rb;
    private bool _hasHit; // 중복 충돌 방지 플래그

    // ─────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────
    private void Awake()
    {
        // Rigidbody2D는 선택 사항 — 없어도 Transform 이동으로 폴백
        _rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        // Rigidbody2D 없이 Transform으로만 이동하는 폴백 경로
        if (_rb == null)
        {
            transform.Translate(_direction * speed * Time.deltaTime, Space.World);
        }
    }

    // ─────────────────────────────────────────────
    // 공개 초기화 메서드
    // ─────────────────────────────────────────────
    /// <summary>
    /// PlayerRangedAttack2D가 생성 직후 호출합니다.
    /// 프리팹 기본값을 런타임 값으로 덮어씁니다.
    /// </summary>
    public void Initialize(Vector2 direction, int dmg, LayerMask layer)
    {
        _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        damage = dmg;
        targetLayer = layer;
        _hasHit = false;

        // Rigidbody2D가 있으면 velocity로 이동 (물리 충돌 연동)
        if (_rb != null)
        {
            _rb.linearVelocity = _direction * speed;
        }

        // 수명 타이머 — 적 못 만나도 자동 제거
        Destroy(gameObject, lifetime);
    }

    // ─────────────────────────────────────────────
    // 충돌 처리 (Trigger / Collision 모두 지원)
    // ─────────────────────────────────────────────
    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleCollision(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleCollision(collision.gameObject);
    }

    private void HandleCollision(GameObject target)
    {
        // 이미 처리된 충돌이면 무시 (관통 방지)
        if (_hasHit) return;

        // targetLayer에 해당하지 않는 오브젝트면 무시
        if ((targetLayer.value & (1 << target.layer)) == 0) return;

        // IDamageable 컴포넌트 탐색 (부모 포함)
        IDamageable damageable = target.GetComponentInParent<IDamageable>();
        if (damageable == null) return;

        // 데미지 적용
        _hasHit = true;
        damageable.TakeDamage(damage);

        // TODO: 속성별 이펙트 재생
        // 예: if (attribute == ProjectileAttribute.Doom) DoomEffectPool.Spawn(transform.position);

        Destroy(gameObject);
    }

    // ─────────────────────────────────────────────
    // 에디터 시각화
    // ─────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0f, 1f); // 보라색 = 파멸 속성
        Gizmos.DrawWireSphere(transform.position, 0.12f);
    }
}
