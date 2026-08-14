using UnityEngine;

public class GoldUI : MonoBehaviour
{
    [Header("골드 이동 목적지")]
    [SerializeField] RectTransform _goldTarget; // Stage 씬의 CurrentGoldImage를 연결합니다.

    [Header("골드 생성")]
    [SerializeField] Gold _goldPrefab; // Assets/4. Prefabs/Item/Gold 프리팹을 연결합니다.
    [SerializeField] RectTransform _goldContainer; // 생성되는 골드를 자식으로 넣을 Canvas의 RectTransform입니다.

    // 외부에서는 골드 아이콘 참조를 읽기만 할 수 있습니다.
    public RectTransform GoldTarget => _goldTarget;

    public void SpawnGold(Vector3 worldPosition)
    {
        // 몬스터의 월드 위치를 화면 위치로 변환합니다.
        Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(Camera.main, worldPosition);

        // Gold 프리팹을 Canvas의 자식으로 생성합니다.
        Gold spawnedGold = Instantiate(_goldPrefab, _goldContainer);

        // 생성된 골드의 UI 위치를 몬스터의 화면 위치로 맞춥니다.
        spawnedGold.transform.position = screenPosition;

        // 씬에 있는 골드 아이콘과 GoldUI를 런타임에 전달합니다.
        //spawnedGold.Initialize(_goldTarget, this);
    }
}
