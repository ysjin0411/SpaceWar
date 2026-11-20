using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 시스템 경고 후 스폰 위치로 이동하여 블랙홀을 발사한다.
/// 블랙홀 소멸 완료까지 특수패턴 상태를 유지한다.
/// </summary>
[CreateAssetMenu(
    fileName = "BossHeadBlackHolePattern",
    menuName = "BossPattern/Head/Black Hole")]
public sealed class BossHeadBlackHolePattern : BossPattern
{
    // 시스템 다이얼로그에 표시할 경고 문구다.
    [TextArea]
    [SerializeField] string _systemMessage = "중력 이상 감지!";

    // 최초 스폰 위치로 이동하는 시간이다.
    [Min(0.01f)]
    [SerializeField] float _moveToSpawnDuration = 0.4f;

    // 발사할 블랙홀 프리팹이다.
    [SerializeField] BossBlackHole _blackHolePrefab;

    // true면 플레이어 기준 위치, false면 고정 월드 좌표를 사용한다.
    [SerializeField] bool _usePlayerRelativeTarget = true;

    // 발사 순간 플레이어 위치에 더할 좌표다.
    // 현재 횡스크롤 기준 오른쪽이 플레이어의 앞쪽이다.
    [SerializeField] Vector2 _playerTargetOffset = new Vector2(1.5f, 0f);

    // 고정 좌표 모드에서 사용할 월드 목적지다.
    [SerializeField] Vector2 _worldTargetPosition = new Vector2(-2f, 0f);

    // 크기는 프리팹에서, 비행·확대·공격 동작은 Config에서 설정한다.
    [SerializeField]
    BossBlackHoleSettings _settings = new BossBlackHoleSettings
    {
        TravelDuration = 1f,
        // 이미지 확대와 소멸을 각각 0.5초 동안 진행한다.
        ExpandDuration = 0.5f,
        FadeDuration = 0.5f,

        // 최종 크기의 15%로 비행한다.
        FlightScaleRatio = 0.15f,

        // 펼쳐진 뒤 5초 동안 공격한다.
        ActiveDuration = 5f,
        PullSpeed = 2.5f,
        DamageInterval = 0.5f,
        Damage = 10,
        RotationSpeed = 90f,
    };

    public override IEnumerator Execute(BossPatternContext context)
    {
        ValidateSettings(context);

        BossEnemy boss = context.Boss;
        BossBlackHole hole = null;
        bool dialogueCompleted = false;

        // UI 완료 이벤트를 받은 뒤 다음 단계로 진행한다.
        Action onDialogueCompleted = () => dialogueCompleted = true;
        boss.SystemDialogueCompleted += onDialogueCompleted;

        try
        {
            // Runner가 특수패턴 시작과 일반 적 생성 중단을 먼저 처리한다.
            boss.RequestSystemDialogue(_systemMessage);

            while (IsAlive(boss) && !dialogueCompleted)
            {
                yield return null;
            }

            if (!IsAlive(boss))
            {
                yield break;
            }

            // 보스가 최초 생성됐던 위치로 이동한다.
            Vector2 start = context.Rigidbody.position;
            Vector2 destination = boss.MovementCenter;
            float elapsed = 0f;

            while (IsAlive(boss) && elapsed < _moveToSpawnDuration)
            {
                elapsed += Time.deltaTime;

                float ratio = Mathf.SmoothStep(
                    0f, 1f,
                    Mathf.Clamp01(elapsed / _moveToSpawnDuration));

                context.Rigidbody.position =
                    Vector2.Lerp(start, destination, ratio);

                yield return null;
            }

            if (!IsAlive(boss) || context.TargetPlayer == null ||
                context.TargetPlayer.IsDead)
            {
                yield break;
            }

            // 목적지는 발사 순간 한 번만 확정한다.
            // 발사 후 플레이어가 움직여도 목적지가 따라가지 않는다.
            Vector2 target = _usePlayerRelativeTarget
                ? (Vector2)context.TargetPlayer.transform.position +
                    _playerTargetOffset
                : _worldTargetPosition;

            hole = Instantiate(
                _blackHolePrefab,
                boss.FirePoint.position,
                Quaternion.identity);

            hole.Initialize(
                boss,
                context.TargetPlayer,
                target,
                _settings);

            // 비행·흡인 유지·소멸을 모두 마칠 때까지 기다린다.
            while (IsAlive(boss) &&
                   hole != null &&
                   hole.isActiveAndEnabled)
            {
                yield return null;
            }
        }
        finally
        {
            if (boss != null)
            {
                boss.SystemDialogueCompleted -= onDialogueCompleted;
            }

            // 패턴이 중단되어도 남아 있는 흡인을 해제한다.
            if (hole != null)
            {
                hole.BeginFadeOut();
            }
        }

        // 여기서 Execute가 끝나면 기존 Runner가 일반 모드로 복귀시킨다.
    }

    private static bool IsAlive(BossEnemy boss)
    {
        return boss != null &&
               !boss.IsDead &&
               boss.isActiveAndEnabled;
    }

    /// <summary>
    /// 실행 전에 필수 참조와 설정 범위를 확인한다.
    /// </summary>
    private void ValidateSettings(BossPatternContext context)
    {
        if (context == null || _blackHolePrefab == null ||
            string.IsNullOrWhiteSpace(_systemMessage))
        {
            throw new InvalidOperationException(
                $"{name}: Context, 블랙홀 프리팹, 경고 문구가 필요합니다.");
        }

        if (!PauseRandomMovement)
        {
            throw new InvalidOperationException(
                $"{name}: Pause Random Movement를 켜세요.");
        }

        // 비행 크기 비율과 각 단계의 시간·공격 수치를 검사한다.
        if (_moveToSpawnDuration <= 0f ||
            _settings.TravelDuration <= 0f ||
            _settings.FlightScaleRatio <= 0f ||
            _settings.FlightScaleRatio > 1f ||
            _settings.ActiveDuration <= 0f ||
            _settings.PullSpeed < 0f ||
            _settings.DamageInterval <= 0f ||
            _settings.Damage <= 0)
        {
            throw new InvalidOperationException(
                $"{name}: 블랙홀 설정 수치를 확인하세요.");
        }
    }
}