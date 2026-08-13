using System;
using UnityEngine;

public class CharacterProgression : MonoBehaviour
{
    // 현재 선택한 캐릭터의 영구 저장 데이터
    CharacterLoadoutSaveData _characterSaveData;

    [Header("런타임 성장 데이터")]
    [Tooltip("현재 캐릭터 레벨")]
    [SerializeField] int _currentLevel;

    [Tooltip("현재 경험치")]
    [SerializeField] int _currentExp;

    [Tooltip("다음 레벨까지 필요한 전체 경험치")]
    [SerializeField] int _requiredExp;

    [Tooltip("레벨업까지 남은 경험치")]
    [SerializeField] int _remainingExp;

    public int CurrentLevel => _currentLevel;
    public int CurrentExp => _currentExp;
    public int RequiredExp => _requiredExp;
    public int RemainingExp => _remainingExp;

    // 레벨, 현재 경험치, 필요 경험치가 변경됐을 때 알린다.
    public event Action<int, int, int> OnProgressChanged;

    // 레벨이 올랐을 때 알린다.
    public event Action<int> OnLevelUp;

    /// <summary>
    /// 선택한 캐릭터의 저장 데이터를 런타임 데이터로 불러온다.
    /// </summary>
    public void Initialize(string characterId)
    {
        _characterSaveData =
            SaveManager.Instance.GetOrCreateCharacterLoadout(characterId);

        // JSON에 저장된 영구 데이터를 런타임 데이터로 복사한다.
        _currentLevel = _characterSaveData.level;
        _currentExp = _characterSaveData.currentExp;

        UpdateCalculatedExp();
        NotifyProgressChanged();
    }

    /// <summary>
    /// 현재 캐릭터에게 경험치를 지급한다.
    /// </summary>
    public void AddExp(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        _currentExp += amount;

        // 많은 경험치를 한 번에 받아 여러 레벨이 오르는 경우도 처리한다.
        while (_currentExp >= GetRequiredExp(_currentLevel))
        {
            int levelUpExp = GetRequiredExp(_currentLevel);

            _currentExp -= levelUpExp;
            _currentLevel++;

            OnLevelUp?.Invoke(_currentLevel);
        }

        // 레벨업 계산 후 경험치 통과 남은 경험치를 다시 계산한다.
        UpdateCalculatedExp();

        // 런타임 결과를 영구 저장 데이터에 반영한다.
        ApplyToSaveData();

        NotifyProgressChanged();

        SaveManager.Instance.SaveToFile();
    }

    /// <summary>
    /// 해당 레벨에서 다음 레벨까지 필요한 경험치를 반환한다.
    /// </summary>
    private int GetRequiredExp(int level)
    {
        return 100 + (level - 1) * 50;
    }

    /// <summary>
    /// 현재 레벨을 기준으로 파생되는 경험치 값을 계산한다.
    /// </summary>
    private void UpdateCalculatedExp()
    {
        _requiredExp = GetRequiredExp(_currentLevel);
        _remainingExp = _requiredExp - _currentExp;
    }

    /// <summary>
    /// 런타임 성장 데이터를 JSON에 저장할 데이터에 반영한다.
    /// </summary>
    private void ApplyToSaveData()
    {
        _characterSaveData.level = _currentLevel;
        _characterSaveData.currentExp = _currentExp;
    }

    private void NotifyProgressChanged()
    {
        OnProgressChanged?.Invoke(
            _currentLevel,
            _currentExp,
            _requiredExp);
    }
}