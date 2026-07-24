namespace CodexWeeklyTray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly CodexAppServerClient _client = new();
    private readonly ToolStripMenuItem _weeklyStatusItem;
    private readonly ToolStripMenuItem _weeklyResetItem;
    private readonly ToolStripMenuItem _fiveHourStatusItem;
    private readonly ToolStripMenuItem _fiveHourResetItem;
    private System.Diagnostics.Process? _openedCodexProcess;
    private readonly ToolStripMenuItem _topMostItem;
    private readonly ToolStripMenuItem _logEnabledItem;
    private readonly ContextMenuStrip _menu;
    private readonly MainForm _mainForm;
    private CancellationTokenSource? _refreshCts;
    private Icon? _currentIcon;
    private RateLimitSnapshot? _latestSnapshot;
    private DateTimeOffset? _lastUpdated;
    private bool _isExiting;

    public TrayApplicationContext()
    {
        _weeklyStatusItem = new ToolStripMenuItem("週間利用状況：取得中…") { Enabled = false };
        _weeklyResetItem = new ToolStripMenuItem("週間リセット：取得中…") { Enabled = false };
        _fiveHourStatusItem = new ToolStripMenuItem { Enabled = false, Visible = false };
        _fiveHourResetItem = new ToolStripMenuItem { Enabled = false, Visible = false };

        var refreshItem = new ToolStripMenuItem("今すぐ更新");
        refreshItem.Click += async (_, _) => await RefreshAsync(showErrorBalloon: true);

        var openCodexItem = new ToolStripMenuItem("Codexを開く");
        openCodexItem.Click += (_, _) => OpenCodex();

        var showWindowItem = new ToolStripMenuItem("ウィンドウを開く");
        showWindowItem.Click += (_, _) => ShowMainWindow();

        _topMostItem = new ToolStripMenuItem("常に手前に表示")
        {
            Checked = true,
            CheckOnClick = true
        };

        _logEnabledItem = new ToolStripMenuItem("ログを記録する")
        {
            Checked = AppLog.IsEnabled,
            CheckOnClick = true
        };

        var exitItem = new ToolStripMenuItem("終了");
        exitItem.Click += (_, _) => ExitApplication();

        _menu = new ContextMenuStrip();
        _menu.Items.AddRange([
            _fiveHourStatusItem,
            _fiveHourResetItem,
            _weeklyStatusItem,
            _weeklyResetItem,
            new ToolStripSeparator(),
            refreshItem,
            showWindowItem,
            openCodexItem,
            _topMostItem,
            _logEnabledItem,
            new ToolStripSeparator(),
            exitItem
        ]);

        _currentIcon = TrayIconRenderer.CreateErrorIcon();
        _notifyIcon = new NotifyIcon
        {
            Icon = _currentIcon,
            Text = "Codex週間利用状況：取得中",
            Visible = true,
            ContextMenuStrip = _menu
        };
        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();

        _mainForm = new MainForm(
            refresh: () => RefreshAsync(showErrorBalloon: true),
            openCodex: OpenCodex,
            exitApplication: ExitApplication);
        _topMostItem.CheckedChanged += (_, _) => _mainForm.SetTopMostEnabled(_topMostItem.Checked);
        _logEnabledItem.CheckedChanged += (_, _) => SetLogEnabled(_logEnabledItem.Checked);
        _mainForm.SetLogEnabled(_logEnabledItem.Checked);
        _mainForm.Show();

        _timer = new System.Windows.Forms.Timer
        {
            Interval = (int)TimeSpan.FromMinutes(10).TotalMilliseconds,
            Enabled = true
        };
        _timer.Tick += async (_, _) => await RefreshAsync(showErrorBalloon: false);

        _ = RefreshAsync(showErrorBalloon: false);
    }

    private async Task RefreshAsync(bool showErrorBalloon)
    {
        if (_isExiting)
        {
            return;
        }

        if (_refreshCts is not null)
        {
            AppLog.Info("refresh", "Skipped refresh because a previous refresh is still running.");
            return;
        }

        var refreshCts = new CancellationTokenSource();
        _refreshCts = refreshCts;
        try
        {
            AppLog.Info("refresh", "Starting refresh.");
            _weeklyStatusItem.Text = "週間利用状況：更新中…";
            _mainForm.SetLoading();
            RateLimitSnapshot snapshot = await _client.ReadWeeklyRateLimitAsync(refreshCts.Token);
            if (_isExiting)
            {
                return;
            }

            UpdateDisplay(snapshot);
            AppLog.Info("refresh", "Refresh completed.");
        }
        catch (Exception ex)
        {
            if (_isExiting)
            {
                return;
            }

            AppLog.Error("refresh", "Refresh failed.", ex);
            UpdateError(ex.Message, showErrorBalloon);
        }
        finally
        {
            if (ReferenceEquals(_refreshCts, refreshCts))
            {
                _refreshCts = null;
            }

            refreshCts.Dispose();
        }
    }

    private void SetLogEnabled(bool enabled)
    {
        AppLog.SetEnabled(enabled);
        _mainForm.SetLogEnabled(enabled);
        if (enabled)
        {
            AppLog.Info("logging", "Logging enabled.");
        }
    }

    private void UpdateDisplay(RateLimitSnapshot snapshot)
    {
        _latestSnapshot = snapshot;
        _lastUpdated = DateTimeOffset.Now;
        UpdateWindowDisplay("週間", snapshot.Weekly, _weeklyStatusItem, _weeklyResetItem);

        RateLimitWindow? fiveHour = snapshot.FiveHour;
        _fiveHourStatusItem.Visible = fiveHour is not null;
        _fiveHourResetItem.Visible = fiveHour is not null;
        if (fiveHour is not null)
        {
            UpdateWindowDisplay("5時間", fiveHour, _fiveHourStatusItem, _fiveHourResetItem);
        }

        ReplaceIcon(TrayIconRenderer.CreatePercentIcon(snapshot.Weekly.RemainingPercent));
        _notifyIcon.Text = TrimTooltip(fiveHour is null
            ? $"Codex 週残{snapshot.Weekly.RemainingPercent:0.#}%"
            : $"Codex 5h残{fiveHour.RemainingPercent:0.#}% / 週残{snapshot.Weekly.RemainingPercent:0.#}%");
        _mainForm.ShowSnapshot(snapshot, _lastUpdated.Value);
    }

    private static void UpdateWindowDisplay(
        string label,
        RateLimitWindow limit,
        ToolStripMenuItem statusItem,
        ToolStripMenuItem resetItem)
    {
        statusItem.Text = $"{label}残量：{limit.RemainingPercent:0.#}%（使用済み {limit.UsedPercent:0.#}%）";
        resetItem.Text = limit.ResetsAt is null
            ? $"{label}リセット：時刻情報なし"
            : $"{label}リセット：{limit.ResetsAt.Value.ToLocalTime():yyyy/MM/dd HH:mm}";
    }

    private void UpdateError(string message, bool showBalloon)
    {
        _weeklyStatusItem.Text = "週間利用状況：取得失敗";
        _weeklyResetItem.Text = "右クリック → 今すぐ更新";
        _fiveHourStatusItem.Visible = false;
        _fiveHourResetItem.Visible = false;
        ReplaceIcon(TrayIconRenderer.CreateErrorIcon());
        _notifyIcon.Text = "Codex週間利用状況：取得失敗";
        _mainForm.SetError(message);

        if (showBalloon)
        {
            _notifyIcon.BalloonTipTitle = "Codex Weekly Tray";
            _notifyIcon.BalloonTipText = message;
            _notifyIcon.BalloonTipIcon = ToolTipIcon.Warning;
            _notifyIcon.ShowBalloonTip(5000);
        }
    }

    private void ReplaceIcon(Icon next)
    {
        Icon? previous = _currentIcon;
        _currentIcon = next;
        _notifyIcon.Icon = next;
        previous?.Dispose();
    }

    private static string TrimTooltip(string text) => text.Length <= 63 ? text : text[..63];

    private void ShowMainWindow()
    {
        if (_isExiting)
        {
            return;
        }

        _mainForm.ShowWindow();
        if (_latestSnapshot is not null && _lastUpdated is not null)
        {
            _mainForm.ShowSnapshot(_latestSnapshot, _lastUpdated.Value);
        }
    }

    private void OpenCodex()
    {
        try
        {
            if (IsTrayLaunchedCodexRunning())
            {
                _notifyIcon.ShowBalloonTip(
                    2500,
                    "Codexは起動済みです",
                    "既にトレイアプリから開いたCodexがあります。",
                    ToolTipIcon.Info);
                return;
            }

            _openedCodexProcess?.Dispose();
            _openedCodexProcess = null;

            string codexCliPath = CodexAppServerClient.ResolveCodexCliPath();

            string workingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!Directory.Exists(workingDirectory))
            {
                workingDirectory = Path.GetDirectoryName(codexCliPath) ?? string.Empty;
            }

            if (!Directory.Exists(workingDirectory))
            {
                throw new InvalidOperationException("Codex CLIの作業ディレクトリを解決できません。");
            }

            _openedCodexProcess = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = codexCliPath,
                WorkingDirectory = workingDirectory,
                UseShellExecute = true
            }) ?? throw new InvalidOperationException("Codex CLIを起動できませんでした。");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Codexを開けませんでした", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private bool IsTrayLaunchedCodexRunning()
    {
        try
        {
            return _openedCodexProcess is not null && !_openedCodexProcess.HasExited;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private void ExitApplication()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        _timer.Stop();
        _refreshCts?.Cancel();
        _mainForm.CloseForApplicationExit();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _currentIcon?.Dispose();
        _openedCodexProcess?.Dispose();
        _currentIcon = null;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _isExiting = true;
            _timer.Dispose();
            _mainForm.Dispose();
            _notifyIcon.Dispose();
            _menu.Dispose();
            _currentIcon?.Dispose();
            _openedCodexProcess?.Dispose();
            _refreshCts?.Cancel();
        }
        base.Dispose(disposing);
    }
}
