
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using BLDC_Demo.Models;
using BLDC_Demo.Controls;

namespace BLDC_Demo
{
    public partial class CommMotorSelectControl : UserControl
    {
        // SINGLETON INSTANCE: Allows background threads to trigger a UI refresh
        public static CommMotorSelectControl Instance { get; private set; }

        internal BL BLLogic { get; set; }
        private DispatcherTimer _pollTimer;
        private bool _isBusy = false; // Guard to prevent Socket Collisions
        private DateTime _tempStatusEndTime = DateTime.MinValue;
        private string _tempStatusMessage = "";

        public CommMotorSelectControl()
        {
            InitializeComponent();
            Instance = this;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Set Heartbeat to 200ms. 100ms is often too fast for Modbus TCP 
            // and causes the 'SocketException' you observed.
            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _pollTimer.Tick += async (s, args) => {
                await PollActiveNodeAsync();
                UpdateStatusText();
            };
            _pollTimer.Start();

            RefreshUiList();
        }

        /// <summary>
        /// Asynchronously polls the hardware. 
        /// Using Task.Run ensures the UI doesn't freeze while waiting for a response.
        /// </summary>
        private async Task PollActiveNodeAsync()
        {
            if (BLLogic == null || _isBusy) return;

            var activeNode = BLLogic.ActiveDevices
                .SelectMany(d => d.ConnectedNodes)
                .FirstOrDefault(n => n.IsActive);

            if (activeNode == null) return;

            try
            {
                _isBusy = true;
                await Task.Run(() => BLLogic.UpdateData(activeNode));
            }
            catch (Exception ex)
            {
                // Catching errors here prevents "Exited with code 0" crashes
                System.Diagnostics.Debug.WriteLine($"Poll Error: {ex.Message}");
            }
            finally
            {
                _isBusy = false;
            }
        }

        /// <summary>
        /// CALL THIS FROM YOUR DISCOVERY LOGIC:
        /// When the 'NODES:' payload is parsed, call CommMotorSelectControl.Instance.RefreshUiList()
        /// </summary>
        public void RefreshUiList()
        {
            // Dispatcher ensures the UI updates even if called from a background RX thread
            Dispatcher.BeginInvoke(new Action(() => {
                if (DeviceListControl == null || BLLogic == null) return;

                var allNodes = BLLogic.ActiveDevices
                                      .SelectMany(d => d.ConnectedNodes)
                                      .ToList();

                // Resetting ItemsSource forces the WPF Sidebar to redraw immediately
                DeviceListControl.ItemsSource = null;
                DeviceListControl.ItemsSource = allNodes;
            }));
        }
        private void TcpConnectButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. Check if we are already connected to perform a DISCONNECT
            //if (!string.IsNullOrEmpty(BLLogic.ActivePortName))
            //{
            //    BLLogic.Close(); // Ensure BLLogic has a Stop or Close method
            //    foreach (var d in BLLogic.ActiveDevices) d.IsActive = false;
            //    RefreshUiList();
            //    return;
            //}
            if (!string.IsNullOrEmpty(BLLogic.ActivePortName))
            {
                BLLogic.Close();

                // This is the "Magic Bullet" for the UI Memory
                if (Application.Current.MainWindow is MainWindow mw)
                {
                    mw.DataContext = null;
                }

                RefreshUiList();
                return;
            }
            // 2. Otherwise, perform CONNECT logic
            if (!int.TryParse(TcpPort.Text, out int port)) return;

            string ip = TcpIpAddress.Text;
            string tcpName = $"{ip}:{port}";

            // Register device in the logic layer
            if (BLLogic.ActiveDevices.All(d => d.PortName != tcpName))
            {
                BLLogic.ActiveDevices.Add(new ModbusDevice
                {
                    PortName = tcpName
                });
            }

            var device = BLLogic.ActiveDevices.First(d => d.PortName == tcpName);
            foreach (var d in BLLogic.ActiveDevices) d.Reset();

            // Attempt Physical Ethernet Connection
            var tcp = new CommEthernet(ip, port);

            if (BLLogic.Start(tcp))
            {
                device.IsActive = true;
                _tempStatusEndTime = DateTime.MinValue;

                // Trigger an initial refresh. 
                // Subsequent refreshes happen automatically when RX data arrives.
                RefreshUiList();
            }
            else
            {
                _tempStatusMessage = "CONNECTION FAILED";
                _tempStatusEndTime = DateTime.Now.AddSeconds(3);
            }
        }
        private void UpdateStatusText()
        {
            if (BLLogic == null) return;

            if (DateTime.Now < _tempStatusEndTime)
            {
                TcpScanStatus.Foreground = Brushes.Red;
                TcpScanStatus.Text = _tempStatusMessage;
            }
            else if (!string.IsNullOrEmpty(BLLogic.ActivePortName))
            {
                // --- CONNECTED STATE ---
                TcpScanStatus.Foreground = Brushes.LimeGreen;
                TcpScanStatus.Text = $"CONNECTED: {BLLogic.ActivePortName}";

                TcpConnectButton.Content = "DISCONNECT";
                TcpConnectButton.IsEnabled = true; // KEEP ENABLED for toggle
                TcpConnectButton.Background = new SolidColorBrush(Color.FromRgb(255, 230, 230)); // Light red tint
            }
            else
            {
                // --- DISCONNECTED STATE ---
                TcpScanStatus.Foreground = Brushes.Gray;
                TcpScanStatus.Text = "NOT CONNECTED";
                DeviceListControl.ItemsSource = null;
                TcpConnectButton.Content = "CONNECT";
                TcpConnectButton.IsEnabled = true;
               // TcpConnectButton.Background = new SolidColorBrush(Color.FromRgb(240, 242, 248)); // Original color
            }
        }
        //private void TcpConnectButton_Click(object sender, RoutedEventArgs e)
        //{
//            if (!int.TryParse(TcpPort.Text, out int port)) return;

//            string ip = TcpIpAddress.Text;
//        string tcpName = $"{ip}:{port}";

//            // Register device in the logic layer
//            if (BLLogic.ActiveDevices.All(d => d.PortName != tcpName))
//            {
//                BLLogic.ActiveDevices.Add(new ModbusDevice { PortName = tcpName
//    });
//            }

//var device = BLLogic.ActiveDevices.First(d => d.PortName == tcpName);
//foreach (var d in BLLogic.ActiveDevices) d.Reset();

//// Attempt Physical Ethernet Connection
//var tcp = new CommEthernet(ip, port);

//if (BLLogic.Start(tcp))
//{
//    device.IsActive = true;
//    _tempStatusEndTime = DateTime.MinValue;

//    // Trigger an initial refresh. 
//    // Subsequent refreshes happen automatically when RX data arrives.
//    RefreshUiList();
//}
//    else
//    {
//        _tempStatusMessage = "CONNECTION FAILED";
//        _tempStatusEndTime = DateTime.Now.AddSeconds(3);
//    }
//}



//private void UpdateStatusText()
//{
//    if (BLLogic == null) return;

//    if (DateTime.Now < _tempStatusEndTime)
//    {
//        TcpScanStatus.Foreground = Brushes.Red;
//        TcpScanStatus.Text = _tempStatusMessage;
//    }
//    else if (!string.IsNullOrEmpty(BLLogic.ActivePortName))
//    {
//        TcpScanStatus.Foreground = Brushes.LimeGreen;
//        TcpScanStatus.Text = $"CONNECTED: {BLLogic.ActivePortName}";
//        TcpConnectButton.IsEnabled = false;
//       // TcpDisconnectButton.IsEnabled = true;
//    }
//    else
//    {
//        TcpScanStatus.Foreground = Brushes.Gray;
//        TcpScanStatus.Text = "NOT CONNECTED";
//        TcpConnectButton.IsEnabled = true;
//       // TcpDisconnectButton.IsEnabled = false;
//    }
//}

private void DeviceRow_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // 1. Identify the clicked node
            if (!(sender is FrameworkElement element) || !(element.DataContext is SubNode clickedNode))
                return;

            // 2. Radio-button logic: Deactivate all other nodes 
            // Optimization: If the node is already active, don't re-run the logic
            if (clickedNode.IsActive) return;

            foreach (var node in BLLogic.ActiveDevices.SelectMany(d => d.ConnectedNodes))
            {
                node.IsActive = false;
            }

            // 3. Activate the selected node
            clickedNode.IsActive = true;

            // 4. Update the MainWindow's DataContext
            // Use Application.Current.MainWindow as a more reliable shortcut
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.DataContext = clickedNode;

                // Ensure the Shell/Panel refreshes
                mainWindow.MainShell?.UpdateMdrUI(clickedNode);
            }

            // 5. Hardware Communication
            // We wrap this to ensure a UI hang doesn't block the selection visual
            Task.Run(() => {
                BLLogic.UpdateData(clickedNode);
            });

            Console.WriteLine($"[UI] Selected Node ID: {clickedNode.SeqId}. Monitoring started.");
        }
    }
}