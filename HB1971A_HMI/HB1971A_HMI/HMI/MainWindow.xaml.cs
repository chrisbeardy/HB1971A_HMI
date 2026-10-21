using CommunityToolkit.Mvvm.Messaging;
using HBControls.Enums;
using HBControls.Themes;
using HBMessages.HBDataTypes.HBControls.Requests;
using HBMessages.HBMessages.HBSystemHandler;
using System.ComponentModel;
using System.Timers;
using static HBMessages.HBDataTypes.HBControls.Requests.ControlSetupData;

namespace HB1971A_HMI
{
    public partial class MainWindow : Window,
        INotifyPropertyChanged,
        IRecipient<UpdateTime>
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private const string PLCID = "PLC1";
        private const int PLCUpdateRateMS = 200;
#if DEBUG
        private const string PLC1_ADDR = "5.86.238.52.1.1";  // the 1971A controller, for running from a development PC
#else
        private const string PLC1_ADDR = "";  // empty = local PLC
#endif

        // Do not delete these — they register the messaging system
        private readonly HBDataStorage.HBDataStore _dataStore = new();
        private readonly HBBeckhoffPLC.BeckhoffPLC _plc1 = new();
        private readonly HBLanguage.HBLanguage _language = new();
        private readonly HBSystemHandler.UpdateHandler _handler = new();
        private readonly HBControls.ControlHandler.ControlHandler _controls = HBControls.ControlHandler.ControlHandler.Instance;
        private readonly HBRemoteServer.RemoteServer _remoteServer = new();

        private Timer dt = new();

        public MainWindow()
        {
            InitializeComponent();
            StrongReferenceMessenger.Default.RegisterAll(this);

            DataStorage.AddPLC(PLCID);
            DataStorage.AddHMIVar("HMI.HBColours", new List<Brush>() { Brushes.Green, Brushes.Red });

            Controls.UpdateControlSetup(
                invalidBrush: new SolidColorBrush(Color.FromArgb(0xFF, 0xEF, 0x62, 0x62)),
                EnableOption: SecuritySettings.IgnoreSecurityLevel,
                enableLevels: null,
                enableVariable: string.Empty,
                visibilitySecurity: ControlSetupData.SecuritySettings.IgnoreSecurityLevel,
                visibilityLevels: null,
                visibilityVariable: string.Empty,
                usePopupDialog: true);  // number entry opens the on-screen keypad, as the panel has no keyboard

            PLC.Connect(PLCID, PLC1_ADDR, 851);

            Remote.Start();

            // Every PLC variable the HMI uses
            SystemHandler.AddRemoteServerVariable(PLCID, "Status",                        "MAIN.Machine.StatusText");
            SystemHandler.AddRemoteServerVariable(PLCID, "At Station",                    "MAIN.Machine.Table.OnDwell");
            SystemHandler.AddRemoteServerVariable(PLCID, "HMI Forward",                   "MAIN.Machine.Table.HMI_Forward");
            SystemHandler.AddRemoteServerVariable(PLCID, "HMI Reverse",                   "MAIN.Machine.Table.HMI_Reverse");
            SystemHandler.AddRemoteServerVariable(PLCID, "HMI Reset",                     "MAIN.Machine.HMI_Reset");
            SystemHandler.AddRemoteServerVariable(PLCID, "Index Velocity",                "MAIN.Machine.Table.IndexVelocity");
            SystemHandler.AddRemoteServerVariable(PLCID, "Max Velocity",                  "MAIN.Machine.TableDrive.MaxVelocity");
            SystemHandler.AddRemoteServerVariable(PLCID, "Ramp Time Min",                 "MAIN.Machine.TableDrive.MIN_RAMP_TIME");
            SystemHandler.AddRemoteServerVariable(PLCID, "Ramp Time Max",                 "MAIN.Machine.TableDrive.MAX_RAMP_TIME");
            SystemHandler.AddRemoteServerVariable(PLCID, "Acceleration To Index Speed",   "MAIN.Machine.Table.AccelerationTimeToIndexVelocity");
            SystemHandler.AddRemoteServerVariable(PLCID, "Deceleration From Index Speed", "MAIN.Machine.Table.DecelerationTimeFromIndexVelocity");
            SystemHandler.AddRemoteServerVariable(PLCID, "Acceleration F002",             "MAIN.Machine.TableDrive.AccelerationTimeToMaxFrequency");
            SystemHandler.AddRemoteServerVariable(PLCID, "Deceleration F003",             "MAIN.Machine.TableDrive.DecelerationTimeFromMaxFrequency");
            SystemHandler.AddRemoteServerVariable(PLCID, "Shutdown Requested",            "MAIN.ShutdownRequested");

            DataContext = this;
        }

        public void Receive(UpdateTime message)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!message.Value.PlcReference.Equals(HBCommon.HBConst.HmiPlcRef))
                    UpdateTimePLC.Content = $"Update: {message.Value.TimeMS:F1}ms";
            }));
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            dt = new Timer(1000);
            dt.Start();
            dt.Elapsed += OnTimedEvent;
            ThemeManager.ApplyTheme(Theme.Cream);
            SizeManager.ApplyDensity(Density.Touch);
            SystemHandler.StartUpdate(PLCUpdateRateMS);
        }

        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                DateTime now = DateTime.Now;
                DateStr.Content = now.ToShortDateString();
                TimeStr.Content = now.ToShortTimeString();
            }));
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            SystemHandler.StopUpdate();
            PLC.Disconnect(PLCID);
            Remote.Stop();
            _remoteServer.Dispose();
        }

        private void TouchNav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && int.TryParse(fe.Tag?.ToString(), out int idx))
                SetTouchContent(idx);
        }

        private void SetTouchContent(int idx)
        {
            if (idx >= 0 && idx < MainTabs.Items.Count)
                MainTabs.SelectedIndex = idx;
        }

        private void NotifyPropertyChanged(string info)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }
    }
}
