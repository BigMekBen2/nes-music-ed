using System.Windows;
using System.Windows.Controls;

namespace NESMusicEditor;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    // All toolbar/menu/button handlers are empty stubs — no functionality yet.
    private void Menu_Stub(object sender, RoutedEventArgs e) { }
    private void Toolbar_Stub(object sender, RoutedEventArgs e) { }
}
