using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using SwissTimingDisplay.Controls;
using SwissTimingDisplay.ViewModels;

namespace SwissTimingDisplay
{
    /// <summary>
    /// Interaction logic for AthsSprintWindow.xaml
    /// </summary>
    public partial class AthsSprintWindow : Window
    {
        private readonly MainViewModel _vm;

        public AthsSprintWindow() : this(MainViewModel.SharedInstance)
        {
        }

        public AthsSprintWindow(MainViewModel vm)
        {
            _vm = vm;
            InitializeComponent();
            DataContext = _vm;
            displayControl.SetUp(_vm, null,this);

            gauge.SetUp(vm, null, this);
            // windSpeedPanel visibility is now bound to MainViewModel.ShowWindGauge
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            _vm.CurrentWindow = MainViewModel.ActiveWindow.None;
            _vm.BeginShutdown();
            if (Application.Current.Windows.Count <= 1)
            {
                Application.Current.Shutdown();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            if (!Application.Current.Windows.OfType<SplashWindow>().Any())
            {
                _vm.Dispose();
            }
            base.OnClosed(e);
        }

    }
}
