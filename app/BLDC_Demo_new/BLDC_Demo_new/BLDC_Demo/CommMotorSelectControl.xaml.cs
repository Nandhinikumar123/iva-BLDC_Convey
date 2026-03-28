//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;
//using System.Windows;
//using System.Windows.Controls;
//using System.Windows.Data;
//using System.Windows.Media;
//using System.Windows.Threading;
//using BLDC_Demo.Models;
//using BLDC_Demo.Controls;

//namespace BLDC_Demo
//{
//    public partial class CommMotorSelectControl : UserControl
//    {
//        internal BL BLLogic { get; set; }

//        private DispatcherTimer _scanTimer;
//        private DateTime _tempStatusEndTime = DateTime.MinValue;
//        private string _tempStatusMessage = "";
//        private Brush _defaultTextGray = new SolidColorBrush(Color.FromRgb(154, 163, 181));

//        public CommMotorSelectControl()
//        {
//            InitializeComponent();
//        }

//        private void UserControl_Loaded(object sender, RoutedEventArgs e)
//        {
//            // Scan and Poll every 2 seconds
//            _scanTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
//            _scanTimer.Tick += (s, args) => {
//                ScanPortsAsync();

//                // Find the currently active motor node and poll its live telemetry
//                var activeDevice = BLLogic?.ActiveDevices.FirstOrDefault(d => d.PortName == BLLogic.ActivePortName);
//                var activeNode = activeDevice?.ConnectedNodes.FirstOrDefault(n => n.IsActive);

//                if (activeNode != null)
//                {
//                    BLLogic.UpdateData(activeNode);
//                }
//            };
//            _scanTimer.Start();
//            ScanPortsAsync();
//        }

//        public async void ScanPortsAsync()
//        {
//            if (BLLogic == null) return;

//            _scanTimer?.Stop();
//            await BLLogic.ScanAsync();

//            // Handle UI clearing if the physical cable is pulled or connection lost
//            if (string.IsNullOrEmpty(BLLogic.ActivePortName))
//            {
//                AppShellControl shell = null;
//                DependencyObject current = this;

//                while (current != null && shell == null)
//                {
//                    current = VisualTreeHelper.GetParent(current);
//                    shell = current as AppShellControl;
//                }

//                if (shell != null)
//                {
//                    shell.LeftMdr?.ClearUI();
//                    shell.RightMdr?.ClearUI();
//                }
//            }

//            UpdateRtuStatus();
//            UpdateTcpStatus();
//            RefreshUiList();

//            _scanTimer?.Start();
//        }

//        private void UpdateRtuStatus()
//        {
//            if (string.IsNullOrEmpty(BLLogic.ActivePortName))
//            {
//                if (BLLogic.ConnectedDevices.Count > 0)
//                {
//                    RtuScanStatus.Foreground = _defaultTextGray;
//                    RtuScanStatus.Text = "Scanning for connected devices...";
//                }
//                else
//                {
//                    RtuScanStatus.Foreground = Brushes.Red;
//                    RtuScanStatus.Text = "No COM ports detected";
//                }
//            }
//            else if (!BLLogic.ActivePortName.Contains(":"))
//            {
//                RtuScanStatus.Foreground = Brushes.LimeGreen;
//                RtuScanStatus.Text = $"ACTIVE: {BLLogic.ActivePortName}";
//            }
//            else
//            {
//                RtuScanStatus.Foreground = _defaultTextGray;
//                RtuScanStatus.Text = "Scanning suspended (TCP Active)";
//            }
//        }

//        private void UpdateTcpStatus()
//        {
//            if (DateTime.Now < _tempStatusEndTime)
//            {
//                TcpScanStatus.Foreground = Brushes.Red;
//                TcpScanStatus.Text = _tempStatusMessage;
//            }
//            else if (!string.IsNullOrEmpty(BLLogic.ActivePortName) && BLLogic.ActivePortName.Contains(":"))
//            {
//                TcpScanStatus.Foreground = Brushes.LimeGreen;
//                TcpScanStatus.Text = $"CONNECTED TO {BLLogic.ActivePortName}";
//            }
//            else if (!string.IsNullOrEmpty(BLLogic.ActivePortName) && BLLogic.ActivePortName.StartsWith("COM"))
//            {
//                TcpScanStatus.Foreground = _defaultTextGray;
//                TcpScanStatus.Text = "Scanning suspended (RTU Active)";
//            }
//            else
//            {
//                TcpScanStatus.Foreground = Brushes.Red;
//                TcpScanStatus.Text = "NOT CONNECTED";
//            }
//        }

//        private async void TcpConnectButton_Click(object sender, RoutedEventArgs e)
//        {
//            string masterIp = TcpIpAddress.Text;

//            TcpScanStatus.Foreground = _defaultTextGray;
//            TcpScanStatus.Text = "DISCOVERING NODES...";

//            try
//            {
//                // Connects to gateway and sends handshake to find ESP32 nodes
//               // await BLLogic.ConnectAndGetEthernetNodesAsync(masterIp);
//                _tempStatusEndTime = DateTime.MinValue;
//            }
//            catch (Exception)
//            {
//                _tempStatusMessage = "DISCOVERY FAILED";
//                _tempStatusEndTime = DateTime.Now.AddSeconds(3);
//            }

//            RefreshUiList();
//            (Window.GetWindow(this) as MainWindow)?.UpdateStatus();
//        }


//        private void DeviceRow_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
//        {
//            if (sender is Border border && border.DataContext is SubNode clickedNode)
//            {
//                var parentDevice = BLLogic.ActiveDevices.FirstOrDefault(d => d.ConnectedNodes.Contains(clickedNode));

//                if (parentDevice != null && BLLogic.ActivePortName != parentDevice.PortName)
//                {
//                    IComm conn;
//                    if (parentDevice.PortName.Contains(":"))
//                    {
//                        // Correctly splitting "192.168.1.50:502"
//                        var parts = parentDevice.PortName.Split(':');
//                        string ip = parts[0];
//                        int port = int.Parse(parts[1]);
//                        conn = new CommEthernet(ip, port); // Now takes 2 arguments
//                    }
//                    else
//                    {
//                        conn = new CommRS485(parentDevice.PortName);
//                    }

//                    if (!BLLogic.Start(conn)) return;
//                }

//                // Logic for selection
//                foreach (var device in BLLogic.ActiveDevices)
//                    foreach (var node in device.ConnectedNodes) node.IsActive = false;

//                clickedNode.IsActive = true;
//                if (parentDevice != null) BLLogic.UpdateData(clickedNode);

//                RefreshUiList();
//            }
//        }

//        private void RefreshUiList()
//        {
//            // Ensure RtuPortList exists in XAML with x:Name="RtuPortList"
//            RtuPortList.Children.Clear();

//            var serialDevices = BLLogic.ActiveDevices.Where(d => !d.PortName.Contains(":")).ToList();
//            var ethernetDevice = BLLogic.ActiveDevices.FirstOrDefault(d => d.PortName.Contains(":"));

//            foreach (var dev in serialDevices)
//            {
//                RtuPortList.Children.Add(CreatePortRow(dev.PortName));
//            }

//            // Ensure TcpNodeList exists in XAML with x:Name="TcpNodeList"
//            TcpNodeList.ItemsSource = ethernetDevice?.ConnectedNodes;
//        }

//        private UIElement CreatePortRow(string portName)
//        {
//            var device = BLLogic.ActiveDevices.FirstOrDefault(d => d.PortName == portName);
//            if (device == null) return new Border();

//            var treeRoot = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };

//            // Port Header (e.g. COM3)
//            var portHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
//            portHeader.Children.Add(new TextBlock { Text = "▼ ", Foreground = Brushes.Gray, FontSize = 10, VerticalAlignment = VerticalAlignment.Center });
//            portHeader.Children.Add(new TextBlock
//            {
//                Text = portName,
//                FontFamily = new FontFamily("Consolas"),
//                FontSize = 12,
//                FontWeight = FontWeights.Bold,
//                Foreground = (Brush)FindResource("AccentBrush")
//            });
//            treeRoot.Children.Add(portHeader);

//            // Sub-nodes under the serial port
//            var deviceListUI = new ItemsControl
//            {
//                ItemTemplate = (DataTemplate)FindResource("SubDeviceTemplate"),
//                ItemsSource = device.ConnectedNodes,
//                Margin = new Thickness(15, 0, 0, 0)
//            };

//            treeRoot.Children.Add(deviceListUI);
//            return treeRoot;
//        }
//    }

//public class HealthToColorConverter : IValueConverter
//{
//    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
//    {
//        if (value is bool isHealthy)
//        {
//            return isHealthy ? new SolidColorBrush(Colors.LimeGreen) : new SolidColorBrush(Colors.Red);
//        }
//        return new SolidColorBrush(Colors.Gray);
//    }

//    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
//        => throw new NotImplementedException();
//}
//}
using System;

using System.Windows;

using System.Windows.Controls;

using System.Windows.Media;

using System.Windows.Data;

using System.Linq;

using System.Windows.Threading;

using BLDC_Demo.Models;

using BLDC_Demo.Controls;



namespace BLDC_Demo

{

    public partial class CommMotorSelectControl : UserControl

    {

        internal BL BLLogic { get; set; }



        private DispatcherTimer _scanTimer;

        private DateTime _tempStatusEndTime = DateTime.MinValue;

        private string _tempStatusMessage = "";

        private Brush _defaultTextGray = new SolidColorBrush(Color.FromRgb(154, 163, 181));



        public CommMotorSelectControl() { InitializeComponent(); }



        private void UserControl_Loaded(object sender, RoutedEventArgs e)

        {

            _scanTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };

            _scanTimer.Tick += (s, args) => {

                ScanPortsAsync();



                // Poll live data for the actively selected motor

                var activeDevice = BLLogic.ActiveDevices.FirstOrDefault(d => d.PortName == BLLogic.ActivePortName);

                var activeNode = activeDevice?.ConnectedNodes.FirstOrDefault(n => n.IsActive);



                if (activeNode != null)

                {

                    BLLogic.UpdateData(activeNode);

                }

            };

            _scanTimer.Start();

            ScanPortsAsync();

        }



        public async void ScanPortsAsync()

        {

            if (BLLogic == null) return;



            _scanTimer?.Stop();

            await BLLogic.ScanAsync();



            // --- RTU (COM PORT) STATUS COLORS ---

            if (string.IsNullOrEmpty(BLLogic.ActivePortName))

            {

                if (BLLogic.ConnectedDevices.Count > 0)

                {

                    RtuScanStatus.Foreground = _defaultTextGray;

                    RtuScanStatus.Text = "Scanning for connected devices...";

                }

                else

                {

                    // No COM ports = RED

                    RtuScanStatus.Foreground = Brushes.Red;

                    RtuScanStatus.Text = "No COM ports detected";

                }

            }

            else if (!BLLogic.ActivePortName.Contains(":"))

            {

                // Connected to COM Port = GREEN

                RtuScanStatus.Foreground = Brushes.LimeGreen;

                RtuScanStatus.Text = $"ACTIVE: {BLLogic.ActivePortName}";

            }

            else

            {

                RtuScanStatus.Foreground = _defaultTextGray;

                RtuScanStatus.Text = "Scanning suspended (TCP Active)";

            }



            // --- TCP (ETHERNET) STATUS COLORS ---

            if (DateTime.Now < _tempStatusEndTime)

            {

                // Error = RED

                TcpScanStatus.Foreground = Brushes.Red;

                TcpScanStatus.Text = _tempStatusMessage;

            }

            else if (!string.IsNullOrEmpty(BLLogic.ActivePortName) && BLLogic.ActivePortName.Contains(":"))

            {

                // Connected to Ethernet = GREEN

                TcpScanStatus.Foreground = Brushes.LimeGreen;

                TcpScanStatus.Text = $"CONNECTED TO {BLLogic.ActivePortName}";

            }

            else

            {

                // Disconnected = RED

                TcpScanStatus.Foreground = Brushes.Red;

                TcpScanStatus.Text = "NOT CONNECTED";

            }



            RefreshUiList();

            _scanTimer?.Start();

        }



        //private void TcpConnectButton_Click(object sender, RoutedEventArgs e)

        //{

        //    if (int.TryParse(TcpPort.Text, out int port))

        //    {

        //        string tcpName = $"{TcpIpAddress.Text}:{port}";



        //        if (BLLogic.ActiveDevices.All(d => d.PortName != tcpName))

        //        {

        //            BLLogic.ActiveDevices.Add(new ModbusDevice { PortName = tcpName });

        //        }



        //        var device = BLLogic.ActiveDevices.First(d => d.PortName == tcpName);

        //        foreach (var d in BLLogic.ActiveDevices) d.Reset();



        //        var tcp = new CommEthernet(TcpIpAddress.Text, port);



        //        if (BLLogic.Start(tcp))

        //        {

        //            device.IsActive = true;

        //            _tempStatusEndTime = DateTime.MinValue;



        //            // Connected = GREEN

        //            TcpScanStatus.Foreground = Brushes.LimeGreen;

        //            TcpScanStatus.Text = $"CONNECTED TO {tcp.Name}";



        //            RtuScanStatus.Foreground = _defaultTextGray;

        //            RtuScanStatus.Text = "Scanning suspended (TCP Active)";

        //        }

        //        else

        //        {

        //            device.IsActive = false;

        //            _tempStatusMessage = "CONNECTION FAILED";

        //            _tempStatusEndTime = DateTime.Now.AddSeconds(3);



        //            // Failed = RED

        //            TcpScanStatus.Foreground = Brushes.Red;

        //            TcpScanStatus.Text = _tempStatusMessage;

        //        }



        //        RefreshUiList();

        //        (Window.GetWindow(this) as MainWindow)?.UpdateStatus();

        //    }

        //}
        private void TcpConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TcpPort.Text, out int port))
            {
                string tcpName = $"{TcpIpAddress.Text}:{port}";

                // Add device if it doesn't exist
                if (BLLogic.ActiveDevices.All(d => d.PortName != tcpName))
                {
                    BLLogic.ActiveDevices.Add(new ModbusDevice { PortName = tcpName });
                }

                var device = BLLogic.ActiveDevices.First(d => d.PortName == tcpName);

                // Reset previous active states
                foreach (var d in BLLogic.ActiveDevices) d.Reset();

                // Create the ethernet object
                var tcp = new CommEthernet(TcpIpAddress.Text, port);

                // CRITICAL FIX: Explicitly open the connection and do the handshake HERE
                if (tcp.Open())
                {
                    // Only start the logic if the hardware connection is fully established
                    if (BLLogic.Start(tcp))
                    {
                        device.IsActive = true;
                        // _tempStatusEndTime = DateTime.MinValue; // Uncomment if you have this defined

                        TcpScanStatus.Foreground = Brushes.LimeGreen;
                        TcpScanStatus.Text = $"CONNECTED TO {tcp.Name}";
                        RtuScanStatus.Text = "Scanning suspended (TCP Active)";
                    }
                }
                else
                {
                    // Handshake failed or ESP32 unreachable
                    device.IsActive = false;
                    // _tempStatusMessage = "CONNECTION FAILED"; // Uncomment if you have this defined
                    // _tempStatusEndTime = DateTime.Now.AddSeconds(3); // Uncomment if you have this defined

                    TcpScanStatus.Foreground = Brushes.Red;
                    TcpScanStatus.Text = "CONNECTION FAILED";
                }

                RefreshUiList(); // Ensure this method exists in your code-behind
                (Window.GetWindow(this) as MainWindow)?.UpdateStatus();
            }
        }
        //private void TcpConnectButton_Click(object sender, RoutedEventArgs e)
        //{
        //    if (int.TryParse(TcpPort.Text, out int port))
        //    {
        //        string tcpName = $"{TcpIpAddress.Text}:{port}";

        //        // Add device if it doesn't exist
        //        if (BLLogic.ActiveDevices.All(d => d.PortName != tcpName))
        //        {
        //            BLLogic.ActiveDevices.Add(new ModbusDevice { PortName = tcpName });
        //        }

        //        var device = BLLogic.ActiveDevices.First(d => d.PortName == tcpName);

        //        // Reset previous active states
        //        foreach (var d in BLLogic.ActiveDevices) d.Reset();

        //        // Create the ethernet object
        //        var tcp = new CommEthernet(TcpIpAddress.Text, port);

        //        // BLLogic.Start(tcp) now handles the Telemetry event internally!
        //        if (BLLogic.Start(tcp))
        //        {
        //            device.IsActive = true;
        //            _tempStatusEndTime = DateTime.MinValue;

        //            TcpScanStatus.Foreground = Brushes.LimeGreen;
        //            TcpScanStatus.Text = $"CONNECTED TO {tcp.Name}";
        //            RtuScanStatus.Text = "Scanning suspended (TCP Active)";

        //        }
        //        else
        //        {
        //            device.IsActive = false;
        //            _tempStatusMessage = "CONNECTION FAILED";
        //            _tempStatusEndTime = DateTime.Now.AddSeconds(3);

        //            TcpScanStatus.Foreground = Brushes.Red;
        //            TcpScanStatus.Text = _tempStatusMessage;

        //        }

        //        RefreshUiList();
        //        (Window.GetWindow(this) as MainWindow)?.UpdateStatus();
        //    }
        //}

        // Triggered when you click on a specific MAC Address in the list

        private void DeviceRow_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)

        {

            if (sender is Border border && border.DataContext is SubNode clickedNode)

            {

                // Ensure the COM port itself is connected first

                var parentDevice = BLLogic.ActiveDevices.FirstOrDefault(d => d.ConnectedNodes.Contains(clickedNode));

                if (parentDevice != null && BLLogic.ActivePortName != parentDevice.PortName)

                {

                    IComm conn = new CommRS485(parentDevice.PortName);

                    if (!BLLogic.Start(conn)) return; // Connection failed

                }



                // Turn off live data UI for all other motors

                foreach (var device in BLLogic.ActiveDevices)

                {

                    foreach (var node in device.ConnectedNodes) node.IsActive = false;

                }



                // Turn ON the live data UI for the clicked motor

                clickedNode.IsActive = true;



                // Immediately fetch its first live data update

                if (parentDevice != null)

                {

                    BLLogic.UpdateData(clickedNode);

                }

            }

        }



        private void RefreshUiList()

        {

            RtuPortList.Children.Clear();

            // TcpPortList.Children.Clear();



            int comPortCount = 0;



            foreach (var dev in BLLogic.ActiveDevices)

            {

                bool isEth = dev.PortName.Contains(":");

                var row = CreatePortRow(dev.PortName, isEth);



                if (isEth)

                {

                    //TcpPortList.Children.Add(row);

                }

                else

                {

                    RtuPortList.Children.Add(row);

                    comPortCount++;

                }

            }



            RtuPortScroll.Visibility = comPortCount > 0 ? Visibility.Visible : Visibility.Collapsed;

            RtuEmptyHint.Visibility = comPortCount == 0 ? Visibility.Visible : Visibility.Collapsed;

        }



        private UIElement CreatePortRow(string portName, bool isEthernet)

        {

            var device = BLLogic.ActiveDevices.FirstOrDefault(d => d.PortName == portName);

            if (device == null) return new Border();



            var row = new Border

            {

                Background = Brushes.White,

                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),

                BorderThickness = new Thickness(1),

                CornerRadius = new CornerRadius(8),

                Padding = new Thickness(12),

                Margin = new Thickness(0, 0, 0, 10),

            };



            var stack = new StackPanel();



            stack.Children.Add(new TextBlock

            {

                Text = portName,

                FontFamily = new FontFamily("Arial Black"),

                FontSize = 11,

                FontWeight = FontWeights.Black,

                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5A6A8A")),

                Margin = new Thickness(0, 0, 0, 5)

            });



            // The dynamic list that generates rows for every connected ESP32 node

            var deviceListUI = new ItemsControl();

            deviceListUI.ItemTemplate = (DataTemplate)FindResource("SubDeviceTemplate");

            deviceListUI.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("ConnectedNodes") { Source = device });

            deviceListUI.SetBinding(UIElement.VisibilityProperty, new Binding("HasDeviceInfo")

            {

                Source = device,

                Converter = (BooleanToVisibilityConverter)Resources["BooleanToVisibilityConverter"]

            });



            stack.Children.Add(deviceListUI);

            row.Child = stack;

            return row;

        }

    }

}
public class HealthToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool isHealthy)
        {
            return isHealthy ? new SolidColorBrush(Colors.LimeGreen) : new SolidColorBrush(Colors.Red);
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => throw new NotImplementedException();
}
