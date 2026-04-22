using BLDC_Demo.Controls;
using System;
using System.Linq;
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
            InitializeComponent();

            // Link the logic to the UI controls
            CommMotorSelect.BLLogic = BLLogic;

            // Handle disconnection events from the logic layer
            BLLogic.OnDisconnected += () =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (CommMotorSelect != null)
                    {
                        CommMotorSelect.TcpScanStatus.Text = "OFFLINE - Disconnected";
                        CommMotorSelect.TcpScanStatus.Foreground = Brushes.Red;
                    }
                    ClearAllUI();
                });
            };

            // Setup polling for live telemetry (1 second interval)
            _pollingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
            _pollingTimer.Tick += PollingTimer_Tick;

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Start the live data polling loop
            _pollingTimer.Start();
        }

        private void PollingTimer_Tick(object sender, EventArgs e)
        {
            // If we have an active TCP connection (IP address is set)
            if (!string.IsNullOrEmpty(BLLogic.ActivePortName))
            {
                // Find the node currently selected by the user for live viewing
                var activeNode = BLLogic.ActiveDevices
                    .SelectMany(d => d.ConnectedNodes)
                    .FirstOrDefault(n => n.IsActive);

                if (activeNode != null)
                {
                    // Request fresh data from the ESP32 via TCP
                    BLLogic.UpdateData(activeNode);

                    // Update the UI Dashboards (Left/Right motor panels)
                    MainShell.UpdateMdrUI(activeNode);

                   
                }
            }
            else
            {
                // No connection? Clear the gauges and error dots
                ClearAllUI();
            }
        }

        public void ClearAllUI()
        {
            MainShell.LeftMdr?.ClearUI();
            MainShell.RightMdr?.ClearUI();
            MainShell.NetworkConfigCtrl?.ClearUI();
        }

     
    }
}