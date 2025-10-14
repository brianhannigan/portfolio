using System.Windows;
using System.Windows.Input;
namespace SurgicalVisualization
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var vm = new ViewModels.MainViewModel();
            this.DataContext = vm;
            vm.AttachViewport(Viewport);

            // Ensure the viewport has keyboard/mouse focus so gestures are active
            Loaded += (_, __) =>
            {
                Viewport.Focus();          // activate camera controller gestures
                Viewport.ZoomExtents();    // fit current content (also runs on first load)
            };


        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.HandleGameKey(e);
            }
        }
    }
}