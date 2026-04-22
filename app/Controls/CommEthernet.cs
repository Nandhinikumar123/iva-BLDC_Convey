
using System;
using System.Diagnostics;
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

                // 1. Connect
                var result = _client.BeginConnect(_ip, _port, null, null);
                if (!result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(3)))
                {
                    Debug.WriteLine("Connection timed out.");
                    return false;
                }

                _stream = _client.GetStream();
                _stream.ReadTimeout = 2000;

                // Clear buffer
                while (_stream.DataAvailable) _stream.ReadByte();

                // 2. Handshake (Send 01 01)
                byte[] handshake = new byte[] { 0x01, 0x01 };
                _stream.Write(handshake, 0, handshake.Length);
                _stream.Flush();

                byte[] response = new byte[2];
                int bytesRead = 0;
                while (bytesRead < 2)
                {
                    int r = _stream.Read(response, bytesRead, 2 - bytesRead);
                    if (r <= 0) return false;
                    bytesRead += r;
                }

                if (response[0] == 0x01 && response[1] == 0x01)
                {
                    Debug.WriteLine("Handshake successful. Sending Activation...");

                    // 3. CRITICAL FIX: Send Activation Frame (Function 0xFF)
                    // This tells ESP32 to set activeSeqId and start telemetry
                    byte[] activationFrame = new byte[] {
                        0x00, 0x01, // Transaction ID
                        0x00, 0x00, // Protocol ID
                        0x00, 0x02, // Length (UnitID + Payload)
                        0x01,       // Unit ID (Slave 1)
                        0xFF        // Function Code: Activate Telemetry
                    };
                    _stream.Write(activationFrame, 0, activationFrame.Length);
                    _stream.Flush();

                    StartBackgroundRead();
                    return true;
                }

                Close();
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Open Error: " + ex.Message);
                Close();
                return false;
            }
        }
        //public bool Open()
        //{
        //    try
        //    {
        //        Close(); // Ensure clean slate before trying
        //        _client = new TcpClient();

        //        // 1. Connect with a slightly longer timeout (3 seconds)
        //        var result = _client.BeginConnect(_ip, _port, null, null);
        //        if (!result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(3)))
        //        {
        //            Debug.WriteLine("Connection attempt timed out.");
        //            return false;
        //        }

        //        _stream = _client.GetStream();
        //        _stream.ReadTimeout = 2000; // Give ESP32 2 full seconds to reply

        //        // --- CRITICAL FIX: The Buffer Flush ---
        //        // Clear out any old garbage data from previous broken sessions
        //        while (_stream.DataAvailable)
        //        {
        //            _stream.ReadByte();
        //        }

        //        // 2. Perform Custom Handshake (Send 01 01)
        //        byte[] handshake = new byte[] { 0x01, 0x01 };
        //        _stream.Write(handshake, 0, handshake.Length);
        //        _stream.Flush();

        //        // 3. Wait for Echo (01 01) from ESP32
        //        byte[] response = new byte[2];
        //        int bytesRead = 0;

        //        // Loop to ensure we actually get 2 bytes, even if they arrive 1 by 1
        //        while (bytesRead < 2)
        //        {
        //            int r = _stream.Read(response, bytesRead, 2 - bytesRead);
        //            if (r == 0) return false; // Connection closed by remote
        //            bytesRead += r;
        //        }

        //        if (response[0] == 0x01 && response[1] == 0x01)
        //        {
        //            Debug.WriteLine("Handshake successful.");
        //            StartBackgroundRead();
        //            return true;
        //        }

        //        Debug.WriteLine($"Handshake failed. Received: {response[0]:X2} {response[1]:X2}");
        //        Close();
        //        return false;
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine("Open Error: " + ex.Message);
        //        Close();
        //        return false;
        //    }
        //}

        //public bool Open()
        //{
        //    try
        //    {
        //       // Close();
        //        _client = new TcpClient();

        //        // 1. Initiate Connection
        //        var result = _client.BeginConnect(_ip, _port, null, null);
        //        if (!result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2))) return false;

        //        _stream = _client.GetStream();
        //        _stream.ReadTimeout = 1000;

        //        // 2. Perform Custom Handshake (Send 01 01)
        //        byte[] handshake = new byte[] { 0x01, 0x01 };
        //        _stream.Write(handshake, 0, handshake.Length);
        //        _stream.Flush();

        //        // 3. Wait for Echo (01 01) from ESP32
        //        byte[] response = new byte[2];
        //        int read = _stream.Read(response, 0, 2);

        //        if (read == 2 && response[0] == 0x01 && response[1] == 0x01)
        //        {
        //            Console.WriteLine("received");
        //            // Handshake successful! Start listening for telemetry
        //            StartBackgroundRead();
        //            return true;
        //        }

        //      //  Close();
        //        return false;
        //    }
        //    catch { return false; }
        //}


        public byte[] SendRecv(byte[] fullFrame)
        {
            if (!IsOpen) return null;
            try
            {
                // IMPORTANT: We do NOT add 6 bytes here. 
                // BL.cs already called SendTcpFrame which added them.
                _stream.Write(fullFrame, 0, fullFrame.Length);
                _stream.Flush();
                return null;
            }
            catch { Close(); return null; }
        }

        public void StartBackgroundRead()
        {
            _isListening = true;
            Task.Run(() =>
            {
                while (_isListening && IsOpen)
                {
                    try
                    {
                        byte[] header = new byte[6];
                        int read = 0;
                        while (read < 6)
                        {
                            int r = _stream.Read(header, read, 6 - read);
                            if (r <= 0) throw new Exception("Disconnect");
                            read += r;
                        }

                        int len = (header[4] << 8) | header[5];
                        if (len <= 0) continue;

                        byte[] payload = new byte[len];
                        int pRead = 0;
                        while (pRead < len)
                        {
                            int r = _stream.Read(payload, pRead, len - pRead);
                            pRead += r;
                        }
                        string hexData = BitConverter.ToString(payload);
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] RX Payload ({len} bytes): {hexData}");
                        // If UI is waiting, update it
                        TelemetryReceived?.Invoke(payload);
                    }
                    catch { Close(); break; }
                }
            });
        }
        //public byte[] SendRecv(byte[] req)
        //{
        //    if (!IsOpen) return null;
        //    try
        //    {
        //        // Wrap RTU request in Modbus TCP Header
        //        byte[] frame = new byte[6 + req.Length];
        //        frame[0] = 0x00; frame[1] = 0x01; // Transaction ID
        //        frame[2] = 0x00; frame[3] = 0x00; // Protocol ID
        //        frame[4] = (byte)(req.Length >> 8);
        //        frame[5] = (byte)(req.Length & 0xFF);
        //        Array.Copy(req, 0, frame, 6, req.Length);

        //        _stream.Write(frame, 0, frame.Length);
        //        _stream.Flush();

        //        // We return null because incoming responses are handled by the background thread
        //        return null;
        //    }
        //    catch { return null; }
        //}

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