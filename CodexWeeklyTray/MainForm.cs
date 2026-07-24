namespace CodexWeeklyTray;

internal sealed class MainForm : Form
{
    private const int ContentMaximumWidth = 480;
    private readonly Func<Task> _refresh;
    private readonly Action _openCodex;
    private readonly Action _exitApplication;
    private readonly Panel _fiveHourPanel;
    private readonly Label _fiveHourValues;
    private readonly RemainingProgressBar _fiveHourRemainingBar;
    private readonly Label _weeklyValues;
    private readonly RemainingProgressBar _weeklyRemainingBar;
    private readonly Label _lastUpdated;
    private readonly Label _state;
    private readonly Label _logState;
    private bool _allowClose;
    private bool _isTopMost = true;

    public MainForm(Func<Task> refresh, Action openCodex, Action exitApplication)
    {
        _refresh = refresh;
        _openCodex = openCodex;
        _exitApplication = exitApplication;

        Text = "Codex Weekly Tray";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(480, 104);
        MinimumSize = new Size(480, 100);
        MaximizeBox = false;
        MinimizeBox = true;
        ShowInTaskbar = true;
        ApplyTopMost();

        var outerPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 2, 10, 2)
        };
        var contentPanel = new Panel
        {
            Anchor = AnchorStyles.Top
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var header = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 2,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 2)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var title = new Label
        {
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 16, 0),
            Text = "Codex利用状況"
        };
        _weeklyValues = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 0),
            Text = "取得中..."
        };
        header.Controls.Add(title, 0, 0);
        header.Controls.Add(_weeklyValues, 1, 0);
        layout.Controls.Add(header, 0, 0);

        _weeklyRemainingBar = new RemainingProgressBar
        {
            Dock = DockStyle.Top,
            Height = 8,
            Margin = new Padding(0)
        };
        layout.Controls.Add(_weeklyRemainingBar, 0, 1);

        _fiveHourPanel = CreateCompactLimitPanel("5時間枠", out _fiveHourValues, out _fiveHourRemainingBar);
        _fiveHourPanel.Visible = false;
        layout.Controls.Add(_fiveHourPanel, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(8, 0, 0, 0),
            WrapContents = false
        };
        var exitButton = CreateCompactButton("終了");
        exitButton.Click += (_, _) => _exitApplication();
        var openCodexButton = CreateCompactButton("Codex");
        openCodexButton.Click += (_, _) => _openCodex();
        var refreshButton = CreateCompactButton("更新");
        refreshButton.Click += async (_, _) => await _refresh();
        buttons.Controls.Add(refreshButton);
        buttons.Controls.Add(openCodexButton);
        buttons.Controls.Add(exitButton);

        var footer = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 2,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 0)
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var details = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        _lastUpdated = new Label { AutoSize = true, Margin = new Padding(0, 3, 10, 0), Text = "更新 未取得" };
        _logState = new Label { AutoSize = true, Margin = new Padding(0, 3, 10, 0), Text = "ログON" };
        _state = new Label { AutoSize = true, Margin = new Padding(0, 3, 0, 0), Text = "状態：取得中" };
        details.Controls.Add(_lastUpdated);
        details.Controls.Add(_logState);
        details.Controls.Add(_state);
        footer.Controls.Add(details, 0, 0);
        footer.Controls.Add(buttons, 1, 0);
        layout.Controls.Add(footer, 0, 3);

        contentPanel.Controls.Add(layout);
        outerPanel.Controls.Add(contentPanel);
        outerPanel.Resize += (_, _) => CenterContentPanel(outerPanel, contentPanel);
        Controls.Add(outerPanel);
        CenterContentPanel(outerPanel, contentPanel);
        FormClosing += OnFormClosing;
    }

    private static Button CreateCompactButton(string text) => new()
    {
        Text = text,
        AutoSize = false,
        Size = new Size(72, 24),
        Margin = new Padding(3, 0, 0, 0)
    };

    public void SetLoading() => UpdateUi(() => _state.Text = "状態：取得中");

    public void SetTopMostEnabled(bool enabled) => UpdateUi(() =>
    {
        _isTopMost = enabled;
        ApplyTopMost();
    });

    public void SetLogEnabled(bool enabled) => UpdateUi(() =>
    {
        _logState.Text = enabled ? "ログON" : "ログOFF";
    });

    public void ShowSnapshot(RateLimitSnapshot snapshot, DateTimeOffset updatedAt) => UpdateUi(() =>
    {
        SetLimitText(_weeklyValues, _weeklyRemainingBar, snapshot.Weekly);
        _fiveHourPanel.Visible = snapshot.FiveHour is not null;
        if (snapshot.FiveHour is not null)
        {
            SetLimitText(_fiveHourValues, _fiveHourRemainingBar, snapshot.FiveHour);
        }

        _lastUpdated.Text = $"更新 {updatedAt.ToLocalTime():HH:mm}";
        _state.Text = "状態：正常";
    });

    public void SetError(string message) => UpdateUi(() =>
    {
        _state.Text = $"状態：エラー - {TrimMessage(message)}";
        _fiveHourPanel.Visible = false;
    });

    public void ShowWindow()
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }

        Show();
        ApplyTopMost();
        Activate();
        BringToFront();
    }

    public void CloseForApplicationExit()
    {
        _allowClose = true;
        Close();
    }

    private static Panel CreateCompactLimitPanel(
        string title,
        out Label values,
        out RemainingProgressBar remainingBar)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 34,
            Margin = new Padding(0, 0, 0, 4)
        };
        var caption = new Label
        {
            AutoSize = true,
            Font = new Font(Control.DefaultFont, FontStyle.Bold),
            Location = new Point(0, 2),
            Text = title
        };
        values = new Label
        {
            AutoSize = true,
            Location = new Point(72, 2),
            Text = "取得中..."
        };
        remainingBar = new RemainingProgressBar
        {
            Dock = DockStyle.Bottom,
            Height = 8
        };
        panel.Controls.Add(caption);
        panel.Controls.Add(values);
        panel.Controls.Add(remainingBar);
        return panel;
    }

    private static void CenterContentPanel(Panel outerPanel, Panel contentPanel)
    {
        if (outerPanel.IsDisposed || contentPanel.IsDisposed)
        {
            return;
        }

        int availableWidth = Math.Max(0, outerPanel.ClientSize.Width - outerPanel.Padding.Horizontal);
        int contentWidth = Math.Min(ContentMaximumWidth, availableWidth);
        contentPanel.Size = new Size(contentWidth, Math.Max(0, outerPanel.ClientSize.Height - outerPanel.Padding.Vertical));
        contentPanel.Location = new Point(
            outerPanel.Padding.Left + Math.Max(0, (availableWidth - contentWidth) / 2),
            outerPanel.Padding.Top);
    }

    private static void SetLimitText(Label label, RemainingProgressBar remainingBar, RateLimitWindow limit)
    {
        string reset = limit.ResetsAt is null
            ? "リセット：時刻情報なし"
            : $"リセット {limit.ResetsAt.Value.ToLocalTime():MM/dd HH:mm}";
        label.Text = $"残量{limit.RemainingPercent:0.#}%（使用済み{limit.UsedPercent:0.#}%）   {reset}";
        remainingBar.RemainingPercent = limit.RemainingPercent;
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        _exitApplication();
    }

    private void UpdateUi(Action update)
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(update);
            return;
        }

        update();
    }

    private void ApplyTopMost()
    {
        TopMost = _isTopMost;
    }

    private static string TrimMessage(string message) =>
        string.IsNullOrWhiteSpace(message) ? "詳細不明" : message[..Math.Min(message.Length, 120)];

    private sealed class RemainingProgressBar : Panel
    {
        private readonly Panel _remaining = new() { Dock = DockStyle.Left };
        private double _remainingPercent;

        public RemainingProgressBar()
        {
            BackColor = Color.FromArgb(224, 224, 224);
            Controls.Add(_remaining);
            UpdateRemainingWidth();
        }

        public double RemainingPercent
        {
            set
            {
                _remainingPercent = Math.Clamp(value, 0, 100);
                _remaining.BackColor = GetRemainingColor(_remainingPercent);
                UpdateRemainingWidth();
            }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateRemainingWidth();
        }

        private void UpdateRemainingWidth()
        {
            _remaining.Width = (int)Math.Round(ClientSize.Width * (_remainingPercent / 100d));
        }

        private static Color GetRemainingColor(double remainingPercent)
        {
            Color blue = Color.FromArgb(25, 118, 210);
            Color lightBlue = Color.FromArgb(30, 136, 229);
            Color green = Color.FromArgb(67, 160, 71);
            Color yellow = Color.FromArgb(251, 192, 45);
            Color red = Color.FromArgb(211, 47, 47);

            if (remainingPercent >= 60)
            {
                return Blend(lightBlue, blue, (remainingPercent - 60) / 40d);
            }

            if (remainingPercent >= 40)
            {
                return Blend(green, lightBlue, (remainingPercent - 40) / 20d);
            }

            if (remainingPercent >= 20)
            {
                return Blend(yellow, green, (remainingPercent - 20) / 20d);
            }

            return Blend(red, yellow, remainingPercent / 20d);
        }

        private static Color Blend(Color from, Color to, double progress)
        {
            progress = Math.Clamp(progress, 0, 1);
            return Color.FromArgb(
                (int)Math.Round(from.R + ((to.R - from.R) * progress)),
                (int)Math.Round(from.G + ((to.G - from.G) * progress)),
                (int)Math.Round(from.B + ((to.B - from.B) * progress)));
        }
    }
}
