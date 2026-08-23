using System;
using UnityEngine;

/// <summary>
/// 증강 시스템의 Model, View, Presenter를 조립하는 Composition Root다.
/// 증강 선택 규칙이나 UI 표시를 직접 처리하지 않는다.
/// </summary>
public class Upgrader : MonoBehaviour
{
    [Header("증강 구성")]

    // 게임에서 등장할 수 있는 전체 증강 데이터 저장소
    [SerializeField] private UpgradeMap _upgradeMap;

    // 증강 선택 화면 전체를 담당하는 View
    [SerializeField] private UpgradeSelectionView _upgradeSelectionView;

    // 전체 증강 중 한 번에 화면에 표시할 후보 개수
    [Min(1)]
    [SerializeField] private int _displayChoiceCount = 3;

    // StageManager가 전달한 현재 스테이지의 GameManager
    private GameManager _gameManager;

    // StageManager가 생성한 현재 플레이어
    private PlayerController _playerController;

    // 현재 플레이어가 사용하는 런타임 무기 관리자
    private Weapon _weapon;

    // 증강 데이터 추첨과 효과 적용을 담당하는 Model
    private UpgradeModel _model;

    // Model과 View의 흐름을 연결하는 Presenter
    private UpgradePresenter _presenter;

    // StageManager에서 런타임 객체를 전달받았는지 확인한다.
    private bool _isInitialized;

    /// <summary>
    /// StageManager가 생성한 런타임 객체로 증강 MVP를 조립한다.
    /// </summary>
    public void Initialize(
        GameManager gameManager,
        PlayerController playerController,
        Weapon weapon)
    {
        _gameManager = gameManager ??
            throw new ArgumentNullException(nameof(gameManager));

        _playerController = playerController ??
            throw new ArgumentNullException(nameof(playerController));

        _weapon = weapon ??
            throw new ArgumentNullException(nameof(weapon));

        if (_upgradeMap == null)
        {
            throw new InvalidOperationException(
                $"{name}의 UpgradeMap이 연결되지 않았습니다.");
        }

        if (_upgradeSelectionView == null)
        {
            throw new InvalidOperationException(
                $"{name}의 UpgradeSelectionView가 연결되지 않았습니다.");
        }

        if (_displayChoiceCount <= 0)
        {
            throw new InvalidOperationException(
                $"{name}의 DisplayChoiceCount는 1 이상이어야 합니다.");
        }

        // 재초기화될 경우 기존 View 이벤트 구독을 먼저 해제한다.
        _presenter?.Dispose();

        _model = new UpgradeModel(
            _upgradeMap,
            _playerController,
            _weapon,
            _gameManager);

        _presenter = new UpgradePresenter(
            _model,
            _upgradeSelectionView,
            _gameManager,
            _displayChoiceCount);

        // 스테이지 시작 시 증강 UI가 보이지 않도록 초기화한다.
        _upgradeSelectionView.Hide();

        _isInitialized = true;
    }

    /// <summary>
    /// 첫 번째 증강 선택 화면을 연다.
    /// GameManager가 게이지를 모두 채웠을 때 호출한다.
    /// </summary>
    public void UpgradeSelectFirst()
    {
        EnsureInitialized();

        // 증강 UI와 TAB 무기 패널이 동시에 보이지 않도록 먼저 닫는다.
        _weapon.CloseWeaponSlotPanel();

        // 후보 추첨과 화면 표시는 Presenter에 요청한다.
        _presenter.Open();
    }

    /// <summary>
    /// StageManager의 런타임 초기화가 완료됐는지 확인한다.
    /// </summary>
    private void EnsureInitialized()
    {
        if (!_isInitialized ||
            _model == null ||
            _presenter == null)
        {
            throw new InvalidOperationException(
                "Upgrader가 아직 초기화되지 않았습니다.");
        }
    }

    private void OnDestroy()
    {
        // View 이벤트에 Presenter가 남지 않도록 구독을 해제한다.
        _presenter?.Dispose();
    }
}