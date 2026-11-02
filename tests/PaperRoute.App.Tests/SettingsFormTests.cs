using PaperRoute.App;
using PaperRoute.Config;
using PaperRoute.Smtp;

namespace PaperRoute.App.Tests;

public sealed class SettingsFormTests
{
    [Fact]
    public void Cancel_closes_the_window() => RunOnStaThread(() =>
    {
        using var form = new SettingsForm(
            new AppConfig(AuthPassword: "Password1234"),
            _ => Task.FromResult(new StartResult(true, null)));
        form.Show();

        ((Button)form.CancelButton!).PerformClick();
        Application.DoEvents();

        Assert.False(form.Visible);
    });

    [Fact]
    public void Copy_with_an_empty_password_does_not_crash() => RunOnStaThread(() =>
    {
        using var form = new SettingsForm(
            new AppConfig(AuthPassword: "Password1234"),
            _ => Task.FromResult(new StartResult(true, null)));
        form.Show();
        Find<TextBox>(form, t => t.UseSystemPasswordChar).Text = string.Empty;

        var error = Record.Exception(() => Find<Button>(form, b => string.Equals(b.Text, "Copy", StringComparison.Ordinal)).PerformClick());

        Assert.Null(error);
    });

    private static T Find<T>(Control root, Func<T, bool> match)
        where T : Control => Descendants(root).OfType<T>().First(match);

    private static IEnumerable<Control> Descendants(Control control) =>
        control.Controls.Cast<Control>().SelectMany(c => Descendants(c).Prepend(c));

    private static void RunOnStaThread(Action test)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                test();
            }
#pragma warning disable CA1031 // Any failure is captured and rethrown on the test thread below.
            catch (Exception e)
#pragma warning restore CA1031
            {
                failure = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            throw new InvalidOperationException("UI test failed", failure);
        }
    }
}
