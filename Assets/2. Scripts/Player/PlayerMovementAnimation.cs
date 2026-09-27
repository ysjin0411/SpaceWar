using UnityEngine;

// 플레이어의 이동 입력을 Animator의 이동 상태에 전달한다.
[RequireComponent(typeof(Animator))]
public sealed class PlayerMovementAnimation : MonoBehaviour
{
    // 문자열 대신 해시를 사용해 이동 파라미터를 갱신한다.
    private static readonly int IsMovingHash =
        Animator.StringToHash("IsMoving");

    // 같은 오브젝트에 있는 캐릭터 Animator.
    private Animator _animator;

    // 부모 오브젝트에 있는 플레이어 이동 컨트롤러.
    private PlayerController _player;

    private void Awake()
    {
        // Renderer의 Animator와 부모의 PlayerController를 연결한다.
        _animator = GetComponent<Animator>();
        _player = GetComponentInParent<PlayerController>();

        // 잘못된 오브젝트에 붙였다면 원인을 명확히 알린다.
        if (_player == null)
        {
            throw new MissingComponentException(
                $"{name}: 부모에 PlayerController가 필요합니다.");
        }
    }

    private void LateUpdate()
    {
        // PlayerController의 Update에서 입력을 읽은 뒤 반영한다.
        // 조작 불가나 사망 상태에서는 HasMoveInput이 false가 된다.
        _animator.SetBool(IsMovingHash, _player.HasMoveInput);
    }

    private void OnDisable()
    {
        // 다시 활성화될 때 이전 이동 상태가 남지 않도록 초기화한다.
        if (_animator != null)
        {
            _animator.SetBool(IsMovingHash, false);
        }
    }
}