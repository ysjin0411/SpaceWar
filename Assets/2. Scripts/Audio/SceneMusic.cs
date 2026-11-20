using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneMusic : MonoBehaviour
{
    // 이 씬을 단독으로 열었을 때 사용할 배경음악.
    [SerializeField] private GameSound _music;
    // 해당 씬에서만 유지할 환경음. 필요 없으면 None.
    [SerializeField] private GameSound _ambience;
    // 환경음을 정지하는 데 필요한 핸들.
    private GameAudio.Handle _ambienceHandle;

    /// <summary>음량 설정 초기화 후 주 씬의 음악을 시작합니다.</summary>
    private IEnumerator Start()
    {
        // AudioSettingsManager.Start가 저장된 Mixer 음량을 먼저 적용하게 한다.
        yield return null;
        // 추가 로드된 서비스는 TownNpcServicePresenter가 임시 음악 요청을 관리한다.
        if (gameObject.scene != SceneManager.GetActiveScene()) yield break;
        GameAudio.SetMusic(_music);
        if (_ambience != GameSound.None)
            _ambienceHandle = GameAudio.PlayOwned(_ambience, this, true);
    }

    /// <summary>씬 환경음이 더 이상 필요 없으면 정지합니다.</summary>
    private void OnDisable()
    {
        // 음악은 공용 재생기가 다음 씬에 맞춰 교체한다.
        GameAudio.Stop(_ambienceHandle);
        _ambienceHandle = default;
    }
}
