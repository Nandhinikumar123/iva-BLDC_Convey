using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace YourNamespace
{
    public partial class FirmwareUploadControl : UserControl
    {
        public FirmwareUploadControl()
        {
            InitializeComponent();
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Firmware .BIN File",
                Filter = "BIN Files (*.bin)|*.bin|All Files (*.*)|*.*",
                FilterIndex = 1
            };

            if (dialog.ShowDialog() == true)
            {
                TxtFirmwarePath.Text = dialog.FileName;
            }
        }

        private void BtnUploadOne_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtFirmwarePath.Text))
            {
                AppendOutput("ERROR: Please select a firmware .BIN file first.");
                return;
            }

            if (LstDevices.SelectedItem == null)
            {
                AppendOutput("ERROR: Please select a device to upload to.");
                return;
            }

            SetUploadingState(true);
            AppendOutput($"Uploading to selected device: {LstDevices.SelectedItem}...");

            // TODO: Add your actual upload logic here
        }

        private void BtnUploadAll_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtFirmwarePath.Text))
            {
                AppendOutput("ERROR: Please select a firmware .BIN file first.");
                return;
            }

            if (LstDevices.Items.Count == 0)
            {
                AppendOutput("ERROR: No devices available to upload to.");
                return;
            }

            SetUploadingState(true);
            AppendOutput($"Uploading to all {LstDevices.Items.Count} device(s)...");

            // TODO: Add your actual upload-all logic here
        }

        private void BtnAbortUploading_Click(object sender, RoutedEventArgs e)
        {
            AppendOutput("Upload aborted by user.");
            SetUploadingState(false);

            // TODO: Add your actual abort logic here (e.g., cancel a CancellationToken)
        }

        // Call this when upload finishes (success or failure)
        public void OnUploadFinished(bool success, string message = null)
        {
            SetUploadingState(false);
            AppendOutput(success
                ? $"Upload completed successfully. {message}"
                : $"Upload failed. {message}");
        }

        // Add a device to the list (call from parent/viewmodel)
        public void AddDevice(string deviceName)
        {
            LstDevices.Items.Add(deviceName);
        }

        private void SetUploadingState(bool isUploading)
        {
            BtnUploadOne.IsEnabled = !isUploading;
            BtnUploadAll.IsEnabled = !isUploading;
            BtnBrowse.IsEnabled = !isUploading;
            BtnAbortUploading.IsEnabled = isUploading;
        }

        private void AppendOutput(string message)
        {
            TxtOutput.AppendText(message + "\n");
            TxtOutput.ScrollToEnd();
        }
    }
}
