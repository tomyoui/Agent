// 데미지를 받을 수 있는 오브젝트 공통 인터페이스
public interface IDamageable
{
    // attribute: 공격 속성. 이펙트·사운드·상성 분기에 활용.
    // default = Justice — 기존 구현체(Health 외)가 시그니처를 갱신하지 않아도 컴파일 유지.
    void TakeDamage(int damage, CombatAttribute attribute = CombatAttribute.Justice);
}