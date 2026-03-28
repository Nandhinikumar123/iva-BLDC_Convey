//using System;
//using System.IO.Ports;
//using System.Threading;

//namespace BLDC_Demo.Controls
//{
//    public class CommRS485 : IComm, IDisposable
//    {
//        private readonly SerialPort _port;
//        private readonly string _portName;

//        public CommRS485(string portName, int baudRate = 115200)
//        {
//            _portName = portName;
//            _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
//            {
//                ReadTimeout = 500,
//                WriteTimeout = 500,
//                // THE FIX: Prevent Windows from triggering the ESP32 Reset button
//                DtrEnable = false,
//                RtsEnable = false
//            };
//        }

//        public string Name => _portName;
//        public bool IsOpen => _port != null && _port.IsOpen;

//        public bool Open()
//        {
//            try
//            {
//                if (!_port.IsOpen) _port.Open();
//                return true;
//            }
//            catch { return false; }
//        }
//        public void SendRaw(byte[] data)
//        {
//            // Use _port instead of _serialPort
//            if (IsOpen)
//            {
//                _port.Write(data, 0, data.Length);
//            }
//        }

//        public int ReceiveRaw(byte[] buffer)
//        {
//            // Use _port instead of _serialPort
//            if (!IsOpen || _port.BytesToRead == 0) return 0;
//            try
//            {
//                return _port.Read(buffer, 0, buffer.Length);
//            }
//            catch { return 0; }
//        }
//        public void Close() => _port?.Close();
//        public byte[] SendRecv(byte[] req)
//        {
//            if (!IsOpen) return null;
//            try
//            {
//                _port.DiscardInBuffer();
//                _port.DiscardOutBuffer(); // Clear everything

//                _port.Write(req, 0, req.Length);

//                // Give the RS485 chip and ESP32 time to switch DE/RE pins
//                Thread.Sleep(80);

//                if (_port.BytesToRead > 0)
//                {
//                    byte[] res = new byte[_port.BytesToRead];
//                    _port.Read(res, 0, res.Length);
//                    return res;
//                }
//                return null;
//            }
//            catch { return null; }
//        }

//        public void Dispose() { Close(); _port?.Dispose(); }
//    }
//}
////using System;
////using System.IO.Ports;
////using System.Threading;

////namespace BLDC_Demo.Controls
////{
////    public class CommRS485 : IComm
////    {
////        private SerialPort _sp;
////        public string Name { get; }
////        public bool IsOpen => _sp?.IsOpen ?? false;

////        public CommRS485(string portName)
////        {
////            Name = portName;
////            // Standard Modbus RTU settings for ESP32
////            _sp = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One)
////            {
////                ReadTimeout = 500,
////                WriteTimeout = 500,
////                DtrEnable = false, // Prevents ESP32 from resetting on connect
////                RtsEnable = false
////            };
////        }

////        public bool Open()
////        {
////            try { if (!_sp.IsOpen) _sp.Open(); return true; }
////            catch { return false; }
////        }

////        public void Close() => _sp?.Close();

////        public byte[] SendRecv(byte[] data)
////        {
////            if (!IsOpen) return null;
////            try
////            {
////                _sp.DiscardInBuffer();
////                _sp.Write(data, 0, data.Length);

////                // RS485 needs a moment for the slave to process and reply
////                Thread.Sleep(60);

////                if (_sp.BytesToRead > 0)
////                {
////                    byte[] buffer = new byte[_sp.BytesToRead];
////                    _sp.Read(buffer, 0, buffer.Length);
////                    return buffer;
////                }
////                return null;
////            }
////            catch { return null; }
////        }
////    }
////}
///
using System;
using System.IO.Ports;
using System.Threading;

namespace BLDC_Demo.Controls
{
    public class CommRS485 : IComm, IDisposable
    {
        private readonly SerialPort _port;
        private readonly string _portName;
        public event Action ConnectionLost; // Add this

        public void Close()
        {
             _port?.Close();
            // your serial close logic...
            ConnectionLost?.Invoke();
        }
        public CommRS485(string portName, int baudRate = 115200)
        {
            _portName = portName;
            _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 500,
                WriteTimeout = 500,
                // THE FIX: Prevent Windows from triggering the ESP32 Reset button
                DtrEnable = false,
                RtsEnable = false
            };
        }

        public string Name => _portName;
        public bool IsOpen => _port != null && _port.IsOpen;

        public bool Open()
        {
            try
            {
                if (!_port.IsOpen) _port.Open();
                return true;
            }
            catch { return false; }
        }

        //public void Close() => _port?.Close();
        public byte[] SendRecv(byte[] req)
        {
            if (!IsOpen) return null;
            try
            {
                _port.DiscardInBuffer();
                _port.DiscardOutBuffer(); // Clear everything

                _port.Write(req, 0, req.Length);

                // Give the RS485 chip and ESP32 time to switch DE/RE pins
                Thread.Sleep(80);

                if (_port.BytesToRead > 0)
                {
                    byte[] res = new byte[_port.BytesToRead];
                    _port.Read(res, 0, res.Length);
                    return res;
                }
                return null;
            }
            catch { return null; }
        }

        public void Dispose() { Close(); _port?.Dispose(); }
    }
}