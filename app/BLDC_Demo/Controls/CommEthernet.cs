////using System;
////using System.Net.Sockets;
////using System.Threading;

////namespace BLDC_Demo.Controls
////{
////    internal class CommEthernet : IComm, IDisposable
////    {
////        private TcpClient _client;
////        private NetworkStream _stream;
////        private readonly string _ip;
////        private readonly int _port;

////        public CommEthernet(string ipAddress, int port = 502)
////        {
////            _ip = ipAddress;
////            _port = port;
////        }

////        public string Name => $"{_ip}:{_port}";

////        public bool IsOpen
////        {
////            get
////            {
////                try
////                {
////                    if (_client == null || !_client.Connected || _client.Client == null) return false;
////                    // Actual check to see if the physical connection is still alive
////                    return !(_client.Client.Poll(1, SelectMode.SelectRead) && _client.Client.Available == 0);
////                }
////                catch { return false; }
////            }
////        }

////        public bool Open()
////        {
////            try
////            {
////                Close(); // Ensure we kill any "ghost" sessions first
////                _client = new TcpClient();

////                // Give the hardware time for the initial handshake
////                var result = _client.BeginConnect(_ip, _port, null, null);
////                bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));

////                if (!success || !_client.Connected) return false;

////                _stream = _client.GetStream();
////                _stream.ReadTimeout = 1000;
////                return true;
////            }
////            catch { return false; }
////        }

////        public byte[] SendRecv(byte[] req)
////        {
////            if (!IsOpen) { if (!Open()) return null; }

////            try
////            {
////                // FULL MODBUS TCP FRAME (6 byte MBAP Header + RTU Data)
////                byte[] frame = new byte[6 + req.Length];

////                frame[0] = 0x00; // Transaction ID Hi
////                frame[1] = 0x01; // Transaction ID Lo
////                frame[2] = 0x00; // Protocol ID Hi
////                frame[3] = 0x00; // Protocol ID Lo
////                frame[4] = (byte)(req.Length >> 8);   // Length Hi
////                frame[5] = (byte)(req.Length & 0xFF); // Length Lo

////                Array.Copy(req, 0, frame, 6, req.Length);

////                _stream.Write(frame, 0, frame.Length);
////                _stream.Flush(); // Force data out of the PC buffer immediately

////                Thread.Sleep(100); // Wait for ESP32/W5500 processing

////                if (_client.Available > 0)
////                {
////                    byte[] buffer = new byte[256];
////                    int received = _stream.Read(buffer, 0, buffer.Length);

////                    if (received <= 6) return null;

////                    // Remove the 6-byte TCP header and return just the Modbus RTU response
////                    byte[] res = new byte[received - 6];
////                    Array.Copy(buffer, 6, res, 0, res.Length);
////                    return res;
////                }
////                return null;
////            }
////            catch
////            {
////                Close(); // Force reset on any error
////                return null;
////            }
////        }

////        public void Close()
////        {
////            try
////            {
////                _stream?.Close();
////                _client?.Close();
////            }
////            catch { }
////            finally
////            {
////                _stream = null;
////                _client = null;
////            }
////        }

////        public void Dispose() => Close();
////    }
////}
////////using System;
////////using System.Net.Sockets;
////////using System.Threading;
////////using System.Threading.Tasks;

////////namespace BLDC_Demo.Controls
////////{
////////    internal class CommEthernet : IComm, IDisposable
////////    {
////////        private TcpClient _client;
////////        private NetworkStream _stream;
////////        private readonly string _ip;
////////        private readonly int _port;

////////        public CommEthernet(string ipAddress, int port = 502)
////////        {
////////            _ip = ipAddress;
////////            _port = port;
////////        }

////////        public string Name => _ip; // Returns the IP for the UI list

////////        public bool IsOpen
////////        {
////////            get
////////            {
////////                try
////////                {
////////                    if (_client == null || !_client.Connected || _client.Client == null) return false;
////////                    return !(_client.Client.Poll(1, SelectMode.SelectRead) && _client.Client.Available == 0);
////////                }
////////                catch { return false; }
////////            }
////////        }

////////        public bool Open()
////////        {
////////            try
////////            {
////////                Close();
////////                _client = new TcpClient();
////////                var result = _client.BeginConnect(_ip, _port, null, null);
////////                bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));
////////                if (!success || !_client.Connected) return false;

////////                _stream = _client.GetStream();
////////                _stream.ReadTimeout = 1000;
////////                return true;
////////            }
////////            catch { return false; }
////////        }

////////        public byte[] SendRecv(byte[] req)
////////        {
////////            if (!IsOpen && !Open()) return null;

////////            try
////////            {
////////                // Build Modbus TCP Frame
////////                byte[] frame = new byte[6 + req.Length];
////////                frame[0] = 0x00; frame[1] = 0x01; // Transaction ID
////////                frame[2] = 0x00; frame[3] = 0x00; // Protocol ID
////////                frame[4] = (byte)(req.Length >> 8);
////////                frame[5] = (byte)(req.Length & 0xFF);
////////                Array.Copy(req, 0, frame, 6, req.Length);

////////                _stream.Write(frame, 0, frame.Length);
////////                _stream.Flush();

////////                Thread.Sleep(50); // Reduced delay for faster polling

////////                if (_client.Available > 0)
////////                {
////////                    byte[] buffer = new byte[256];
////////                    int received = _stream.Read(buffer, 0, buffer.Length);
////////                    if (received <= 6) return null;

////////                    byte[] res = new byte[received - 6];
////////                    Array.Copy(buffer, 6, res, 0, res.Length);
////////                    return res;
////////                }
////////                return null;
////////            }
////////            catch { Close(); return null; }
////////        }

////////        public void Close()
////////        {
////////            _stream?.Close();
////////            _client?.Close();
////////            _stream = null;
////////            _client = null;
////////        }

////////        public void Dispose() => Close();
////////    }
////////}
//////using System;
//////using System.Net.Sockets;
//////using System.Threading;
//////using System.Threading.Tasks;

//////namespace BLDC_Demo.Controls
//////{
//////    internal class CommEthernet : IComm, IDisposable
//////    {
//////        private TcpClient _client;
//////        private NetworkStream _stream;
//////        private readonly string _ip;
//////        private readonly int _port;

//////        public CommEthernet(string ipAddress, int port = 502)
//////        {
//////            _ip = ipAddress;
//////            _port = port;
//////        }

//////        public string Name => _ip; // Returns the IP for the UI list

//////        public bool IsOpen
//////        {
//////            get
//////            {
//////                try
//////                {
//////                    if (_client == null || !_client.Connected || _client.Client == null) return false;
//////                    // Check if the socket is actually still responsive
//////                    return !(_client.Client.Poll(1, SelectMode.SelectRead) && _client.Client.Available == 0);
//////                }
//////                catch { return false; }
//////            }
//////        }

//////        public bool Open()
//////        {
//////            try
//////            {
//////                Close();
//////                _client = new TcpClient();
//////                var result = _client.BeginConnect(_ip, _port, null, null);
//////                bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));

//////                if (!success || !_client.Connected) return false;

//////                _stream = _client.GetStream();
//////                _stream.ReadTimeout = 1000;
//////                return true;
//////            }
//////            catch { return false; }
//////        }

//////        // Modbus Specific: Automatically wraps request in MBAP header
//////        public byte[] SendRecv(byte[] req)
//////        {
//////            if (!IsOpen && !Open()) return null;

//////            try
//////            {
//////                // Build Modbus TCP Frame (6 byte Header + PDU)
//////                byte[] frame = new byte[6 + req.Length];
//////                frame[0] = 0x00; frame[1] = 0x01; // Transaction ID
//////                frame[2] = 0x00; frame[3] = 0x00; // Protocol ID
//////                frame[4] = (byte)(req.Length >> 8);
//////                frame[5] = (byte)(req.Length & 0xFF);
//////                Array.Copy(req, 0, frame, 6, req.Length);

//////                _stream.Write(frame, 0, frame.Length);
//////                _stream.Flush();

//////                Thread.Sleep(50); // Small wait for device processing

//////                if (_client.Available > 0)
//////                {
//////                    byte[] buffer = new byte[256];
//////                    int received = _stream.Read(buffer, 0, buffer.Length);
//////                    if (received <= 6) return null;

//////                    // Strip 6-byte header for the response
//////                    byte[] res = new byte[received - 6];
//////                    Array.Copy(buffer, 6, res, 0, res.Length);
//////                    return res;
//////                }
//////                return null;
//////            }
//////            catch { Close(); return null; }
//////        }

//////        // --- NEW: Implementation of IComm Raw methods ---

//////        public void SendRaw(byte[] data)
//////        {
//////            if (!IsOpen && !Open()) return;
//////            try
//////            {
//////                _stream?.Write(data, 0, data.Length);
//////                _stream?.Flush();
//////            }
//////            catch { Close(); }
//////        }

//////        public int ReceiveRaw(byte[] buffer)
//////        {
//////            if (!IsOpen || _stream == null) return 0;
//////            try
//////            {
//////                // Only read if there is data waiting to prevent blocking the UI/Background thread
//////                if (_client.Available > 0)
//////                {
//////                    return _stream.Read(buffer, 0, buffer.Length);
//////                }
//////            }
//////            catch { }
//////            return 0;
//////        }

//////        // --- Cleanup ---

//////        public void Close()
//////        {
//////            try
//////            {
//////                _stream?.Close();
//////                _client?.Close();
//////            }
//////            catch { }
//////            finally
//////            {
//////                _stream = null;
//////                _client = null;
//////            }
//////        }

//////        public void Dispose() => Close();
//////    }
//////}
//////using System;
//////using System.Net.Sockets;
//////using System.Threading;

//////namespace BLDC_Demo.Controls
//////{
//////    public class CommEthernet : IComm
//////    {
//////        private TcpClient _client;
//////        private NetworkStream _stream;

//////        // Fields for connection details
//////        private readonly string _ip;
//////        private readonly int _port;

//////        public string Name => _ip; // Returns the IP for the UI list
//////        public bool IsOpen => _client != null && _client.Connected;

//////        // FIX: Constructor now accepts 2 arguments to match code-behind calls
//////        public CommEthernet(string ipAddress, int port = 502)
//////        {
//////            _ip = ipAddress;
//////            _port = port;
//////        }

//////        public bool Open()
//////        {
//////            try
//////            {
//////                Close(); // Ensure old connection is cleared
//////                _client = new TcpClient();

//////                // Use the stored IP and Port
//////                var result = _client.BeginConnect(_ip, _port, null, null);
//////                bool success = result.AsyncWaitHandle.WaitOne(1000); // 1s Timeout

//////                if (success && _client.Connected)
//////                {
//////                    _stream = _client.GetStream();
//////                    _stream.ReadTimeout = 500;
//////                    return true;
//////                }
//////                return false;
//////            }
//////            catch { return false; }
//////        }

//////        public void Close()
//////        {
//////            try
//////            {
//////                _stream?.Close();
//////                _client?.Close();
//////            }
//////            catch { }
//////            finally
//////            {
//////                _stream = null;
//////                _client = null;
//////            }
//////        }

//////        public byte[] SendRecv(byte[] data)
//////        {
//////            if (!IsOpen) return null;
//////            try
//////            {
//////                _stream.Write(data, 0, data.Length);

//////                // Small sleep to allow ESP32/W5500 processing time
//////                Thread.Sleep(40);

//////                if (_client.Available > 0)
//////                {
//////                    byte[] buffer = new byte[_client.Available];
//////                    int received = _stream.Read(buffer, 0, buffer.Length);

//////                    // Return only the data actually read
//////                    if (received == buffer.Length) return buffer;

//////                    byte[] trimmed = new byte[received];
//////                    Array.Copy(buffer, trimmed, received);
//////                    return trimmed;
//////                }
//////                return null;
//////            }
//////            catch { return null; }
//////        }

//////        // Fix: Ensure this matches the format expected by BLLogic.ActivePortName
//////        public override string ToString()
//////        {
//////            return $"{_ip}:{_port}";
//////        }
//////    }
//////}
////using System;
////using System.Net.Sockets;
////using System.Threading;

////namespace BLDC_Demo.Controls
////{
////    internal class CommEthernet : IComm, IDisposable
////    {
////        private TcpClient _client;
////        private NetworkStream _stream;
////        private readonly string _ip;
////        private readonly int _port;

////        public CommEthernet(string ipAddress, int port = 502)
////        {
////            _ip = ipAddress;
////            _port = port;
////        }

////        public string Name => $"{_ip}:{_port}";

////        public bool IsOpen
////        {
////            get
////            {
////                try
////                {
////                    if (_client == null || !_client.Connected || _client.Client == null) return false;
////                    // Actual check to see if the physical connection is still alive
////                    return !(_client.Client.Poll(1, SelectMode.SelectRead) && _client.Client.Available == 0);
////                }
////                catch { return false; }
////            }
////        }

////        public bool Open()
////        {
////            try
////            {
////                Close(); // Ensure we kill any "ghost" sessions first
////                _client = new TcpClient();

////                // Give the hardware time for the initial handshake
////                var result = _client.BeginConnect(_ip, _port, null, null);
////                bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));

////                if (!success || !_client.Connected) return false;

////                _stream = _client.GetStream();
////                _stream.ReadTimeout = 1000;
////                return true;
////            }
////            catch { return false; }
////        }

////        public byte[] SendRecv(byte[] req)
////        {
////            if (!IsOpen) { if (!Open()) return null; }

////            try
////            {
////                // FULL MODBUS TCP FRAME (6 byte MBAP Header + RTU Data)
////                byte[] frame = new byte[6 + req.Length];

////                frame[0] = 0x00; // Transaction ID Hi
////                frame[1] = 0x01; // Transaction ID Lo
////                frame[2] = 0x00; // Protocol ID Hi
////                frame[3] = 0x00; // Protocol ID Lo
////                frame[4] = (byte)(req.Length >> 8);   // Length Hi
////                frame[5] = (byte)(req.Length & 0xFF); // Length Lo

////                Array.Copy(req, 0, frame, 6, req.Length);

////                _stream.Write(frame, 0, frame.Length);
////                _stream.Flush(); // Force data out of the PC buffer immediately

////                Thread.Sleep(100); // Wait for ESP32/W5500 processing

////                if (_client.Available > 0)
////                {
////                    byte[] buffer = new byte[256];
////                    int received = _stream.Read(buffer, 0, buffer.Length);

////                    if (received <= 6) return null;

////                    // Remove the 6-byte TCP header and return just the Modbus RTU response
////                    byte[] res = new byte[received - 6];
////                    Array.Copy(buffer, 6, res, 0, res.Length);
////                    return res;
////                }
////                return null;
////            }
////            catch
////            {
////                Close(); // Force reset on any error
////                return null;
////            }
////        }

////        public void Close()
////        {
////            try
////            {
////                _stream?.Close();
////                _client?.Close();
////            }
////            catch { }
////            finally
////            {
////                _stream = null;
////                _client = null;
////            }
////        }

////        public void Dispose() => Close();
////    }
////}
////using System;
////using System.Net.Sockets;
////using System.Threading;
////using System.Threading.Tasks;

////namespace BLDC_Demo.Controls
////{
////    internal class CommEthernet : IDisposable
////    {
////        private TcpClient _client;
////        private NetworkStream _stream;
////        private readonly string _ip;
////        private readonly int _port;
////        private bool _isListening = false;

////        // Event for the UI to subscribe to for Live Data
////        public event Action<byte[]> TelemetryReceived;

////        public CommEthernet(string ipAddress, int port = 502)
////        {
////            _ip = ipAddress;
////            _port = port;
////        }

////        public bool IsOpen => _client != null && _client.Connected;

////        public bool Open()
////        {
////            try
////            {
////                Close();
////                _client = new TcpClient();

////                // 1. Connect
////                var result = _client.BeginConnect(_ip, _port, null, null);
////                if (!result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2))) return false;

////                _stream = _client.GetStream();
////                _stream.ReadTimeout = 1000;

////                // 2. Perform Handshake (01 01)
////                byte[] handshake = new byte[] { 0x01, 0x01 };
////                _stream.Write(handshake, 0, handshake.Length);

////                byte[] response = new byte[2];
////                int read = _stream.Read(response, 0, 2);

////                if (read == 2 && response[0] == 0x01 && response[1] == 0x01)
////                {
////                    // 3. Handshake successful, start the background "ear"
////                    StartBackgroundRead();
////                    return true;
////                }

////                Close();
////                return false;
////            }
////            catch { return false; }
////        }

////        private void StartBackgroundRead()
////        {
////            _isListening = true;
////            Task.Run(() =>
////            {
////                byte[] buffer = new byte[1024];
////                while (_isListening && IsOpen)
////                {
////                    try
////                    {
////                        if (_client.Available > 0)
////                        {
////                            int received = _stream.Read(buffer, 0, buffer.Length);
////                            if (received > 0)
////                            {
////                                byte[] data = new byte[received];
////                                Array.Copy(buffer, data, received);

////                                // If the packet is a standard Modbus TCP frame (Length > 6)
////                                // Strip the 6-byte header and send to the UI
////                                if (data.Length > 6)
////                                {
////                                    byte[] rtuData = new byte[data.Length - 6];
////                                    Array.Copy(data, 6, rtuData, 0, rtuData.Length);
////                                    TelemetryReceived?.Invoke(rtuData);
////                                }
////                            }
////                        }
////                        Thread.Sleep(5); // Don't burn the CPU
////                    }
////                    catch { break; }
////                }
////            });
////        }

////        // Used for manual commands (like setting speed)
////        public void SendOnly(byte[] req)
////        {
////            if (!IsOpen) return;
////            try
////            {
////                // Wrap in Modbus TCP Header
////                byte[] frame = new byte[6 + req.Length];
////                frame[0] = 0x00; frame[1] = 0x01; // Trans ID
////                frame[2] = 0x00; frame[3] = 0x00; // Protocol
////                frame[4] = (byte)(req.Length >> 8);
////                frame[5] = (byte)(req.Length & 0xFF);
////                Array.Copy(req, 0, frame, 6, req.Length);

////                _stream.Write(frame, 0, frame.Length);
////                _stream.Flush();
////            }
////            catch { Close(); }
////        }

////        public void Close()
////        {
////            _isListening = false;
////            try
////            {
////                _stream?.Close();
////                _client?.Close();
////            }
////            catch { }
////            finally
////            {
////                _stream = null;
////                _client = null;
////            }
////        }

////        public void Dispose() => Close();
////    }
////}
//using System;
//using System.Net.Sockets;
//using System.Threading;
//using System.Threading.Tasks;

//namespace BLDC_Demo.Controls
//{
//    // Inheriting from IComm fixes the "cannot convert" error
//    internal class CommEthernet : IComm, IDisposable
//    {
//        private TcpClient _client;
//        private NetworkStream _stream;
//        private readonly string _ip;
//        private readonly int _port;
//        private bool _isListening = false;

//        // Event to push data to the BL logic
//        public event Action<byte[]> TelemetryReceived;

//        public CommEthernet(string ipAddress, int port = 502)
//        {
//            _ip = ipAddress;
//            _port = port;
//        }

//        // Fixes the "does not contain a definition for Name" error
//        public string Name => $"{_ip}:{_port}";

//        public bool IsOpen => _client != null && _client.Connected;

//        public bool Open()
//        {
//            try
//            {
//                Close();
//                _client = new TcpClient();
//                var result = _client.BeginConnect(_ip, _port, null, null);

//                if (!result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2))) return false;

//                _stream = _client.GetStream();
//                _stream.ReadTimeout = 1000;

//                // --- HANDSHAKE ---
//                byte[] handshake = new byte[] { 0x01, 0x01 };
//                _stream.Write(handshake, 0, handshake.Length);

//                byte[] response = new byte[2];
//                int read = _stream.Read(response, 0, 2);

//                if (read == 2 && response[0] == 0x01 && response[1] == 0x01)
//                {
//                    StartBackgroundRead();
//                    return true;
//                }
//                return false;
//            }
//            catch { return false; }
//        }
//        public event Action ConnectionLost; // Add this

//        public void Close()
//        {
//            _isListening = false;
//            _stream?.Close();
//            _client?.Close();
//            ConnectionLost?.Invoke(); // Trigger it here
//        }
//        private void StartBackgroundRead()
//        {
//            _isListening = true;
//            Task.Run(() =>
//            {
//                byte[] buffer = new byte[1024];
//                while (_isListening && IsOpen)
//                {
//                    try
//                    {
//                        if (_client.Available > 0)
//                        {
//                            int received = _stream.Read(buffer, 0, buffer.Length);
//                            if (received >= 12) // Minimum Modbus TCP frame size
//                            {
//                                byte[] data = new byte[received];
//                                Array.Copy(buffer, data, received);

//                                // Strip 6-byte MBAP header, send RTU part to BL
//                                byte[] rtuData = new byte[data.Length - 6];
//                                Array.Copy(data, 6, rtuData, 0, rtuData.Length);
//                                TelemetryReceived?.Invoke(rtuData);
//                            }
//                        }
//                        Thread.Sleep(5);
//                    }
//                    catch { break; }
//                }
//            });
//        }

//        public byte[] SendRecv(byte[] req)
//        {
//            if (!IsOpen) return null;
//            try
//            {
//                byte[] frame = new byte[6 + req.Length];
//                frame[0] = 0x00; frame[1] = 0x01; // Transaction ID
//                frame[4] = (byte)(req.Length >> 8);
//                frame[5] = (byte)(req.Length & 0xFF);
//                Array.Copy(req, 0, frame, 6, req.Length);

//                _stream.Write(frame, 0, frame.Length);
//                return null; // Telemetry is handled by the background thread
//            }
//            catch { return null; }
//        }

//        //public void Close()
//        //{
//        //    _isListening = false;
//        //    _stream?.Close();
//        //    _client?.Close();
//        //}

//        public void Dispose() => Close();
//    }
//}
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace BLDC_Demo.Controls
{
    internal class CommEthernet : IComm, IDisposable
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private readonly string _ip;
        private readonly int _port;
        private bool _isListening = false;

        // Events for UI and Logic updates
        public event Action<byte[]> TelemetryReceived;
        public event Action ConnectionLost;

        public CommEthernet(string ipAddress, int port = 502)
        {
            _ip = ipAddress;
            _port = port;
        }

        public string Name => $"{_ip}:{_port}";

        public bool IsOpen => _client != null && _client.Connected;

        public bool Open()
        {
            try
            {
                Close();
                _client = new TcpClient();

                // 1. Initiate Connection
                var result = _client.BeginConnect(_ip, _port, null, null);
                if (!result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2))) return false;

                _stream = _client.GetStream();
                _stream.ReadTimeout = 1000;

                // 2. Perform Custom Handshake (Send 01 01)
                byte[] handshake = new byte[] { 0x01, 0x01 };
                _stream.Write(handshake, 0, handshake.Length);
                _stream.Flush();

                // 3. Wait for Echo (01 01) from ESP32
                byte[] response = new byte[2];
                int read = _stream.Read(response, 0, 2);

                if (read == 2 && response[0] == 0x01 && response[1] == 0x01)
                {
                    Console.WriteLine("received");
                    // Handshake successful! Start listening for telemetry
                    StartBackgroundRead();
                    return true;
                }

                Close();
                return false;
            }
            catch { return false; }
        }

        //private void StartBackgroundRead()
        //{
        //    _isListening = true;
        //    _ = Task.Run(() =>
        //    {
        //        byte[] buffer = new byte[2048]; // Increased buffer size to be safe
        //        while (_isListening && IsOpen)
        //        {
        //            try
        //            {
        //                if (_client.Available > 0)
        //                {
        //                    int received = _stream.Read(buffer, 0, buffer.Length);

        //                    // A standard Modbus TCP frame has at least a 6-byte MBAP header
        //                    if (received > 6)
        //                    {
        //                        byte[] data = new byte[received];
        //                        Array.Copy(buffer, data, received);

        //                        // Strip the 6-byte Modbus TCP Header, send the RTU payload to BL logic
        //                        byte[] rtuData = new byte[data.Length - 6];
        //                        Array.Copy(data, 6, rtuData, 0, rtuData.Length);

        //                        TelemetryReceived?.Invoke(rtuData);
        //                    }
        //                }
        //                Thread.Sleep(10); // Prevent CPU burn
        //            }
        //            catch
        //            {
        //                Close(); // If stream breaks, close gracefully
        //                break;
        //            }
        //        }
        //    });
        //}
        public void StartBackgroundRead()
        {
            _isListening = true;
            Task.Run(() =>
            {
                while (_isListening && IsOpen)
                {
                    try
                    {
                        // 1. Read 6-byte Header
                        byte[] header = new byte[6];
                        int read = 0;
                        while (read < 6)
                        {
                            int r = _stream.Read(header, read, 6 - read);
                            if (r <= 0) throw new Exception();
                            read += r;
                        }

                        // 2. Get Length and Read Payload
                        int len = (header[4] << 8) | header[5];
                        byte[] payload = new byte[len];
                        int pRead = 0;
                        while (pRead < len)
                        {
                            int r = _stream.Read(payload, pRead, len - pRead);
                            pRead += r;
                        }

                         Console.WriteLine($"RX Raw: {BitConverter.ToString(payload).Replace("-", " ")}");
                        // 3. Fire event for Logic layer
                        //TelemetryReceived?.Invoke(payload);
                    }
                    catch { Close(); break; }
                }
            });
        }
        //public void StartBackgroundRead()
        //{
        //    _isListening = true;
        //    Task.Run(() =>
        //    {
        //        Console.WriteLine(">>> TCP Listening Thread Started."); // Check if this prints
        //        while (_isListening && IsOpen)
        //        {
        //            try
        //            {
        //                // 1. Read 6-byte Header
        //                byte[] header = new byte[6];
        //                int read = 0;
        //                while (read < 6)
        //                {
        //                    // Add a timeout or check if we are still connected
        //                    int r = _stream.Read(header, read, 6 - read);
        //                    if (r <= 0) throw new Exception("Connection closed by peer");
        //                    read += r;
        //                }

        //                Console.WriteLine($">>> Header RX: {BitConverter.ToString(header)}");

        //                // 2. Get Length and Read Payload
        //                int len = (header[4] << 8) | header[5];
        //                Console.WriteLine($">>> Expecting {len} bytes of payload...");

        //                byte[] payload = new byte[len];
        //                int pRead = 0;
        //                while (pRead < len)
        //                {
        //                    int r = _stream.Read(payload, pRead, len - pRead);
        //                    if (r <= 0) break;
        //                    pRead += r;
        //                }

        //                // 3. Fire event
        //                Console.WriteLine($">>> Payload RX: {BitConverter.ToString(payload)}");
        //                TelemetryReceived?.Invoke(payload);
        //            }
        //            catch (Exception ex)
        //            {
        //                Console.WriteLine($">>> TCP THREAD ERROR: {ex.Message}");
        //                Close();
        //                break;
        //            }
        //        }
        //    });
        //}
        // Used by BLLogic to send parameter writes (Speed, etc.)
        public byte[] SendRecv(byte[] req)
        {
            if (!IsOpen) return null;
            try
            {
                // Wrap RTU request in Modbus TCP Header
                byte[] frame = new byte[6 + req.Length];
                frame[0] = 0x00; frame[1] = 0x01; // Transaction ID
                frame[2] = 0x00; frame[3] = 0x00; // Protocol ID
                frame[4] = (byte)(req.Length >> 8);
                frame[5] = (byte)(req.Length & 0xFF);
                Array.Copy(req, 0, frame, 6, req.Length);

                _stream.Write(frame, 0, frame.Length);
                _stream.Flush();

                // We return null because incoming responses are handled by the background thread
                return null;
            }
            catch { return null; }
        }

        public void Close()
        {
            bool wasOpen = _isListening;
            _isListening = false;

            try
            {
                _stream?.Close();
                _client?.Close();
            }
            catch { }
            finally
            {
                _stream = null;
                _client = null;

                // Notify the UI if we unexpectedly lost connection
                if (wasOpen) ConnectionLost?.Invoke();
            }
        }

        public void Dispose() => Close();
    }
}