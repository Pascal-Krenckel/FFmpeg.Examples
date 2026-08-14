using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace _9._SimpleVideoPlayer;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        string[] args = Environment.GetCommandLineArgs();
        if (args.Length != 2)
        {
            MessageBox.Show("You need to provide the media file as an argument.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
        else if (!File.Exists(args[1]))
        {
            MessageBox.Show("File not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
        else
        {
            videoPlayer.OpenAsync(args[1]);
        }
    }
}