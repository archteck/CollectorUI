using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CollectorUI.Services;
using System.Collections.ObjectModel;
using Avalonia.Input;

namespace CollectorUI.Views.Dialogs;

public partial class SelectRecentDialog : Window
{
    private readonly ObservableCollection<string> _items = [];

    public SelectRecentDialog()
    {
        InitializeComponent();
        LoadItems();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void LoadItems()
    {
        _items.Clear();
        foreach (var path in SelectionService.GetRecentSolutions(20))
        {
            _items.Add(path);
        }

        var list = this.FindControl<ListBox>("RecentList");
        if (list is not null)
        {
            list.ItemsSource = _items;
            if (_items.Count > 0)
            {
                list.SelectedIndex = 0;
            }
        }
    }

    private void OnOpen(object? sender, RoutedEventArgs e)
    {
        var list = this.FindControl<ListBox>("RecentList");
        if (list?.SelectedItem is string path && !string.IsNullOrWhiteSpace(path))
        {
            Close(path);
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);

    private void OnListDoubleTapped(object? sender, TappedEventArgs e) => OnOpen(sender, e);
}
