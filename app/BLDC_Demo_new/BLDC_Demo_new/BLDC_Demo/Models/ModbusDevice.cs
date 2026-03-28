using System.Collections.ObjectModel;
using System.ComponentModel;

namespace BLDC_Demo.Models
{
    public class SubNode : INotifyPropertyChanged
    {
        private string _ipAddress;
        public string IpAddress
        {
            get => _ipAddress;
            set { _ipAddress = value; OnPropertyChanged(nameof(IpAddress)); }
        }
        public string MacAddress { get; set; }
        public string SeqId { get; set; }
        public string Status { get; set; }

        private bool _isActive = false;
        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(nameof(IsActive)); }
        }
        private int _leftBrakeIndex;
        public int LeftBrakeIndex
        {
            get => _leftBrakeIndex;
            set { _leftBrakeIndex = value; OnPropertyChanged(nameof(LeftBrakeIndex)); }
        }

        private int _rightMotorHallTypeIndex;
        public int RightMotorHallTypeIndex
        {
            get => _rightMotorHallTypeIndex;
            set { _rightMotorHallTypeIndex = value; OnPropertyChanged(nameof(RightMotorHallTypeIndex)); }
        }
        private int _leftMotorHallTypeIndex;
        public int LeftMotorHallTypeIndex
        {
            get => _leftMotorHallTypeIndex;
            set { _leftMotorHallTypeIndex = value; OnPropertyChanged(nameof(LeftMotorHallTypeIndex)); }
        }

        private int _rightBrakeIndex;
        public int RightBrakeIndex
        {
            get => _rightBrakeIndex;
            set { _rightBrakeIndex = value; OnPropertyChanged(nameof(RightBrakeIndex)); }
        }
        // ==========================================
        // --- LEFT MOTOR PROPERTIES ---
        // ==========================================
        private string _leftSpeed = "0";
        public string LeftSpeed { get => _leftSpeed; set { _leftSpeed = value; OnPropertyChanged(nameof(LeftSpeed)); } }

        private string _leftAcceleration = "0";
        public string LeftAcceleration { get => _leftAcceleration; set { _leftAcceleration = value; OnPropertyChanged(nameof(LeftAcceleration)); } }

        private string _leftDirection = "CW";
        public string LeftDirection { get => _leftDirection; set { _leftDirection = value; OnPropertyChanged(nameof(LeftDirection)); } }

        private string _leftDeceleration = "0";
        public string LeftDeceleration { get => _leftDeceleration; set { _leftDeceleration = value; OnPropertyChanged(nameof(LeftDeceleration)); } }

        private string _leftCurrent = "0";
        public string LeftCurrent { get => _leftCurrent; set { _leftCurrent = value; OnPropertyChanged(nameof(LeftCurrent)); } }

        // --- LEFT MOTOR ERROR FLAGS ---
        private bool _leftHallSensorError;
        public bool LeftHallSensorError { get => _leftHallSensorError; set { _leftHallSensorError = value; OnPropertyChanged(nameof(LeftHallSensorError)); } }

        private bool _leftOverCurrent;
        public bool LeftOverCurrent { get => _leftOverCurrent; set { _leftOverCurrent = value; OnPropertyChanged(nameof(LeftOverCurrent)); } }

        private bool _leftOverVoltage;
        public bool LeftOverVoltage { get => _leftOverVoltage; set { _leftOverVoltage = value; OnPropertyChanged(nameof(LeftOverVoltage)); } }

        private bool _leftUnderVoltage;
        public bool LeftUnderVoltage { get => _leftUnderVoltage; set { _leftUnderVoltage = value; OnPropertyChanged(nameof(LeftUnderVoltage)); } }

        private bool _leftMotorStalled;
        public bool LeftMotorStalled { get => _leftMotorStalled; set { _leftMotorStalled = value; OnPropertyChanged(nameof(LeftMotorStalled)); } }

        private bool _leftOverTemperature;
        public bool LeftOverTemperature { get => _leftOverTemperature; set { _leftOverTemperature = value; OnPropertyChanged(nameof(LeftOverTemperature)); } }
        // --- BRAKE STATUS PROPERTIES ---

       

       
        /// <summary>
        /// Parses the raw 16-bit status word for the Left Motor (Register 0x003A / 4:0058)
        /// </summary>
        public void ParseLeftStatus(ushort rawStatus)
        {
            // Bitmapping based on documentation
            LeftOverVoltage = (rawStatus & (1 << 6)) != 0;  // bit 06
            LeftUnderVoltage = (rawStatus & (1 << 7)) != 0;  // bit 07
            LeftOverTemperature = (rawStatus & (1 << 8)) != 0;  // bit 08
            LeftOverCurrent = (rawStatus & (1 << 9)) != 0;  // bit 09
            LeftMotorStalled = (rawStatus & (1 << 13)) != 0; // bit 13
            LeftHallSensorError = (rawStatus & (1 << 14)) != 0; // bit 14
        }
        //public void ClearValues()
        //{
        //    // 1. Wipe strings so TextBoxes show nothing
        //    LeftSpeed = RightSpeed = string.Empty;
        //    LeftCurrent = RightCurrent = string.Empty;
        //    LeftAcceleration = RightAcceleration = string.Empty;
        //    LeftDeceleration = RightDeceleration = string.Empty;
        //    LeftDirection = RightDirection = string.Empty;
        //    LeftBrakeIndex = -1;
        //    RightBrakeIndex = -1;
        //    // 2. Reset bools to false so Ellipses turn Gray
        //    LeftHallSensorError = LeftOverCurrent = LeftOverVoltage =
        //    LeftUnderVoltage = LeftMotorStalled = LeftOverTemperature = false;

        //    RightHallSensorError = RightOverCurrent = RightOverVoltage =
        //    RightUnderVoltage = RightMotorStalled = RightOverTemperature = false;

        //    // 3. IMPORTANT: Tell WPF that the data has changed
        //    OnPropertyChanged(string.Empty);
        //}
        //public void ResetData()
        //{
        //    // Wipe the strings so the TextBoxes go blank
        //    LeftSpeed = string.Empty;
        //    LeftCurrent = string.Empty;
        //    LeftAcceleration = string.Empty;
        //    LeftDeceleration = string.Empty;

        //    RightSpeed = string.Empty;
        //    RightCurrent = string.Empty;
        //    RightAcceleration = string.Empty;
        //    RightDeceleration = string.Empty;

        //    // Set errors to false so the UI dots turn Gray
        //    LeftHallSensorError = LeftOverCurrent = LeftOverVoltage = false;
        //    RightHallSensorError = RightOverCurrent = RightOverVoltage = false;
        //    LeftBrakeIndex = -1;
        //    RightBrakeIndex = -1;
        //    // This tells the UI: "The data changed, please look at me again!"
        //    OnPropertyChanged(string.Empty);
        //}

        // ==========================================
        // --- RIGHT MOTOR PROPERTIES ---
        // ==========================================
        private string _rightSpeed = "0";
        public string RightSpeed { get => _rightSpeed; set { _rightSpeed = value; OnPropertyChanged(nameof(RightSpeed)); } }

        private string _rightAcceleration = "0";
        public string RightAcceleration { get => _rightAcceleration; set { _rightAcceleration = value; OnPropertyChanged(nameof(RightAcceleration)); } }

        private string _rightDirection = "CW";
        public string RightDirection { get => _rightDirection; set { _rightDirection = value; OnPropertyChanged(nameof(RightDirection)); } }

        private string _rightDeceleration = "0";
        public string RightDeceleration { get => _rightDeceleration; set { _rightDeceleration = value; OnPropertyChanged(nameof(RightDeceleration)); } }

        private string _rightCurrent = "0";
        public string RightCurrent { get => _rightCurrent; set { _rightCurrent = value; OnPropertyChanged(nameof(RightCurrent)); } }
     

        // --- RIGHT MOTOR ERROR FLAGS ---
        private bool _rightHallSensorError;
        public bool RightHallSensorError { get => _rightHallSensorError; set { _rightHallSensorError = value; OnPropertyChanged(nameof(RightHallSensorError)); } }

        private bool _rightOverCurrent;
        public bool RightOverCurrent { get => _rightOverCurrent; set { _rightOverCurrent = value; OnPropertyChanged(nameof(RightOverCurrent)); } }

        private bool _rightOverVoltage;
        public bool RightOverVoltage { get => _rightOverVoltage; set { _rightOverVoltage = value; OnPropertyChanged(nameof(RightOverVoltage)); } }

        private bool _rightUnderVoltage;
        public bool RightUnderVoltage { get => _rightUnderVoltage; set { _rightUnderVoltage = value; OnPropertyChanged(nameof(RightUnderVoltage)); } }

        private bool _rightMotorStalled;
        public bool RightMotorStalled { get => _rightMotorStalled; set { _rightMotorStalled = value; OnPropertyChanged(nameof(RightMotorStalled)); } }

        private bool _rightOverTemperature;
        public bool RightOverTemperature { get => _rightOverTemperature; set { _rightOverTemperature = value; OnPropertyChanged(nameof(RightOverTemperature)); } }

        /// <summary>
        /// Parses the raw 16-bit status word for the Right Motor (Register 0x0052 / 4:0082)
        /// </summary>
        public void ParseRightStatus(ushort rawStatus)
        {
            // Bitmapping based on documentation
            RightOverVoltage = (rawStatus & (1 << 6)) != 0;  // bit 06
            RightUnderVoltage = (rawStatus & (1 << 7)) != 0;  // bit 07
            RightOverTemperature = (rawStatus & (1 << 8)) != 0;  // bit 08
            RightOverCurrent = (rawStatus & (1 << 9)) != 0;  // bit 09
            RightMotorStalled = (rawStatus & (1 << 13)) != 0; // bit 13
            RightHallSensorError = (rawStatus & (1 << 14)) != 0; // bit 14
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
    public class ModbusDevice : INotifyPropertyChanged
    {
        public string PortName { get; set; }
        private bool _isActive = false;
        public bool IsActive { get => _isActive; set { _isActive = value; OnPropertyChanged(nameof(IsActive)); } }
        private bool _hasDeviceInfo = false;
        public bool HasDeviceInfo { get => _hasDeviceInfo; set { _hasDeviceInfo = value; OnPropertyChanged(nameof(HasDeviceInfo)); } }
        public ObservableCollection<SubNode> ConnectedNodes { get; } = new ObservableCollection<SubNode>();
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        public void Reset() { IsActive = false; }
    }
}