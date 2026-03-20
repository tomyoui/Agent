/// <summary>
/// 전투 속성 열거형.
///
/// 현재 구현:
///   Justice (정의) — PlayerCombat2D, 근접 검 공격
///   Doom    (파멸) — PlayerRangedAttack2D, 히트스캔 총
///
/// 확장 자리 (미구현 — 기능 추가 시 주석 해제):
///   Pleasure (쾌락), Harmony (조화), Life (생명), Greed (탐욕)
/// </summary>
public enum CombatAttribute
{
    Justice,   // 정의 — 검, 근접, 밝은 계열 이펙트
    Doom,      // 파멸 — 총, 히트스캔, 보라/암흑 계열 이펙트

    // --- 미구현 속성 (확장 자리) ---
    // Pleasure,  // 쾌락
    // Harmony,   // 조화
    // Life,      // 생명
    // Greed,     // 탐욕
}
