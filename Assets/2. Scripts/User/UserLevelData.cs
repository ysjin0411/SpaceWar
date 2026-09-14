using UnityEngine;

// User 레벨별로 다음 레벨까지 필요한 경험치를 설정한다.
[CreateAssetMenu(
    fileName = "UserLevelData",
    menuName = "GameSettings/User Level Data")]
public class UserLevelData : ScriptableObject
{
    // 배열의 첫 항목은 1→2레벨, 두 번째는 2→3레벨의 필요 경험치다.
    // 현재 경험치나 플레이어 레벨은 이 에셋에 저장하지 않는다.
    [SerializeField]
    private int[] _requiredExpByLevel = new int[0];

    // 해당 레벨의 필요 경험치가 설정되어 있으면 반환한다.
    public bool TryGetRequiredExp(int level, out int requiredExp)
    {
        requiredExp = 0;

        // 레벨은 1부터 시작하지만 배열 인덱스는 0부터 시작한다.
        int index = level - 1;

        if (_requiredExpByLevel == null ||
            index < 0 ||
            index >= _requiredExpByLevel.Length ||
            _requiredExpByLevel[index] <= 0)
        {
            return false;
        }

        requiredExp = _requiredExpByLevel[index];
        return true;
    }
}