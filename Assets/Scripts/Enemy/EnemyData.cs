using UnityEngine;

[CreateAssetMenu(menuName = "Agent/Enemy Data", fileName = "EnemyData")]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string enemyId = "enemy";
    [SerializeField] private string displayName = "Enemy";

    [Header("Health")]
    [SerializeField] private int maxHP = 30;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.5f;

    [Header("Attack")]
    [SerializeField] private int attackDamage = 5;
    [SerializeField] private float attackRange = 1.0f;
    [SerializeField] private float attackWindup = 0.2f;
    [SerializeField] private float attackCooldown = 1.0f;

    [Header("Death Feedback")]
    [SerializeField] private GameObject deathVfxPrefab;
    [SerializeField] private float deathVfxLifetime = 0.5f;
    [SerializeField] private AudioClip deathSfx;
    [SerializeField, Range(0f, 1f)] private float deathSfxVolume = 1f;

    public string EnemyId => enemyId;
    public string DisplayName => displayName;
    public int MaxHP => maxHP;
    public float MoveSpeed => moveSpeed;
    public int AttackDamage => attackDamage;
    public float AttackRange => attackRange;
    public float AttackWindup => attackWindup;
    public float AttackCooldown => attackCooldown;
    public GameObject DeathVfxPrefab => deathVfxPrefab;
    public float DeathVfxLifetime => deathVfxLifetime;
    public AudioClip DeathSfx => deathSfx;
    public float DeathSfxVolume => deathSfxVolume;
}
