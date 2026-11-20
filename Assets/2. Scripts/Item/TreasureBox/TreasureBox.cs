using System;
using UnityEngine;
using DG.Tweening;
using System.Collections;

// 상자의 피격 및 개봉 진행 상태다.
public enum TreasureBoxState
{
    Waiting = 0,         // 초기화 또는 낙하 연출 중
    ReceivingDamage = 1, // 피해를 누적하는 중
    Charge = 2,          // 승급 또는 개봉을 위한 추가 타격 대기
    Opened = 3,          // 개봉 완료
    Upgrading = 4        // 다음 등급으로 바뀌는 연출 중
}

[RequireComponent(typeof(BoxCollider2D))]
public class TreasureBox : MonoBehaviour, IDamageable
{
    // Wood, Silver, Gold, Emerald, Diamond 순서로 데이터를 연결한다.
    [SerializeField]
    TreasureBoxGradeData[] _gradeDatas = new TreasureBoxGradeData[5];

    // 등급별 외형 프리팹을 생성할 빈 자식 오브젝트다.
    [SerializeField]
    Transform _visualRoot;

    // 플레이 중 현재 상자의 상태를 확인하기 위한 값이다.
    [SerializeField]
    TreasureBoxState _state = TreasureBoxState.Waiting;

    // 현재 화면에 표시되는 등급이다.
    [SerializeField]
    TreasureBoxGrade _currentGrade = TreasureBoxGrade.Wood;

    // 등장 시 한 번 결정되고 이후에는 바뀌지 않는 최종 보상 등급이다.
    [SerializeField]
    TreasureBoxGrade _finalGrade;

    // 현재 등급에서 누적된 피해량이다.
    [SerializeField]
    float _accumulatedDamage;

    // 기존 상자가 작아져 사라지는 시간이다.
    [SerializeField, Min(0.01f)]
    float _upgradeShrinkDuration = 0.12f;

    // 다음 등급 상자가 원래 크기로 펼쳐지는 시간이다.
    [SerializeField, Min(0.01f)]
    float _upgradeGrowDuration = 0.22f;

    // 상자가 제거될 때 승급 연출도 정리하기 위해 보관한다.
    Sequence _upgradeSequence;

    // 공통 상자 본체의 피격 영역이다.
    BoxCollider2D _hitCollider;

    // 현재 생성되어 있는 등급별 외형이다.
    GameObject _currentVisual;

    // 현재 등급 외형의 열린 스프라이트 전환을 담당한다.
    TreasureBoxVisual _currentVisualController;

    // 초기화가 완료되었는지 확인한다.
    bool _isInitialized;

    // 기존 IDamageable 규약에 맞춘 피격 알림이다.
    public event Action<float> Damaged;

    // 이후 Charge 등의 시각 효과를 연결할 상태 변경 알림이다.
    public event Action<TreasureBoxState> StateChanged;

    // 개봉 시 최종 등급 데이터를 전달한다.
    // 실제 보상 생성과 지급은 이 이벤트를 받는 쪽에서 처리한다.
    public event Action<TreasureBoxGradeData> Opened;

    // 열린 상자는 더 이상 공격 대상으로 취급하지 않는다.
    public bool IsDead => _state == TreasureBoxState.Opened;

    // 유도탄 등이 사용할 공격 대상 위치다.
    public Transform TargetTransform => transform;

    // 외부에서 현재 상태와 등급을 확인할 때 사용한다.
    public TreasureBoxState State => _state;
    public TreasureBoxGrade CurrentGrade => _currentGrade;
    // 현재 등급 외형에 설정한 보상 출발 위치를 전달한다.
    // 외형 초기화가 완료된 이후에 사용한다.
    public Vector3 RewardPosition => _currentVisualController.RewardPosition;

    // 등급 번호는 1부터, 배열 인덱스는 0부터 시작한다.
    TreasureBoxGradeData CurrentData =>
        _gradeDatas[(int)_currentGrade - 1];

    /// <summary>
    /// 컴포넌트 참조와 초기 실행 상태를 준비한다.
    /// </summary>
    private void Awake()
    {
        // 낙하 연출이 끝나기 전에는 공격받지 않는다.
        _hitCollider = GetComponent<BoxCollider2D>();
        _hitCollider.isTrigger = true;
        _hitCollider.enabled = false;
    }

    // 외부에서 추첨한 최종 등급을 전달한다.
    // 최종 등급과 관계없이 외형은 항상 Wood부터 시작한다.
    /// <summary>
    /// 최종 등급과 관계없이 외형은 항상 Wood부터 시작한다.
    /// </summary>
    public void Initialize(TreasureBoxGrade finalGrade)
    {
        // 같은 상자의 최종 등급을 다시 설정하지 않는다.
        if (_isInitialized)
        {
            throw new InvalidOperationException(
                $"{name}: 이미 초기화된 상자입니다.");
        }

        ValidateSettings();

        // 정의된 5개 등급만 최종 등급으로 허용한다.
        if ((int)finalGrade < 1 || (int)finalGrade > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(finalGrade));
        }

        _finalGrade = finalGrade;
        _currentGrade = TreasureBoxGrade.Wood;
        _accumulatedDamage = 0;
        _state = TreasureBoxState.Waiting;

        ChangeVisual();
        _isInitialized = true;
        GameAudio.Play(GameSound.TreasureAppear);
    }

    // 낙하와 두 번의 반동이 모두 끝났을 때 호출한다.
    /// <summary>
    /// 낙하와 두 번의 반동이 모두 끝났을 때 호출한다.
    /// </summary>
    public void BeginReceivingDamage()
    {
        // 초기화 전이거나 이미 시작한 상자는 처리하지 않는다.
        if (!_isInitialized || _state != TreasureBoxState.Waiting)
        {
            return;
        }

        _hitCollider.enabled = true;
        ChangeState(TreasureBoxState.ReceivingDamage);
    }

    // 기존 무기가 IDamageable을 통해 호출하는 피격 처리다.
    /// <summary>
    /// 소수점 피해를 반영하고 피격 및 사망 처리를 실행한다.
    /// </summary>
    public void TakeHit(float damage)
    {
        // 초기화가 끝나고 피해 누적 또는 Charge 상태일 때만 타격을 처리한다.
        // 낙하, 승급 연출, 개봉 완료 상태에서는 피해를 받지 않는다.
        if (!_isInitialized ||
            (_state != TreasureBoxState.ReceivingDamage &&
             _state != TreasureBoxState.Charge) ||
            damage <= 0)
        {
            return;
        }

        // 이미 Charge였다면 이번 타격으로 승급 또는 개봉한다.
        if (_state == TreasureBoxState.Charge)
        {
            ResolveChargedHit();
            Damaged?.Invoke(damage);
            return;
        }

        // 필요한 만큼만 누적하여 기준을 넘는 피해는 버린다.
        float remainingDamage =
            CurrentData.RequiredDamage - _accumulatedDamage;

        _accumulatedDamage += Mathf.Min(damage, remainingDamage);

        // 기준에 도달한 타격은 Charge 진입까지만 처리한다.
        // 이 호출 안에서 승급하거나 개봉하지 않는다.
        if (_accumulatedDamage >= CurrentData.RequiredDamage)
        {
            ChangeState(TreasureBoxState.Charge);
        }

        Damaged?.Invoke(damage);
    }

    // Charge 상태에서 받은 추가 타격으로 승급하거나 보상을 요청한다.
    /// <summary>
    /// Charge 상태에서 받은 추가 타격으로 승급하거나 보상을 요청한다.
    /// </summary>
    private void ResolveChargedHit()
    {
        if (_currentGrade < _finalGrade)
        {
            // 다음 등급이 남아 있다면 즉시 교체하지 않고 승급 연출을 시작한다.
            PlayUpgrade();
            return;
        }

        // 최종 등급의 개봉을 확정하고 추가 피격과 Charge 연출을 종료한다.
        _hitCollider.enabled = false;
        ChangeState(TreasureBoxState.Opened);

        // 골드를 생성하기 전에 열린 상자 그림으로 바꾼다.
        _currentVisualController.ShowOpened();

        // 열린 모습으로 바뀐 뒤 해당 등급의 보상을 요청한다.
        if (Opened != null)
        {
            Opened.Invoke(CurrentData);
        }
    }

    // 공통 본체는 유지하고 등급별 외형만 교체한다.
    /// <summary>
    /// 공통 본체는 유지하고 등급별 외형만 교체한다.
    /// </summary>
    private void ChangeVisual()
    {
        if (_currentVisual != null)
        {
            // Destroy는 프레임 끝에 처리되므로 먼저 숨겨 겹침을 막는다.
            _currentVisual.SetActive(false);
            Destroy(_currentVisual);
        }

        // 프리팹 내부 효과의 위치와 크기는 그대로 유지한다.
        _currentVisual = Instantiate(
            CurrentData.VisualPrefab, _visualRoot, false);

        // 외형 루트는 공통 기준점에 맞춘다.
        _currentVisual.transform.localPosition = Vector3.zero;
        _currentVisual.transform.localRotation = Quaternion.identity;

        // 새로 생성한 등급 외형에서 스프라이트 전환 컴포넌트를 가져온다.
        if (!_currentVisual.TryGetComponent<TreasureBoxVisual>(
                out _currentVisualController))
        {
            throw new InvalidOperationException(
                $"{name}: 등급별 외형의 최상위에 TreasureBoxVisual이 필요합니다.");
        }
    }

    // 상태를 변경하고 연출 담당 코드에 알린다.
    /// <summary>
    /// 상태를 변경하고 연출 담당 코드에 알린다.
    /// </summary>
    private void ChangeState(TreasureBoxState nextState)
    {
        if (_state == nextState)
        {
            return;
        }

        _state = nextState;
        if (nextState == TreasureBoxState.Charge) GameAudio.Play(GameSound.TreasureCharge);
        else if (nextState == TreasureBoxState.Opened) GameAudio.Play(GameSound.TreasureOpen);
        StateChanged?.Invoke(_state);

        Debug.Log($"{name}: {_currentGrade} / {_state}", this);
    }

    // 배열 누락이나 순서 오류를 초기화 시점에 확인한다.
    /// <summary>
    /// 배열 누락이나 순서 오류를 초기화 시점에 확인한다.
    /// </summary>
    private void ValidateSettings()
    {
        if (_visualRoot == null || _visualRoot == transform)
        {
            throw new InvalidOperationException(
                $"{name}: Visual Root에 별도의 빈 자식을 연결하세요.");
        }

        if (_gradeDatas == null || _gradeDatas.Length != 5)
        {
            throw new InvalidOperationException(
                $"{name}: 등급 데이터 5개를 연결하세요.");
        }

        for (int i = 0; i < _gradeDatas.Length; i++)
        {
            TreasureBoxGradeData data = _gradeDatas[i];

            if (data == null ||
                (int)data.Grade != i + 1 ||
                data.VisualPrefab == null ||
                data.RequiredDamage < 1)
            {
                throw new InvalidOperationException(
                    $"{name}: {i + 1}단계 데이터의 등급, 외형, 피해량을 확인하세요.");
            }
        }
    }

    // 등급별 가중치로 최종 등급을 한 번 추첨하고 상자를 초기화한다.
    /// <summary>
    /// 등급별 가중치로 최종 등급을 한 번 추첨하고 상자를 초기화한다.
    /// </summary>
    public void InitializeRandom()
    {
        // 이미 초기화된 상자는 재추첨하지 않는다.
        if (_isInitialized)
        {
            throw new InvalidOperationException(
                $"{name}: 이미 초기화된 상자입니다.");
        }

        ValidateSettings();

        double totalWeight = 0;
        TreasureBoxGrade lastSelectableGrade = TreasureBoxGrade.Wood;

        foreach (TreasureBoxGradeData data in _gradeDatas)
        {
            float weight = data.SelectionWeight;

            // 잘못된 가중치는 추첨 전에 설정 오류로 알린다.
            if (float.IsNaN(weight) ||
                float.IsInfinity(weight) ||
                weight < 0f)
            {
                throw new InvalidOperationException(
                    $"{name}: {data.Grade}의 가중치를 확인하세요.");
            }

            // 가중치가 0인 등급은 최종 등급 추첨에서 제외한다.
            if (weight == 0f)
            {
                continue;
            }

            totalWeight += weight;
            lastSelectableGrade = data.Grade;
        }

        if (totalWeight <= 0)
        {
            throw new InvalidOperationException(
                $"{name}: 하나 이상의 등급 가중치가 0보다 커야 합니다.");
        }

        // 전체 가중치 구간에서 위치 하나를 뽑는다.
        double roll = UnityEngine.Random.value * totalWeight;

        foreach (TreasureBoxGradeData data in _gradeDatas)
        {
            if (data.SelectionWeight <= 0f)
            {
                continue;
            }

            // 선택한 위치가 현재 등급의 구간 안에 있으면 확정한다.
            if (roll < data.SelectionWeight)
            {
                Initialize(data.Grade);
                return;
            }

            roll -= data.SelectionWeight;
        }

        // Random.value가 정확히 1인 경우 마지막 유효 등급을 선택한다.
        Initialize(lastSelectableGrade);
    }

    // 기존 외형을 축소한 뒤 다음 등급 외형을 펼친다.
    /// <summary>
    /// 기존 외형을 축소한 뒤 다음 등급 외형을 펼친다.
    /// </summary>
    private void PlayUpgrade()
    {
        // 연출 중에는 충돌 판정과 직접 전달되는 추가 피해를 모두 막는다.
        _hitCollider.enabled = false;
        ChangeState(TreasureBoxState.Upgrading);

        // 위 상태 변경으로 Charge 효과가 종료되고 원래 크기로 복구된다.
        // 복구된 크기를 승급 연출의 최종 크기로 사용한다.
        Vector3 originalScale = _visualRoot.localScale;

        // 이전 등급에서 누적된 피해는 다음 등급으로 넘기지 않는다.
        _accumulatedDamage = 0;

        _upgradeSequence = DOTween.Sequence();

        // 상자 본체가 제거되면 연결된 승급 연출도 종료한다.
        _upgradeSequence.SetLink(gameObject);

        // 기존 상자와 효과를 중앙으로 빠르게 수축시킨다.
        _upgradeSequence.Append(
            _visualRoot.DOScale(Vector3.zero, _upgradeShrinkDuration)
                .SetEase(Ease.InQuad));

        // 외형이 보이지 않는 시점에 한 단계 승급하고 프리팹을 교체한다.
        _upgradeSequence.AppendCallback(() =>
        {
            _currentGrade =
                (TreasureBoxGrade)((int)_currentGrade + 1);

            ChangeVisual();
        });

        // 새 외형이 약간 크게 튀어나왔다 원래 크기로 자리 잡도록 한다.
        _upgradeSequence.Append(
            _visualRoot.DOScale(originalScale, _upgradeGrowDuration)
                .SetEase(Ease.OutBack));

        _upgradeSequence.OnComplete(() =>
        {
            _upgradeSequence = null;

            // 연출이 끝난 크기를 정확하게 맞춘다.
            _visualRoot.localScale = originalScale;

            // 새 등급의 피해 누적을 시작한다.
            ChangeState(TreasureBoxState.ReceivingDamage);
            _hitCollider.enabled = true;
        });
    }

    // 실패나 씬 전환으로 상자가 제거되면 진행 중인 승급 연출을 정리한다.
    /// <summary>
    /// 실패나 씬 전환으로 상자가 제거되면 진행 중인 승급 연출을 정리한다.
    /// </summary>
    private void OnDestroy()
    {
        if (_upgradeSequence != null)
        {
            // 완료 처리를 실행하지 않고 연출을 종료한다.
            _upgradeSequence.Kill();
            _upgradeSequence = null;
        }
    }

    // 현재 등급 외형의 퇴장이 끝날 때까지 외부에서 기다릴 수 있게 한다.
    /// <summary>
    /// 현재 등급 외형의 퇴장이 끝날 때까지 외부에서 기다릴 수 있게 한다.
    /// </summary>
    public IEnumerator PlayExit(float duration)
    {
        return _currentVisualController.FadeOut(duration);
    }
}