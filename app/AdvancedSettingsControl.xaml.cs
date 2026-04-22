using System.Windows.Controls;

namespace BLDC_Demo
{
    public partial class AdvancedSettingsControl : UserControl
    {
        // ── Lazy-loaded tab controls ─────────────────────────────────
        private LookAheadTimingControl _lookAhead;
        private Connections _connections;
        private NetworkService _networkService;
        private AuxUsage _auxUsage;
        //private Firmwareupload _firmwareupload;
        public AdvancedSettingsControl()
        {
            InitializeComponent();
            LoadTab(0);
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl tc)
                LoadTab(tc.SelectedIndex);
        }

        private void LoadTab(int index)
        {
            switch (index)
            {
                // ── Tab 0: Look Ahead & Timing ───────────────────────
                case 0:
                    if (LookAheadContent == null) return;
                    if (_lookAhead == null)
                        _lookAhead = new LookAheadTimingControl();
                    LookAheadContent.Content = _lookAhead;
                    break;

                // ── Tab 6: Connections ───────────────────────────────
                case 6:
                    if (ConnectionsContent == null) return;
                    if (_connections == null)
                        _connections = new Connections();
                    ConnectionsContent.Content = _connections;
                    break;
                //case 6:
                //    if (FirmwareuploadContent == null) return;
                //    if (_firmwareupload == null)
                //        _firmwareupload = new FirmwareUploadControl();
                //    FirmwareuploadContent.Content = _firmwareupload;
                //    break;
                // ── Tab 7: Network Services ──────────────────────────
                case 7:
                    if (NetworkServicesContent == null) return;
                    if (_networkService == null)
                        _networkService = new NetworkService();
                    NetworkServicesContent.Content = _networkService;
                    break;
                case 1
                :
                    if (AuxUsageContent == null) return;
                    if (_auxUsage == null)
                        _auxUsage = new AuxUsage();
                    AuxUsageContent.Content = _auxUsage;
                    break;
            }
        }
    }
}
