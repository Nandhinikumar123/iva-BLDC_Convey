using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using BLDC_Demo.Models;

namespace BLDC_Demo.Controls
{
    public class BL
    {
        // UI Binding Collections
        public readonly ObservableCollection<ModbusDevice> ActiveDevices = new ObservableCollection<ModbusDevice>();

        public string ActivePortName { get; private set; } = string.Empty;
        private IComm _comm;

        // Events for UI communication
        public event Action OnDisconnected;

        public bool IsConnected => _comm != null && _comm.IsOpen;

        /// <summary>
        /// Initializes the communication and hooks up the Telemetry event
        /// </summary>
        public bool Start(IComm c)
        {
            _comm = c;

            if (_comm is CommEthernet tcp)
            {
                tcp.TelemetryReceived += (payload) =>
                {
                    if (payload == null || payload.Length < 2) return;

                    // 1. DYNAMIC DISCOVERY
                    // If we get a packet from a Slave ID (payload[0]) that isn't in our sidebar yet,
                    // we add it automatically, regardless of the Function Code (0x00 or 0x03).
                    string slaveIdStr = payload[0].ToString("D2");
                    var currentDevice = ActiveDevices.FirstOrDefault(d => d.PortName == ActivePortName);

                    if (currentDevice != null && !currentDevice.ConnectedNodes.Any(n => n.SeqId == slaveIdStr))
                    {
                        ParseBinaryDiscovery(payload);
                        return; // Exit after adding to sidebar so user can click it first
                    }

                    // 2. LIVE DATA UPDATE
                    // Only update values if the user has clicked/selected a node in the UI
                    var activeNode = ActiveDevices
                        .SelectMany(d => d.ConnectedNodes)
                        .FirstOrDefault(n => n.IsActive);

                    if (activeNode != null && payload[0] == byte.Parse(activeNode.SeqId))
                    {
                        ParseAndUpdateUI(payload, activeNode);
                    }
                };
            }

            if (_comm.Open())
            {
                ActivePortName = _comm.Name;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Adds new nodes to the Sidebar and triggers a UI Refresh
        /// </summary>
        private void ParseBinaryDiscovery(byte[] payload)
        {
            string seqId = payload[0].ToString("D2");

            Application.Current.Dispatcher.Invoke(() =>
            {
                var currentDevice = ActiveDevices.FirstOrDefault(d => d.PortName == ActivePortName);
                if (currentDevice != null)
                {
                    if (!currentDevice.ConnectedNodes.Any(n => n.SeqId == seqId))
                    {
                        currentDevice.ConnectedNodes.Add(new SubNode
                        {
                            SeqId = seqId,
                            IpAddress = _comm.Name.Split(':')[0],
                            Status = "ONLINE",
                            IsActive = false
                        });

                        // FORCE UI REFRESH: This makes the "ID: 01" appear in the sidebar immediately
                        CommMotorSelectControl.Instance?.RefreshUiList();
                        Debug.WriteLine($"[Discovery] Registered Node {seqId}");
                    }
                }
            });
        }

        /// <summary>
        /// Requests data for a specific node
        /// </summary>
        public void UpdateData(SubNode activeNode)
        {
            if (!IsConnected || activeNode == null) return;
            if (byte.TryParse(activeNode.SeqId, out byte slaveId))
            {
                // Send Function Code 0xFF to tell ESP32: "Start sending telemetry for this ID"
                SendTcpFrame(slaveId, 0xFF, new byte[] { 0x00, 0x00 });
            }
        }
        /// <summary>
        /// Maps incoming Modbus values to the UI SubNode properties
        /// </summary>
        public void ParseAndUpdateUI(byte[] payload, SubNode activeNode)
        {
            // Requires at least SlaveID(1) + FC(1) + Addr(2) + Val(2) = 6 bytes
            if (payload.Length < 6 || payload[1] != 0x03) return;

            ushort regAddr = (ushort)((payload[2] << 8) | payload[3]);
            ushort val = (ushort)((payload[4] << 8) | payload[5]);

            Application.Current.Dispatcher.Invoke(() =>
            {
                switch (regAddr)
                {
                    //case 0x0027: activeNode.LeftSpeed = val.ToString(); break;
                    //case 0x0036: activeNode.LeftCurrent = (val / 10.0).ToString("F1"); break;
                    //case 0x003A: // Status Bitmask
                    //    activeNode.LeftOverCurrent = (val & (1 << 9)) != 0;
                    //    activeNode.LeftMotorStalled = (val & (1 << 13)) != 0;
                    //    break;

                    //case 0x003F: activeNode.RightSpeed = val.ToString(); break;
                    //case 0x004E: activeNode.RightCurrent = (val / 10.0).ToString("F1"); break;
                    //case 0x0052: // Status Bitmask
                    //    activeNode.RightMotorStalled = (val & (1 << 13)) != 0;
                    //    break;
                    case 0x0020: // Speed
                        activeNode.DeviceID = val.ToString();
                        break;
                    case 0x0021: // Speed
                        activeNode.FirmwareVersion = val.ToString();
                        break;
                    case 0x0022: // Speed
                        activeNode.HardwareRevision = val.ToString();
                        break;
                    case 0x0023: // Speed
                        activeNode.DeviceStatus = val.ToString();
                        break;
                    case 0x0024: // Speed
                        activeNode.Node1= val.ToString();
                        break;
                    case 0x0025: // Speed
                        activeNode.Node2= val.ToString();
                        break;
                    case 0x0026: // Speed
                        activeNode.SerialNumber = val.ToString();
                        break;
                    case 0x0027: // Speed
                        activeNode.LeftSpeed = val.ToString();
                        break;
                    case 0x002A: // Acceleration
                        activeNode.LeftAcceleration = val.ToString();
                        break;
                    case 0x002B: // Deceleration
                        activeNode.LeftDeceleration = val.ToString();
                        break;
                    case 0x002F: // Direction (1 = CCW, 0 = CW)
                        activeNode.LeftDirection = (val == 1) ? "CCW" : "CW";
                        break;
                    case 0x0036: // Current (1 decimal place)
                        activeNode.LeftCurrent = (val / 10.0).ToString("F1");
                        break;
                    case 0x002E: // Brake Index
                        activeNode.LeftBrakeIndex = val;
                        break;
                    case 0x0001: // Hall Type
                        activeNode.LeftMotorHallTypeIndex = val;
                        break;
                    case 0x003A: // LEFT STATUS WORD (Bitmask)
                        activeNode.LeftOverVoltage = (val & (1 << 6)) != 0;
                        activeNode.LeftUnderVoltage = (val & (1 << 7)) != 0;
                        activeNode.LeftOverTemperature = (val & (1 << 8)) != 0;
                        activeNode.LeftOverCurrent = (val & (1 << 9)) != 0;
                        activeNode.LeftMotorStalled = (val & (1 << 13)) != 0;
                        activeNode.LeftHallSensorError = (val & (1 << 14)) != 0;
                        break;

                    // ================== RIGHT MOTOR ==================
                    case 0x003F: // Speed
                        activeNode.RightSpeed = val.ToString();
                        break;
                    case 0x0042: // Acceleration
                        activeNode.RightAcceleration = val.ToString();
                        break;
                    case 0x0043: // Deceleration
                        activeNode.RightDeceleration = val.ToString();
                        break;
                    case 0x0047: // Direction
                        activeNode.RightDirection = (val == 1) ? "CCW" : "CW";
                        break;
                    case 0x004E: // Current
                        activeNode.RightCurrent = (val / 10.0).ToString("F1");
                        break;
                    case 0x0046: // Brake Index
                        activeNode.RightBrakeIndex = val;
                        break;
                    case 0x0002: // Hall Type
                        activeNode.RightMotorHallTypeIndex = val;
                        break;
                    case 0x0052: // RIGHT STATUS WORD (Bitmask)
                        activeNode.RightOverVoltage = (val & (1 << 6)) != 0;
                        activeNode.RightUnderVoltage = (val & (1 << 7)) != 0;
                        activeNode.RightOverTemperature = (val & (1 << 8)) != 0;
                        activeNode.RightOverCurrent = (val & (1 << 9)) != 0;
                        activeNode.RightMotorStalled = (val & (1 << 13)) != 0;
                        activeNode.RightHallSensorError = (val & (1 << 14)) != 0;
                        break;

                    default:
                        // Optional: Debug unknown registers
                        // Debug.WriteLine($"Unknown Reg: 0x{regAddr:X4} Val: {val}");
                        break;
                }
            });
        }
        public void SendWriteCommand(byte slaveId, ushort registerAddress, ushort value)
        {
            if (!IsConnected) return;

            byte[] data = {
                (byte)(registerAddress >> 8), (byte)(registerAddress & 0xFF),
                (byte)(value >> 8), (byte)(value & 0xFF)
            };
            SendTcpFrame(slaveId, 0x06, data);
        }
        /// <summary>
        /// Standard Modbus TCP Frame Wrapper (6-byte Header + PDU)
        /// </summary>
        private void SendTcpFrame(byte slaveId, byte functionCode, byte[] data)
        {
            byte[] pdu = new byte[2 + data.Length];
            pdu[0] = slaveId;
            pdu[1] = functionCode;
            Array.Copy(data, 0, pdu, 2, data.Length);

            byte[] tcpFrame = new byte[6 + pdu.Length];
            tcpFrame[0] = 0x00; tcpFrame[1] = 0x01; // Transaction ID
            tcpFrame[2] = 0x00; tcpFrame[3] = 0x00; // Protocol ID
            tcpFrame[4] = (byte)(pdu.Length >> 8);
            tcpFrame[5] = (byte)(pdu.Length & 0xFF);
            Array.Copy(pdu, 0, tcpFrame, 6, pdu.Length);

            _comm.SendRecv(tcpFrame);
        }
        public void Close()
        {
            if (_comm != null)
            {
                _comm.Close();
                _comm = null;
                ActivePortName = string.Empty;

                // --- CLEARING THE IN-MEMORY COLLECTIONS ---
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var device in ActiveDevices)
                    {
                        device.IsActive = false;

                        // Reach into the nodes and wipe their stored strings
                        foreach (var node in device.ConnectedNodes)
                        {
                            node.ClearMemory();
                        }
                    }

                    // OPTIONAL: If you want to delete the nodes from the sidebar entirely:
                    // foreach (var device in ActiveDevices) device.ConnectedNodes.Clear();
                });

                OnDisconnected?.Invoke();
            }
        }
        //public void Close()
        //{
        //    if (_comm != null)
        //    {
        //        _comm.Close();
        //        _comm = null;
        //        ActivePortName = string.Empty;
        //        OnDisconnected?.Invoke();
        //    }
        //}
    }
}