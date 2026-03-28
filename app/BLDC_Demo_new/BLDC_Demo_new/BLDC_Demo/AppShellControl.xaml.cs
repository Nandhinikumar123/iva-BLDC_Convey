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

namespace BLDC_Demo
{
    /// <summary>
    /// Interaction logic for AppShellControl.xaml
    /// </summary>
    public partial class AppShellControl : UserControl
    {
        public AppShellControl()
        {
            InitializeComponent();
        }

        public void UpdateMdrUI(SubNode activeNode)
        {
            LeftMdr.UpdateMotorTelemetry(activeNode);
            RightMdr.UpdateMotorTelemetry(activeNode);
        }

        // Add this helper to clear both at once
        public void ResetAllMotors()
        {
            LeftMdr.ClearUI();
            RightMdr.ClearUI();
        }
    }
}
