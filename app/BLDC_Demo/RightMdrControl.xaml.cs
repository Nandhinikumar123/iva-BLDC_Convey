//using System.Windows;
//using System.Windows.Controls;
//using System.Windows.Media;
//using BLDC_Demo.Models;

//namespace BLDC_Demo
//{
//    public partial class RightMdrControl : UserControl
//    {
//        // 0 = Idle, 1 = Running, 2 = Stopped
//        private int _buttonState = 0;

//        public RightMdrControl()
//        {
//            InitializeComponent();
//        }

//        /// <summary>
//        /// Updates the UI with telemetry data specifically for the RIGHT motor.
//        /// </summary>
//        //public void UpdateMotorTelemetry(SubNode node)
//        //{
//        //    Application.Current.Dispatcher.Invoke(() =>
//        //    {
//        //        // Prevent telemetry from overwriting your typing on the Right side
//        //        if (!RightSpeedTextBox.IsFocused)
//        //            RightSpeedTextBox.Text = node.RightSpeed;

//        //        if (!RightAccelTextBox.IsFocused)
//        //            RightAccelTextBox.Text = node.RightAcceleration;

//        //        if (!RightDecelTextBox.IsFocused)
//        //            RightDecelTextBox.Text = node.RightDeceleration;

//        //        // Current is usually read-only
//        //        RightCurrentTextBox.Text = node.RightCurrent;
//        //    });
//        //}
//        //public void ClearUI()
//        //{
//        //    // Ensure we are on the UI thread
//        //    Application.Current.Dispatcher.Invoke(() =>
//        //    {
//        //        // Setting to empty string ""
//        //        RightSpeedTextBox.Text = string.Empty;
//        //        RightAccelTextBox.Text = string.Empty;
//        //        RightDecelTextBox.Text = string.Empty;
//        //        RightCurrentTextBox.Text = string.Empty;

//        //        // Reset the button state
//        //        _buttonState = 0;
//        //        TripleStateButton.Content = "▶ RUN";

//        //        // Ghost the UI so it looks inactive
//        //        this.IsEnabled = false;
//        //        this.Opacity = 0.5;
//        //    });
//        //}
//        public void ClearUI()
//        {
//            Application.Current.Dispatcher.Invoke(() =>
//            {
//                // Clear Text
//                RightSpeedTextBox.Text = string.Empty;
//                RightAccelTextBox.Text = string.Empty;
//                RightDecelTextBox.Text = string.Empty;
//                RightCurrentTextBox.Text = string.Empty;

//                // Reset Button
//                _buttonState = 0;
//                TripleStateButton.Content = "▶ RUN";

//                // Ghost the UI
//                this.IsEnabled = false;
//                this.Opacity = 0.5;
//            });
//        }
//        public void UpdateMotorTelemetry(SubNode node)
//        {
//            // IMPORTANT: Removed !this.IsEnabled check so "0" data can still be written 
//            // once when the port disconnects.
//            if (node == null) return;

//            Application.Current.Dispatcher.Invoke(() =>
//            {
//                // We update if the box isn't focused OR if the value is "0" (forced clear)
//                if (!RightSpeedTextBox.IsFocused || node.RightSpeed == "0")
//                    RightSpeedTextBox.Text = node.RightSpeed;

//                if (!RightAccelTextBox.IsFocused || node.RightAcceleration == "0")
//                    RightAccelTextBox.Text = node.RightAcceleration;

//                if (!RightDecelTextBox.IsFocused || node.RightDeceleration == "0")
//                    RightDecelTextBox.Text = node.RightDeceleration;

//                // Current is always updated
//                RightCurrentTextBox.Text = node.RightCurrent;

//                // Update Error Flags
//                FlagHallSensor.Tag = node.RightHallSensorError;
//                FlagOverCurrent.Tag = node.RightOverCurrent;
//                FlagOverVoltage.Tag = node.RightOverVoltage;
//                FlagUnderVoltage.Tag = node.RightUnderVoltage;
//                FlagMotorStalled.Tag = node.RightMotorStalled;
//                FlagOverTemp.Tag = node.RightOverTemperature;
//            });
//        }
//        //public void UpdateMotorTelemetry(SubNode node)
//        //{
//        //    if (!this.IsEnabled || node == null) return;

//        //    Application.Current.Dispatcher.Invoke(() =>
//        //    {
//        //        // Check IsFocused before updating. 
//        //        // If you are typing, IsFocused is true, and the UI won't flicker back to the old value.

//        //        // Prevent telemetry from overwriting your typing on the Right side
//        //        if (!RightSpeedTextBox.IsFocused)
//        //            RightSpeedTextBox.Text = node.RightSpeed;

//        //        if (!RightAccelTextBox.IsFocused)
//        //            RightAccelTextBox.Text = node.RightAcceleration;

//        //        if (!RightDecelTextBox.IsFocused)
//        //            RightDecelTextBox.Text = node.RightDeceleration;

//        //        // Current is usually read-only
//        //        RightCurrentTextBox.Text = node.RightCurrent;

//        //        // Current is read-only, so we update it always

//        //        FlagHallSensor.Tag = node.RightHallSensorError;   // bit 14
//        //        FlagOverCurrent.Tag = node.RightOverCurrent;       // bit 09
//        //        FlagOverVoltage.Tag = node.RightOverVoltage;       // bit 06
//        //        FlagUnderVoltage.Tag = node.RightUnderVoltage;      // bit 07
//        //        FlagMotorStalled.Tag = node.RightMotorStalled;      // bit 13
//        //        FlagOverTemp.Tag = node.RightOverTemperature;

//        //    });
//        //}
//        private void SetRightSpeed_Click(object sender, RoutedEventArgs e)
//        {
//            var mainWindow = Application.Current.MainWindow as MainWindow;
//            if (ushort.TryParse(RightSpeedTextBox.Text, out ushort val))
//            {
//                // Register 0x003F is typically Right Motor Speed
//                mainWindow?.BLLogic.SendWriteCommand(1, 0x003F, val);
//            }
//        }

//        private void SetRightAccel_Click(object sender, RoutedEventArgs e)
//        {
//            var mainWindow = Application.Current.MainWindow as MainWindow;
//            if (ushort.TryParse(RightAccelTextBox.Text, out ushort val))
//            {
//                // Register 0x0042 is typically Right Motor Accel
//                mainWindow?.BLLogic.SendWriteCommand(1, 0x0042, val);
//            }
//        }
//        private void SetDecel_Click(object sender, RoutedEventArgs e)
//        {
//            var mainWindow = Application.Current.MainWindow as MainWindow;
//            if (mainWindow?.BLLogic == null) return;

//            if (ushort.TryParse(RightDecelTextBox.Text, out ushort val))
//            {
//                // Register 0x0043 is typically Right Motor Decel
//                mainWindow?.BLLogic.SendWriteCommand(1, 0x0043, val);
//            }
//        }


//        private void TripleStateButton_Click(object sender, RoutedEventArgs e)
//        {
//            var mainWindow = Application.Current.MainWindow as MainWindow;
//            if (mainWindow?.BLLogic == null) return;

//            // Example Address 0x0048 for Right Motor control
//            if (_buttonState == 0 || _buttonState == 2)
//            {
//                // Start Motor (Speed 75)
//                mainWindow.BLLogic.SendWriteCommand(1, 0x0048, 75);
//                TripleStateButton.Content = "RUN";
//                _buttonState = 1;
//            }
//            else if (_buttonState == 1)
//            {
//                // Stop Motor
//                mainWindow.BLLogic.SendWriteCommand(1, 0x0048, 0);
//                TripleStateButton.Content = "STOP";
//                _buttonState = 2;
//            }
//        }
//    }
//}
using System.Windows;
using System.Windows.Controls;
using BLDC_Demo.Models;

namespace BLDC_Demo
{
    public partial class RightMdrControl : UserControl
    {
        private int _buttonState = 0;

        public RightMdrControl()
        {
            InitializeComponent();
        }

        public void UpdateMotorTelemetry(SubNode node)
        {
            if (node == null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!RightSpeedTextBox.IsFocused || node.RightSpeed == "0")
                    RightSpeedTextBox.Text = node.RightSpeed;

                if (!RightAccelTextBox.IsFocused || node.RightAcceleration == "0")
                    RightAccelTextBox.Text = node.RightAcceleration;

                if (!RightDecelTextBox.IsFocused || node.RightDeceleration == "0")
                    RightDecelTextBox.Text = node.RightDeceleration;

                if (!RightBrakeCombo.IsDropDownOpen)
                    RightBrakeCombo.SelectedIndex = node.RightBrakeIndex;

                if (!RightMotorCombo.IsDropDownOpen)
                    RightMotorCombo.SelectedIndex = node.RightMotorHallTypeIndex;
                RightCurrentTextBox.Text = node.RightCurrent;

                FlagHallSensor.Tag = node.RightHallSensorError;
                FlagOverCurrent.Tag = node.RightOverCurrent;
                FlagOverVoltage.Tag = node.RightOverVoltage;
                FlagUnderVoltage.Tag = node.RightUnderVoltage;
                FlagMotorStalled.Tag = node.RightMotorStalled;
                FlagOverTemp.Tag = node.RightOverTemperature;
            });
        }

        public void ClearUI()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                RightSpeedTextBox.Text = string.Empty;
                RightAccelTextBox.Text = string.Empty;
                RightDecelTextBox.Text = string.Empty;
                RightCurrentTextBox.Text = string.Empty;
                RightBrakeCombo.Text = string.Empty;
                FlagHallSensor.Tag = FlagOverCurrent.Tag = FlagOverVoltage.Tag =
                FlagUnderVoltage.Tag = FlagMotorStalled.Tag = FlagOverTemp.Tag = null;
                RightMotorCombo.Text = string.Empty;
                _buttonState = 0;
                TripleStateButton.Content = "▶ RUN";

               
            });
        }
        private void SetRightBrake_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow?.BLLogic == null || RightBrakeCombo.SelectedIndex == -1) return;

            ushort val = (ushort)RightBrakeCombo.SelectedIndex;
            // Register 0x0046 for Right
            mainWindow.BLLogic.SendWriteCommand(1, 0x0046, val);
        }
        private void SetRightSpeed_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (ushort.TryParse(RightSpeedTextBox.Text, out ushort val))
                mainWindow?.BLLogic.SendWriteCommand(1, 0x003F, val);
        }
        private void SetRightMotorType_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow?.BLLogic == null || RightMotorCombo.SelectedIndex == -1) return;

            ushort val = (ushort)RightMotorCombo.SelectedIndex;
            // Register 0x002E for Left
            mainWindow.BLLogic.SendWriteCommand(1, 0x0002, val);
        }
        private void SetRightAccel_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (ushort.TryParse(RightAccelTextBox.Text, out ushort val))
                mainWindow?.BLLogic.SendWriteCommand(1, 0x0042, val);
        }

        private void SetDecel_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (ushort.TryParse(RightDecelTextBox.Text, out ushort val))
                mainWindow?.BLLogic.SendWriteCommand(1, 0x0043, val);
        }

        private void TripleStateButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow?.BLLogic == null) return;

            if (_buttonState == 0 || _buttonState == 2)
            {
                mainWindow.BLLogic.SendWriteCommand(1, 0x0048, 75);
                TripleStateButton.Content = "STOP";
                _buttonState = 1;
            }
            else
            {
                mainWindow.BLLogic.SendWriteCommand(1, 0x0048, 0);
                TripleStateButton.Content = "RUN";
                _buttonState = 2;
            }
        }
    }
}