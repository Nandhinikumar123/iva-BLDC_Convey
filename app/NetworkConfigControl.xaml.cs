using BLDC_Demo.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace BLDC_Demo
{
    /// <summary>
    /// Interaction logic for NetworkConfigControl.xaml
    /// </summary>
    public partial class NetworkConfigControl : UserControl
    {
        public NetworkConfigControl()
        {
            InitializeComponent();
        }
        public void UpdateMotorTelemetry(SubNode node)
        {
            if (node == null) return;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                FirmwareTextBox.Text = node.FirmwareVersion;
                HardwareTextBox.Text = node.HardwareRevision;
                DevicestsTextBox.Text = node.DeviceStatus;
                DeviceIDTextBox.Text = node.DeviceID;
                // Example for the Range fields
                Node1TextBox.Text = node.Node1;
                Node2TextBox.Text = node.Node2;

                // Serial Number (if available in your node data)
                SerialNOTextBox.Text = node.SerialNumber;

            });
        }
        private void AdvancedButton_Click(object sender, RoutedEventArgs e)
        {
            // Replace 'AdvancedSettingsControl' with the actual name of your new UserControl
            var advancedControl = new AdvancedSettingsControl();

            Window popup = new Window
            {
                Title = "Advanced Configuration",
                Content = advancedControl,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = System.Windows.Application.Current.MainWindow
            };

            popup.ShowDialog(); // Use ShowDialog() to make it a modal (blocks main window)
        }
        public void ClearUI()
        {
            Dispatcher.Invoke(() =>
            {
                this.DataContext = null;
                FirmwareTextBox.Text = string.Empty; 
                HardwareTextBox.Text = string.Empty; 
                DevicestsTextBox.Text = string.Empty; 
                // DeviceIDTextBox.Text = node.FirmwareVersion;
                // Example for the Range fields
                Node1TextBox.Text = string.Empty; 
                Node2TextBox.Text = string.Empty;
                DeviceIDTextBox.Text = string.Empty;
                // Serial Number (if available in your node data)
                SerialNOTextBox.Text = string.Empty; 
            });
        }

    }
}
