using System;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace BLDC_Demo.Services
{
    public class BLDCService
    {
        private TcpClient _client;
        private Thread _receiveThread;

        public event Action<bool> EthernetStatusChanged;
        public event Action<string> DataReceived;

        public void Start(string ip, int port)
        {
            CheckEthernetStatus();
            Connect(ip, port);
        }

        private void CheckEthernetStatus()
        {
            bool status = NetworkInterface.GetIsNetworkAvailable();
            EthernetStatusChanged?.Invoke(status);
        }

        private void Connect(string ip, int port)
        {
            try
            {
                _client = new TcpClient(ip, port);
                EthernetStatusChanged?.Invoke(true);

                _receiveThread = new Thread(ReceiveData);
                _receiveThread.IsBackground = true;
                _receiveThread.Start();
            }
            catch
            {
                EthernetStatusChanged?.Invoke(false);
            }
        }

        private void ReceiveData()
        {
            NetworkStream stream = _client.GetStream();
            byte[] buffer = new byte[1024];

            while (true)
            {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);

                if (bytesRead > 0)
                {
                    string data = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    DataReceived?.Invoke(data);
                }
            }
        }
    }
}
