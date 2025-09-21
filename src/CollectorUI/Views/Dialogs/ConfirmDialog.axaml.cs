using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CollectorUI.Views.Dialogs;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string title, string message, string yesText = "Yes", string noText = "No")
    {
        InitializeComponent();
        Title = title;
        var msg = this.FindControl<TextBlock>("MessageText");
        var yesBtn = this.FindControl<Button>("YesButton");
        var noBtn = this.FindControl<Button>("NoButton");

        if (msg is not null)
        {
            msg.Text = message;
        }

        if (yesBtn is not null)
        {
            yesBtn.Content = yesText;
        }

        if (noBtn is not null)
        {
            noBtn.Content = noText;
        }
    }

    private void OnYes(object? sender, RoutedEventArgs e) => Close(true);
    private void OnNo(object? sender, RoutedEventArgs e) => Close(false);
}
