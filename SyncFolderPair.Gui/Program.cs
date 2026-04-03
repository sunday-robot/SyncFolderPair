using System.Windows;
using SyncFolderPair.Gui.ViewModels;
using SyncFolderPair.Gui.Views;

namespace SyncFolderPair.Gui;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var app = new Application();
        using var vm = new MainViewModel();
        var window = new MainWindow(vm);
        app.Run(window);
    }
}
