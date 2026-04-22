using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using BLDC_Demo.Models;

namespace BLDC_Demo
{
    public partial class LeftMdrControl : UserControl
    {
        private int _buttonState = 0;

        public LeftMdrControl()
        {
            InitializeComponent();
        }

        // Helper to get the Slave ID of the node the user clicked in the sidebar
        private byte GetActiveSlaveId()
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            // Find the node that is currently marked as 'Active' in the BLLogic collection
            var activeNode = mainWindow?.BLLogic.ActiveDevices
                .SelectMany(d => d.ConnectedNodes)
                .FirstOrDefault(n => n.IsActive);

            if (activeNode != null && byte.TryParse(activeNode.SeqId, out byte id))
            {
                return id;
            }
            return 1; // Fallback to 1 if nothing is selected
        }

        public void UpdateMotorTelemetry(SubNode node)
        {
            if (node == null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!LeftSpeedTextBox.IsFocused || node.LeftSpeed == "0")
                    LeftSpeedTextBox.Text = node.LeftSpeed;

                if (!LeftAccelTextBox.IsFocused || node.LeftAcceleration == "0")
                    LeftAccelTextBox.Text = node.LeftAcceleration;

                if (!LeftDecelTextBox.IsFocused || node.LeftDeceleration == "0")
                    LeftDecelTextBox.Text = node.LeftDeceleration;

                LeftCurrentTextBox.Text = node.LeftCurrent;

                if (!LeftBrakeCombo.IsDropDownOpen)
                    LeftBrakeCombo.SelectedIndex = node.LeftBrakeIndex;

                if (!LeftMotorCombo.IsDropDownOpen)
                    LeftMotorCombo.SelectedIndex = node.LeftMotorHallTypeIndex;

                // Error Flags
                //FlagHallSensor.Tag = node.LeftHallSensorError;
                //FlagOverCurrent.Tag = node.LeftOverCurrent;
                //FlagOverVoltage.Tag = node.LeftOverVoltage;
                //FlagUnderVoltage.Tag = node.LeftUnderVoltage;
                //FlagMotorStalled.Tag = node.LeftMotorStalled;
                //FlagOverTemp.Tag = node.LeftOverTemperature;
            });
        }

        public void ClearUI()
        {
            Dispatcher.Invoke(() =>
            {
                LeftSpeedTextBox.Text = string.Empty;
                LeftAccelTextBox.Text = string.Empty;
                LeftDecelTextBox.Text = string.Empty;
                LeftCurrentTextBox.Text = string.Empty;
                LeftBrakeCombo.SelectedIndex = -1;
                LeftMotorCombo.SelectedIndex = -1;

                //FlagHallSensor.Tag = null;
                //FlagOverCurrent.Tag = null;
                //FlagOverVoltage.Tag = null;
                //FlagUnderVoltage.Tag = null;
                //FlagMotorStalled.Tag = null;
                //FlagOverTemp.Tag = null;

                _buttonState = 0;
                TripleStateButton.Content = " RUN";
            });
        }

        private void SetSpeed_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow?.BLLogic == null || !ushort.TryParse(LeftSpeedTextBox.Text, out ushort val)) return;

            // Use dynamic Slave ID instead of hardcoded 1
            mainWindow.BLLogic.SendWriteCommand(GetActiveSlaveId(), 0x0027, val);
        }

        private void SetAccel_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow?.BLLogic == null || !ushort.TryParse(LeftAccelTextBox.Text, out ushort val)) return;

            mainWindow.BLLogic.SendWriteCommand(GetActiveSlaveId(), 0x002A, val);
        }

        private void SetLeftBrake_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow?.BLLogic == null || LeftBrakeCombo.SelectedIndex == -1) return;

            ushort val = (ushort)LeftBrakeCombo.SelectedIndex;
            mainWindow.BLLogic.SendWriteCommand(GetActiveSlaveId(), 0x002E, val);
        }

        private void SetLeftMotorType_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow?.BLLogic == null || LeftMotorCombo.SelectedIndex == -1) return;

            ushort val = (ushort)LeftMotorCombo.SelectedIndex;
            mainWindow.BLLogic.SendWriteCommand(GetActiveSlaveId(), 0x0001, val);
        }

        private void SetDecel_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow?.BLLogic == null || !ushort.TryParse(LeftDecelTextBox.Text, out ushort val)) return;

            mainWindow.BLLogic.SendWriteCommand(GetActiveSlaveId(), 0x002B, val);
        }
        public void UpdateRunState(bool isStartClicked)
        {
            Dispatcher.Invoke(() =>
            {
                if (isStartClicked)
                {
                    TripleStateButton.Content = "▶ RUN";
                    _buttonState = 1;
                }
                else
                {
                    TripleStateButton.Content = "⏹ STOP";
                    _buttonState = 2;
                }
            });
        }
        private void TripleStateButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow?.BLLogic == null) return;

            byte slaveId = GetActiveSlaveId();

            if (_buttonState == 0 || _buttonState == 2)
            {
                mainWindow.BLLogic.SendWriteCommand(slaveId, 0x0030, 1);
                TripleStateButton.Content = "▶ RUN";
                _buttonState = 1;
            }
            else
            {
                mainWindow.BLLogic.SendWriteCommand(slaveId, 0x0030, 0);
                TripleStateButton.Content = "⏹ STOP";
                _buttonState = 2;
            }
        }
        //private void TripleStateButton_Click(object sender, RoutedEventArgs e)
        //{
        //    var mainWindow = Application.Current.MainWindow as MainWindow;
        //    if (mainWindow?.BLLogic == null) return;

        //    byte slaveId = GetActiveSlaveId();

        //    if (_buttonState == 0 || _buttonState == 2)
        //    {
        //        mainWindow.BLLogic.SendWriteCommand(slaveId, 0x0030, 1);
        //        TripleStateButton.Content = "STOP";
        //        _buttonState = 1;
        //    }
        //    else
        //    {
        //        mainWindow.BLLogic.SendWriteCommand(slaveId, 0x0030, 0);
        //        TripleStateButton.Content = "RUN";
        //        _buttonState = 2;
        //    }
        //}
    }
}