////using System.Windows;
////using System.Windows.Controls;
////using System.Windows.Media;
////using BLDC_Demo.Models; // Added to access SubNode

////namespace BLDC_Demo
////{
////    public partial class LeftMdrControl : UserControl
////    {
////        // 0 = Initial (Gray), 1 = Running (Green), 2 = Stopped (Red)
////        private int _buttonState = 0;

////        public LeftMdrControl()
////        {
////            InitializeComponent();
////        }

////        // --- NEW METHOD TO INJECT DATA INTO THE UI ---
////        //public void UpdateMotorTelemetry(SubNode node)
////        //{
////        //    // Use the Dispatcher to prevent "Thread Access" errors
////        //    Application.Current.Dispatcher.Invoke(() =>
////        //    {
////        //        // These match the x:Name tags in your XAML
////        //        LeftSpeedTextBox.Text = node.LeftSpeed;
////        //        LeftAccelTextBox.Text = node.LeftAcceleration;
////        //        LeftDecelTextBox.Text = node.LeftDeceleration;

////        //        // For the "Status" (CW/CCW), you might want to show it in a label or the button
////        //        // TripleStateButton.Content = node.LeftDirection;
////        //    });
////        //}
////        //public void UpdateMotorTelemetry(SubNode node)
////        //{
////        //    Application.Current.Dispatcher.Invoke(() =>
////        //    {
////        //        // Check IsFocused before updating. 
////        //        // If you are typing, IsFocused is true, and the UI won't flicker back to the old value.

////        //        if (!LeftSpeedTextBox.IsFocused)
////        //            LeftSpeedTextBox.Text = node.LeftSpeed;

////        //        if (!LeftAccelTextBox.IsFocused)
////        //            LeftAccelTextBox.Text = node.LeftAcceleration;

////        //        if (!LeftDecelTextBox.IsFocused)
////        //            LeftDecelTextBox.Text = node.LeftDeceleration;

////        //        // Current is read-only, so we update it always
////        //        LeftCurrentTextBox.Text = node.LeftCurrent;

////        //    });
////        //}
////        public void UpdateMotorTelemetry(SubNode node)
////        {
////            // Safety 1: If the control is "Ghosted" (IsEnabled = false), 
////            // stop any updates from filling the boxes back up.
////            if (!this.IsEnabled || node == null) return;

////            Application.Current.Dispatcher.Invoke(() =>
////            {
////                // Safety 2: Check IsFocused for each input field.
////                // This prevents the UI from overwriting what you are currently typing.

////                // --- SPEED (%) ---
////                if (!LeftSpeedTextBox.IsFocused)
////                {
////                    LeftSpeedTextBox.Text = node.LeftSpeed;
////                }

////                // --- ACCELERATION (sec) ---
////                if (!LeftAccelTextBox.IsFocused)
////                {
////                    LeftAccelTextBox.Text = node.LeftAcceleration;
////                }

////                // --- DECELERATION (sec) ---
////                if (!LeftDecelTextBox.IsFocused)
////                {
////                    LeftDecelTextBox.Text = node.LeftDeceleration;
////                }

////                // --- CURRENT (mA) ---
////                // Current is usually read-only/telemetry only, so we update it always
////                LeftCurrentTextBox.Text = node.LeftCurrent;

////                // --- OPTIONAL: UPDATE ERROR FLAGS ---
////                // If you have status circles or checkboxes for errors, update them here:
////                // HallErrorCircle.Fill = node.HasHallError ? Brushes.Red : Brushes.LightGray;
////            });
////        }
////        public void ClearUI()
////        {
////            Dispatcher.Invoke(() =>
////            {
////                // 1. Kill the data binding to drop the "ghost" values
////                this.DataContext = null;

////                // 2. Force the boxes to empty
////                LeftSpeedTextBox.Text = string.Empty;
////                LeftAccelTextBox.Text = string.Empty;
////                LeftDecelTextBox.Text = string.Empty;
////                LeftCurrentTextBox.Text = string.Empty;

////                // 3. Visual feedback
////                this.IsEnabled = false;
////                this.Opacity = 0.5;
////            });
////        }
////        // ---------------------------------------------
////        // --- SPEED SET ---
////        private void SetSpeed_Click(object sender, RoutedEventArgs e)
////        {
////            var mainWindow = Application.Current.MainWindow as MainWindow;
////            if (mainWindow?.BLLogic == null) return;

////            if (ushort.TryParse(LeftSpeedTextBox.Text, out ushort val))
////            {
////                // Register 0x0027 for Left Motor Speed
////                mainWindow.BLLogic.SendWriteCommand(1, 0x0027, val);
////            }
////        }

////        // --- ACCELERATION SET ---
////        private void SetAccel_Click(object sender, RoutedEventArgs e)
////        {
////            var mainWindow = Application.Current.MainWindow as MainWindow;
////            if (mainWindow?.BLLogic == null) return;

////            if (ushort.TryParse(LeftAccelTextBox.Text, out ushort val))
////            {
////                // Register 0x002A for Left Motor Accel
////                mainWindow.BLLogic.SendWriteCommand(1, 0x002A, val);
////            }
////        }

////        // --- DECELERATION SET ---
////        private void SetDecel_Click(object sender, RoutedEventArgs e)
////        {
////            var mainWindow = Application.Current.MainWindow as MainWindow;
////            if (mainWindow?.BLLogic == null) return;

////            if (ushort.TryParse(LeftDecelTextBox.Text, out ushort val))
////            {
////                // Register 0x002B for Left Motor Decel
////                mainWindow.BLLogic.SendWriteCommand(1, 0x002B, val);
////            }
////        }
////        private void TripleStateButton_Click(object sender, RoutedEventArgs e)
////        {
////            var mainWindow = Application.Current.MainWindow as MainWindow;

////            // Safety check: Make sure we can talk to the Modbus brain
////            if (mainWindow == null || mainWindow.BLLogic == null) return;

////            // STATE LOGIC: If currently Gray (0) or Red (2), switch to Green (1)
////            if (_buttonState == 0 || _buttonState == 2)
////            {
////                // Action: Send Value 50
////                // This generates Frame: 01 06 00 30 00 32 08 11
////                mainWindow.BLLogic.SendWriteCommand(1, 0x0030, 75);

////                // Update UI to Green
////                TripleStateButton.Content = "RUN";

////                _buttonState = 1;
////            }
////            // STATE LOGIC: If currently Green (1), switch to Red (2)
////            else if (_buttonState == 1)
////            {
////                // Action: Send Value 0
////                // This generates Frame: 01 06 00 30 00 00 89 C5
////                mainWindow.BLLogic.SendWriteCommand(1, 0x0030, 0);

////                // Update UI to Red
////                TripleStateButton.Content = "STOP";

////                _buttonState = 2;
////            }
////        }
////    }
////}
//using System.Windows;
//using System.Windows.Controls;
//using System.Windows.Media;
//using BLDC_Demo.Models;

//namespace BLDC_Demo
//{
//    public partial class LeftMdrControl : UserControl
//    {
//        // 0 = Initial (Gray), 1 = Running (Green), 2 = Stopped (Red)
//        private int _buttonState = 0;

//        public LeftMdrControl()
//        {
//            InitializeComponent();
//        }

//        /// <summary>
//        /// Updates the UI with fresh data from the ESP32.
//        /// Handles bitwise flag conversion for the status LEDs.
//        /// </summary>
//        public void UpdateMotorTelemetry(SubNode node)
//        {
//            if (!this.IsEnabled || node == null) return;

//            Application.Current.Dispatcher.Invoke(() =>
//            {
//                // Update TextBoxes only if user is not currently typing
//                if (!LeftSpeedTextBox.IsFocused)
//                    LeftSpeedTextBox.Text = node.LeftSpeed;

//                if (!LeftAccelTextBox.IsFocused)
//                    LeftAccelTextBox.Text = node.LeftAcceleration;

//                if (!LeftDecelTextBox.IsFocused)
//                    LeftDecelTextBox.Text = node.LeftDeceleration;

//                // Current is read-only telemetry
//                LeftCurrentTextBox.Text = node.LeftCurrent;

//                // --- UPDATE ERROR FLAGS (Tag Binding Logic) ---
//                // We set the Tag property which triggers the XAML Style to change color
//                FlagHallSensor.Tag = node.LeftHallSensorError;   // bit 14
//                FlagOverCurrent.Tag = node.LeftOverCurrent;       // bit 09
//                FlagOverVoltage.Tag = node.LeftOverVoltage;       // bit 06
//                FlagUnderVoltage.Tag = node.LeftUnderVoltage;      // bit 07
//                FlagMotorStalled.Tag = node.LeftMotorStalled;      // bit 13
//                FlagOverTemp.Tag = node.LeftOverTemperature;   // bit 08
//            });
//        }

//        public void ClearUI()
//        {
//            Dispatcher.Invoke(() =>
//            {
//                this.DataContext = null;
//                LeftSpeedTextBox.Text = string.Empty;
//                LeftAccelTextBox.Text = string.Empty;
//                LeftDecelTextBox.Text = string.Empty;
//                LeftCurrentTextBox.Text = string.Empty;

//                // Reset Flags to false/grey
//                FlagHallSensor.Tag = FlagOverCurrent.Tag = FlagOverVoltage.Tag =
//           FlagUnderVoltage.Tag = FlagMotorStalled.Tag = FlagOverTemp.Tag = null;
//                this.IsEnabled = false;
//                this.Opacity = 0.5;
//            });
//        }

//        private void SetSpeed_Click(object sender, RoutedEventArgs e)
//        {
//            var mainWindow = Application.Current.MainWindow as MainWindow;
//            if (mainWindow?.BLLogic == null || !ushort.TryParse(LeftSpeedTextBox.Text, out ushort val)) return;

//            // Register 0x0027 for Left Motor Speed
//            mainWindow.BLLogic.SendWriteCommand(1, 0x0027, val);
//        }

//        private void SetAccel_Click(object sender, RoutedEventArgs e)
//        {
//            var mainWindow = Application.Current.MainWindow as MainWindow;
//            if (mainWindow?.BLLogic == null || !ushort.TryParse(LeftAccelTextBox.Text, out ushort val)) return;

//            mainWindow.BLLogic.SendWriteCommand(1, 0x002A, val);
//        }

//        private void SetDecel_Click(object sender, RoutedEventArgs e)
//        {
//            var mainWindow = Application.Current.MainWindow as MainWindow;
//            if (mainWindow?.BLLogic == null || !ushort.TryParse(LeftDecelTextBox.Text, out ushort val)) return;

//            mainWindow.BLLogic.SendWriteCommand(1, 0x002B, val);
//        }

//        private void TripleStateButton_Click(object sender, RoutedEventArgs e)
//        {
//            var mainWindow = Application.Current.MainWindow as MainWindow;
//            if (mainWindow?.BLLogic == null) return;

//            if (_buttonState == 0 || _buttonState == 2)
//            {
//                mainWindow.BLLogic.SendWriteCommand(1, 0x0030, 75);
//                TripleStateButton.Content = "STOP";
//                _buttonState = 1;
//            }
//            else
//            {
//                mainWindow.BLLogic.SendWriteCommand(1, 0x0030, 0);
//                TripleStateButton.Content = "RUN";
//                _buttonState = 2;
//            }
//        }
//    }
//}
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BLDC_Demo.Models;

namespace BLDC_Demo
{
    public partial class LeftMdrControl : UserControl
    {
        // 0 = Initial, 1 = Running, 2 = Stopped
        private int _buttonState = 0;

        public LeftMdrControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Updates the UI with telemetry data for the LEFT motor.
        /// </summary>
        //public void UpdateMotorTelemetry(SubNode node)
        //{
        //    // CRITICAL: We only return if node is null. 
        //    // We allow the update even if IsEnabled is false to facilitate clearing.
        //    if (node == null) return;

        //    Application.Current.Dispatcher.Invoke(() =>
        //    {
        //        // Update text if not focused OR if we are forcing a "0" clear
        //        if (!LeftSpeedTextBox.IsFocused || node.LeftSpeed == "0")
        //            LeftSpeedTextBox.Text = node.LeftSpeed;

        //        if (!LeftAccelTextBox.IsFocused || node.LeftAcceleration == "0")
        //            LeftAccelTextBox.Text = node.LeftAcceleration;

        //        if (!LeftDecelTextBox.IsFocused || node.LeftDeceleration == "0")
        //            LeftDecelTextBox.Text = node.LeftDeceleration;

        //        // Current is always updated as it is read-only
        //        LeftCurrentTextBox.Text = node.LeftCurrent;

        //        // --- UPDATE ERROR FLAGS (Tag Binding Logic) ---
        //        FlagHallSensor.Tag = node.LeftHallSensorError;
        //        FlagOverCurrent.Tag = node.LeftOverCurrent;
        //        FlagOverVoltage.Tag = node.LeftOverVoltage;
        //        FlagUnderVoltage.Tag = node.LeftUnderVoltage;
        //        FlagMotorStalled.Tag = node.LeftMotorStalled;
        //        FlagOverTemp.Tag = node.LeftOverTemperature;
        //    });
        //}
        public void UpdateMotorTelemetry(SubNode node)
        {
            if (node == null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                // --- EXISTING TEXTBOX UPDATES ---
                if (!LeftSpeedTextBox.IsFocused || node.LeftSpeed == "0")
                    LeftSpeedTextBox.Text = node.LeftSpeed;

                if (!LeftAccelTextBox.IsFocused || node.LeftAcceleration == "0")
                    LeftAccelTextBox.Text = node.LeftAcceleration;

                if (!LeftDecelTextBox.IsFocused || node.LeftDeceleration == "0")
                    LeftDecelTextBox.Text = node.LeftDeceleration;

                LeftCurrentTextBox.Text = node.LeftCurrent;

                // --- NEW COMBOBOX BINDING FIX ---
                // This forces the ComboBox to select the correct index (0, 1, or 2)
                // only if the user isn't currently trying to change it.
                if (!LeftBrakeCombo.IsDropDownOpen)
                    LeftBrakeCombo.SelectedIndex = node.LeftBrakeIndex;
            if (!LeftMotorCombo.IsDropDownOpen)
                LeftMotorCombo.SelectedIndex = node.LeftMotorHallTypeIndex;

           

        // --- EXISTING ERROR FLAGS ---
        FlagHallSensor.Tag = node.LeftHallSensorError;
                FlagOverCurrent.Tag = node.LeftOverCurrent;
                FlagOverVoltage.Tag = node.LeftOverVoltage;
                FlagUnderVoltage.Tag = node.LeftUnderVoltage;
                FlagMotorStalled.Tag = node.LeftMotorStalled;
                FlagOverTemp.Tag = node.LeftOverTemperature;
            });
        }
        /// <summary>
        /// Clears the UI and "ghosts" the control when disconnected.
        /// </summary>
        public void ClearUI()
        {
            Dispatcher.Invoke(() =>
            {
                // Clear the visual text
                LeftSpeedTextBox.Text = string.Empty; 
                LeftAccelTextBox.Text = string.Empty;
                LeftDecelTextBox.Text = string.Empty;
                LeftCurrentTextBox.Text = string.Empty;
                LeftBrakeCombo.Text = string.Empty;
                LeftMotorCombo.Text = string.Empty;
                // Reset Error Tags to null (Gray)
                FlagHallSensor.Tag = null;
                FlagOverCurrent.Tag = null;
                FlagOverVoltage.Tag = null;
                FlagUnderVoltage.Tag = null;
                FlagMotorStalled.Tag = null;
                FlagOverTemp.Tag = null;

                // Reset the button
                _buttonState = 0;
                TripleStateButton.Content = "▶ RUN";

                // Visual feedback for disconnected state
             
            });
        }

        private void SetSpeed_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow?.BLLogic == null || !ushort.TryParse(LeftSpeedTextBox.Text, out ushort val)) return;

            // Register 0x0027 for Left Motor Speed
            mainWindow.BLLogic.SendWriteCommand(1, 0x0027, val);
        }

        private void SetAccel_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow?.BLLogic == null || !ushort.TryParse(LeftAccelTextBox.Text, out ushort val)) return;

            // Register 0x002A for Left Motor Accel
            mainWindow.BLLogic.SendWriteCommand(1, 0x002A, val);
        }
        private void SetLeftBrake_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow?.BLLogic == null || LeftBrakeCombo.SelectedIndex == -1) return;

            ushort val = (ushort)LeftBrakeCombo.SelectedIndex;
            // Register 0x002E for Left
            mainWindow.BLLogic.SendWriteCommand(1, 0x002E, val);
        }
        private void SetLeftMotorType_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow?.BLLogic == null || LeftMotorCombo.SelectedIndex == -1) return;

            ushort val = (ushort)LeftMotorCombo.SelectedIndex;
            // Register 0x002E for Left
            mainWindow.BLLogic.SendWriteCommand(1, 0x0001, val);
        }
        private void SetDecel_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow?.BLLogic == null || !ushort.TryParse(LeftDecelTextBox.Text, out ushort val)) return;

            // Register 0x002B for Left Motor Decel
            mainWindow.BLLogic.SendWriteCommand(1, 0x002B, val);
        }

        private void TripleStateButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow?.BLLogic == null) return;

            if (_buttonState == 0 || _buttonState == 2)
            {
                // Start Motor (Speed 75)
                mainWindow.BLLogic.SendWriteCommand(1, 0x0030, 75);
                TripleStateButton.Content = "STOP";
                _buttonState = 1;
            }
            else
            {
                // Stop Motor
                mainWindow.BLLogic.SendWriteCommand(1, 0x0030, 0);
                TripleStateButton.Content = "RUN";
                _buttonState = 2;
            }
        }
    }
}