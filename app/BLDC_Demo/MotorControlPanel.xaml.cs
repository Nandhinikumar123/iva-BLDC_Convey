using System.Windows;
using System.Windows.Controls;

namespace BLDC_Demo
{
    public partial class MotorControlPanel : UserControl
    {
        public MotorControlPanel()
        {
            InitializeComponent();
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null && mainWindow.BLLogic != null)
            {
                // Generates: 01 06 00 30 00 01 48 05
                mainWindow.BLLogic.SendWriteCommand(1, 0x0030, 0x0001);
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null && mainWindow.BLLogic != null)
            {
                // Generates: 01 06 00 30 00 00 89 C5
                mainWindow.BLLogic.SendWriteCommand(1, 0x0030, 0x0000);
            }
        }

        // --- NEW: Handle Direction Dropdown Changes ---
        private void DirectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            var mainWindow = Application.Current.MainWindow as MainWindow;

            // Make sure the window is loaded and an item is actually selected
            if (comboBox == null || comboBox.SelectedIndex == -1 || mainWindow == null || mainWindow.BLLogic == null)
                return;

            if (comboBox.SelectedIndex == 0)
            {
                // Index 0 is "CLOCKWISE"
                // Generates: 01 06 00 47 00 01 F8 1F
                mainWindow.BLLogic.SendWriteCommand(1, 0x0047, 0x0001);
            }
            else if (comboBox.SelectedIndex == 1)
            {
                // Index 1 is "ANTI-CW"
                // Generates: 01 06 00 47 00 00 39 DE
                mainWindow.BLLogic.SendWriteCommand(1, 0x0047, 0x0000);
            }
        }
    }
}