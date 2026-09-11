using System;
using UnityEngine;

// 상자의 피격 및 개봉 진행 상태다.
public enum TreasureBoxState
{
    Waiting,         // 초기화 전 또는 낙하·착지 연출 중
    ReceivingDamage, // 피해를 누적하는 중
    Charge,          // 추가 타격을 기다리는 중
    Opened           // 개봉이 확정된 상태
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
    int _accumulatedDamage;

    // 공통 상자 본체의 피격 영역이다.
    BoxCollider2D _hitCollider;

    // 현재 생성되어 있는 등급별 외형이다.
    GameObject _currentVisual;

    // 초기화가 완료되었는지 확인한다.
    bool _isInitialized;

    // 기존 IDamageable 규약에 맞춘 피격 알림이다.
    public event Action<int> Damaged;

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

    // 등급 번호는 1부터, 배열 인덱스는 0부터 시작한다.
    TreasureBoxGradeData CurrentData =>
        _gradeDatas[(int)_currentGrade - 1];

    private void Awake()
    {
        // 낙하 연출이 끝나기 전에는 공격받지 않는다.
        _hitCollider = GetComponent<BoxCollider2D>();
        _hitCollider.isTrigger = true;
        _hitCollider.enabled = false;
    }

    // 외부에서 추첨한 최종 등급을 전달한다.
    // 최종 등급과 관계없이 외형은 항상 Wood부터 시작한다.
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
    }

    // 낙하와 두 번의 반동이 모두 끝났을 때 호출한다.
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
    public void TakeHit(int damage)
    {
        // 초기화 전, 낙하 중, 개봉 후, 유효하지 않은 피해는 무시한다.
        if (!_isInitialized ||
            _state == TreasureBoxState.Waiting ||
            _state == TreasureBoxState.Opened ||
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
        int remainingDamage =
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

    // Charge 상태에서 받은 추가 타격의 결과를 처리한다.
    private void ResolveChargedHit()
    {
        if (_currentGrade < _finalGrade)
        {
            // 최종 등급까지 반드시 한 단계씩 올라간다.
            _currentGrade =
                (TreasureBoxGrade)((int)_currentGrade + 1);

            // 이전 등급의 피해와 추가 타격 피해는 다음 등급에 넘기지 않는다.
            _accumulatedDamage = 0;

            ChangeVisual();
            ChangeState(TreasureBoxState.ReceivingDamage);
            return;
        }

        // 먼저 피격을 막아 보상 요청이 중복 발생하지 않게 한다.
        _hitCollider.enabled = false;
        ChangeState(TreasureBoxState.Opened);

        // 실제 보상이 지급되었다는 뜻이 아니라 지급을 요청하는 알림이다.
        Opened?.Invoke(CurrentData);
    }

    // 공통 본체는 유지하고 등급별 외형만 교체한다.
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
    }

    // 상태를 변경하고 연출 담당 코드에 알린다.
    private void ChangeState(TreasureBoxState nextState)
    {
        if (_state == nextState)
        {
            return;
        }

        _state = nextState;
        StateChanged?.Invoke(_state);

        Debug.Log($"{name}: {_currentGrade} / {_state}", this);
    }

    // 배열 누락이나 순서 오류를 초기화 시점에 확인한다.
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
}