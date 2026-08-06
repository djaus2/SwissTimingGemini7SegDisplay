using System.Windows;
using SwissTimingDisplay.Controls;
using SwissTimingDisplay.ViewModels;

namespace SwissTimingDisplay
{
    public partial class SiriccoWindowControlled : Window
    {
        public SiriccoWindowControlled() : this(MainViewModel.SharedInstance, null)
        {
        }

        public SiriccoWindowControlled(MainViewModel vm, MainWindow? mainWindow = null)
        {
            InitializeComponent();
            LayoutRoot.Children.Add(new SiriccoWindGaugeControl(vm, mainWindow));
        }
    }
}
