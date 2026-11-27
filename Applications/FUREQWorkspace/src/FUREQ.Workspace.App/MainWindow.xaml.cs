using FUREQ.Workspace.App.Presentation;

namespace FUREQ.Workspace.App;

public partial class MainWindow : System.Windows.Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
