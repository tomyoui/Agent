# 프로젝트 개요

Unity 2D 탑다운 액션 RPG. 3인 파티 캐릭터 전환 시스템이 핵심.

## 기술 스택
- Unity (URP, 2D)
- C# / Unity Input System (PlayerInput + InputAction)
- TextMesh Pro (데미지 숫자)
- Physics2D (Rigidbody2D, Raycast, CircleCast)

## 핵심 시스템
- **PartyManager2D**: 3인 파티, 1/2/3키로 전환 (현재 1번만 활성화)
- **전투**: BasePlayableCombat2D → PlayerCombat2D(근접), PlayerRangedAttack2D(원거리)
- **데미지**: DamageFormula (계수×공격력×보너스×치명타×방어감소)
- **속성**: Justice(정의/검), Doom(파멸/총)
- **적**: EnemyMelee2D + EnemySpawner
- **GameManager**: GameOver(timeScale=0), RestartGame(씬 리로드)

## 스크립트 경로
Assets/Scripts/
- Core/: GameManager, IDamageable
- Camera/: CameraFollow2D
- Party/: PartyManager2D
- Player/: PlayerController2D, BasePlayableCombat2D, PlayerCombat2D, PlayerRangedAttack2D
- Enemy/: EnemyMelee2D, EnemySpawner, KnockbackReceiver2D
- combat/: CharacterStats, StatBlock, Health, HealthBarUI, DamageFormula, AttackDefinition, AttackCategory, CombatAttribute, MeleeHitResolver2D, MeleeSlashVFX, DamageNumber, DoomProjectile2D
