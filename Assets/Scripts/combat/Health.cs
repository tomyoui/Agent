using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class Health : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [Tooltip("최대 HP")]
    [SerializeField] private int maxHP = 30;
    [Tooltip("현재 HP")]
    [SerializeField] private int currentHP = 30;

    [Header("Hit Feedback")]
    [Tooltip("피격 플래시 지속 시간")]
    [SerializeField] private float hitFlashDuration = 0.08f;
    [Tooltip("피격 플래시 색상")]
    [SerializeField] private Color hitFlashColor = Color.red;

    [Header("Damage Number")]
    [Tooltip("데미지 숫자 프리팹. 미할당 시 경고 로그만 출력하고 다른 피드백은 정상 동작.")]
    [SerializeField] private DamageNumber damageNumberPrefab;

    [Header("Death Feedback")]
    [Tooltip("사망 연출 총 지속 시간 (초). 이 시간 동안 팝 → 페이드아웃 후 오브젝트 제거.")]
    [SerializeField] private float deathDuration = 0.3f;
    [Tooltip("사망 시 순간 팝 스케일 배수. 1.3이면 30% 커졌다가 원래 크기로 줄어들며 페이드아웃.")]
    [SerializeField, Range(1f, 2f)] private float deathScaleMultiplier = 1.3f;
    [Tooltip("사망 시 스폰할 VFX 프리팹. 미할당 시 조용히 스킵.")]
    [SerializeField] private GameObject deathVfxPrefab;
    [Tooltip("사망 VFX 자동 제거까지의 시간 (초)")]
    [SerializeField] private float deathVfxLifetime = 0.5f;
    [Tooltip("사망 사운드 재생용 AudioSource. 미할당 시 자신에서 탐색.")]
    [SerializeField] private AudioSource deathAudioSource;
    [Tooltip("사망 시 재생할 사운드 클립.\n" +
             "미할당 시 프로토타입용 crack 사운드가 코드로 자동 생성되어 재생됨.\n" +
             "실제 사운드 준비 후 이 슬롯에 드래그해 교체 가능.")]
    [SerializeField] private AudioClip deathSfx;
    [SerializeField, Range(0f, 1f)] private float deathSfxVolume = 1f;

    // deathSfx가 없을 때 재생할 프로토타입 fallback 클립.
    // static: 씬 내 모든 Health 인스턴스가 공유 — 최초 1회만 생성.
    private static AudioClip _fallbackDeathSfx;

    public int MaxHP => maxHP;
    public int CurrentHP => currentHP;
    public bool IsDead => _isDead;

    private SpriteRenderer _spriteRenderer;
    private Color _originalColor = Color.white;
    private Vector3 _originalScale;
    private Coroutine _hitFlashRoutine;

    // 중복 사망 처리 방지 플래그
    private bool _isDead;

    private void Awake()
    {
        if (maxHP < 1) maxHP = 1;
        if (currentHP <= 0 || currentHP > maxHP) currentHP = maxHP;

        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null)
        {
            _originalColor = _spriteRenderer.color;
        }

        // 사망 팝 연출을 위해 원래 스케일 캐싱
        _originalScale = transform.localScale;

        if (deathAudioSource == null)
        {
            deathAudioSource = GetComponent<AudioSource>();
        }

        // deathSfx 미설정 시 fallback 클립을 최초 1회 생성.
        // static이므로 이미 생성된 경우 재사용.
        if (deathSfx == null && _fallbackDeathSfx == null)
        {
            _fallbackDeathSfx = CreateFallbackDeathSfx();
        }
    }

    public void TakeDamage(int damage, CombatAttribute attribute = CombatAttribute.Justice)
    {
        // 이미 사망 처리 중이면 추가 데미지 무시
        if (_isDead) return;

        if (damage <= 0)
        {
            Debug.Log($"[Health] {gameObject.name} 유효하지 않은 데미지 무시: {damage}", this);
            return;
        }

        currentHP = Mathf.Max(0, currentHP - damage);

        // 속성별 반응 분기.
        // 현재는 로그만 출력. 이후 이펙트/사운드/상성 효과 확장 시 이 switch에 추가.
        switch (attribute)
        {
            case CombatAttribute.Justice:
                Debug.Log($"[Health] {gameObject.name} [정의] {damage} 데미지. HP: {currentHP}/{maxHP}", this);
                break;
            case CombatAttribute.Doom:
                Debug.Log($"[Health] {gameObject.name} [파멸] {damage} 데미지. HP: {currentHP}/{maxHP}", this);
                break;
            default:
                Debug.Log($"[Health] {gameObject.name} [{attribute}] {damage} 데미지. HP: {currentHP}/{maxHP}", this);
                break;
        }

        // 데미지 숫자 표시: 피격 대상 머리 위 월드 위치에 스폰
        DamageNumber.Spawn(damageNumberPrefab, transform.position, damage);

        TriggerHitFlash();

        if (currentHP <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        // 중복 호출 방지 — TakeDamage에서 체크하지만 외부 호출 대비 이중 방어
        if (_isDead) return;
        _isDead = true;

        Debug.Log($"[Health] Die() called for {gameObject.name}. root={transform.root.name}", this);

        // 진행 중인 피격 플래시 즉시 중단 — 사망 연출 중 색상 충돌 방지
        if (_hitFlashRoutine != null)
        {
            StopCoroutine(_hitFlashRoutine);
            _hitFlashRoutine = null;
            if (_spriteRenderer != null) _spriteRenderer.color = _originalColor;
        }

        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        // ── 1. VFX 스폰 ─────────────────────────────────────────────────────
        // 위치 정보가 유효한 시점에 미리 생성. 미할당 시 조용히 스킵.
        if (deathVfxPrefab != null)
        {
            GameObject vfx = Instantiate(deathVfxPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, deathVfxLifetime);
        }

        // ── 2. SFX 재생 ──────────────────────────────────────────────────────
        // PlayClipAtPoint: 오브젝트 파괴 후에도 월드 공간의 임시 AudioSource에서 끝까지 재생됨.
        AudioClip clipToPlay = deathSfx != null ? deathSfx : _fallbackDeathSfx;
        if (clipToPlay != null)
        {
            AudioSource.PlayClipAtPoint(clipToPlay, transform.position, deathSfxVolume);
        }

        // ── 3. 스케일 팝 + 알파 페이드아웃 ──────────────────────────────────
        // 순간 팝: 스케일을 deathScaleMultiplier 배로 즉시 키움
        transform.localScale = _originalScale * deathScaleMultiplier;

        float elapsed = 0f;
        while (elapsed < deathDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / deathDuration);

            // 알파: 1 → 0 선형 감소 (빠른 소멸감)
            if (_spriteRenderer != null)
            {
                Color c = _originalColor;
                c.a = 1f - t;
                _spriteRenderer.color = c;
            }

            // 스케일: 팝 크기 → 원래 크기로 선형 수렴 (커졌다가 돌아오며 사라지는 느낌)
            transform.localScale = Vector3.Lerp(
                _originalScale * deathScaleMultiplier,
                _originalScale,
                t
            );

            yield return null;
        }

        // ── 4. 오브젝트 비활성화 + 파티 매니저에 사망 알림 ──────────────────
        GameObject deathOwner = transform.root != null ? transform.root.gameObject : gameObject;
        Debug.Log($"[Health] DeathRoutine completed for {gameObject.name}. Notify PartyManager with owner={deathOwner.name}", this);
        PartyManager2D.Instance?.OnMemberDied(deathOwner);
        gameObject.SetActive(false);
    }

    private void TriggerHitFlash()
    {
        if (_spriteRenderer == null) return;

        if (_hitFlashRoutine != null)
        {
            StopCoroutine(_hitFlashRoutine);
            _spriteRenderer.color = _originalColor;
        }

        _hitFlashRoutine = StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        _spriteRenderer.color = hitFlashColor;
        yield return new WaitForSeconds(hitFlashDuration);

        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = _originalColor;
        }

        _hitFlashRoutine = null;
    }

    private void OnDisable()
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = _originalColor;
        }
    }

    // ─────────────────────────────────────────────
    // 프로토타입 fallback 사운드 생성
    // deathSfx가 없을 때만 호출. static 필드에 저장해 1회만 생성.
    // ─────────────────────────────────────────────

    /// <summary>
    /// 짧은 crack/pop 계열 사운드를 PCM 데이터로 직접 생성.
    /// 백색소음(70%) + 사인파(30%)를 급격히 감쇠시켜
    /// "pchk" 또는 "크랙" 느낌의 0.1초짜리 클립을 만든다.
    /// </summary>
    private static AudioClip CreateFallbackDeathSfx()
    {
        const int sampleRate = 44100;
        const float duration  = 0.10f;
        const float decayRate = 45f;   // 높을수록 더 짧고 날카로운 팝
        const float sineFreq  = 650f;  // crack의 배음 느낌을 주는 주파수
        const float noiseAmt  = 0.70f; // 백색소음 비율 (0~1)
        const float sineAmt   = 0.30f; // 사인파 비율 (0~1)

        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        // 결정론적 시드 — 매 플레이마다 동일한 소리, Random.state에 영향 없음
        System.Random rng = new System.Random(7);

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Exp(-t * decayRate);

            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float sine  = Mathf.Sin(2f * Mathf.PI * sineFreq * t);

            samples[i] = (noise * noiseAmt + sine * sineAmt) * envelope;
        }

        AudioClip clip = AudioClip.Create(
            name:          "DeathSfx_Fallback",
            lengthSamples: sampleCount,
            channels:      1,
            frequency:     sampleRate,
            stream:        false
        );
        clip.SetData(samples, offsetSamples: 0);
        return clip;
    }
}
