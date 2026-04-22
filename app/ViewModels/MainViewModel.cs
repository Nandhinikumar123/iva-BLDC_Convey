using BLDC_Demo.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BLDC_Demo.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private BLDCService _service;

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set { _isConnected = value; OnPropertyChanged(); }
        }

        private string _motorData;
        public string MotorData
        {
            get => _motorData;
            set { _motorData = value; OnPropertyChanged(); }
        }

        public MainViewModel()
        {
            _service = new BLDCService();

            _service.EthernetStatusChanged += (status) =>
            {
                IsConnected = status;
            };

            _service.DataReceived += (data) =>
            {
                MotorData += data + "\n";
            };

            _service.Start("192.168.150.22", 5000); // Change IP
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
