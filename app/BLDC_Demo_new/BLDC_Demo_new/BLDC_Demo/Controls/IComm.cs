using System;

namespace BLDC_Demo.Controls
{
    public interface IComm
    {
        event Action ConnectionLost;

        bool Open();
        void Close();
        byte[] SendRecv(byte[] req);
        bool IsOpen { get; }
        string Name { get; }
    }
}
//namespace BLDC_Demo.Controls
//{
//    public interface IComm
//    {
//        bool Open();
//        void Close();
//        byte[] SendRecv(byte[] req);
//        void SendRaw(byte[] data);    // Add this
//        int ReceiveRaw(byte[] buffer); // Add this
//        bool IsOpen { get; }
//        string Name { get; }
//    }
//}
//namespace BLDC_Demo.Controls
//{
//    public interface IComm
//    {
//        string Name { get; }
//        bool IsOpen { get; }
//        bool Open();
//        void Close();
//        byte[] SendRecv(byte[] data);
//    }
//}