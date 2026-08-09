using System;
using System.ComponentModel;
using System.Windows;
using SwissTimingDisplay.Controls;
using SwissTimingDisplay.ViewModels;

namespace SwissTimingDisplay
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;

        public MainWindow() : this(MainViewModel.SharedInstance)
        {
        }

        public MainWindow(MainViewModel vm)
        {
            _vm = vm;
            InitializeComponent();
            DataContext = _vm;
            displayControl.SetUp(_vm, this);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            _vm.BeginShutdown();
            _vm.Disconnect();
            _vm.DisconnectReceive();
            Application.Current.Shutdown();
        }

        protected override void OnClosed(EventArgs e)
        {
            _vm.Dispose();
            base.OnClosed(e);
        }
    }
}
