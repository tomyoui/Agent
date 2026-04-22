# 게임에 추가해야 할 것들 (2026-04-20 분석)

## 긴급/버그
1. **파티 전환 2번·3번 키 미연결** — PartyManager2D.OnEnable에서 _switch2/_switch3.performed 구독이 TODO 주석으로 막혀있음. 테스트 후 복구 필요.

## 핵심 게임플레이 미구현
2. **캐릭터 2번·3번 전투 스킬** — OnESkillPerformed에서 index 1,2는 아무것도 안 함. 각 캐릭터 E스킬 정의 필요.
3. **궁극기 ExecuteUltimate() 구현** — BasePlayableCombat2D에 게이지 시스템은 완성, PlayerCombat2D/PlayerRangedAttack2D의 ExecuteUltimate() 실제 동작 없음.
4. **속성 상성/내성 시스템** — Health.TakeDamage에서 switch(attribute)가 로그만 출력. 실제 배율 적용 없음.

## UI
5. **게임 오버 UI** — GameManager.GameOver()가 Time.timeScale=0으로 멈추지만 화면에 아무것도 없음. "Game Over" 패널 + 재시작 버튼 필요.
6. **파티 전체 HP/게이지 UI** — HealthBarUI는 단일 Health만 추적. 3인 파티 전체 상태 표시 UI 없음.
7. **궁극기 게이지 UI** — BasePlayableCombat2D.UltimateGaugeRatio가 있지만 화면에 표시 안 됨.

## VFX/SFX
8. **백스텝 VFX/SFX** — PlayerRangedAttack2D.OnBackstepStart/End에 TODO만 있고 실제 이펙트 없음.
9. **속성별 피격 이펙트** — Justice/Doom 각각 다른 색·파티클 적용 안 됨.

## 적 시스템
10. **적에 CharacterStats 없음** — EnemyMelee2D가 flatAttack만 사용. CharacterStats 붙이면 방어력·치명타 등 정식 계산 가능.
11. **원거리 적 없음** — EnemyMelee2D만 존재. 원거리 공격 적 추가 필요.
12. **보스 없음** — 단순 스폰 루프만. 보스 AI/페이즈 없음.
13. **적 속성 고정** — EnemyMelee2D의 공격 속성이 Justice 하드코딩. Inspector 필드로 분리 필요.

## 확장 속성
14. **미구현 속성** — CombatAttribute에 Pleasure/Harmony/Life/Greed 주석 처리됨.

## 시스템
15. **세이브/로드 없음** — StatBlock 기반이라 직렬화 구조는 있지만 저장 로직 없음.
16. **레벨/씬 디자인** — 씬 1개. 맵 구성, 오브젝트, 레벨 진행 없음.
17. **CharacterStats AddModifier** — 버프/장비 합산 API가 주석 설계만 있고 미구현.
