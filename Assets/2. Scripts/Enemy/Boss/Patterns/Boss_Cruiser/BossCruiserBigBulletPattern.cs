using System;
using System.Collections;
using UnityEngine;

[CreateAssetMenu(
    fileName = "BossCruiserBigBulletPattern",
    menuName = "BossPattern/Cruiser/Big Bullet")]
public sealed class BossCruiserBigBulletPattern : BossPattern
{
    // 충전 전에 시스템 다이얼로그로 표시할 경고 문구.
    [SerializeField, TextArea]
    private string _systemMessage = "경고! 크루저의 고에너지 공격이 감지되었습니다.";

    // 앞에서 준비한 CrusierBigBullet 프리팹.
    [SerializeField]
    private CruiserBigBullet _bulletPrefab;

    // 발사 방향을 결정하는 월드 좌표.
    // 이 위치에 도착하면 멈추는 것이 아니라 해당 방향으로 계속 이동한다.
    [SerializeField]
    private Vector2 _targetPosition = new Vector2(-6f, -2f);

    public override IEnumerator Execute(BossPatternContext context)
    {
        // 다른 보스에 잘못 연결한 경우를 확인한다.
        Boss_Cruiser cruiser = context.Boss as Boss_Cruiser;

        if (cruiser == null)
        {
            throw new InvalidOperationException(
                $"{name}: Boss_Cruiser 전용 패턴입니다.");
        }

        if (_bulletPrefab == null ||
            string.IsNullOrWhiteSpace(_systemMessage))
        {
            throw new InvalidOperationException(
                $"{name}: Bullet Prefab과 System Message를 설정하세요.");
        }

        // 대기열에서 기다리는 사이 2페이즈가 되었다면 실행하지 않는다.
        if (!CanExecute(cruiser))
        {
            yield break;
        }

        Camera battleCamera = Camera.main;

        if (battleCamera == null)
        {
            throw new InvalidOperationException(
                $"{name}: MainCamera 태그가 지정된 카메라가 필요합니다.");
        }

        // 실행 상태는 공유되는 패턴 에셋의 필드가 아닌 지역 변수에 보관한다.
        bool dialogueCompleted = false;
        CruiserBigBullet bullet = null;

        Action handleDialogueCompleted =
            () => dialogueCompleted = true;

        // 경고 요청 전에 완료 이벤트를 구독한다.
        cruiser.SystemDialogueCompleted += handleDialogueCompleted;

        try
        {
            cruiser.RequestSystemDialogue(_systemMessage);

            // 경고 표시가 끝날 때까지 기다린다.
            while (!dialogueCompleted)
            {
                if (cruiser == null ||
                    cruiser.IsDead ||
                    !cruiser.isActiveAndEnabled)
                {
                    yield break;
                }

                yield return null;
            }

            // 경고 중 모든 타워가 파괴되었다면 충전을 시작하지 않는다.
            if (!CanExecute(cruiser))
            {
                yield break;
            }

            // 본체 FirePoint에서 투사체를 생성한다.
            bullet = Instantiate(
                _bulletPrefab,
                cruiser.FirePoint.position,
                Quaternion.identity);

            // 투사체가 성장, 대기, 발사를 순서대로 진행한다.
            bullet.Initialize(
                cruiser,
                context.TargetPlayer,
                cruiser.FirePoint,
                battleCamera,
                _targetPosition);

            // 충전 중에는 패턴 실행 상태를 유지한다.
            while (bullet != null && !bullet.HasLaunched)
            {
                // 발사 전에 2페이즈가 되면 아래 finally에서 탄을 제거한다.
                if (!CanExecute(cruiser))
                {
                    yield break;
                }

                yield return null;
            }

            // 발사 후에는 투사체가 독립적으로 이동과 수명을 관리한다.
        }
        finally
        {
            // 정상 완료와 중도 취소 모두 이벤트 구독을 정리한다.
            if (cruiser != null)
            {
                cruiser.SystemDialogueCompleted -= handleDialogueCompleted;
            }

            // 아직 발사하지 않은 탄만 취소한다.
            // 먼저 비활성화하여 예약된 DOTween 발사도 즉시 중단한다.
            if (bullet != null && !bullet.HasLaunched)
            {
                bullet.gameObject.SetActive(false);
                Destroy(bullet.gameObject);
            }
        }
    }

    // 어느 페이즈에서 요청할지는 크루저의 두 패턴 목록이 결정한다.
    // 페이즈가 전환되면 크루저가 실행기를 통해 기존 패턴을 중단한다.
    private static bool CanExecute(Boss_Cruiser cruiser)
    {
        return cruiser != null &&
               cruiser.isActiveAndEnabled &&
               !cruiser.IsDead;
    }
}