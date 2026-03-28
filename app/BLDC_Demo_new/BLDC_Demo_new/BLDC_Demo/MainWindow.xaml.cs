using BLDC_Demo.Controls;
using System;
using System.Linq; // 1. THIS FIXES THE FIRSTORDERTODEFAULT ERROR
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace BLDC_Demo
{
    public partial class MainWindow : Window
    {
        public BL BLLogic { get; } = new BL();
        private readonly DispatcherTimer _pollingTimer;

        public MainWindow()
        {
            BLLogic.OnDisconnected += () =>
            {
                // Use the Dispatcher to switch from the Background Thread to the UI Thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    //ClearAllUI();

                    // Access the public field via the control instance 'CommMotorSelect'
                    if (CommMotorSelect != null)
                    {
                        CommMotorSelect.TcpScanStatus.Text = "OFFLINE - Disconnected";
                        CommMotorSelect.TcpScanStatus.Foreground = Brushes.Red;
                    }
                });
            };

            InitializeComponent();
            CommMotorSelect.BLLogic = BLLogic;

            _pollingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
            _pollingTimer.Tick += PollingTimer_Tick;
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            StartContinuousScan();
            _pollingTimer.Start();
        }

        private async void StartContinuousScan()
        {
            while (true)
            {
                await BLLogic.ScanAsync();
                CommMotorSelect.ScanPortsAsync();
                UpdateStatus();
                await Task.Delay(3000);
            }
        }

        private void PollingTimer_Tick(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(BLLogic.ActivePortName))
            {
                BLLogic.UpdateData(BLLogic.ActivePortName);

                // Now that you added 'using System.Linq', this will work!
                var activeDevice = BLLogic.ActiveDevices.FirstOrDefault(d => d.PortName == BLLogic.ActivePortName);
                var activeNode = activeDevice?.ConnectedNodes.FirstOrDefault(n => n.IsActive);

                if (activeNode != null)
                {
                    // This will now find 'MainShell' once you add x:Name to your XAML
                    MainShell.UpdateMdrUI(activeNode);
                }
           
            }
            else
            {
                // Force the UI to clear the textboxes and turn dots gray
                ClearAllUI();
            }
        }
        public void ClearAllUI()
        {
        MainShell.LeftMdr?.ClearUI();
        MainShell.RightMdr?.ClearUI();
        }
        //private void PollingTimer_Tick(object sender, EventArgs e)
        //{
        //    // CASE A: PORT IS CONNECTED
        //    if (!string.IsNullOrEmpty(BLLogic.ActivePortName))
        //    {
        //        BLLogic.UpdateData(BLLogic.ActivePortName);

        //        var activeDevice = BLLogic.ActiveDevices.FirstOrDefault(d => d.PortName == BLLogic.ActivePortName);
        //        var activeNode = activeDevice?.ConnectedNodes.FirstOrDefault(n => n.IsActive);

        //        if (activeNode != null)
        //        {
        //            MainShell.UpdateMdrUI(activeNode);
        //        }

        //    }
        //    // CASE B: PORT IS DISCONNECTED (Cable pulled)
        //    else
        //    {
        //        // Force the UI to clear the textboxes and turn dots gray
        //        MainShell.LeftMdr?.ClearUI();
        //        MainShell.RightMdr?.ClearUI();
        //    }
        //}
        public void UpdateStatus()
        {
            if (ConnectionLabel == null) return;
            // ... (rest of your logic)
        }
    }
}