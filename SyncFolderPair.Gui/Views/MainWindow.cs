using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using SyncFolderPair.Gui.ViewModels;

namespace SyncFolderPair.Gui.Views;

public sealed class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        Title = "SyncFolderPair GUI";
        Width = 900;
        Height = 600;
        MinWidth = 700;
        MinHeight = 500;

        DataContext = viewModel;
        Content = BuildLayout();
    }

    UIElement BuildLayout()
    {
        var root = new Grid
        {
            Margin = new Thickness(12),
        };

        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var pairPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 10),
        };

        pairPanel.Children.Add(new TextBlock
        {
            Text = "ペア",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        });

        var pairCombo = new ComboBox
        {
            Width = 300,
            DisplayMemberPath = ".",
            Margin = new Thickness(0, 0, 8, 0),
        };
        pairCombo.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(MainViewModel.PairNames)));
        pairCombo.SetBinding(Selector.SelectedItemProperty, new Binding(nameof(MainViewModel.SelectedPairName)) { Mode = BindingMode.TwoWay });
        pairPanel.Children.Add(pairCombo);

        pairPanel.Children.Add(CreateButton("更新", nameof(MainViewModel.RefreshCommand)));
        pairPanel.Children.Add(CreateButton("同期", nameof(MainViewModel.SyncCommand)));
        pairPanel.Children.Add(CreateButton("プレビュー", nameof(MainViewModel.PreviewCommand)));
        pairPanel.Children.Add(CreateButton("中止", nameof(MainViewModel.CancelCommand)));

        Grid.SetRow(pairPanel, 0);
        root.Children.Add(pairPanel);

        var status = new TextBlock
        {
            Margin = new Thickness(0, 0, 0, 10),
        };
        status.SetBinding(TextBlock.TextProperty, new Binding(nameof(MainViewModel.StatusMessage)));
        Grid.SetRow(status, 1);
        root.Children.Add(status);

        var logList = new ListBox();
        logList.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(MainViewModel.Logs)));
        Grid.SetRow(logList, 2);
        root.Children.Add(logList);

        return root;
    }

    static Button CreateButton(string text, string commandPropertyName)
    {
        var button = new Button
        {
            Content = text,
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(12, 4, 12, 4),
        };
        button.SetBinding(Button.CommandProperty, new Binding(commandPropertyName));
        return button;
    }
}
