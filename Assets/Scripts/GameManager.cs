using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("레벨")]
    [SerializeField] int _level = 1;
    [SerializeField] int _exp = 0;
    [SerializeField] int _needExp = 100;

    [Header("레벨업 보상")]
    [SerializeField] int _skillPointReward = 1;
    [SerializeField] int _statPointReward = 3;
    [SerializeField] int _expIncreasePerLevel = 50;

    [Header("기본 능력치")]
    [SerializeField] int _attack = 10;
    [SerializeField] int _maxHp = 100;
    [SerializeField] float _moveSpeed = 5f;

    public int Level => _level;
    public int Exp => _exp;
    public int NeedExp => _needExp;

    public int Attack => _attack;
    public int MaxHp => _maxHp;
    public float MoveSpeed => _moveSpeed;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void GetExp(int amount)
    {
        _exp += amount;

        if (_exp >= _needExp)
        {
            LevelUp();
        }
    }

    void LevelUp()
    {
        _level++;
        _exp -= _needExp;
        _needExp += _expIncreasePerLevel;

        // 보상 지급
        // 나중에 포인트 변수 추가 예정

        Debug.Log($"레벨업! 스킬포인트 +{_skillPointReward}, 능력치포인트 +{_statPointReward}");
    }
}