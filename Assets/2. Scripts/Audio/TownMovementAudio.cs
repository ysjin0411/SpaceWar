using UnityEngine;

public sealed class TownMovementAudio : MonoBehaviour
{
    // 실제 이동량을 읽을 TownPlayer의 Rigidbody2D.
    [SerializeField] private Rigidbody2D _body;
    // 발소리 간격과 작은 지면 흔들림을 제외할 최소 체공 시간.
    [SerializeField, Min(0.05f)] private float _stepInterval = 0.3f;
    [SerializeField, Min(0f)] private float _minimumAirTime = 0.12f;
    // 상태 전이와 이동량 계산용 값.
    private bool _hasSample;
    private bool _wasGrounded;
    private Vector2 _lastPosition;
    private float _airTime;
    private float _stepTime;
    private int _nextStep;

    /// <summary>TownPlayer가 계산한 접지·이동 상태로 발소리와 착지음을 갱신합니다.</summary>
    public void Tick(bool grounded, bool wantsMove, float deltaTime)
    {
        // 일시정지 중에는 타이머를 진행하지 않는다.
        if (_body == null || deltaTime <= 0f) return;
        Vector2 position = _body.position;
        if (!_hasSample)
        {
            // 생성 직후 땅에 놓인 상태를 착지로 오인하지 않는다.
            _hasSample = true;
            _wasGrounded = grounded;
            _lastPosition = position;
            return;
        }
        bool moved = (position - _lastPosition).sqrMagnitude > 0.000001f;
        _lastPosition = position;

        // 일정 시간 공중에 있었다가 접지될 때만 착지음을 낸다.
        if (!grounded) _airTime += deltaTime;
        else
        {
            if (!_wasGrounded && _airTime >= _minimumAirTime)
                GameAudio.Play(GameSound.PlayerLand);
            _airTime = 0f;
        }
        _wasGrounded = grounded;

        // 벽에 막혀 실제로 움직이지 않거나 공중이면 발소리를 멈춘다.
        if (!grounded || !wantsMove || !moved)
        {
            _stepTime = 0f;
            return;
        }
        _stepTime += deltaTime;
        if (_stepTime < _stepInterval) return;
        _stepTime -= _stepInterval;

        // 준비된 세 변형을 번갈아 재생한다.
        switch (_nextStep)
        {
            case 0: GameAudio.Play(GameSound.PlayerFootstep01); break;
            case 1: GameAudio.Play(GameSound.PlayerFootstep02); break;
            default: GameAudio.Play(GameSound.PlayerFootstep03); break;
        }
        _nextStep = (_nextStep + 1) % 3;
    }

    /// <summary>실제 점프가 승인된 순간 점프음을 재생합니다.</summary>
    public void PlayJump()
    {
        // 키 입력이 아니라 점프 속도가 적용된 지점에서 호출한다.
        GameAudio.Play(GameSound.PlayerJump);
    }

    /// <summary>오브젝트 재활성화 시 이전 이동 상태를 사용하지 않게 합니다.</summary>
    private void OnDisable()
    {
        // 텔레포트나 재생성 직후 잘못된 착지음을 방지한다.
        _hasSample = false;
        _airTime = 0f;
        _stepTime = 0f;
    }
}
