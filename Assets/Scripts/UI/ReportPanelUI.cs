using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using CivilSim.Core;
using CivilSim.UI;

public class ReportPanelUI : MonoBehaviour
{
    [Header("패널 루트 (자식 오브젝트)")]
    [SerializeField] private GameObject _panel;

    [Header("열기/닫기 버튼 (미할당 시 자동 탐색)")]
    [SerializeField] private Button _openButton;
    [SerializeField] private Button _closeButton;

    [Header("리포트 텍스트 (미할당 시 자동 탐색)")]
    [SerializeField] private TextMeshProUGUI _budgetText;
    [SerializeField] private TextMeshProUGUI _goalText;
    [SerializeField] private TextMeshProUGUI _resultText;
    [SerializeField] private TextMeshProUGUI _notificationText;

    [Header("텍스트 색상")]
    [SerializeField] private Color _resultWinColor = new Color(0.2f, 0.9f, 0.3f);
    [SerializeField] private Color _resultLoseColor = new Color(1.0f, 0.3f, 0.3f);
    [SerializeField] private Color _infoColor = new Color(0.8f, 0.9f, 1f);
    [SerializeField] private Color _warningColor = new Color(1.0f, 0.85f, 0.3f);
    [SerializeField] private Color _alertColor = new Color(1.0f, 0.45f, 0.45f);

    private bool _isOpen;
    private bool _hasBudgetReport;
    private bool _hasGoalProgress;
    private bool _hasResult;
    private bool _hasNotification;
    private BudgetReportEvent _lastBudgetReport;
    private GoalProgressEvent _lastGoalProgress;

    private void Awake()
    {
        AutoBindTexts();
        AutoBindButtons();
        BindButtonListeners();
        SetVisible(false);
        ApplyFallbackTexts();
    }

    private void Start()
    {
        if (_openButton == null || _closeButton == null)
        {
            AutoBindButtons();
            BindButtonListeners();
        }

        // Progression 값이 이미 생성된 경우를 대비해 초기 목표 텍스트를 갱신한다.
        var progression = GameManager.Instance?.Progression;
        if (progression != null)
        {
            _hasGoalProgress = true;
            _lastGoalProgress = new GoalProgressEvent
            {
                TargetPopulation = progression.TargetPopulation,
                CurrentPopulation = progression.CurrentPopulation,
                TargetBalance = progression.TargetBalance,
                CurrentBalance = progression.CurrentBalance,
                UseBalanceGoal = progression.UseBalanceGoal
            };
            UpdateGoalText(_lastGoalProgress);
        }
    }

    private void OnEnable()
    {
        PanelOpenCoordinator.PanelOpened += OnOtherPanelOpened;
        GameEventBus.Subscribe<BudgetReportEvent>(OnBudgetReport);
        GameEventBus.Subscribe<GoalProgressEvent>(OnGoalProgress);
        GameEventBus.Subscribe<GameWonEvent>(OnGameWon);
        GameEventBus.Subscribe<GameLostEvent>(OnGameLost);
        GameEventBus.Subscribe<NotificationEvent>(OnNotification);
    }

    private void OnDisable()
    {
        PanelOpenCoordinator.PanelOpened -= OnOtherPanelOpened;
        GameEventBus.Unsubscribe<BudgetReportEvent>(OnBudgetReport);
        GameEventBus.Unsubscribe<GoalProgressEvent>(OnGoalProgress);
        GameEventBus.Unsubscribe<GameWonEvent>(OnGameWon);
        GameEventBus.Unsubscribe<GameLostEvent>(OnGameLost);
        GameEventBus.Unsubscribe<NotificationEvent>(OnNotification);
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.escapeKey.wasPressedThisFrame && _isOpen)
            Hide();
    }

    private void OnDestroy()
    {
        if (_openButton != null)
            _openButton.onClick.RemoveListener(Toggle);
        if (_closeButton != null)
            _closeButton.onClick.RemoveListener(Hide);
    }

    public void Toggle() => SetVisible(!_isOpen);
    public void Show() => SetVisible(true);
    public void Hide() => SetVisible(false);
    public bool IsOpen => _isOpen;

    private void SetVisible(bool visible)
    {
        bool changed = _isOpen != visible;
        _isOpen = visible;
        if (_panel != null)
            _panel.SetActive(visible);

        if (visible && changed)
            PanelOpenCoordinator.NotifyOpened(this);

        if (visible)
        {
            if (_hasBudgetReport) UpdateBudgetText(_lastBudgetReport);
            if (_hasGoalProgress) UpdateGoalText(_lastGoalProgress);
        }

        if (visible)
            GameManager.Instance?.CancelAllModes();
    }

    private void OnOtherPanelOpened(object panelOwner)
    {
        if (ReferenceEquals(panelOwner, this)) return;
        if (_isOpen) Hide();
    }

    private void OnBudgetReport(BudgetReportEvent e)
    {
        _hasBudgetReport = true;
        _lastBudgetReport = e;
        UpdateBudgetText(e);
    }

    private void OnGoalProgress(GoalProgressEvent e)
    {
        _hasGoalProgress = true;
        _lastGoalProgress = e;
        UpdateGoalText(e);
    }

    private void OnGameWon(GameWonEvent e)
    {
        _hasResult = true;
        if (_resultText != null)
        {
            _resultText.text = $"승리 - {e.Year}년 {e.Month:D2}월 {e.Reason}";
            _resultText.color = _resultWinColor;
        }
    }

    private void OnGameLost(GameLostEvent e)
    {
        _hasResult = true;
        if (_resultText != null)
        {
            _resultText.text = $"패배 - {e.Year}년 {e.Month:D2}월 {e.Reason}";
            _resultText.color = _resultLoseColor;
        }
    }

    private void OnNotification(NotificationEvent e)
    {
        _hasNotification = true;
        if (_notificationText == null) return;

        _notificationText.text = e.Message;
        _notificationText.color = e.Type switch
        {
            NotificationType.Info => _infoColor,
            NotificationType.Warning => _warningColor,
            _ => _alertColor
        };
    }

    private void UpdateBudgetText(BudgetReportEvent e)
    {
        if (_budgetText == null) return;
        int net = e.Income - e.Expenditure;
        _budgetText.text =
            $"{e.Year}년 {e.Month:D2}월 결산 |수입 {e.Income:N0} | 지출 {e.Expenditure:N0} | 순이익 {FormatSigned(net)}| 잔액 {e.Balance:N0}";
    }

    private void UpdateGoalText(GoalProgressEvent e)
    {
        if (_goalText == null) return;

        string population = $"인구 {e.CurrentPopulation:N0}/{e.TargetPopulation:N0}";
        if (e.UseBalanceGoal)
            _goalText.text = $"목표 : {population}, 자금 {e.CurrentBalance:N0}/{e.TargetBalance:N0}";
        else
            _goalText.text = $"목표 : {population}";
    }

    private void ApplyFallbackTexts()
    {
        if (_budgetText != null && !_hasBudgetReport)
        {
            int currentMoney = GameManager.Instance?.Economy != null ? GameManager.Instance.Economy.Money : 0;
            _budgetText.text = $"최근 결산 없음 | 현재 잔액 {currentMoney:N0}";
        }

        if (_goalText != null && !_hasGoalProgress)
            _goalText.text = "목표 데이터 대기 중";

        if (_resultText != null && !_hasResult)
            _resultText.text = "결과 : 진행 중";

        if (_notificationText != null && !_hasNotification)
            _notificationText.text = "알림 없음";
    }

    private void AutoBindTexts()
    {
        if (_budgetText == null)
            _budgetText = FindTextByName("BudgetText") ?? FindTextByName("BudgetReportText") ?? FindTextByName("MonthlyReportText");

        if (_goalText == null)
            _goalText = FindTextByName("GoalText") ?? FindTextByName("ObjectiveText") ?? FindTextByName("GoalProgressText");

        if (_resultText == null)
            _resultText = FindTextByName("ResultText") ?? FindTextByName("OutcomeText");

        if (_notificationText == null)
            _notificationText = FindTextByName("NotificationText");
    }

    private void AutoBindButtons()
    {
        if (_openButton == null)
        {
            _openButton = FindButtonByName("ReportButton")
                ?? FindButtonByName("ReportUIButton")
                ?? FindButtonByName("ReportOpenButton")
                ?? FindButtonByName("ReportBuuton")
                ?? FindButtonByName("BudgetButton")
                ?? FindButtonByContains("report")
                ?? FindButtonByContains("budget");
        }

        if (_closeButton == null && _panel != null)
        {
            _closeButton = FindButtonInChildrenByName(_panel.transform, "Exit")
                ?? FindButtonInChildrenByName(_panel.transform, "Close")
                ?? FindButtonInChildrenByContains(_panel.transform, "exit")
                ?? FindButtonInChildrenByContains(_panel.transform, "close");
        }
    }

    private void BindButtonListeners()
    {
        if (_openButton != null)
        {
            _openButton.onClick.RemoveListener(Toggle);
            _openButton.onClick.AddListener(Toggle);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(Hide);
            _closeButton.onClick.AddListener(Hide);
        }
    }

    private static string FormatSigned(int value)
        => value >= 0 ? $"+{value:N0}" : value.ToString("N0");

    private TextMeshProUGUI FindTextByName(string objectName)
    {
        if (_panel == null) return null;
        var texts = _panel.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var t in texts)
        {
            if (t != null && t.gameObject.name == objectName)
                return t;
        }
        return null;
    }

    private static Button FindButtonByName(string objectName)
    {
        var go = GameObject.Find(objectName);
        return go != null ? go.GetComponent<Button>() : null;
    }

    private static Button FindButtonByContains(string textLower)
    {
        var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var button in buttons)
        {
            if (button == null || button.gameObject == null) continue;
            string nameLower = button.gameObject.name.ToLowerInvariant();
            if (nameLower.Contains(textLower))
                return button;
        }
        return null;
    }

    private static Button FindButtonInChildrenByName(Transform parent, string objectName)
    {
        if (parent == null) return null;
        var transforms = parent.GetComponentsInChildren<Transform>(true);
        foreach (var tr in transforms)
        {
            if (tr == null || tr.name != objectName) continue;
            var button = tr.GetComponent<Button>();
            if (button != null) return button;
        }
        return null;
    }

    private static Button FindButtonInChildrenByContains(Transform parent, string textLower)
    {
        if (parent == null) return null;
        var buttons = parent.GetComponentsInChildren<Button>(true);
        foreach (var button in buttons)
        {
            if (button == null || button.gameObject == null) continue;
            string nameLower = button.gameObject.name.ToLowerInvariant();
            if (nameLower.Contains(textLower))
                return button;
        }
        return null;
    }
}
