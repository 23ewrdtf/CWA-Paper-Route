using System.Globalization;

using PaperRoute.Config;
using PaperRoute.Smtp;

namespace PaperRoute.App;

/// <summary>Port, login, password and recipient, plus "Copy CWA settings".</summary>
internal sealed class SettingsForm : Form
{
    private readonly AppConfig _config;
    private readonly Func<AppConfig, Task<StartResult>> _apply;
    private readonly NumericUpDown _port = new() { Minimum = AppConfig.MinPort, Maximum = AppConfig.MaxPort, Width = 100 };
    private readonly TextBox _login = new() { Width = 260 };
    private readonly TextBox _password = new() { Width = 260, UseSystemPasswordChar = true };
    private readonly TextBox _recipient = new() { Width = 260 };
    private readonly Label _error = new() { AutoSize = true, ForeColor = Color.Firebrick, MaximumSize = new Size(460, 0) };
    private readonly Button _save = new() { Text = "Save", AutoSize = true };

    public SettingsForm(AppConfig config, Func<AppConfig, Task<StartResult>> apply)
    {
        _config = config;
        _apply = apply;
        Text = $"{AppInfo.Name} settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        _port.Value = AppConfig.ParseListen(config.Listen).Port;
        _login.Text = config.AuthLogin;
        _password.Text = config.AuthPassword;
        _recipient.Text = config.Recipient;

        Controls.Add(BuildLayout());
    }

    private TableLayoutPanel BuildLayout()
    {
        var showPassword = new CheckBox { Text = "Show", AutoSize = true };
        showPassword.CheckedChanged += (_, _) => _password.UseSystemPasswordChar = !showPassword.Checked;
        var copyPassword = new Button { Text = "Copy", AutoSize = true };
        copyPassword.Click += (_, _) => CopyToClipboard(_password.Text);
        var regenerate = new Button { Text = "New password", AutoSize = true };
        regenerate.Click += (_, _) => RegeneratePassword();
        var copyAll = new Button { Text = "Copy CWA settings", AutoSize = true };
        copyAll.Click += (_, _) => CopyCwaSettings();
        // The window is shown modeless (Show, not ShowDialog), so DialogResult alone would not close it.
        var cancel = new Button { Text = "Cancel", AutoSize = true };
        cancel.Click += (_, _) => Close();
        _save.Click += (_, _) => _ = SaveAsync();
        AcceptButton = _save;
        CancelButton = cancel;

        var grid = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };
        AddRow(grid, "Port:", _port);
        AddRow(grid, "Login:", _login);
        AddRow(grid, "Password:", Row(_password, showPassword, copyPassword, regenerate));
        AddRow(grid, "eReader e-mail:", _recipient);
        var help = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(460, 0),
            Text = "In CWA: Admin > Basic Configuration > E-mail Server - use \"Copy CWA settings\" and paste the values. "
                + "Put the eReader e-mail in your CWA user profile. EPUB books are converted with Calibre.",
        };
        grid.Controls.Add(help, 0, grid.RowCount);
        grid.SetColumnSpan(help, 2);
        grid.RowCount++;
        grid.Controls.Add(_error, 0, grid.RowCount);
        grid.SetColumnSpan(_error, 2);
        grid.RowCount++;
        var buttons = Row(copyAll, _save, cancel);
        grid.Controls.Add(buttons, 0, grid.RowCount);
        grid.SetColumnSpan(buttons, 2);
        return grid;
    }

    private static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        row.Controls.AddRange(controls);
        return row;
    }

    private static void AddRow(TableLayoutPanel grid, string label, Control control)
    {
        grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, grid.RowCount);
        grid.Controls.Add(control, 1, grid.RowCount);
        grid.RowCount++;
    }

    private AppConfig Edited()
    {
        var address = AppConfig.ParseListen(_config.Listen).Address;
        var listen = new System.Net.IPEndPoint(address, (int)_port.Value).ToString();
        return _config with
        {
            Listen = listen,
            AuthLogin = _login.Text.Trim(),
            AuthPassword = _password.Text,
            Recipient = _recipient.Text.Trim(),
        };
    }

    private bool CopyToClipboard(string text)
    {
        _error.ForeColor = Color.Firebrick;
        if (text.Length == 0)
        {
            _error.Text = "Nothing to copy";
            return false;
        }

        try
        {
            Clipboard.SetText(text);
            return true;
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            _error.Text = "The clipboard is busy - try again";
            return false;
        }
    }

    private static string? Validate(AppConfig config)
    {
        if (config.AuthLogin.Length == 0)
        {
            return "Login can't be empty";
        }

        if (config.AuthPassword.Length < 12)
        {
            return "Use a password of at least 12 characters (\"New password\" makes a strong one)";
        }

        return config.Recipient.Contains('@', StringComparison.Ordinal) ? null : "The eReader e-mail must look like name@something";
    }

    private void RegeneratePassword()
    {
        var answer = MessageBox.Show(
            this,
            "CWA will stop working until you paste the new password into its e-mail settings. Continue?",
            AppInfo.Name,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (answer == DialogResult.Yes)
        {
            _password.Text = ConfigStore.NewPassword();
        }
    }

    private void CopyCwaSettings()
    {
        var config = Edited();
        var error = Validate(config);
        if (error is not null)
        {
            _error.Text = error;
            return;
        }

        if (CopyToClipboard(CwaSettingsText.Build(config)))
        {
            _error.ForeColor = Color.DarkGreen;
            _error.Text = "Copied. Save here too if you changed anything.";
        }
    }

    private async Task SaveAsync()
    {
        _error.ForeColor = Color.Firebrick;
        var config = Edited();
        var error = Validate(config);
        if (error is not null)
        {
            _error.Text = error;
            return;
        }

        _save.Enabled = false;
        try
        {
            var result = await _apply(config).ConfigureAwait(true);
            if (result.Ok)
            {
                Close();
                return;
            }

            _error.Text = result.Error ?? string.Create(CultureInfo.InvariantCulture, $"Could not use port {_port.Value}");
        }
        finally
        {
            _save.Enabled = true;
        }
    }
}
