using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace BLDC_Demo
{
    // ────────────────────────────────────────────────────────────
    //  Model: a discovered network module
    // ────────────────────────────────────────────────────────────
    public class NetworkModule
    {
        public string SerialNumber { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string Mask { get; set; } = string.Empty;
        public string Gateway { get; set; } = string.Empty;
        public string FirmwareRev { get; set; } = string.Empty;
        public string Position { get; set; } = "—";

        public override string ToString() =>
            $"{IpAddress}   SN: {SerialNumber}   FW: {FirmwareRev}";
    }

    // ────────────────────────────────────────────────────────────
    //  Backup container
    // ────────────────────────────────────────────────────────────
    public class ModuleBackup
    {
        public string SerialNumber { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string Mask { get; set; } = string.Empty;
        public string Gateway { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public DateTime BackupTime { get; set; } = DateTime.Now;
    }

    // ────────────────────────────────────────────────────────────
    //  UserControl code-behind  (targets C# 7.3)
    // ────────────────────────────────────────────────────────────
    public partial class NetworkService : UserControl
    {
        private readonly Dictionary<string, ModuleBackup> _backupStore
            = new Dictionary<string, ModuleBackup>();

        public NetworkService()
        {
            InitializeComponent();
            AttachOctetHandlers();
        }

        // ════════════════════════════════════════════════════════
        //  OCTET HELPERS
        // ════════════════════════════════════════════════════════
        private void AttachOctetHandlers()
        {
            var ipBoxes = new[] { TxtIp1, TxtIp2, TxtIp3, TxtIp4 };
            var maskBoxes = new[] { TxtMask1, TxtMask2, TxtMask3, TxtMask4 };
            var gwBoxes = new[] { TxtGw1, TxtGw2, TxtGw3, TxtGw4 };

            AttachToGroup(ipBoxes);
            AttachToGroup(maskBoxes);
            AttachToGroup(gwBoxes);

            ChkGateway.Checked += (s, e) => SetGatewayEnabled(true);
            ChkGateway.Unchecked += (s, e) => SetGatewayEnabled(false);
        }

        private void SetGatewayEnabled(bool enabled)
        {
            TxtGw1.IsEnabled = TxtGw2.IsEnabled =
            TxtGw3.IsEnabled = TxtGw4.IsEnabled = enabled;
        }

        private static void AttachToGroup(TextBox[] boxes)
        {
            for (int i = 0; i < boxes.Length; i++)
            {
                int idx = i;

                boxes[idx].PreviewTextInput += (s, e) =>
                {
                    e.Handled = !Regex.IsMatch(e.Text, @"^\d$");
                };

                boxes[idx].PreviewKeyDown += (s, e) =>
                {
                    TextBox tb = (TextBox)s;
                    if (e.Key == Key.OemPeriod || e.Key == Key.Decimal)
                    {
                        e.Handled = true;
                        if (idx + 1 < boxes.Length) boxes[idx + 1].Focus();
                    }
                    else if (e.Key == Key.Back && tb.Text.Length == 0 && idx > 0)
                    {
                        boxes[idx - 1].Focus();
                        boxes[idx - 1].SelectAll();
                    }
                };

                boxes[idx].TextChanged += (s, e) =>
                {
                    TextBox tb = (TextBox)s;
                    if (tb.Text.Length == 3 && idx + 1 < boxes.Length)
                        boxes[idx + 1].Focus();
                };

                boxes[idx].LostFocus += (s, e) =>
                {
                    TextBox tb = (TextBox)s;
                    if (int.TryParse(tb.Text, out int val))
                        tb.Text = Clamp(val, 0, 255).ToString();
                    else if (tb.Text.Length > 0)
                        tb.Text = string.Empty;
                };
            }
        }

        // Math.Clamp is not available in .NET < 3.0 / C# 7.3 targets
        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        // ════════════════════════════════════════════════════════
        //  IP FIELD READ / WRITE
        // ════════════════════════════════════════════════════════
        private string GetIp() => $"{TxtIp1.Text}.{TxtIp2.Text}.{TxtIp3.Text}.{TxtIp4.Text}";
        private string GetMask() => $"{TxtMask1.Text}.{TxtMask2.Text}.{TxtMask3.Text}.{TxtMask4.Text}";

        private string GetGateway() =>
            ChkGateway.IsChecked == true
                ? $"{TxtGw1.Text}.{TxtGw2.Text}.{TxtGw3.Text}.{TxtGw4.Text}"
                : string.Empty;

        private void SetIpFields(string ip, string mask, string gw)
        {
            FillOctets(ip, TxtIp1, TxtIp2, TxtIp3, TxtIp4);
            FillOctets(mask, TxtMask1, TxtMask2, TxtMask3, TxtMask4);

            bool hasGw = !string.IsNullOrWhiteSpace(gw);
            ChkGateway.IsChecked = hasGw;
            SetGatewayEnabled(hasGw);

            if (hasGw) FillOctets(gw, TxtGw1, TxtGw2, TxtGw3, TxtGw4);
            else ClearOctets(TxtGw1, TxtGw2, TxtGw3, TxtGw4);
        }

        private static void FillOctets(string address,
            TextBox b1, TextBox b2, TextBox b3, TextBox b4)
        {
            string[] parts = address != null
                ? address.Split('.')
                : new string[0];

            b1.Text = parts.Length > 0 ? parts[0] : string.Empty;
            b2.Text = parts.Length > 1 ? parts[1] : string.Empty;
            b3.Text = parts.Length > 2 ? parts[2] : string.Empty;
            b4.Text = parts.Length > 3 ? parts[3] : string.Empty;
        }

        private static void ClearOctets(params TextBox[] boxes)
        {
            foreach (TextBox b in boxes) b.Text = string.Empty;
        }

        private static bool IsValidIp(string ip) =>
            IPAddress.TryParse(ip, out _);

        // ════════════════════════════════════════════════════════
        //  DISCOVER
        // ════════════════════════════════════════════════════════
        private async void BtnDiscover_Click(object sender, RoutedEventArgs e)
        {
            LstModules.Items.Clear();
            TxtStatus.Text = "Discovering modules on the network…";

            Button btn = (Button)sender;
            btn.IsEnabled = false;

            try
            {
                List<NetworkModule> modules = await Task.Run(() => DiscoverModules());

                foreach (NetworkModule m in modules)
                    LstModules.Items.Add(m);

                TxtStatus.Text = modules.Count == 0
                    ? "No modules found."
                    : $"Found {modules.Count} module(s).";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Discovery error: {ex.Message}";
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }

        // Ping-sweep — replace body with your real scanner
        private static List<NetworkModule> DiscoverModules()
        {
            var found = new List<NetworkModule>();
            string baseIp = GetLocalSubnetBase();
            if (string.IsNullOrEmpty(baseIp)) return found;

            var tasks = new List<Task<Tuple<bool, string>>>();

            for (int i = 1; i <= 254; i++)
            {
                string target = $"{baseIp}.{i}";
                tasks.Add(PingAsync(target));
            }

            Task.WaitAll(tasks.ToArray());

            foreach (Task<Tuple<bool, string>> t in tasks)
            {
                if (t.Result.Item1)
                {
                    found.Add(new NetworkModule
                    {
                        IpAddress = t.Result.Item2,
                        SerialNumber = "—",
                        FirmwareRev = "—",
                        Position = "—"
                    });
                }
            }

            return found;
        }

        private static async Task<Tuple<bool, string>> PingAsync(string ip)
        {
            try
            {
                using (var ping = new Ping())
                {
                    PingReply reply = await ping.SendPingAsync(ip, 200);
                    return Tuple.Create(reply.Status == IPStatus.Success, ip);
                }
            }
            catch
            {
                return Tuple.Create(false, ip);
            }
        }

        private static string GetLocalSubnetBase()
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                foreach (UnicastIPAddressInformation ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        string[] parts = ua.Address.ToString().Split('.');
                        if (parts.Length == 4)
                            return $"{parts[0]}.{parts[1]}.{parts[2]}";
                    }
                }
            }
            return string.Empty;
        }

        // ════════════════════════════════════════════════════════
        //  LIST SELECTION → populate fields
        // ════════════════════════════════════════════════════════
        private void LstModules_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            NetworkModule m = LstModules.SelectedItem as NetworkModule;
            if (m == null) return;

            TxtSerialNumber.Text = m.SerialNumber;
            TxtPosition.Text = m.Position;
            SetIpFields(m.IpAddress, m.Mask, m.Gateway);
        }

        // ════════════════════════════════════════════════════════
        //  SELECT ALL / NONE
        // ════════════════════════════════════════════════════════
        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
            => LstModules.SelectAll();

        private void BtnSelectNone_Click(object sender, RoutedEventArgs e)
            => LstModules.UnselectAll();

        // ════════════════════════════════════════════════════════
        //  SET
        // ════════════════════════════════════════════════════════
        private void BtnSet_Click(object sender, RoutedEventArgs e)
        {
            NetworkModule module = LstModules.SelectedItem as NetworkModule;
            if (module == null)
            { Warn("Please select a module first.", "No Selection"); return; }

            string ip = GetIp();
            string mask = GetMask();
            string gateway = GetGateway();

            if (!IsValidIp(ip))
            { Warn("Enter a valid IP address.", "Invalid IP"); return; }

            if (!IsValidIp(mask))
            { Warn("Enter a valid subnet mask.", "Invalid Mask"); return; }

            if (!string.IsNullOrEmpty(gateway) && !IsValidIp(gateway))
            { Warn("Enter a valid gateway address.", "Invalid Gateway"); return; }

            module.IpAddress = ip;
            module.Mask = mask;
            module.Gateway = gateway;

            int idx = LstModules.SelectedIndex;
            LstModules.Items.Refresh();
            LstModules.SelectedIndex = idx;

            TxtStatus.Text = $"Settings applied → IP: {ip}  Mask: {mask}" +
                             (string.IsNullOrEmpty(gateway) ? "" : $"  GW: {gateway}");
        }

        // ════════════════════════════════════════════════════════
        //  BACKUP
        // ════════════════════════════════════════════════════════
        private void BtnBackup_Click(object sender, RoutedEventArgs e)
        {
            if (LstModules.SelectedItems.Count == 0)
            { Warn("Select at least one module to back up.", "No Selection"); return; }

            SaveFileDialog dlg = new SaveFileDialog
            {
                Title = "Save Backup",
                Filter = "Backup files (*.bak)|*.bak|All files (*.*)|*.*",
                DefaultExt = ".bak",
                FileName = $"modules_backup_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (dlg.ShowDialog() != true) return;

            var lines = new List<string>();
            foreach (NetworkModule m in LstModules.SelectedItems)
            {
                var bk = new ModuleBackup
                {
                    SerialNumber = m.SerialNumber,
                    IpAddress = m.IpAddress,
                    Mask = m.Mask,
                    Gateway = m.Gateway,
                    Position = m.Position,
                    BackupTime = DateTime.Now
                };
                _backupStore[m.SerialNumber] = bk;
                lines.Add($"{bk.BackupTime:s},{bk.SerialNumber},{bk.IpAddress}," +
                          $"{bk.Mask},{bk.Gateway},{bk.Position}");
            }

            System.IO.File.WriteAllLines(dlg.FileName, lines);
            TxtStatus.Text =
                $"Backup saved → {dlg.FileName}  ({LstModules.SelectedItems.Count} module(s))";
        }

        // ════════════════════════════════════════════════════════
        //  RESTORE
        // ════════════════════════════════════════════════════════
        private void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (LstModules.SelectedItems.Count == 0)
            { Warn("Select at least one module to restore.", "No Selection"); return; }

            OpenFileDialog dlg = new OpenFileDialog
            {
                Title = "Open Backup File",
                Filter = "Backup files (*.bak)|*.bak|All files (*.*)|*.*"
            };

            if (dlg.ShowDialog() != true) return;

            int restored = 0;
            foreach (string line in System.IO.File.ReadAllLines(dlg.FileName))
            {
                string[] parts = line.Split(',');
                if (parts.Length < 6) continue;

                string sn = parts[1].Trim();
                foreach (NetworkModule m in LstModules.SelectedItems)
                {
                    if (m.SerialNumber != sn) continue;
                    m.IpAddress = parts[2].Trim();
                    m.Mask = parts[3].Trim();
                    m.Gateway = parts[4].Trim();
                    m.Position = parts[5].Trim();
                    restored++;
                }
            }

            LstModules.Items.Refresh();
            TxtStatus.Text = restored > 0
                ? $"Restored {restored} module(s) from {dlg.FileName}"
                : "No matching modules found in the backup file.";
        }

        // ════════════════════════════════════════════════════════
        //  RESTORE BY IP
        // ════════════════════════════════════════════════════════
        private void BtnRestoreByIp_Click(object sender, RoutedEventArgs e)
        {
            string ip = GetIp();

            if (!IsValidIp(ip))
            { Warn("Enter a valid target IP address first.", "No IP"); return; }

            OpenFileDialog dlg = new OpenFileDialog
            {
                Title = "Open Backup File",
                Filter = "Backup files (*.bak)|*.bak|All files (*.*)|*.*"
            };

            if (dlg.ShowDialog() != true) return;

            bool found = false;
            foreach (string line in System.IO.File.ReadAllLines(dlg.FileName))
            {
                string[] parts = line.Split(',');
                if (parts.Length < 6) continue;
                if (parts[2].Trim() != ip) continue;

                foreach (NetworkModule m in LstModules.Items)
                {
                    if (m.IpAddress != ip) continue;
                    m.Mask = parts[3].Trim();
                    m.Gateway = parts[4].Trim();
                    m.Position = parts[5].Trim();
                    found = true;
                }
            }

            LstModules.Items.Refresh();
            TxtStatus.Text = found
                ? $"Restore by IP complete → {ip}"
                : $"No backup entry found for IP {ip}.";
        }

        // ════════════════════════════════════════════════════════
        //  REORDER IPs
        // ════════════════════════════════════════════════════════
        private void BtnReorderIPs_Click(object sender, RoutedEventArgs e)
        {
            if (LstModules.Items.Count == 0)
            { Warn("No modules in the list to reorder.", "Empty List"); return; }

            ReorderIpDialog dlg = new ReorderIpDialog();
            if (dlg.ShowDialog() != true) return;

            if (!IPAddress.TryParse(dlg.StartIp, out IPAddress startAddr))
            { Warn("Invalid start IP address.", "Invalid IP"); return; }

            byte[] bytes = startAddr.GetAddressBytes();
            int counter = bytes[3];

            foreach (NetworkModule m in LstModules.Items)
            {
                bytes[3] = (byte)Clamp(counter++, 1, 254);
                m.IpAddress = new IPAddress(bytes).ToString();
            }

            LstModules.Items.Refresh();
            TxtStatus.Text = $"IPs reordered starting from {dlg.StartIp}";
        }

        // ════════════════════════════════════════════════════════
        //  UPGRADE FW
        // ════════════════════════════════════════════════════════
        private async void BtnUpgradeFw_Click(object sender, RoutedEventArgs e)
        {
            if (LstModules.SelectedItems.Count == 0)
            { Warn("Select at least one module to upgrade.", "No Selection"); return; }

            MessageBoxResult confirm = MessageBox.Show(
                $"Upgrade firmware on {LstModules.SelectedItems.Count} module(s)?\n" +
                "Do NOT power off during upgrade.",
                "Confirm Firmware Upgrade",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            OpenFileDialog dlg = new OpenFileDialog
            {
                Title = "Select Firmware File",
                Filter = "Firmware files (*.bin;*.fw)|*.bin;*.fw|All files (*.*)|*.*"
            };

            if (dlg.ShowDialog() != true) return;

            Button btn = (Button)sender;
            btn.IsEnabled = false;
            TxtStatus.Text = "Firmware upgrade in progress… do not power off.";

            // Snapshot selection before awaiting
            List<NetworkModule> targets = LstModules.SelectedItems
                .Cast<NetworkModule>()
                .ToList();

            string fwPath = dlg.FileName;

            try
            {
                await Task.Run(() =>
                {
                    foreach (NetworkModule m in targets)
                    {
                        // TODO: replace with real flash logic:
                        //   FirmwareFlasher.Flash(m.IpAddress, fwPath);
                        System.Threading.Thread.Sleep(500);
                        m.FirmwareRev = "NEW";
                    }
                });

                LstModules.Items.Refresh();
                TxtStatus.Text =
                    $"Firmware upgrade complete for {targets.Count} module(s).";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Firmware upgrade failed: {ex.Message}";
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }

        // ════════════════════════════════════════════════════════
        //  UTILITY
        // ════════════════════════════════════════════════════════
        private static void Warn(string message, string title) =>
            MessageBox.Show(message, title,
                            MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    // ────────────────────────────────────────────────────────────
    //  Minimal "Enter start IP" dialog for Reorder IPs
    // ────────────────────────────────────────────────────────────
    public class ReorderIpDialog : Window
    {
        private readonly TextBox _input;
        public string StartIp => _input.Text.Trim();

        public ReorderIpDialog()
        {
            Title = "Reorder IPs";
            Width = 280;
            Height = 130;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            StackPanel sp = new StackPanel { Margin = new Thickness(12) };

            sp.Children.Add(new TextBlock
            {
                Text = "Enter starting IP address:",
                Margin = new Thickness(0, 0, 0, 6)
            });

            _input = new TextBox { Height = 24, Text = "192.168.1.1" };
            sp.Children.Add(_input);

            StackPanel btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            Button ok = new Button
            {
                Content = "OK",
                Width = 70,
                Height = 24,
                Margin = new Thickness(0, 0, 6, 0),
                IsDefault = true
            };
            ok.Click += (s, ev) => { DialogResult = true; };

            Button cancel = new Button
            {
                Content = "Cancel",
                Width = 70,
                Height = 24,
                IsCancel = true
            };

            btnRow.Children.Add(ok);
            btnRow.Children.Add(cancel);
            sp.Children.Add(btnRow);

            Content = sp;
        }
    }
}