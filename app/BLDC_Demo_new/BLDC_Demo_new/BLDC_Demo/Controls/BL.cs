//using System;
//using System.Collections.Generic;
//using System.Collections.ObjectModel;
//using System.IO.Ports;
//using System.Linq;
//using System.Threading.Tasks;
//using BLDC_Demo.Models; // Fixes the 'ModbusDevice' not found error

//namespace BLDC_Demo.Controls
//{
//    public class BL
//    {
//        public ObservableCollection<ModbusDevice> ActiveDevices { get; } = new ObservableCollection<ModbusDevice>();
//        public List<string> ConnectedDevices { get; } = new List<string>();
//        public string ActivePortName { get; private set; } = string.Empty;

//        private IComm _comm;

//        public async Task ScanAsync()
//        {
//            string[] systemPorts = SerialPort.GetPortNames();
//            List<string> verifiedPorts = new List<string>();

//            // Handle physical disconnection of the active cable
//            if (!string.IsNullOrEmpty(ActivePortName) && !ActivePortName.Contains(":"))
//            {
//                if (!systemPorts.Contains(ActivePortName))
//                {
//                    ActivePortName = string.Empty;
//                    if (_comm != null)
//                    {
//                        _comm.Close();
//                        _comm = null;
//                    }
//                }
//            }

//            byte[] handshake = AppendModbusCRC(new byte[] { 0x00, 0x01 });

//            foreach (var p in systemPorts)
//            {
//                if (p == ActivePortName) { verifiedPorts.Add(p); continue; }

//                bool verified = await Task.Run(() =>
//                {
//                    try
//                    {
//                        using (var sp = new SerialPort(p, 115200, Parity.None, 8, StopBits.One) { ReadTimeout = 400, DtrEnable = false, RtsEnable = false })
//                        {
//                            sp.Open();
//                            sp.DiscardInBuffer();
//                            sp.Write(handshake, 0, handshake.Length);

//                            List<byte> fullBuffer = new List<byte>();
//                            int silenceCounter = 0;

//                            while (sp.BytesToRead == 0 && silenceCounter < 50) { System.Threading.Thread.Sleep(10); silenceCounter++; }

//                            silenceCounter = 0;
//                            while (silenceCounter < 20)
//                            {
//                                if (sp.BytesToRead > 0)
//                                {
//                                    byte[] chunk = new byte[sp.BytesToRead];
//                                    sp.Read(chunk, 0, chunk.Length);
//                                    fullBuffer.AddRange(chunk);
//                                    silenceCounter = 0;
//                                }
//                                else { System.Threading.Thread.Sleep(10); silenceCounter++; }
//                            }

//                            byte[] buffer = fullBuffer.ToArray();
//                            if (buffer.Length >= 13)
//                            {
//                                var tempDevice = new ModbusDevice { PortName = p, HasDeviceInfo = false };

//                                for (int i = 0; i <= buffer.Length - 13; i++)
//                                {
//                                    if (buffer[i + 1] == 0x03 && buffer[i + 2] == 0x08)
//                                    {
//                                        string mac = $"{buffer[i + 3]:X2}:{buffer[i + 4]:X2}:{buffer[i + 5]:X2}:{buffer[i + 6]:X2}:{buffer[i + 7]:X2}:{buffer[i + 8]:X2}";
//                                        byte seqId = buffer[i + 9];
//                                        byte status = buffer[i + 10];

//                                        tempDevice.ConnectedNodes.Add(new SubNode
//                                        {
//                                            MacAddress = mac,
//                                            SeqId = seqId.ToString(),
//                                            Status = status == 1 ? "OK" : $"ERR (Code: {status})"
//                                        });

//                                        i += 12;
//                                    }
//                                }

//                                if (tempDevice.ConnectedNodes.Count > 0)
//                                {
//                                    tempDevice.HasDeviceInfo = true;
//                                    App.Current.Dispatcher.Invoke(() =>
//                                    {
//                                        var existing = ActiveDevices.FirstOrDefault(d => d.PortName == p);
//                                        if (existing == null) ActiveDevices.Add(tempDevice);
//                                        else
//                                        {
//                                            existing.ConnectedNodes.Clear();
//                                            foreach (var node in tempDevice.ConnectedNodes) existing.ConnectedNodes.Add(node);
//                                            existing.HasDeviceInfo = true;
//                                        }
//                                    });
//                                    return true;
//                                }
//                            }
//                            return false;
//                        }
//                    }
//                    catch { return false; }
//                });

//                if (verified) verifiedPorts.Add(p);
//            }

//            App.Current.Dispatcher.Invoke(() =>
//            {
//                ConnectedDevices.Clear();
//                ConnectedDevices.AddRange(verifiedPorts);

//                for (int i = ActiveDevices.Count - 1; i >= 0; i--)
//                    if (!verifiedPorts.Contains(ActiveDevices[i].PortName)) ActiveDevices.RemoveAt(i);

//                foreach (var p in verifiedPorts)
//                    if (ActiveDevices.All(d => d.PortName != p))
//                        ActiveDevices.Add(new ModbusDevice { PortName = p });
//            });
//        }

//        public void SendWriteCommand(byte slaveId, ushort registerAddress, ushort value)
//        {
//            if (_comm == null || !_comm.IsOpen) return;

//            byte[] req = new byte[] {
//                slaveId,
//                0x06,
//                (byte)(registerAddress >> 8), (byte)(registerAddress & 0xFF),
//                (byte)(value >> 8), (byte)(value & 0xFF)
//            };

//            req = AppendModbusCRC(req);
//            _comm.SendRecv(req);
//        }

//        private byte[] AppendModbusCRC(byte[] data)
//        {
//            ushort crc = 0xFFFF;
//            for (int pos = 0; pos < data.Length; pos++)
//            {
//                crc ^= (ushort)data[pos];
//                for (int i = 8; i != 0; i--)
//                {
//                    if ((crc & 0x0001) != 0) { crc >>= 1; crc ^= 0xA001; }
//                    else { crc >>= 1; }
//                }
//            }
//            byte[] result = new byte[data.Length + 2];
//            Array.Copy(data, result, data.Length);
//            result[result.Length - 2] = (byte)(crc & 0xFF);
//            result[result.Length - 1] = (byte)((crc >> 8) & 0xFF);
//            return result;
//        }

//        public bool Start(IComm c)
//        {
//            _comm = c;
//            if (_comm.Open()) { ActivePortName = _comm.Name; return true; }
//            return false;
//        }

//        public void UpdateData(SubNode activeNode)
//        {
//            if (_comm == null || !_comm.IsOpen || activeNode == null) return;
//            if (!byte.TryParse(activeNode.SeqId, out byte slaveId)) return;

//            byte[] trigger = AppendModbusCRC(new byte[] { slaveId, 0xFF });
//            var res = _comm.SendRecv(trigger);

//            if (res == null || res.Length < 8) return;

//            for (int i = 0; i <= res.Length - 8; i += 8)
//            {
//                if (res[i] != slaveId) continue;

//                ushort register = (ushort)((res[i + 2] << 8) | res[i + 3]);
//                int value = (res[i + 4] << 8) | res[i + 5];

//                switch (register)
//                {
//                    // --- LEFT MOTOR ---
//                    case 0x0027: activeNode.LeftSpeed = $"{value}"; break;
//                    case 0x002A: activeNode.LeftAcceleration = $"{value}"; break;
//                    case 0x002F: activeNode.LeftDirection = (value == 1) ? "CCW" : "CW"; break;
//                    case 0x002B: activeNode.LeftDeceleration = $"{value}"; break;
//                    case 0x0036: activeNode.LeftCurrent = $"{value}"; break;

//                    // LEFT MOTOR STATUS (Mapped to 4:0058 / 0x3A)
//                    // --- LEFT MOTOR STATUS ---
//                    case 0x003A:
//                        activeNode.LeftOverVoltage = (value & (1 << 6)) == 0;      // 0 = Healthy (True)
//                        activeNode.LeftUnderVoltage = (value & (1 << 7)) == 0;     // 0 = Healthy (True)
//                        activeNode.LeftOverTemperature = (value & (1 << 8)) == 0;
//                        activeNode.LeftOverCurrent = (value & (1 << 9)) == 0;
//                        activeNode.LeftMotorStalled = (value & (1 << 13)) == 0;
//                        activeNode.LeftHallSensorError = (value & (1 << 14)) == 0;
//                        break;

//                    // --- RIGHT MOTOR ---
//                    case 0x003F: activeNode.RightSpeed = $"{value}"; break;
//                    case 0x0042: activeNode.RightAcceleration = $"{value}"; break;
//                    case 0x0047: activeNode.RightDirection = (value == 1) ? "CCW" : "CW"; break;
//                    case 0x0043: activeNode.RightDeceleration = $"{value}"; break;
//                    case 0x004E: activeNode.RightCurrent = $"{value}"; break;

//                    // RIGHT MOTOR STATUS (Mapped to 4:0082 / 0x52)

//                    // --- RIGHT MOTOR STATUS ---
//                    case 0x0052:
//                        activeNode.RightOverVoltage = (value & (1 << 6)) == 0;     // 0 = Healthy (True)
//                        activeNode.RightUnderVoltage = (value & (1 << 7)) == 0;    // 0 = Healthy (True)
//                        activeNode.RightOverTemperature = (value & (1 << 8)) == 0;
//                        activeNode.RightOverCurrent = (value & (1 << 9)) == 0;
//                        activeNode.RightMotorStalled = (value & (1 << 13)) == 0;
//                        activeNode.RightHallSensorError = (value & (1 << 14)) == 0;
//                        break;
//                }
//            }
//        }

//        public void UpdateData(string portName)
//        {
//            var activeDevice = ActiveDevices.FirstOrDefault(d => d.PortName == portName);
//            var activeNode = activeDevice?.ConnectedNodes.FirstOrDefault(n => n.IsActive);
//            if (activeNode != null) UpdateData(activeNode);
//        }
//    }
//}
////using System;
////using System.Collections.Generic;
////using System.Collections.ObjectModel;
////using System.ComponentModel;
////using System.IO.Ports;
////using System.Linq;
////using System.Net.Sockets; // Added for TcpClient
////using System.Runtime.CompilerServices;
////using System.Threading.Tasks;
////using BLDC_Demo.Models;

////namespace BLDC_Demo.Controls
////{
////    public class BL : INotifyPropertyChanged
////    {
////        public ObservableCollection<ModbusDevice> ActiveDevices { get; } = new ObservableCollection<ModbusDevice>();
////        public List<string> ConnectedDevices { get; } = new List<string>();
////        public string ActivePortName { get; private set; } = string.Empty;

////        private IComm _comm;
////        public static string CurrentActivePort { get; set; } = string.Empty;
////        // --- NEW: PROPERTY NOTIFICATION LOGIC ---

////        private bool _isCommunicating;
////        public bool IsCommunicating
////        {
////            get => _isCommunicating;
////            set
////            {
////                if (_isCommunicating != value)
////                {
////                    _isCommunicating = value;
////                    OnPropertyChanged();
////                }
////            }
////        }

////        public event PropertyChangedEventHandler PropertyChanged;
////        protected void OnPropertyChanged([CallerMemberName] string name = null)
////        {
////            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
////        }

////        // --- START AND STOP METHODS ---

////        public bool Start(IComm c)
////        {
////            _comm = c;
////            try
////            {
////                if (_comm.Open())
////                {
////                    ActivePortName = _comm.Name;
////                    IsCommunicating = true; // Signals UI to disable TCP button
////                    return true;
////                }
////            }
////            catch (Exception ex)
////            {
////                System.Diagnostics.Debug.WriteLine($"Connection Error: {ex.Message}");
////            }

////            IsCommunicating = false;
////            return false;
////        }

////        public void Stop()
////        {
////            if (_comm != null)
////            {
////                _comm.Close();
////                _comm = null;
////            }
////            IsCommunicating = false; // Signals UI to re-enable TCP button
////            ActivePortName = string.Empty;
////        }

////        // --- NEW: ETHERNET DISCOVERY LOGIC ---

////        /// <summary>
////        /// Scans the subnet of the provided IP for any devices responding on Port 502.
////        /// </summary>
////        public async Task DiscoverEthernetAsync(string inputIp)
////        {
////            // 1. Extract subnet (e.g., "192.168.1.50" -> "192.168.1")
////            int lastDot = inputIp.LastIndexOf('.');
////            if (lastDot == -1) return;
////            string subnet = inputIp.Substring(0, lastDot);

////            // 2. Create scanning tasks for IPs .1 through .254
////            var tasks = new List<Task<string>>();
////            for (int i = 1; i < 255; i++)
////            {
////                string target = $"{subnet}.{i}";
////                tasks.Add(CheckIpAsync(target));
////            }

////            // 3. Run all pings in parallel for maximum speed
////            string[] foundIps = await Task.WhenAll(tasks);

////            // 4. Update the UI collection with found devices
////            App.Current.Dispatcher.Invoke(() =>
////            {
////                foreach (var ip in foundIps.Where(x => x != null))
////                {
////                    if (ActiveDevices.All(d => d.PortName != ip))
////                    {
////                        ActiveDevices.Add(new ModbusDevice { PortName = ip });
////                    }
////                }
////            });
////        }

////        private async Task<string> CheckIpAsync(string ip)
////        {
////            TcpClient tcp = null;
////            try
////            {
////                tcp = new TcpClient();
////                // Use a Task.Run to isolate the connection from the UI logging context
////                var connectTask = tcp.ConnectAsync(ip, 502);
////                var delayTask = Task.Delay(300); // 300ms timeout

////                if (await Task.WhenAny(connectTask, delayTask) == connectTask)
////                {
////                    // Wait for the task to finish to catch any internal socket exceptions
////                    await connectTask;
////                    if (tcp.Connected) return ip;
////                }
////            }
////            catch
////            {
////                // Catch-all to prevent the "Logging.Enter" crash seen in your screenshot
////            }
////            finally
////            {
////                tcp?.Close();
////                tcp?.Dispose();
////            }
////            return null;
////        }
////        // --- NEW: DAISY-CHAIN ETHERNET DISCOVERY ---

////        public async Task ConnectAndGetEthernetNodesAsync(string masterIp)
////        {
////            // 1. Connect to the Master Gateway ESP32
////            var ethComm = new CommEthernet(masterIp);

////            if (ethComm.Open())
////            {
////                _comm = ethComm;
////                ActivePortName = masterIp;
////                IsCommunicating = true;

////                await Task.Run(() =>
////                {
////                    // Give ESP32 a moment to recognize connection and send data
////                    System.Threading.Thread.Sleep(200);

////                    // Send a dummy byte (or handshake) to trigger the ESP32 to send the list.
////                    // If your ESP32 sends it automatically upon connection, change this to an empty array: new byte[0]
////                    byte[] triggerForList = new byte[] { 0xAA };
////                    byte[] response = _comm.SendRecv(triggerForList);

////                    if (response != null && response.Length > 0)
////                    {
////                        var masterDevice = new ModbusDevice { PortName = masterIp, HasDeviceInfo = true };

////                        // =========================================================
////                        // PARSE YOUR IP AND SEQ ID DATA HERE
////                        // Assuming format: 5 bytes per module -> [IP1, IP2, IP3, IP4, SeqID]
////                        // =========================================================
////                        for (int i = 0; i <= response.Length - 5; i += 5)
////                        {
////                            string parsedIp = $"{response[i]}.{response[i + 1]}.{response[i + 2]}.{response[i + 3]}";
////                            byte seqId = response[i + 4];

////                            masterDevice.ConnectedNodes.Add(new SubNode
////                            {
////                                IpAddress = parsedIp, // Requires adding this property to SubNode class
////                                SeqId = seqId.ToString(),
////                                Status = "Connected"
////                            });
////                        }

////                        // Update the UI
////                        App.Current.Dispatcher.Invoke(() =>
////                        {
////                            ActiveDevices.Clear();
////                            ActiveDevices.Add(masterDevice);
////                        });
////                    }
////                });
////            }
////        }
////        // --- EXISTING SERIAL SCAN LOGIC ---

////        public async Task ScanAsync()
////        {
////            string[] systemPorts = SerialPort.GetPortNames();
////            List<string> verifiedPorts = new List<string>();

////            if (!string.IsNullOrEmpty(ActivePortName) && !ActivePortName.Contains(":"))
////            {
////                if (!systemPorts.Contains(ActivePortName))
////                {
////                    ActivePortName = string.Empty;
////                    if (_comm != null)
////                    {
////                        _comm.Close();
////                        _comm = null;
////                    }
////                }
////            }

////            byte[] handshake = AppendModbusCRC(new byte[] { 0x00, 0x01 });

////            foreach (var p in systemPorts)
////            {
////                if (p == ActivePortName) { verifiedPorts.Add(p); continue; }

////                bool verified = await Task.Run(() => {
////                    try
////                    {
////                        using (var sp = new SerialPort(p, 115200, Parity.None, 8, StopBits.One) { ReadTimeout = 400, DtrEnable = false, RtsEnable = false })
////                        {
////                            sp.Open();
////                            sp.DiscardInBuffer();
////                            sp.Write(handshake, 0, handshake.Length);

////                            List<byte> fullBuffer = new List<byte>();
////                            int silenceCounter = 0;

////                            while (sp.BytesToRead == 0 && silenceCounter < 50) { System.Threading.Thread.Sleep(10); silenceCounter++; }

////                            silenceCounter = 0;
////                            while (silenceCounter < 20)
////                            {
////                                if (sp.BytesToRead > 0)
////                                {
////                                    byte[] chunk = new byte[sp.BytesToRead];
////                                    sp.Read(chunk, 0, chunk.Length);
////                                    fullBuffer.AddRange(chunk);
////                                    silenceCounter = 0;
////                                }
////                                else { System.Threading.Thread.Sleep(10); silenceCounter++; }
////                            }

////                            byte[] buffer = fullBuffer.ToArray();
////                            if (buffer.Length >= 13)
////                            {
////                                var tempDevice = new ModbusDevice { PortName = p, HasDeviceInfo = false };

////                                for (int i = 0; i <= buffer.Length - 13; i++)
////                                {
////                                    if (buffer[i + 1] == 0x03 && buffer[i + 2] == 0x08)
////                                    {
////                                        string mac = $"{buffer[i + 3]:X2}:{buffer[i + 4]:X2}:{buffer[i + 5]:X2}:{buffer[i + 6]:X2}:{buffer[i + 7]:X2}:{buffer[i + 8]:X2}";
////                                        byte seqId = buffer[i + 9];
////                                        byte status = buffer[i + 10];

////                                        tempDevice.ConnectedNodes.Add(new SubNode
////                                        {
////                                            MacAddress = mac,
////                                            SeqId = seqId.ToString(),
////                                            Status = status == 1 ? "OK" : $"ERR (Code: {status})"
////                                        });

////                                        i += 12;
////                                    }
////                                }

////                                if (tempDevice.ConnectedNodes.Count > 0)
////                                {
////                                    tempDevice.HasDeviceInfo = true;
////                                    App.Current.Dispatcher.Invoke(() => {
////                                        var existing = ActiveDevices.FirstOrDefault(d => d.PortName == p);
////                                        if (existing == null) ActiveDevices.Add(tempDevice);
////                                        else
////                                        {
////                                            existing.ConnectedNodes.Clear();
////                                            foreach (var node in tempDevice.ConnectedNodes) existing.ConnectedNodes.Add(node);
////                                            existing.HasDeviceInfo = true;
////                                        }
////                                    });
////                                    return true;
////                                }
////                            }
////                            return false;
////                        }
////                    }
////                    catch { return false; }
////                });

////                if (verified) verifiedPorts.Add(p);
////            }

////            App.Current.Dispatcher.Invoke(() => {
////                ConnectedDevices.Clear();
////                ConnectedDevices.AddRange(verifiedPorts);

////                for (int i = ActiveDevices.Count - 1; i >= 0; i--)
////                    if (!verifiedPorts.Contains(ActiveDevices[i].PortName)) ActiveDevices.RemoveAt(i);

////                foreach (var p in verifiedPorts)
////                    if (ActiveDevices.All(d => d.PortName != p))
////                        ActiveDevices.Add(new ModbusDevice { PortName = p });
////            });
////        }

////        //public void SendWriteCommand(byte slaveId, ushort registerAddress, ushort value)
////        //{
////        //    if (_comm == null || !_comm.IsOpen) return;

////        //    byte[] req = new byte[] {
////        //        slaveId,
////        //        0x06,
////        //        (byte)(registerAddress >> 8), (byte)(registerAddress & 0xFF),
////        //        (byte)(value >> 8), (byte)(value & 0xFF)
////        //    };

////        //    // Ethernet logic usually skips the CRC because TCP handles data integrity.
////        //    // If your gateway expects RTU inside TCP, keep this line. 
////        //    // Otherwise, check your CommEthernet implementation.
////        //    if (_comm is CommEthernet) { /* TCP handles integrity */ }
////        //    else { req = AppendModbusCRC(req); }

////        //    _comm.SendRecv(req);
////        //}
////        public void SendWriteCommand(byte slaveId, ushort registerAddress, ushort value)
////        {
////            if (_comm == null || !_comm.IsOpen) return;

////            // The basic Modbus command (PDU)
////            byte[] req = new byte[] {
////        slaveId,
////        0x06,
////        (byte)(registerAddress >> 8), (byte)(registerAddress & 0xFF),
////        (byte)(value >> 8), (byte)(value & 0xFF)
////    };

////            if (_comm is CommEthernet)
////            {
////                // DO NOT APPEND CRC.
////                // The TCP stack handles the 'handshake' and data integrity.
////                _comm.SendRecv(req);
////            }
////            else
////            {
////                // RS485 REQUIRES CRC.
////                req = AppendModbusCRC(req);
////                _comm.SendRecv(req);
////            }
////        }
////        private byte[] AppendModbusCRC(byte[] data)
////        {
////            ushort crc = 0xFFFF;
////            for (int pos = 0; pos < data.Length; pos++)
////            {
////                crc ^= (ushort)data[pos];
////                for (int i = 8; i != 0; i--)
////                {
////                    if ((crc & 0x0001) != 0) { crc >>= 1; crc ^= 0xA001; }
////                    else { crc >>= 1; }
////                }
////            }
////            byte[] result = new byte[data.Length + 2];
////            Array.Copy(data, result, data.Length);
////            result[result.Length - 2] = (byte)(crc & 0xFF);
////            result[result.Length - 1] = (byte)((crc >> 8) & 0xFF);
////            return result;
////        }
////        // Inside BL.cs



////        //public bool Start(IComm c)
////        //{
////        //    _comm = c;
////        //    if (_comm.Open()) { ActivePortName = _comm.Name; return true; }
////        //    return false;
////        //}

////        //public void UpdateData(SubNode activeNode)
////        //{
////        //    if (_comm == null || !_comm.IsOpen || activeNode == null) return;
////        //    if (!byte.TryParse(activeNode.SeqId, out byte slaveId)) return;

////        //    // Trigger request (Modbus Function Code + Slave ID)
////        //    byte[] trigger = new byte[] { slaveId, 0xFF };

////        //    // Only append CRC for Serial/RTU connections
////        //    if (!(_comm is CommEthernet)) trigger = AppendModbusCRC(trigger);

////        //    var res = _comm.SendRecv(trigger);

////        //    if (res == null || res.Length < 8) return;

////        //    for (int i = 0; i <= res.Length - 8; i += 8)
////        //    {
////        //        if (res[i] != slaveId) continue;

////        //        ushort register = (ushort)((res[i + 2] << 8) | res[i + 3]);
////        //        int value = (res[i + 4] << 8) | res[i + 5];

////        //        switch (register)
////        //        {
////        //            case 0x0027: activeNode.LeftSpeed = $"{value}"; break;
////        //            case 0x002A: activeNode.LeftAcceleration = $"{value}"; break;
////        //            case 0x002F: activeNode.LeftDirection = (value == 1) ? "CCW" : "CW"; break;
////        //            case 0x002B: activeNode.LeftDeceleration = $"{value}"; break;
////        //            case 0x0036: // Left Current
////        //                activeNode.LeftCurrent = (value / 10.0).ToString("F1");
////        //                break;

////        //            case 0x003A:
////        //                activeNode.LeftOverVoltage = (value & (1 << 6)) == 0;
////        //                activeNode.LeftUnderVoltage = (value & (1 << 7)) == 0;
////        //                activeNode.LeftOverTemperature = (value & (1 << 8)) == 0;
////        //                activeNode.LeftOverCurrent = (value & (1 << 9)) == 0;
////        //                activeNode.LeftMotorStalled = (value & (1 << 13)) == 0;
////        //                activeNode.LeftHallSensorError = (value & (1 << 14)) == 0;
////        //                break;


////        //            //case 0x002E:
////        //            //    // Updates the Left UI Dropdown
////        //            //    activeNode.LeftBrakeIndex = value;
////        //            //    break;

////        //            //case 0x0046:
////        //            //    // Updates the Right UI Dropdown
////        //            //    activeNode.RightBrakeIndex = value;
////        //            //    break;

////        //            case 0x002E:
////        //                // Updates the Left UI Dropdown safely on the UI Thread
////        //                App.Current.Dispatcher.Invoke(() => {
////        //                    activeNode.LeftBrakeIndex = value;
////        //                });
////        //                break;

////        //            case 0x0046:
////        //                // Updates the Right UI Dropdown safely on the UI Thread
////        //                App.Current.Dispatcher.Invoke(() => {

////        //                    activeNode.RightBrakeIndex = value;
////        //                });
////        //                break;
////        //            case 0x0001:
////        //                // Updates the Left UI Dropdown safely on the UI Thread

////        //                    activeNode.LeftMotorHallTypeIndex = value;

////        //                break;

////        //            case 0x0002:
////        //                // Updates the Right UI Dropdown safely on the UI Thread

////        //                    activeNode.RightMotorHallTypeIndex = value;

////        //                break;
////        //            case 0x003F: activeNode.RightSpeed = $"{value}"; break;
////        //            case 0x0042: activeNode.RightAcceleration = $"{value}"; break;
////        //            case 0x0047: activeNode.RightDirection = (value == 1) ? "CCW" : "CW"; break;
////        //            case 0x0043: activeNode.RightDeceleration = $"{value}"; break;
////        //            case 0x004E:
////        //                activeNode.RightCurrent = (value / 10.0).ToString("F1");
////        //                break;

////        //            case 0x0052:
////        //                activeNode.RightOverVoltage = (value & (1 << 6)) == 0;
////        //                activeNode.RightUnderVoltage = (value & (1 << 7)) == 0;
////        //                activeNode.RightOverTemperature = (value & (1 << 8)) == 0;
////        //                activeNode.RightOverCurrent = (value & (1 << 9)) == 0;
////        //                activeNode.RightMotorStalled = (value & (1 << 13)) == 0;
////        //                activeNode.RightHallSensorError = (value & (1 << 14)) == 0;
////        //                break;
////        //        }
////        //    }
////        //}
////        //public void UpdateData(SubNode activeNode)
////        //{
////        //    // 1. Safety Checks
////        //    if (_comm == null || !_comm.IsOpen || activeNode == null) return;
////        //    if (!byte.TryParse(activeNode.SeqId, out byte slaveId)) return;

////        //    // 2. Build Request
////        //    byte[] trigger = new byte[] { slaveId, 0xFF }; // Custom Trigger Command

////        //    // Only append CRC for Serial/RTU (TCP handles error checking via Ethernet layer)
////        //    if (!(_comm is CommEthernet))
////        //    {
////        //        trigger = AppendModbusCRC(trigger);
////        //    }

////        //    var res = _comm.SendRecv(trigger);

////        //    // 3. Validate Response Length
////        //    if (res == null || res.Length < 8) return;

////        //    // 4. Parse 8-byte Chunks
////        //    for (int i = 0; i <= res.Length - 8; i += 8)
////        //    {
////        //        if (res[i] != slaveId) continue;

////        //        // Register: Bytes 2 & 3 | Value: Bytes 4 & 5
////        //        ushort register = (ushort)((res[i + 2] << 8) | res[i + 3]);
////        //        int value = (res[i + 4] << 8) | res[i + 5];

////        //        // Use Dispatcher for ALL updates if this runs in a background thread
////        //        // to prevent "Cross-thread operation not valid" errors
////        //        App.Current.Dispatcher.Invoke(() =>
////        //        {
////        //            switch (register)
////        //            {
////        //                case 0x0027: activeNode.LeftSpeed = $"{value}"; break;
////        public void UpdateData(SubNode activeNode)
////        {
////            // 1. Safety Checks
////            if (_comm == null || !_comm.IsOpen || activeNode == null) return;
////            if (!byte.TryParse(activeNode.SeqId, out byte slaveId)) return;

////            // 2. Build Request based on connection type
////            byte[] trigger;

////            if (_comm is CommEthernet)
////            {
////                // ETHERNET MODE: Send 0x00 and Seq Number (No CRC required)
////                trigger = new byte[] { 0x00, slaveId };
////            }
////            else
////            {
////                // RS485 MODE: Send old Trigger Command (0xFF) and append CRC
////                trigger = new byte[] { slaveId, 0xFF };
////                trigger = AppendModbusCRC(trigger);
////            }

////            var res = _comm.SendRecv(trigger);

////            // 3. Validate Response Length
////            if (res == null || res.Length < 8) return;

////            // ... (KEEP THE REST OF YOUR EXISTING PARSING LOGIC EXACTLY THE SAME) ...
////            for (int i = 0; i <= res.Length - 8; i += 8)
////            {
////                if (res[i] != slaveId) continue;

////                ushort register = (ushort)((res[i + 2] << 8) | res[i + 3]);
////                int value = (res[i + 4] << 8) | res[i + 5];

////                App.Current.Dispatcher.Invoke(() =>
////                {
////                    switch (register)
////                    {
////                        case 0x0027: activeNode.LeftSpeed = $"{value}"; break;
////                        // ... rest of your switch cases ...
////                        case 0x002A: activeNode.LeftAcceleration = $"{value}"; break;
////                        case 0x002F: activeNode.LeftDirection = (value == 1) ? "CCW" : "CW"; break;
////                        case 0x002B: activeNode.LeftDeceleration = $"{value}"; break;
////                        case 0x0036: // Left Current
////                            activeNode.LeftCurrent = (value / 10.0).ToString("F1");
////                            break;

////                        case 0x003A:
////                            activeNode.LeftOverVoltage = (value & (1 << 6)) == 0;
////                            activeNode.LeftUnderVoltage = (value & (1 << 7)) == 0;
////                            activeNode.LeftOverTemperature = (value & (1 << 8)) == 0;
////                            activeNode.LeftOverCurrent = (value & (1 << 9)) == 0;
////                            activeNode.LeftMotorStalled = (value & (1 << 13)) == 0;
////                            activeNode.LeftHallSensorError = (value & (1 << 14)) == 0;
////                            break;


////                        //case 0x002E:
////                        //    // Updates the Left UI Dropdown
////                        //    activeNode.LeftBrakeIndex = value;
////                        //    break;

////                        //case 0x0046:
////                        //    // Updates the Right UI Dropdown
////                        //    activeNode.RightBrakeIndex = value;
////                        //    break;

////                        case 0x002E:
////                            // Updates the Left UI Dropdown safely on the UI Thread
////                            App.Current.Dispatcher.Invoke(() =>
////                            {
////                                activeNode.LeftBrakeIndex = value;
////                            });
////                            break;

////                        case 0x0046:
////                            // Updates the Right UI Dropdown safely on the UI Thread
////                            App.Current.Dispatcher.Invoke(() =>
////                            {

////                                activeNode.RightBrakeIndex = value;
////                            });
////                            break;
////                        case 0x0001:
////                            // Updates the Left UI Dropdown safely on the UI Thread

////                            activeNode.LeftMotorHallTypeIndex = value;

////                            break;

////                        case 0x0002:
////                            // Updates the Right UI Dropdown safely on the UI Thread

////                            activeNode.RightMotorHallTypeIndex = value;

////                            break;
////                        case 0x003F: activeNode.RightSpeed = $"{value}"; break;
////                        case 0x0042: activeNode.RightAcceleration = $"{value}"; break;
////                        case 0x0047: activeNode.RightDirection = (value == 1) ? "CCW" : "CW"; break;
////                        case 0x0043: activeNode.RightDeceleration = $"{value}"; break;
////                        case 0x004E:
////                            activeNode.RightCurrent = (value / 10.0).ToString("F1");
////                            break;

////                        case 0x0052:
////                            activeNode.RightOverVoltage = (value & (1 << 6)) == 0;
////                            activeNode.RightUnderVoltage = (value & (1 << 7)) == 0;
////                            activeNode.RightOverTemperature = (value & (1 << 8)) == 0;
////                            activeNode.RightOverCurrent = (value & (1 << 9)) == 0;
////                            activeNode.RightMotorStalled = (value & (1 << 13)) == 0;
////                            activeNode.RightHallSensorError = (value & (1 << 14)) == 0;
////                            break;
////                    }
////                });
////            }
////        }

////        //public void UpdateData(string portName)
////        //{
////        //    // If there is no port, STOP. Do not touch the SubNode.
////        //    if (string.IsNullOrEmpty(portName)) return;

////        //    var activeDevice = ActiveDevices.FirstOrDefault(d => d.PortName == portName);
////        //    var activeNode = activeDevice?.ConnectedNodes.FirstOrDefault(n => n.IsActive);

////        //    if (activeNode != null) UpdateData(activeNode);
////        //}
////        public void UpdateData(string portName)
////        {
////            var activeDevice = ActiveDevices.FirstOrDefault(d => d.PortName == portName);
////            var activeNode = activeDevice?.ConnectedNodes.FirstOrDefault(n => n.IsActive);
////            if (activeNode != null) UpdateData(activeNode);

////        }
////    }
////}
///
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using BLDC_Demo.Models;
using System.Windows;
namespace BLDC_Demo.Controls
{
    public class BL
    {
        public ObservableCollection<ModbusDevice> ActiveDevices { get; } = new ObservableCollection<ModbusDevice>();
        public List<string> ConnectedDevices { get; } = new List<string>();
        public string ActivePortName { get; private set; } = string.Empty;

        private IComm _comm;
        public event Action OnDisconnected;
        public async Task ScanAsync()
        {
            string[] systemPorts = SerialPort.GetPortNames();
            List<string> verifiedPorts = new List<string>();

            // =========================================================================
            // THE FIX: If the active USB cable is physically unplugged, forget it!
            // This forces the app to rescan it and show the data when plugged back in.
            // =========================================================================
            if (!string.IsNullOrEmpty(ActivePortName) && !ActivePortName.Contains(":"))
            {
                if (!systemPorts.Contains(ActivePortName))
                {
                    ActivePortName = string.Empty; // Reset the active port
                    if (_comm != null)
                    {
                        _comm.Close(); // Safely kill the dead connection
                        _comm = null;
                    }
                }
            }
            // =========================================================================

            // Generate the discovery handshake: [0x00] [0x01] [CRC_L] [CRC_H]
            byte[] handshake = AppendModbusCRC(new byte[] { 0x00, 0x01 });

            foreach (var p in systemPorts)
            {
                if (p == ActivePortName) { verifiedPorts.Add(p); continue; }

                bool verified = await Task.Run(() => {
                    // =========================================================================
                    // THE NEW FIX: The Try-Catch now completely surrounds the 'using' block.
                    // If the cable is yanked, it safely catches the error and won't crash!
                    // =========================================================================
                    try
                    {
                        using (var sp = new SerialPort(p, 115200, Parity.None, 8, StopBits.One) { ReadTimeout = 400, DtrEnable = false, RtsEnable = false })
                        {
                            sp.Open();
                            sp.DiscardInBuffer();

                            // 1. Send the broadcast ping
                            sp.Write(handshake, 0, handshake.Length);

                            // 2. Dynamic Listener for multiple devices
                            List<byte> fullBuffer = new List<byte>();
                            int silenceCounter = 0;

                            while (sp.BytesToRead == 0 && silenceCounter < 50) { System.Threading.Thread.Sleep(10); silenceCounter++; }

                            silenceCounter = 0;
                            while (silenceCounter < 20)
                            {
                                if (sp.BytesToRead > 0)
                                {
                                    byte[] chunk = new byte[sp.BytesToRead];
                                    sp.Read(chunk, 0, chunk.Length);
                                    fullBuffer.AddRange(chunk);
                                    silenceCounter = 0;
                                }
                                else { System.Threading.Thread.Sleep(10); silenceCounter++; }
                            }

                            // 3. Process the collected 13-byte ID responses
                            byte[] buffer = fullBuffer.ToArray();
                            if (buffer.Length >= 13)
                            {
                                var tempDevice = new ModbusDevice { PortName = p, HasDeviceInfo = false };

                                for (int i = 0; i <= buffer.Length - 13; i++)
                                {
                                    if (buffer[i + 1] == 0x03 && buffer[i + 2] == 0x08)
                                    {
                                        string mac = $"{buffer[i + 3]:X2}:{buffer[i + 4]:X2}:{buffer[i + 5]:X2}:{buffer[i + 6]:X2}:{buffer[i + 7]:X2}:{buffer[i + 8]:X2}";
                                        byte seqId = buffer[i + 9];
                                        byte status = buffer[i + 10];

                                        tempDevice.ConnectedNodes.Add(new SubNode
                                        {
                                            MacAddress = mac,
                                            SeqId = seqId.ToString(),
                                            Status = status == 1 ? "OK" : $"ERR (Code: {status})"
                                        });

                                        i += 12;
                                    }
                                }

                                if (tempDevice.ConnectedNodes.Count > 0)
                                {
                                    tempDevice.HasDeviceInfo = true;
                                    App.Current.Dispatcher.Invoke(() => {
                                        var existing = ActiveDevices.FirstOrDefault(d => d.PortName == p);
                                        if (existing == null) ActiveDevices.Add(tempDevice);
                                        else
                                        {
                                            existing.ConnectedNodes.Clear();
                                            foreach (var node in tempDevice.ConnectedNodes) existing.ConnectedNodes.Add(node);
                                            existing.HasDeviceInfo = true;
                                        }
                                    });
                                    return true;
                                }
                            }
                            return false;
                        }
                    }
                    catch (Exception)
                    {
                        // Safely swallows the IOException if the device is unplugged mid-scan
                        return false;
                    }
                });

                if (verified) verifiedPorts.Add(p);
            }

            App.Current.Dispatcher.Invoke(() => {
                ConnectedDevices.Clear();
                ConnectedDevices.AddRange(verifiedPorts);

                for (int i = ActiveDevices.Count - 1; i >= 0; i--)
                    if (!verifiedPorts.Contains(ActiveDevices[i].PortName)) ActiveDevices.RemoveAt(i);

                foreach (var p in verifiedPorts)
                    if (ActiveDevices.All(d => d.PortName != p))
                        ActiveDevices.Add(new ModbusDevice { PortName = p });
            });
        }
        // Add this inside your BL class
        // Add this direct-send method to your BL class
        //public void SendWriteCommand(byte slaveId, ushort registerAddress, ushort value)
        //{
        //    // If there's no active connection (Ethernet or RS485), do nothing
        //    if (_comm == null || !_comm.IsOpen) return;

        //    // Build the core command: [Slave ID] [06] [Reg High] [Reg Low] [Val High] [Val Low]
        //    byte[] req = new byte[] {
        //        slaveId,
        //        0x06,
        //        (byte)(registerAddress >> 8), (byte)(registerAddress & 0xFF),
        //        (byte)(value >> 8), (byte)(value & 0xFF)
        //    };

        //    // Automatically attach the correct CRC at the end
        //    req = AppendModbusCRC(req);

        //    // Blast it directly out the active port!
        //    _comm.SendRecv(req);
        //}
        public void SendWriteCommand(byte slaveId, ushort registerAddress, ushort value)
        {
            // 1. Safety Check
            if (_comm == null || !_comm.IsOpen) return;

            // 2. Build the "Core" Modbus PDU (Function 06 + Address + Value)
            byte[] pdu = new byte[] {
        slaveId,
        0x06,
        (byte)(registerAddress >> 8), (byte)(registerAddress & 0xFF),
        (byte)(value >> 8), (byte)(value & 0xFF)
    };

            byte[] finalFrame;

            // 3. Branch logic based on connection type
            if (_comm is CommEthernet)
            {
                // --- MODBUS TCP: Add 6-byte MBAP Header, NO CRC ---
                finalFrame = new byte[6 + pdu.Length];
                finalFrame[0] = 0x00; finalFrame[1] = 0x01; // Transaction ID
                finalFrame[2] = 0x00; finalFrame[3] = 0x00; // Protocol ID (0 = Modbus)
                finalFrame[4] = 0x00;                       // Length High
                finalFrame[5] = (byte)pdu.Length;           // Length Low (usually 6)

                Array.Copy(pdu, 0, finalFrame, 6, pdu.Length);
            }
            else
            {
                // --- MODBUS RTU: Keep existing CRC logic ---
                finalFrame = AppendModbusCRC(pdu);
            }

            // 4. Send the correctly formatted frame
            _comm.SendRecv(finalFrame);
        }
        private byte[] AppendModbusCRC(byte[] data)
        {
            ushort crc = 0xFFFF;
            for (int pos = 0; pos < data.Length; pos++)
            {
                crc ^= (ushort)data[pos];
                for (int i = 8; i != 0; i--)
                {
                    if ((crc & 0x0001) != 0) { crc >>= 1; crc ^= 0xA001; }
                    else { crc >>= 1; }
                }
            }
            byte[] result = new byte[data.Length + 2];
            Array.Copy(data, result, data.Length);
            result[result.Length - 2] = (byte)(crc & 0xFF);
            result[result.Length - 1] = (byte)((crc >> 8) & 0xFF);
            return result;
        }

        //public bool Start(IComm c)
        //{
        //    _comm = c;
        //    if (_comm.Open()) { ActivePortName = _comm.Name; return true; }
        //    return false;
        //}


        public bool Start(IComm c)
        {
            _comm = c;

            // Hook up the live telemetry listener automatically
            if (_comm is CommEthernet tcp)
            {
                tcp.TelemetryReceived += (rtuData) =>
                {
                    // Find the sub-node currently marked as "IsActive" in the UI
                    var activeNode = ActiveDevices
                        .SelectMany(d => d.ConnectedNodes)
                        .FirstOrDefault(n => n.IsActive);

                    if (activeNode != null)
                    {
                        ParseAndUpdateUI(rtuData, activeNode);
                    }
                };
            }

            if (_comm.Open()) { ActivePortName = _comm.Name; return true; }
            return false;
        }
        public void Close()
        {
            if (_comm != null)
            {
                _comm.Close();
                _comm = null;
                // Trigger UI clear when manually closed
                OnDisconnected?.Invoke();
            }
        }
        // This method lives inside BL.cs, which is why the UI was complaining it couldn't find it
        //private void ParseAndUpdateUI(byte[] rtuData, SubNode node)
        //{
        //    if (rtuData.Length < 6) return;

        //    int register = (rtuData[2] << 8) | rtuData[3];
        //    short value = (short)((rtuData[4] << 8) | rtuData[5]);

        //    Application.Current.Dispatcher.Invoke(() =>
        //    {
        //private void ParseAndUpdateUI(byte[] rtuData, SubNode node)
        //{
        //    if (rtuData.Length < 6) return;

        //    int register = (rtuData[2] << 8) | rtuData[3];
        //    short value = (short)((rtuData[4] << 8) | rtuData[5]);

        //    // Ensure UI updates happen on the Main Thread
        //    Application.Current.Dispatcher.Invoke(() =>
        //    {
        //        switch (register)
        //            {
        //                case 0x0027: node.LeftSpeed = $"{value}"; break;
        //                case 0x002A: node.LeftAcceleration = $"{value}"; break;
        //                case 0x002F: node.LeftDirection = (value == 1) ? "CCW" : "CW"; break;
        //                case 0x002B: node.LeftDeceleration = $"{value}"; break;
        //                case 0x0036: // Left Current
        //                node.LeftCurrent = (value / 10.0).ToString("F1");
        //                    break;

        //                case 0x003A:
        //                node.LeftOverVoltage = (value & (1 << 6)) == 0;
        //                node.LeftUnderVoltage = (value & (1 << 7)) == 0;
        //                node.LeftOverTemperature = (value & (1 << 8)) == 0;
        //                node.LeftOverCurrent = (value & (1 << 9)) == 0;
        //                node.LeftMotorStalled = (value & (1 << 13)) == 0;
        //                node.LeftHallSensorError = (value & (1 << 14)) == 0;
        //                    break;


        //                //case 0x002E:
        //                //    // Updates the Left UI Dropdown
        //                //    activeNode.LeftBrakeIndex = value;
        //                //    break;

        //                //case 0x0046:
        //                //    // Updates the Right UI Dropdown
        //                //    activeNode.RightBrakeIndex = value;
        //                //    break;

        //                case 0x002E:
        //                    // Updates the Left UI Dropdown safely on the UI Thread
        //                    App.Current.Dispatcher.Invoke(() =>
        //                    {
        //                        node.LeftBrakeIndex = value;
        //                    });
        //                    break;

        //                case 0x0046:
        //                    // Updates the Right UI Dropdown safely on the UI Thread
        //                    App.Current.Dispatcher.Invoke(() =>
        //                    {

        //                        node.RightBrakeIndex = value;
        //                    });
        //                    break;
        //                case 0x0001:
        //                // Updates the Left UI Dropdown safely on the UI Thread

        //                node.LeftMotorHallTypeIndex = value;

        //                    break;

        //                case 0x0002:
        //                // Updates the Right UI Dropdown safely on the UI Thread

        //                node.RightMotorHallTypeIndex = value;

        //                    break;
        //                case 0x003F: node.RightSpeed = $"{value}"; break;
        //                case 0x0042: node.RightAcceleration = $"{value}"; break;
        //                case 0x0047: node.RightDirection = (value == 1) ? "CCW" : "CW"; break;
        //                case 0x0043: node.RightDeceleration = $"{value}"; break;
        //                case 0x004E:
        //                node.RightCurrent = (value / 10.0).ToString("F1");
        //                    break;

        //                case 0x0052:
        //                node.RightOverVoltage = (value & (1 << 6)) == 0;
        //                node.RightUnderVoltage = (value & (1 << 7)) == 0;
        //                node.RightOverTemperature = (value & (1 << 8)) == 0;
        //                node.RightOverCurrent = (value & (1 << 9)) == 0;
        //                node.RightMotorStalled = (value & (1 << 13)) == 0;
        //                node.RightHallSensorError = (value & (1 << 14)) == 0;
        //                    break;
        //        }
        //    });
        //}
        private void ParseAndUpdateUI(byte[] rtuData, SubNode node)
        {
            try
            {
                if (rtuData.Length < 6) return;

                // Based on your RX Raw: 01 03 [00 3F] [00 64]
                int register = (rtuData[2] << 8) | rtuData[3];
                short value = (short)((rtuData[4] << 8) | rtuData[5]);

                // We update the MODEL. The UI will update itself automatically!
                switch (register)
                {
                    case 0x003F:
                        node.LeftSpeed = value.ToString();
                        break;
                    case 0x004E:
                        node.LeftCurrent = (value / 10.0).ToString("F1");
                        break;
                    case 0x0002:
                        node.LeftMotorHallTypeIndex = value;
                        break;
                        // Add other cases...
                }
            }
            catch { }
        }
        //private void ParseAndUpdateUI(byte[] rtuData, SubNode node)
        //{
        //    if (rtuData.Length < 6) return;

        //    // Extracting Register and Value from the RTU Payload
        //    int register = (rtuData[2] << 8) | rtuData[3];
        //    short value = (short)((rtuData[4] << 8) | rtuData[5]);

        //    // Printing to Console for verification
        //    Console.WriteLine($"[TELEMETRY] Reg: 0x{register:X4} | Val: {value} | Target Node: {node.IpAddress}");

        //    Application.Current.Dispatcher.Invoke(() =>
        //    {
        //        switch (register)
        //        {
        //            // --- Right Motor Logic (Matching your RX Logs) ---
        //            case 0x003F:
        //                node.RightSpeed = value.ToString();
        //                Console.WriteLine($"-> RightSpeed: {node.RightSpeed} RPM");
        //                break;

        //            case 0x004E:
        //                // Format: 25 becomes "2.5"
        //                node.RightCurrent = (value / 10.0).ToString("F1");
        //                Console.WriteLine($"-> RightCurrent: {node.RightCurrent} A");
        //                break;

        //            case 0x0052:
        //                node.ParseRightStatus((ushort)value);
        //                Console.WriteLine($"-> RightStatus Updated (Bits: {Convert.ToString(value, 2).PadLeft(16, '0')})");
        //                break;

        //            // --- LEFT MOTOR ---
        //            case 0x0027:
        //                node.LeftSpeed = value.ToString();
        //                Console.WriteLine($"-> LeftSpeed: {node.LeftSpeed} RPM");
        //                break;

        //            case 0x0036:
        //                node.LeftCurrent = (value / 10.0).ToString("F1");
        //                Console.WriteLine($"-> LeftCurrent: {node.LeftCurrent} A");
        //                break;

        //            case 0x003A:
        //                node.ParseLeftStatus((ushort)value);
        //                Console.WriteLine($"-> LeftStatus Updated (Bits: {Convert.ToString(value, 2).PadLeft(16, '0')})");
        //                break;
        //            // --- Left Motor Logic ---

        //        }
        //    });
        //}
        public void UpdateData(SubNode activeNode)
        {
            {
                if (_comm == null || !_comm.IsOpen || activeNode == null) return;
                if (!byte.TryParse(activeNode.SeqId, out byte slaveId)) return;

                // Trigger request (Modbus Function Code + Slave ID)
                byte[] trigger = new byte[] { slaveId, 0xFF };

                // Only append CRC for Serial/RTU connections
                if (!(_comm is CommEthernet)) trigger = AppendModbusCRC(trigger);

                var res = _comm.SendRecv(trigger);

                if (res == null || res.Length < 8) return;

                for (int i = 0; i <= res.Length - 8; i += 8)
                {
                    if (res[i] != slaveId) continue;

                    ushort register = (ushort)((res[i + 2] << 8) | res[i + 3]);
                    int value = (res[i + 4] << 8) | res[i + 5];

                    switch (register)
                    {
                        case 0x0027: activeNode.LeftSpeed = $"{value}"; break;
                        case 0x002A: activeNode.LeftAcceleration = $"{value}"; break;
                        case 0x002F: activeNode.LeftDirection = (value == 1) ? "CCW" : "CW"; break;
                        case 0x002B: activeNode.LeftDeceleration = $"{value}"; break;
                        case 0x0036: // Left Current
                            activeNode.LeftCurrent = (value / 10.0).ToString("F1");
                            break;

                        case 0x003A:
                            activeNode.LeftOverVoltage = (value & (1 << 6)) == 0;
                            activeNode.LeftUnderVoltage = (value & (1 << 7)) == 0;
                            activeNode.LeftOverTemperature = (value & (1 << 8)) == 0;
                            activeNode.LeftOverCurrent = (value & (1 << 9)) == 0;
                            activeNode.LeftMotorStalled = (value & (1 << 13)) == 0;
                            activeNode.LeftHallSensorError = (value & (1 << 14)) == 0;
                            break;


                        //case 0x002E:
                        //    // Updates the Left UI Dropdown
                        //    activeNode.LeftBrakeIndex = value;
                        //    break;

                        //case 0x0046:
                        //    // Updates the Right UI Dropdown
                        //    activeNode.RightBrakeIndex = value;
                        //    break;

                        case 0x002E:
                            // Updates the Left UI Dropdown safely on the UI Thread
                            App.Current.Dispatcher.Invoke(() =>
                            {
                                activeNode.LeftBrakeIndex = value;
                            });
                            break;

                        case 0x0046:
                            // Updates the Right UI Dropdown safely on the UI Thread
                            App.Current.Dispatcher.Invoke(() =>
                            {

                                activeNode.RightBrakeIndex = value;
                            });
                            break;
                        case 0x0001:
                            // Updates the Left UI Dropdown safely on the UI Thread

                            activeNode.LeftMotorHallTypeIndex = value;

                            break;

                        case 0x0002:
                            // Updates the Right UI Dropdown safely on the UI Thread

                            activeNode.RightMotorHallTypeIndex = value;

                            break;
                        case 0x003F: activeNode.RightSpeed = $"{value}"; break;
                        case 0x0042: activeNode.RightAcceleration = $"{value}"; break;
                        case 0x0047: activeNode.RightDirection = (value == 1) ? "CCW" : "CW"; break;
                        case 0x0043: activeNode.RightDeceleration = $"{value}"; break;
                        case 0x004E:
                            activeNode.RightCurrent = (value / 10.0).ToString("F1");
                            break;

                        case 0x0052:
                            activeNode.RightOverVoltage = (value & (1 << 6)) == 0;
                            activeNode.RightUnderVoltage = (value & (1 << 7)) == 0;
                            activeNode.RightOverTemperature = (value & (1 << 8)) == 0;
                            activeNode.RightOverCurrent = (value & (1 << 9)) == 0;
                            activeNode.RightMotorStalled = (value & (1 << 13)) == 0;
                            activeNode.RightHallSensorError = (value & (1 << 14)) == 0;
                            break;
                    }
                    }
                }
            }
        public void UpdateData(string portName)
        {
            var activeDevice = ActiveDevices.FirstOrDefault(d => d.PortName == portName);
            var activeNode = activeDevice?.ConnectedNodes.FirstOrDefault(n => n.IsActive);

            if (activeNode != null)
            {
                UpdateData(activeNode);
            }
        }
    }
}

