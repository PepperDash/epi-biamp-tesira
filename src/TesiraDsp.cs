using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Bridge.JoinMaps;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Dialer;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Expander;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Interfaces;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Queue;
using PepperDash.Core;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.DeviceInfo;
using PepperDash.Essentials.Core.Queues;
using Feedback = PepperDash.Essentials.Core.Feedback;
using IRoutingWithFeedback = Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Interfaces.IRoutingWithFeedback;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    public class TesiraDsp : EssentialsBridgeableDevice,
        IDspPresets,
        ICommunicationMonitor,
        IDeviceInfoProvider,
        IHasFeedback
    {
        public const string KeyFormatter = "{0}--{1}";
        /// <summary>
        /// Collection of all Device Feedbacks
        /// </summary>
        public FeedbackCollection<Feedback> Feedbacks { get; private set; }

        /// <summary>
        /// Data Returning from Device
        /// </summary>
        public string DeviceRx { get; set; }

        public TesiraQueue CommandQueue { get; set; }

        /// <summary>
        /// Communication Object for Device
        /// </summary>
		public IBasicCommunication Communication { get; private set; }
        /// <summary>
        /// Communication Response Gather for Device
        /// </summary>
		public CommunicationGather PortGather { get; private set; }
        /// <summary>
        /// Communication Monitor for Device
        /// </summary>
		public StatusMonitorBase CommunicationMonitor { get; private set; }

        /// <summary>
        /// Command Responses from Device
        /// </summary>
		public StringFeedback CommandPassthruFeedback { get; set; }

        /// <summary>
        /// True when all components have been subscribed
        /// </summary>
        public bool IsSubscribed
        {
            get
            {
                bool subscribeTracker;
                lock (watchdogLock)
                {
                    subscribeTracker = ControlPointList.All(subscribedComponent => subscribedComponent.IsSubscribed);
                }
                if (subscribeThread == null) return subscribeTracker;
                if (subscribeTracker && subscribeThread.ThreadState == ThreadState.Running)
                    StopSubscriptionThread();
                return subscribeTracker;
            }
        }

        private GenericQueue transmitQueue;

        private System.Timers.Timer watchDogTimer;
        private System.Timers.Timer watchDogTimeoutTimer;

        private System.Timers.Timer unsubscribeTimer;
        private System.Timers.Timer queueCheckTimer;

        private Thread subscribeThread;

        private readonly bool isSerialComm;

        public bool InitialStart = true;
        public bool OkayToSend = false;
        public bool ControlsAdded = false;

        private TesiraDspDeviceInfo DevInfo { get; set; }
        private Dictionary<string, TesiraDspFaderControl> Faders { get; set; }
        private Dictionary<string, TesiraDspDialer> Dialers { get; set; }
        private Dictionary<string, TesiraDspSwitcher> Switchers { get; set; }
        private Dictionary<string, TesiraDspRouter> Routers { get; set; }
        private Dictionary<string, TesiraDspSourceSelector> SourceSelectors { get; set; }
        private Dictionary<string, TesiraDspStateControl> States { get; set; }
        private Dictionary<string, TesiraDspMeter> Meters { get; set; }

        private Dictionary<string, TesiraDspLogicMeter> LogicMeters { get; set; }
        private Dictionary<string, TesiraDspCrosspointState> CrosspointStates { get; set; }
        private Dictionary<string, TesiraDspRoomCombiner> RoomCombiners { get; set; }
        public List<TesiraPreset> TesiraPresets { get; private set; }
        private List<ISubscribedComponent> ControlPointList { get; set; }
        public Dictionary<string, IKeyName> Presets { get; private set; }

        private TesiraExpanderTracker ExpanderTracker { get; set; }

        private bool initalSubscription = true;

        private readonly object watchdogLock = new object();
        private bool watchDogSnifferValue;
        private int watchDogExpectedResponses = 0;
        private int watchDogReceivedResponses = 0;
        private HashSet<ISubscribedComponent> recentlyCheckedComponents = new HashSet<ISubscribedComponent>();
        private bool WatchDogSniffer
        {
            get
            {
                lock (watchdogLock)
                {
                    return watchDogSnifferValue;
                }
            }
            set
            {
                lock (watchdogLock)
                {
                    watchDogSnifferValue = value;
                }
            }
        }
        public bool WatchdogSuspend { get; private set; }

        private readonly DeviceConfig dc;

        public bool ShowHexResponse { get; set; }

        public string ResubscriptionString { get; set; }

        public DeviceInfo DeviceInfo
        {
            get
            {
                if (DevInfo != null) return DevInfo.DeviceInfo;

                else return new DeviceInfo();
            }
        }

        /// <summary>
        /// Consturctor for base Tesira DSP Device
        /// </summary>
        /// <param name="key">Tesira DSP Device Key</param>
        /// <param name="name">Tesira DSP Device Friendly Name</param>
        /// <param name="comm">Device Communication Object</param>
        /// <param name="dc">Full device configuration object</param>
        public TesiraDsp(string key, string name, IBasicCommunication comm, DeviceConfig dc)
            : base(key, name)
        {
            this.dc = dc;

            CommandQueue = new TesiraQueue(2000, this);

            transmitQueue = new GenericQueue($"{Key}-tx-queue", 250, 500);

            CommandPassthruFeedback = new StringFeedback("commandPassthru", () => DeviceRx);

            Communication = comm;

            if (comm is ISocketStatus socket)
            {
                this.LogDebug("DEVICE IS CONTROLLED VIA NETWORK CONNECTION");

                // This instance uses IP control
                socket.ConnectionChange += Socket_ConnectionChange;
                isSerialComm = false;


                if (comm is GenericSshClient ssh)
                {
                    DeviceInfo.IpAddress = ssh.Hostname;
                    DeviceInfo.HostName = ssh.Hostname;
                }


                if (comm is GenericTcpIpClient tcp)
                {
                    DeviceInfo.IpAddress = tcp.Hostname;
                    DeviceInfo.HostName = tcp.Hostname;
                }
            }
            else
            {
                this.LogDebug("DEVICE IS CONTROLLED VIA RS232");
                // This instance uses RS-232 control
                isSerialComm = true;
            }

            PortGather = new CommunicationGather(Communication, "\r\n")
            {
                IncludeDelimiter = false
            };
            PortGather.LineReceived += Port_LineReceived;

            CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, 20000, 120000, 300000, () => CommandQueue.EnqueueCommand("SESSION set verbose false"));

            ControlPointList = new List<ISubscribedComponent>();

            //Initialize Dictionaries
            Feedbacks = new FeedbackCollection<Feedback>();
            Faders = new Dictionary<string, TesiraDspFaderControl>();
            Presets = new Dictionary<string, IKeyName>();
            TesiraPresets = new List<TesiraPreset>();
            Dialers = new Dictionary<string, TesiraDspDialer>();
            Switchers = new Dictionary<string, TesiraDspSwitcher>();
            States = new Dictionary<string, TesiraDspStateControl>();
            Meters = new Dictionary<string, TesiraDspMeter>();
            LogicMeters = new Dictionary<string, TesiraDspLogicMeter>();
            CrosspointStates = new Dictionary<string, TesiraDspCrosspointState>();
            RoomCombiners = new Dictionary<string, TesiraDspRoomCombiner>();
            Routers = new Dictionary<string, TesiraDspRouter>();
            SourceSelectors = new Dictionary<string, TesiraDspSourceSelector>();

            CrestronEnvironment.ProgramStatusEventHandler += CrestronEnvironment_ProgramStatusEventHandler;


            CommunicationMonitor.StatusChange += CommunicationMonitor_StatusChange;
            CrestronConsole.AddNewConsoleCommand(CommandQueue.EnqueueCommand, "send" + Key, "", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(s => Communication.Connect(), "con" + Key, "", ConsoleAccessLevelEnum.AccessOperator);

            Feedbacks.Add(CommunicationMonitor.IsOnlineFeedback);
            Feedbacks.Add(CommandPassthruFeedback);

            CreateDspObjects();
        }

        private void CheckSerialSendStatus()
        {
            if (isSerialComm) this.LogVerbose("CheckSerialSendStatus");

            if (OkayToSend && ControlsAdded && isSerialComm && InitialStart)
            {
                InitialStart = false;
                this.LogVerbose("CheckSerialStatus Ready");

                StartSubscriptionThread();
                return;
            }
            if (isSerialComm) this.LogVerbose("CheckSerialSendStatus NOT READY");

        }

        public override void Initialize()
        {
            Communication.Connect();

            if (!isSerialComm) return;
            CommunicationMonitor.Start();
            OkayToSend = true;
            CheckSerialSendStatus();
        }

        private CancellationTokenSource subscriptionCancellationSource;

        private void StartSubscriptionThread()
        {
            StopSubscriptionThread();

            subscriptionCancellationSource = new CancellationTokenSource();
            var token = subscriptionCancellationSource.Token;

            subscribeThread = new Thread(o => HandleAttributeSubscriptions(token))
            {
                Name = string.Format("{0}-subscription", Key)
            };

            subscribeThread.Start();
        }

        private void StopSubscriptionThread()
        {
            this.LogDebug("Stopping subscription thread");

            // Cancel the subscription process
            subscriptionCancellationSource?.Cancel();

            // Dispose of unsubscribe timer
            unsubscribeTimer?.Stop();
            unsubscribeTimer?.Dispose();
            unsubscribeTimer = null;

            subscribeThread = null;

            this.LogDebug("Subscription thread stopped and resources cleaned up");
        }

        private void CrestronEnvironment_ProgramStatusEventHandler(eProgramStatusEventType programEventType)
        {
            if (programEventType != eProgramStatusEventType.Stopping) return;

            // Stop subscription thread
            StopSubscriptionThread();

            if (watchDogTimer != null)
            {
                watchDogTimer.Stop();
                watchDogTimer.Dispose();
            }

            watchDogTimeoutTimer?.Dispose();
            if (CommunicationMonitor != null)
            {
                CommunicationMonitor.Stop();
                Communication.Disconnect();
            }
        }

        private void CreateDspObjects()
        {
            this.LogVerbose("Creating DSP Objects");

            var props = JsonConvert.DeserializeObject<TesiraDspPropertiesConfig>(dc.Properties.ToString());

            ResubscriptionString = !string.IsNullOrEmpty(props.ResubscribeString)
                ? props.ResubscribeString
                : "resubscribeAll";

            // Lock the entire control point list creation process
            lock (watchdogLock)
            {
                Faders.Clear();
                Presets.Clear();
                Dialers.Clear();
                States.Clear();
                Switchers.Clear();
                ControlPointList.Clear();
                Meters.Clear();
                LogicMeters.Clear();
                RoomCombiners.Clear();

                CreateFaders(props);

                CreateSwitchers(props);

                CreateRouters(props);

                CreateSourceSelectors(props);

                CreateDialers(props);

                CreateStates(props);

                CreatePresets(props);

                CreateMeters(props);

                CreateLogicMeters(props);

                CreateCrosspoints(props);

                CreateRoomCombiners(props);
            }

            CreateDevInfo();

            //Keep me at the end of this method!
            CreateExpanderTracker(props);

            ControlsAdded = true;
            CheckSerialSendStatus();

        }

        private void CreateDevInfo()
        {
            DevInfo = new TesiraDspDeviceInfo(this);
            if (DevInfo != null)
                DeviceManager.AddDevice(DevInfo);
        }

        private void CreatePresets(TesiraDspPropertiesConfig props)
        {
            if (props.Presets == null) return;
            foreach (var preset in props.Presets)
            {
                var value = preset.Value;
                var tesiraPreset = new TesiraPreset(preset.Key)
                {
                    Label = value.Label,
                    PresetIndex = value.PresetIndex,
                    PresetName = value.PresetName,
                    PresetId = value.PresetId
                };
                Presets.Add(preset.Key, tesiraPreset);
                TesiraPresets.Add(tesiraPreset);
                this.LogVerbose("Added Preset {0} {1}", value.Label, value.PresetName);
            }

            var presetDevice = new TesiraDspPresetDevice(this);
            DeviceManager.AddDevice(presetDevice);
        }

        private void CreateFaders(TesiraDspPropertiesConfig props)
        {
            if (props.FaderControlBlocks == null) return;
            this.LogVerbose("faderControlBlocks is not null - There are {0} of them",
                props.FaderControlBlocks.Count());
            foreach (var block in props.FaderControlBlocks)
            {
                var key = block.Key;
                this.LogVerbose("faderControlBlock Key - {0}", key);
                var value = block.Value;

                Faders.Add(key, new TesiraDspFaderControl(key, value, this));
                this.LogVerbose("Added faderControlPoint {0} levelTag: {1} muteTag: {2}", key, value.LevelInstanceTag,
                    value.MuteInstanceTag);
                if (block.Value.Enabled)
                {
                    //Add ControlPoint to the list for the watchdog
                    ControlPointList.Add(Faders[key]);
                }
                DeviceManager.AddDevice(Faders[key]);
            }
        }

        private void CreateRoomCombiners(TesiraDspPropertiesConfig props)
        {
            if (props.RoomCombinerControlBlocks == null) return;
            foreach (var roomCombiner in props.RoomCombinerControlBlocks)
            {
                var key = roomCombiner.Key;
                var value = roomCombiner.Value;
                RoomCombiners.Add(key, new TesiraDspRoomCombiner(key, value, this));
                this.LogVerbose("Adding Mixer {0} InstanceTag: {1}", key, value.RoomCombinerInstanceTag);

                if (value.Enabled)
                {
                    ControlPointList.Add(RoomCombiners[key]);
                }
                DeviceManager.AddDevice(RoomCombiners[key]);

            }
        }

        private void CreateCrosspoints(TesiraDspPropertiesConfig props)
        {
            if (props.CrosspointStateControlBlocks == null) return;
            foreach (var mixer in props.CrosspointStateControlBlocks)
            {
                var key = mixer.Key;
                var value = mixer.Value;
                CrosspointStates.Add(key, new TesiraDspCrosspointState(key, value, this));
                this.LogVerbose("Adding CrosspointState {0} InstanceTag: {1}", key, value.MatrixInstanceTag);

                if (value.Enabled)
                {
                    ControlPointList.Add(CrosspointStates[key]);
                }
                DeviceManager.AddDevice(CrosspointStates[key]);
            }
        }

        private void CreateMeters(TesiraDspPropertiesConfig props)
        {
            if (props.MeterControlBlocks == null) return;
            foreach (var meter in props.MeterControlBlocks)
            {
                var key = meter.Key;
                var value = meter.Value;
                Meters.Add(key, new TesiraDspMeter(key, value, this));
                this.LogVerbose("Adding Meter {0} InstanceTag: {1}", key, value.MeterInstanceTag);

                if (value.Enabled)
                {
                    ControlPointList.Add(Meters[key]);
                }
                DeviceManager.AddDevice(Meters[key]);

            }
        }

        private void CreateLogicMeters(TesiraDspPropertiesConfig props)
        {
            try
            {
                if (props.LogicMeterControlBlocks == null) return;

                foreach (var meter in props.LogicMeterControlBlocks)
                {
                    try
                    {
                        var key = meter.Key;
                        var value = meter.Value;
                        LogicMeters.Add(key, new TesiraDspLogicMeter(key, value, this));
                        this.LogVerbose("Adding Logic Meter {0} InstanceTag: {1}", key, value.MeterInstanceTag);

                        if (value.Enabled)
                        {
                            ControlPointList.Add(LogicMeters[key]);
                        }
                        DeviceManager.AddDevice(LogicMeters[key]);
                    }
                    catch (Exception ex)
                    {
                        this.LogError("Exception adding Logic meter {key}: {message}", meter.Key, ex.Message);
                        this.LogVerbose(ex, "Stack trace:");
                    }
                }
            }
            catch (Exception ex)
            {
                this.LogError("Exception adding Logic meters: {message}", ex.Message);
                this.LogVerbose(ex, "Stack trace:");
            }
        }

        private void CreateStates(TesiraDspPropertiesConfig props)
        {
            if (props.StateControlBlocks == null) return;
            this.LogVerbose("stateControlBlocks is not null - There are {0} of them",
                props.StateControlBlocks.Count());
            foreach (var block in props.StateControlBlocks)
            {
                var key = block.Key;
                var value = block.Value;
                States.Add(key, new TesiraDspStateControl(key, value, this));
                this.LogVerbose("Added DspState {0} InstanceTag: {1}", key, value.StateInstanceTag);

                if (block.Value.Enabled)
                    ControlPointList.Add(States[key]);
                DeviceManager.AddDevice(States[key]);

            }
        }

        private void CreateDialers(TesiraDspPropertiesConfig props)
        {
            if (props.DialerControlBlocks == null) return;
            this.LogVerbose("DialerControlBlocks is not null - There are {0} of them",
                props.DialerControlBlocks.Count());
            foreach (var block in props.DialerControlBlocks)
            {
                var key = block.Key;
                this.LogVerbose("DialerControlBlock Key - {0}", key);
                var value = block.Value;
                Dialers.Add(key, new TesiraDspDialer(key, value, this));
                this.LogVerbose("Added DspDialer {0} ControlStatusTag: {1} DialerTag: {2}", key,
                    value.ControlStatusInstanceTag, value.DialerInstanceTag);

                if (block.Value.Enabled)
                {
                    ControlPointList.Add(Dialers[key]);
                }
                DeviceManager.AddDevice(Dialers[key]);

            }
        }

        private void CreateSwitchers(TesiraDspPropertiesConfig props)
        {
            if (props.SwitcherControlBlocks == null) return;
            this.LogVerbose("switcherControlBlocks is not null - There are {0} of them",
                props.SwitcherControlBlocks.Count());
            foreach (var block in props.SwitcherControlBlocks)
            {
                var key = block.Key;
                this.LogVerbose("SwitcherControlBlock Key - {0}", key);
                var value = block.Value;

                Switchers.Add(key, new TesiraDspSwitcher(key, value, this));
                this.LogVerbose("Added TesiraSwitcher {0} InstanceTag {1}", key, value.SwitcherInstanceTag);

                if (block.Value.Enabled && block.Value.Type != "router") //if you don't do this check, you'll add devices that are unable to be subscribed into the watchdog
                {
                    //Add ControlPoint to the list for the watchdog
                    ControlPointList.Add(Switchers[key]);
                }
                DeviceManager.AddDevice(Switchers[key]);

            }
        }
        private void CreateSourceSelectors(TesiraDspPropertiesConfig props)
        {
            if (props.SourceSelectorControlBlocks == null) return;
            this.LogVerbose("sourceSelectorControlBlocks is not null - There are {0} of them",
                props.SourceSelectorControlBlocks.Count());
            foreach (var block in props.SourceSelectorControlBlocks)
            {
                var key = block.Key;
                this.LogVerbose("Source Selector ControlBlock Key - {0}", key);
                var value = block.Value;

                SourceSelectors.Add(key, new TesiraDspSourceSelector(key, value, this));
                this.LogVerbose("Added TesiraSwitcher {0} InstanceTag {1}", key, value.SourceSelectorInstanceTag);

                ControlPointList.Add(SourceSelectors[key]);
                DeviceManager.AddDevice(SourceSelectors[key]);

            }
        }

        private void CreateRouters(TesiraDspPropertiesConfig props)
        {
            if (props.RouterControlBlocks == null) return;
            this.LogVerbose("routerControlBlocks is not null - There are {0} of them",
                props.RouterControlBlocks.Count());
            foreach (var block in props.RouterControlBlocks)
            {
                var key = block.Key;
                this.LogVerbose("RouterControlBlock Key - {0}", key);
                var value = block.Value;

                Routers.Add(key, new TesiraDspRouter(key, value, this));
                this.LogVerbose("Added Router {0} InstanceTag {1}", key, value.RouterInstanceTag);

                DeviceManager.AddDevice(Routers[key]);

            }
        }

        private void CreateExpanderTracker(TesiraDspPropertiesConfig props)
        {
            if (props.ExpanderBlocks == null) return;
            this.LogVerbose("ExpanderBlocks is not null - there are {0} of them", props.ExpanderBlocks.Count());

            ExpanderTracker = new TesiraExpanderTracker(this, props.ExpanderBlocks);

            DeviceManager.AddDevice(ExpanderTracker);

        }

        #region Communications

        private void CommunicationMonitor_StatusChange(object sender, MonitorStatusChangeEventArgs e)
        {
            this.LogVerbose("Communication monitor state: {0}", CommunicationMonitor.Status);
            if (e.Status == MonitorStatus.IsOk)
            {
                //StartSubsciptionThread();
            }
            else if (e.Status != MonitorStatus.IsOk)
            {
                StopWatchDog();
            }
        }

        private void Socket_ConnectionChange(object sender, GenericSocketStatusChageEventArgs e)
        {
            this.LogVerbose("Socket Status Change: {0}", e.Client.ClientStatus.ToString());

            if (e.Client.IsConnected)
            {

                SuspendWatchdog(false);
            }

            if (!e.Client.IsConnected)
            {
                SuspendWatchdog(true);
            }
            else
            {
                // Cleanup items from this session
                CommandQueue.Clear();
            }
        }

        #endregion

        #region Watchdog

        private void SuspendWatchdog(bool data)
        {
            WatchdogSuspend = data;
        }


        private void StartWatchDog()
        {
            // Use 2 minutes (120000ms) interval for random sampling approach
            // Check 5-10 random components each interval to reduce network traffic
            const int watchDogIntervalMs = 120000; // 2 minutes

            if (watchDogTimer == null)
            {
                watchDogTimer = new System.Timers.Timer(watchDogIntervalMs);
                watchDogTimer.Elapsed += (sender, e) => CheckWatchDog();
                watchDogTimer.AutoReset = true;
                watchDogTimer.Start();
            }
            else
            {
                watchDogTimer.Stop();
                watchDogTimer.Interval = watchDogIntervalMs;
                watchDogTimer.Start();
            }
        }

        private void StopWatchDog()
        {
            if (watchDogTimer == null) return;
            watchDogTimer.Stop();
            watchDogTimer.Dispose();
            watchDogTimer = null;

            // Also clean up timeout timer
            watchDogTimeoutTimer?.Dispose();
            watchDogTimeoutTimer = null;

            // Reset watchdog state
            lock (watchdogLock)
            {
                WatchDogSniffer = false;
                watchDogExpectedResponses = 0;
                watchDogReceivedResponses = 0;
                recentlyCheckedComponents.Clear();
            }
        }


        private void CheckWatchDog()
        {
            try
            {
                // Create a snapshot of the ControlPointList to avoid collection modification issues
                List<ISubscribedComponent> controlPointSnapshot;
                lock (watchdogLock)
                {
                    if (ControlPointList.Count == 0) return;
                    controlPointSnapshot = new List<ISubscribedComponent>(ControlPointList);
                }

                // Safety check - make sure subscription process is actually complete
                var subscribedCount = controlPointSnapshot.Count(c => c.IsSubscribed);
                if (subscribedCount == 0)
                {
                    this.LogDebug("Watchdog postponed - no components are subscribed yet.");
                    return;
                }

                if (WatchdogSuspend)
                {
                    WatchDogSniffer = false;
                    return;
                }

                if (WatchDogSniffer)
                {
                    this.LogDebug("Resubscribing all control points.");
                    Resubscribe();
                    return;
                }

                // Get subscribed components that haven't been checked recently
                var subscribedComponents = controlPointSnapshot.Where(c => c.IsSubscribed).ToList();
                var availableToCheck = subscribedComponents.Where(c => !recentlyCheckedComponents.Contains(c)).ToList();

                // If no unchecked components available, clear the recently checked list and use all subscribed
                if (availableToCheck.Count == 0 && subscribedComponents.Count > 0)
                {
                    this.LogDebug("All components recently checked, clearing recent check history.");
                    lock (watchdogLock)
                    {
                        recentlyCheckedComponents.Clear();
                    }
                    availableToCheck = subscribedComponents;
                }

                if (availableToCheck.Count == 0)
                {
                    this.LogDebug("No subscribed components to check.");
                    return;
                }

                // Select 5-10 random components to check (scale with total component count)
                int componentsToCheck = Math.Min(Math.Max(5, subscribedComponents.Count / 20), Math.Min(10, availableToCheck.Count));
                var random = new Random();
                var componentsToCheckList = availableToCheck.OrderBy(x => random.Next()).Take(componentsToCheck).ToList();

                this.LogDebug("Watchdog checking {count} random components out of {total} subscribed.",
                    componentsToCheckList.Count, subscribedComponents.Count);

                // Initialize watchdog tracking
                lock (watchdogLock)
                {
                    WatchDogSniffer = true;
                    watchDogExpectedResponses = componentsToCheckList.Count;
                    watchDogReceivedResponses = 0;

                    // Add checked components to recently checked list
                    foreach (var component in componentsToCheckList)
                    {
                        recentlyCheckedComponents.Add(component);
                    }
                }

                // Set up timeout timer (5 seconds should be plenty for random sample)
                watchDogTimeoutTimer?.Dispose();
                watchDogTimeoutTimer = CreateOneShotTimer(HandleWatchDogTimeout, 5000);

                // Subscribe to selected components to verify their subscription status
                foreach (var component in componentsToCheckList)
                {
                    component.Subscribe();
                }

            }
            catch (Exception ex)
            {
                this.LogError("Watchdog Error: {message}", ex.Message);
                this.LogDebug(ex, "Stack Trace: ");
            }
        }

        private void HandleWatchDogTimeout()
        {
            lock (watchdogLock)
            {
                if (WatchDogSniffer)
                {
                    this.LogWarning("Watchdog timeout - only received {received}/{expected} responses. Triggering resubscribe.",
                        watchDogReceivedResponses, watchDogExpectedResponses);

                    // Clear watchdog state and trigger resubscribe
                    WatchDogSniffer = false;
                    watchDogExpectedResponses = 0;
                    watchDogReceivedResponses = 0;

                    // Trigger resubscribe on next watchdog cycle
                    this.LogDebug("Scheduling resubscribe due to watchdog timeout.");
                    Resubscribe();
                }
            }
        }

        /// <summary>
        /// Creates a one-shot timer that executes after the specified delay
        /// </summary>
        /// <param name="action">Action to execute</param>
        /// <param name="delayMs">Delay in milliseconds</param>
        /// <returns>Timer instance</returns>
        private System.Timers.Timer CreateOneShotTimer(Action action, double delayMs)
        {
            var timer = new System.Timers.Timer(delayMs)
            {
                AutoReset = false
            };
            timer.Elapsed += (sender, e) =>
                  {
                      action();
                      timer.Dispose();
                  };
            timer.Start();
            return timer;
        }

        /// <summary>
        /// Creates a repeating timer that executes at the specified interval
        /// </summary>
        /// <param name="action">Action to execute</param>
        /// <param name="intervalMs">Interval in milliseconds</param>
        /// <returns>Timer instance</returns>
        private System.Timers.Timer CreateRepeatingTimer(Action action, double intervalMs)
        {
            var timer = new System.Timers.Timer(intervalMs)
            {
                AutoReset = true
            };
            timer.Elapsed += (sender, e) => action();
            timer.Start();
            return timer;
        }

        #endregion

        #region String Handling

        /// <summary>
        /// Sends a command to the DSP (with delimiter appended)
        /// </summary>
        /// <param name="s">Command to send</param>
        public void SendLine(string s, bool bypassTxQueue = false)
        {
            if (string.IsNullOrEmpty(s))
                return;

            SendLineRaw($"{s}\r\n", bypassTxQueue);
        }

        /// <summary>
        /// Sends a command to the DSP (without delimiter appended)
        /// </summary>
        /// <param name="s">Command to send</param>
        public void SendLineRaw(string s, bool bypassTxQueue = false)
        {
            if (string.IsNullOrEmpty(s))
                return;

            var message = new ProcessStringMessage(s, Communication.SendText);

            if (bypassTxQueue)
            {
                Communication.SendText(s);
                return;
            }

            transmitQueue.Enqueue(message);
        }

        private const string subscriptionPattern = "! [\\\"](.*?[^\\\\])[\\\"] (.*)";
        private readonly static Regex subscriptionRegex = new Regex(subscriptionPattern);

        public event DeviceInfoChangeHandler DeviceInfoChanged;

        private void Port_LineReceived(object dev, GenericCommMethodReceiveTextArgs args)
        {
            if (args == null) return;

            if (string.IsNullOrEmpty(args.Text)) return;

            try
            {
                DeviceRx = args.Text;

                CommandPassthruFeedback.FireUpdate();

                if (args.Text.Length == 0) return;

                if (args.Text.Contains("Welcome"))
                {
                    // Indicates a new TTP session
                    if (!isSerialComm)
                    {
                        CommunicationMonitor.Start();
                    }
                    StartSubscriptionThread();
                    return;
                }
                if (args.Text.Equals(ResubscriptionString, StringComparison.OrdinalIgnoreCase))
                {
                    CommandQueue.Clear();
                    Resubscribe();
                    return;
                }

                // Subscription Message
                if (args.Text.IndexOf("! ", StringComparison.Ordinal) >= 0)
                {

                    var match = subscriptionRegex.Match(args.Text);

                    if (!match.Success) return;

                    var customName = match.Groups[1].Value;
                    var value = match.Groups[2].Value;
                    this.LogVerbose("Subscription Message: 'Name: {0} Value:{1}'", customName, value);

                    // Create a snapshot to avoid collection modification issues
                    List<ISubscribedComponent> controlPointSnapshot;
                    lock (watchdogLock)
                    {
                        controlPointSnapshot = new List<ISubscribedComponent>(ControlPointList);
                    }

                    foreach (var component in from component in controlPointSnapshot let item = component from n in item.CustomNames.Where(n => n == customName) select component)
                    {
                        if (component == null)
                        {
                            this.LogDebug("Unable to find matching Custom Name {0}", customName);
                            return;
                        }
                        component.ParseSubscriptionMessage(customName, value);
                    }
                    return;
                }

                if (args.Text.IndexOf("+OK", StringComparison.Ordinal) == 0)
                {
                    if (InitialStart)
                    {
                        CheckSerialSendStatus();
                    }

                    CommandQueue.HandleResponse(args.Text);
                    return;
                }

                if (args.Text.IndexOf("DEVICE recallPresetByName", StringComparison.Ordinal) == 0)
                {
                    CommandQueue.HandleResponse(args.Text);
                    return;
                }

                if (args.Text.IndexOf("-ERR", StringComparison.Ordinal) >= 0)
                {
                    // Error response
                    if (args.Text.IndexOf("ALREADY_SUBSCRIBED", StringComparison.Ordinal) >= 0)
                    {
                        if (WatchDogSniffer)
                        {
                            lock (watchdogLock)
                            {
                                watchDogReceivedResponses++;
                                this.LogVerbose("Watchdog response {received}/{expected} - Component already subscribed.",
                                    watchDogReceivedResponses, watchDogExpectedResponses);

                                CommandQueue.HandleResponse(args.Text);

                                // Clear sniffer flag when all responses received
                                if (watchDogReceivedResponses >= watchDogExpectedResponses)
                                {
                                    this.LogDebug("All watchdog responses received. Subscriptions verified.");
                                    WatchDogSniffer = false;
                                    watchDogExpectedResponses = 0;
                                    watchDogReceivedResponses = 0;

                                    // Cancel timeout timer since we received all responses
                                    watchDogTimeoutTimer?.Dispose();
                                    watchDogTimeoutTimer = null;
                                }
                            }
                        }
                    }
                    else
                    {
                        this.LogDebug("Error From DSP: '{0}'", args.Text);

                        // Any other error during watchdog check means we need to resubscribe
                        if (WatchDogSniffer)
                        {
                            lock (watchdogLock)
                            {
                                WatchDogSniffer = false;
                                watchDogExpectedResponses = 0;
                                watchDogReceivedResponses = 0;
                            }
                        }

                        CommandQueue.HandleResponse(args.Text);
                    }

                    return;
                }
            }
            catch (Exception e)
            {
                this.LogError("Exception handling response: {response}: {exception}", args.Text, e.Message);
                this.LogDebug(e, "Stack trace: ");
            }
        }

        #endregion


        #region Presets

        public void RunPresetNumber(ushort n)
        {
            this.LogVerbose("Attempting to run preset {0}", n);

            foreach (var preset in Presets.OfType<TesiraPreset>().Where(preset => preset.Index == n))
            {
                this.LogVerbose("Found a matching Preset - {0}", preset.PresetId);
                RecallPreset(preset.Key);
            }

        }

        /// <summary>
        /// Sends a command to execute a preset
        /// </summary>
        /// <param name="name">Preset Name</param>
        public void RunPreset(string name)
        {
            this.LogVerbose("Running Preset By Name - {0}", name);
            CommandQueue.EnqueueCommand(string.Format("DEVICE recallPresetByName \"{0}\"", name));
        }

        /// <summary>
        /// Sends a command to execute a preset
        /// </summary>
        /// <param name="id">Preset id</param>
        public void RunPreset(int id)
        {
            this.LogVerbose("Running Preset By ID - {0}", id);
            CommandQueue.EnqueueCommand(string.Format("DEVICE recallPreset {0}", id));
        }

        public void RecallPreset(string key)
        {
            var preset = Presets[key] as TesiraPreset;
            this.LogVerbose("Running preset {0}", preset.Name);
            if (preset == null) return;

            this.LogVerbose("Checking Preset {0} | presetIndex {1} | presetId {2} | presetName {3}",
                preset.Name, preset.PresetIndex, preset.PresetId, preset.PresetName);
            // - changed string check reference from 'tesiraPreset.PresetName' to 'tesiraPreset.PreetData.PresetName'
            if (!string.IsNullOrEmpty(preset.PresetName))
            {
                RunPreset(preset.PresetName);
            }
            else
            {
                if (preset.PresetId == 0)
                {
                    this.LogVerbose("Preset {0} has an invalid presetId {1}", preset.Name, preset.PresetId);
                    return;
                }
                RunPreset(preset.PresetId);
            }
        }

        #endregion

        #region SubscriptionHandling        

        #region Unsubscribe


        private void UnsubscribeFromComponents(CancellationToken token = default)
        {
            if (token.IsCancellationRequested)
            {
                this.LogDebug("UnsubscribeFromComponents cancelled");
                return;
            }

            UnsubscribeFromComponent(0, token);
        }

        private void UnsubscribeFromComponent(int index, CancellationToken token = default)
        {
            if (token.IsCancellationRequested)
            {
                this.LogDebug("UnsubscribeFromComponent cancelled at index {0}", index);
                return;
            }

            // Create a snapshot to avoid collection modification issues
            ISubscribedComponent controlPoint;
            int controlPointCount;
            lock (watchdogLock)
            {
                if (index >= ControlPointList.Count)
                {
                    this.LogDebug("Index {0} is out of range for ControlPointList", index);
                    return;
                }
                controlPoint = ControlPointList[index];
                controlPointCount = ControlPointList.Count;
            }

            if (controlPoint != null) UnsubscribeFromComponent(controlPoint);

            var newIndex = index + 1;

            this.LogVerbose("NewIndex == {0} and ControlPointListCount == {1}", newIndex, controlPointCount);

            if (newIndex < controlPointCount)
            {
                unsubscribeTimer = CreateOneShotTimer(() => UnsubscribeFromComponent(newIndex, token), 250);
            }
            else
            {
                this.LogDebug("Subscribe To Components");
                unsubscribeTimer?.Dispose();
                SubscribeToComponents(token);
            }
        }

        private void UnsubscribeFromComponent(ISubscribedComponent data)
        {
            if (!data.Enabled) return;
            this.LogVerbose("Unsubscribing From Object - {0}", data.InstanceTag1);
            data.Unsubscribe();
        }

        #endregion

        #region Subscribe

        private void SubscribeToComponents(CancellationToken token = default)
        {
            if (token.IsCancellationRequested)
            {
                this.LogDebug("SubscribeToComponents cancelled");
                return;
            }

            this.LogDebug("Subscribing to Components - using queue approach");

            unsubscribeTimer?.Dispose();
            initalSubscription = false;
            if (DevInfo == null)
            {
                return;
            }

            this.LogVerbose("DevInfo Not Null - queuing all sync commands");

            // Queue device info request
            DevInfo.GetDeviceInfo();

            // Queue expander initialization if available
            ExpanderTracker?.Initialize();

            // Queue all min level requests
            var volumeComponents = ControlPointList.OfType<IVolumeComponent>().ToList();
            this.LogDebug("Queuing min/max level requests for {count} volume components", volumeComponents.Count);

            foreach (var component in volumeComponents)
            {
                component.GetMinLevel();
            }

            // Queue all max level requests  
            foreach (var component in volumeComponents)
            {
                component.GetMaxLevel();
            }

            // Queue all component subscriptions
            var subscribableComponents = ControlPointList.Where(c => c.Enabled).ToList();
            this.LogDebug("Queuing subscriptions for {count} components", subscribableComponents.Count);

            foreach (var component in subscribableComponents)
            {
                component.Subscribe();
            }

            // Start monitoring for queue completion
            StartQueueCompletionMonitoring();
        }

        private void StartQueueCompletionMonitoring()
        {
            this.LogDebug("Starting queue completion monitoring");

            // Use existing queueCheckTimer but simplify the logic
            if (queueCheckTimer != null)
            {
                queueCheckTimer.Stop();
                queueCheckTimer.Dispose();
            }

            queueCheckTimer = CreateRepeatingTimer(() => CheckQueueCompletion(), 1000);
        }

        private void CheckQueueCompletion()
        {
            this.LogVerbose("Queue check - LocalQueue: {count} items, InProgress: {inProgress}",
                CommandQueue.LocalQueue.Count, CommandQueue.CommandQueueInProgress);

            if (!CommandQueue.LocalQueue.Any() && !CommandQueue.CommandQueueInProgress)
            {
                this.LogDebug("All queue commands completed, finishing subscription process");

                queueCheckTimer?.Stop();
                queueCheckTimer?.Dispose();
                queueCheckTimer = null;

                // Do final polling and start watchdog
                EndSubscriptionProcess();
            }
        }


        private void EndSubscriptionProcess()
        {
            foreach (var control in Switchers.Select(switcher => switcher.Value).Where(control => control.SelectorCustomName == string.Empty))
            {
                control.DoPoll();
            }
            foreach (var control in Routers.Select(router => router.Value))
            {
                control.DoPoll();
            }

            // Start watchdog only after all subscriptions are complete
            this.LogDebug("All subscriptions complete. Starting watchdog.");
            StartWatchDog();
        }

        #endregion

        /// <summary>
        /// Resubscribe to all controls
        /// </summary>
        public void Resubscribe()
        {
            this.LogInformation("Issue Detected with device subscriptions - resubscribing to all controls");
            StopWatchDog();
            StartSubscriptionThread();
        }

        private object HandleAttributeSubscriptions(CancellationToken token)
        {
            this.LogDebug("HandleAttributeSubscriptions - LIVE");

            try
            {
                // Check for cancellation before starting
                token.ThrowIfCancellationRequested();

                if (Communication.IsConnected)
                {
                    // Check for cancellation before sending commands
                    token.ThrowIfCancellationRequested();

                    SendLine("SESSION set verbose false");

                    // Add delay with cancellation support
                    if (token.WaitHandle.WaitOne(250))
                    {
                        this.LogDebug("Subscription thread cancelled during initial delay");
                        return null;
                    }

                    if (isSerialComm && initalSubscription)
                    {
                        token.ThrowIfCancellationRequested();
                        initalSubscription = false;
                        UnsubscribeFromComponents(token);
                    }
                    else
                    {
                        token.ThrowIfCancellationRequested();
                        //Subscribe
                        SubscribeToComponents(token);
                    }
                }

                // Check for cancellation before starting watchdog
                token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                this.LogDebug("HandleAttributeSubscriptions cancelled");
                return null;
            }
            catch (Exception ex)
            {
                this.LogError(ex, "Error in HandleAttributeSubscriptions");
            }

            return null;
        }

        #endregion

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {

            var deviceJoinMap = new TesiraDspDeviceJoinMapAdvanced(joinStart);
            var dialerJoinMap = new TesiraDialerJoinMapAdvanced(joinStart);
            var faderJoinMap = new TesiraFaderJoinMapAdvanced(joinStart);
            var stateJoinMap = new TesiraStateJoinMapAdvanced(joinStart);
            var switcherJoinMap = new TesiraSwitcherJoinMapAdvanced(joinStart);
            var presetJoinMap = new TesiraPresetJoinMapAdvanced(joinStart);
            var meterJoinMap = new TesiraMeterJoinMapAdvanced(joinStart);
            var crosspointStateJoinMap = new TesiraCrosspointStateJoinMapAdvanced(joinStart);
            var roomCombinerJoinMap = new TesiraRoomCombinerJoinMapAdvanced(joinStart);

            if (bridge != null)
            {
                bridge.AddJoinMap(string.Format("{0}--DeviceInfoJoinMap", Key), deviceJoinMap);
                bridge.AddJoinMap(string.Format("{0}--DialerJoinMap", Key), dialerJoinMap);
                bridge.AddJoinMap(string.Format("{0}--FaderJoinMap", Key), faderJoinMap);
                bridge.AddJoinMap(string.Format("{0}--StateJoinMap", Key), stateJoinMap);
                bridge.AddJoinMap(string.Format("{0}--SwitcherJoinMap", Key), switcherJoinMap);
                bridge.AddJoinMap(string.Format("{0}--PresetsJoinMap", Key), presetJoinMap);
                bridge.AddJoinMap(string.Format("{0}--MeterJoinMap", Key), meterJoinMap);
                bridge.AddJoinMap(string.Format("{0}--CrosspointStateJoinMap", Key), crosspointStateJoinMap);
                bridge.AddJoinMap(string.Format("{0}--RoomCombinerJoinMap", Key), roomCombinerJoinMap);
            }

            this.LogDebug("Linking to Trilist '{ipId:X}'", trilist.ID);


            CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[deviceJoinMap.IsOnline.JoinNumber]);
            CommandPassthruFeedback.LinkInputSig(trilist.StringInput[deviceJoinMap.CommandPassThru.JoinNumber]);
            trilist.SetStringSigAction(presetJoinMap.PresetName.JoinNumber, RunPreset);

            trilist.SetStringSigAction(deviceJoinMap.CommandPassThru.JoinNumber, (s) => SendLineRaw(s));

            trilist.SetSigTrueAction(deviceJoinMap.Resubscribe.JoinNumber, Resubscribe);

            LinkFadersToApi(trilist, faderJoinMap);

            LinkStatesToApi(trilist, stateJoinMap);

            LinkLegacySwitchersToApi(trilist, switcherJoinMap);

            LinkSwitchersToApi(trilist, switcherJoinMap);

            LinkSourceSelectorsToApi(trilist, switcherJoinMap);

            LinkPresetsToApi(trilist, presetJoinMap);

            LinkDialersToApi(trilist, dialerJoinMap);

            LinkMetersToApi(trilist, meterJoinMap);

            LinkMatricesToApi(trilist, crosspointStateJoinMap);

            LinkRoomCombinersToApi(trilist, roomCombinerJoinMap);

            trilist.OnlineStatusChange += (d, args) =>
            {
                if (!args.DeviceOnLine) return;

                foreach (var feedback in Feedbacks)
                {
                    feedback.FireUpdate();
                }

            };
        }

        private void LinkRoomCombinersToApi(BasicTriList trilist, TesiraRoomCombinerJoinMapAdvanced roomCombinerJoinMap)
        {
            this.LogVerbose("There are {0} Room Combiner Control Points", RoomCombiners.Count);
            //x = 0;
            foreach (var item in RoomCombiners)
            {
                var roomCombiner = item.Value;
                var data = roomCombiner.BridgeIndex;
                if (data == null) continue;
                var y = (uint)data;

                var x = y > 1 ? ((y - 1) * 6) : 0;

                this.LogVerbose("Tesira Room Combiner {0} connect", x);

                var genericChannel = roomCombiner as IBasicVolumeWithFeedback;
                if (!roomCombiner.Enabled) continue;

                this.LogVerbose("TesiraChannel {0} Is Enabled", x);

                roomCombiner.NameFeedback.LinkInputSig(trilist.StringInput[roomCombinerJoinMap.Label.JoinNumber + x]);
                roomCombiner.VisibleFeedback.LinkInputSig(trilist.BooleanInput[roomCombinerJoinMap.Visible.JoinNumber + x]);
                roomCombiner.TypeFeedback.LinkInputSig(trilist.UShortInput[roomCombinerJoinMap.Type.JoinNumber + x]);
                roomCombiner.PermissionsFeedback.LinkInputSig(trilist.UShortInput[roomCombinerJoinMap.Permissions.JoinNumber + x]);
                roomCombiner.RoomGroupFeedback.LinkInputSig(trilist.UShortInput[roomCombinerJoinMap.Group.JoinNumber + x]);

                genericChannel.MuteFeedback.LinkInputSig(trilist.BooleanInput[roomCombinerJoinMap.MuteToggle.JoinNumber + x]);
                genericChannel.MuteFeedback.LinkInputSig(trilist.BooleanInput[roomCombinerJoinMap.MuteOn.JoinNumber + x]);
                genericChannel.MuteFeedback.LinkComplementInputSig(trilist.BooleanInput[roomCombinerJoinMap.MuteOff.JoinNumber + x]);
                genericChannel.VolumeLevelFeedback.LinkInputSig(trilist.UShortInput[roomCombinerJoinMap.Volume.JoinNumber + x]);

                trilist.SetSigTrueAction(roomCombinerJoinMap.MuteToggle.JoinNumber + x, genericChannel.MuteToggle);
                trilist.SetSigTrueAction(roomCombinerJoinMap.MuteOn.JoinNumber + x, genericChannel.MuteOn);
                trilist.SetSigTrueAction(roomCombinerJoinMap.MuteOff.JoinNumber + x, genericChannel.MuteOff);

                trilist.SetBoolSigAction(roomCombinerJoinMap.VolumeUp.JoinNumber + x, genericChannel.VolumeUp);
                trilist.SetBoolSigAction(roomCombinerJoinMap.VolumeDown.JoinNumber + x, genericChannel.VolumeDown);

                trilist.SetUShortSigAction(roomCombinerJoinMap.Volume.JoinNumber + x, u => { if (u > 0) { genericChannel.SetVolume(u); } });

                trilist.SetUShortSigAction(roomCombinerJoinMap.Group.JoinNumber + x, u => { if (u > 0) { roomCombiner.SetRoomGroup(u); } });
            }
        }

        private void LinkMatricesToApi(BasicTriList trilist, TesiraCrosspointStateJoinMapAdvanced crosspointStateJoinMap)
        {
            this.LogVerbose("There are {0} Crosspoint State Control Points", CrosspointStates.Count);
            foreach (var item in CrosspointStates)
            {
                var xpointState = item.Value;
                var joinOffset = (xpointState.BridgeIndex - 1) * 3;
                if (joinOffset == null) continue;


                var channel = item.Value;
                var data = channel.BridgeIndex;
                if (data == null) continue;


                this.LogVerbose("Adding Crosspoint State ControlPoint {0} | JoinStart:{1}", xpointState.Key, crosspointStateJoinMap.Toggle.JoinNumber + joinOffset);
                xpointState.CrosspointStateFeedback.LinkInputSig(trilist.BooleanInput[(uint)(crosspointStateJoinMap.Toggle.JoinNumber + joinOffset)]);
                xpointState.CrosspointStateFeedback.LinkInputSig(trilist.BooleanInput[(uint)(crosspointStateJoinMap.On.JoinNumber + joinOffset)]);

                trilist.SetSigTrueAction((uint)(crosspointStateJoinMap.Toggle.JoinNumber + joinOffset), xpointState.StateToggle);
                trilist.SetSigTrueAction((uint)(crosspointStateJoinMap.On.JoinNumber + joinOffset), xpointState.StateOn);
                trilist.SetSigTrueAction((uint)(crosspointStateJoinMap.Off.JoinNumber + joinOffset), xpointState.StateOff);


            }
        }

        private void LinkMetersToApi(BasicTriList trilist, TesiraMeterJoinMapAdvanced meterJoinMap)
        {
            this.LogVerbose("There are {0} Meter Control Points", Meters.Count);
            foreach (var item in Meters)
            {
                var meter = item.Value;
                var data = meter.BridgeIndex;
                if (data == null) continue;
                var x = (uint)(data - 1);


                this.LogVerbose("AddingMeterBridge {0} | Join:{1}", meter.Key, meterJoinMap.Label.JoinNumber);

                meter.MeterFeedback.LinkInputSig(trilist.UShortInput[meterJoinMap.Meter.JoinNumber + x]);
                meter.NameFeedback.LinkInputSig(trilist.StringInput[meterJoinMap.Label.JoinNumber + x]);
                meter.SubscribedFeedback.LinkInputSig(trilist.BooleanInput[meterJoinMap.Subscribe.JoinNumber + x]);

                trilist.SetSigTrueAction(meterJoinMap.Subscribe.JoinNumber, meter.Subscribe);
                trilist.SetSigFalseAction(meterJoinMap.Subscribe.JoinNumber, meter.UnSubscribe);

            }
        }

        private void LinkDialersToApi(BasicTriList trilist, TesiraDialerJoinMapAdvanced dialerJoinMap)
        {
            uint lineOffset = 0;
            foreach (var line in Dialers)
            {
                var dialer = line.Value;
                var bridgeIndex = dialer.BridgeIndex;
                if (bridgeIndex == null)
                {
                    this.LogVerbose("BridgeIndex is missing for Dialer {0}", dialer.Key);
                    continue;
                }

                var dialerLineOffset = lineOffset;
                this.LogVerbose("AddingDialerBridge {0} {1} Offset", dialer.Key, dialerLineOffset);
                for (var i = 0; i < dialerJoinMap.KeyPadNumeric.JoinSpan; i++)
                {
                    var tempi = i;
                    trilist.SetSigTrueAction(dialerJoinMap.KeyPadNumeric.JoinNumber + (uint)i + dialerLineOffset, () => dialer.SendKeypad((TesiraDspDialer.EKeypadKeys)tempi));
                }

                trilist.SetSigTrueAction(dialerJoinMap.KeyPadStar.JoinNumber + dialerLineOffset, () => dialer.SendKeypad(TesiraDspDialer.EKeypadKeys.Star));
                trilist.SetSigTrueAction(dialerJoinMap.KeyPadPound.JoinNumber + dialerLineOffset, () => dialer.SendKeypad(TesiraDspDialer.EKeypadKeys.Pound));
                trilist.SetSigTrueAction(dialerJoinMap.KeyPadClear.JoinNumber + dialerLineOffset, () => dialer.SendKeypad(TesiraDspDialer.EKeypadKeys.Clear));
                trilist.SetSigTrueAction(dialerJoinMap.KeyPadBackspace.JoinNumber + dialerLineOffset, () => dialer.SendKeypad(TesiraDspDialer.EKeypadKeys.Backspace));

                trilist.SetSigTrueAction(dialerJoinMap.KeyPadDial.JoinNumber + dialerLineOffset, dialer.Dial);
                trilist.SetSigTrueAction(dialerJoinMap.DoNotDisturbToggle.JoinNumber + dialerLineOffset, dialer.DoNotDisturbToggle);
                trilist.SetSigTrueAction(dialerJoinMap.DoNotDisturbOn.JoinNumber + dialerLineOffset, dialer.DoNotDisturbOn);
                trilist.SetSigTrueAction(dialerJoinMap.DoNotDisturbOff.JoinNumber + dialerLineOffset, dialer.DoNotDisturbOff);
                trilist.SetSigTrueAction(dialerJoinMap.AutoAnswerToggle.JoinNumber + dialerLineOffset, dialer.AutoAnswerToggle);
                trilist.SetSigTrueAction(dialerJoinMap.AutoAnswerOn.JoinNumber + dialerLineOffset, dialer.AutoAnswerOn);
                trilist.SetSigTrueAction(dialerJoinMap.AutoAnswerOff.JoinNumber + dialerLineOffset, dialer.AutoAnswerOff);
                trilist.SetSigTrueAction(dialerJoinMap.Answer.JoinNumber + dialerLineOffset, dialer.Answer);
                trilist.SetSigTrueAction(dialerJoinMap.EndCall.JoinNumber + dialerLineOffset, dialer.OnHook);
                trilist.SetSigTrueAction(dialerJoinMap.OnHook.JoinNumber + dialerLineOffset, dialer.OnHook);
                trilist.SetSigTrueAction(dialerJoinMap.OffHook.JoinNumber + dialerLineOffset, dialer.OffHook);

                trilist.SetSigTrueAction(dialerJoinMap.HoldCall.JoinNumber + dialerLineOffset, dialer.HoldCall);
                trilist.SetSigTrueAction(dialerJoinMap.ResumeCall.JoinNumber + dialerLineOffset, dialer.ResumeCall);
                trilist.SetSigTrueAction(dialerJoinMap.HoldToggle.JoinNumber + dialerLineOffset, dialer.HoldToggle);

                trilist.SetStringSigAction(dialerJoinMap.DialString.JoinNumber + dialerLineOffset, dialer.SetDialString);

                dialer.DoNotDisturbFeedback.LinkInputSig(trilist.BooleanInput[dialerJoinMap.DoNotDisturbToggle.JoinNumber + dialerLineOffset]);
                dialer.DoNotDisturbFeedback.LinkInputSig(trilist.BooleanInput[dialerJoinMap.DoNotDisturbOn.JoinNumber + dialerLineOffset]);
                dialer.DoNotDisturbFeedback.LinkComplementInputSig(trilist.BooleanInput[dialerJoinMap.DoNotDisturbOff.JoinNumber + dialerLineOffset]);

                dialer.OffHookFeedback.LinkInputSig(trilist.BooleanInput[dialerJoinMap.KeyPadDial.JoinNumber + dialerLineOffset]);
                dialer.OffHookFeedback.LinkInputSig(trilist.BooleanInput[dialerJoinMap.OffHook.JoinNumber + dialerLineOffset]);
                dialer.OffHookFeedback.LinkComplementInputSig(trilist.BooleanInput[dialerJoinMap.OnHook.JoinNumber + dialerLineOffset]);
                dialer.IncomingCallFeedback.LinkInputSig(trilist.BooleanInput[dialerJoinMap.IncomingCall.JoinNumber + dialerLineOffset]);

                dialer.AutoAnswerFeedback.LinkInputSig(trilist.BooleanInput[dialerJoinMap.AutoAnswerToggle.JoinNumber + dialerLineOffset]);
                dialer.AutoAnswerFeedback.LinkInputSig(trilist.BooleanInput[dialerJoinMap.AutoAnswerOn.JoinNumber + dialerLineOffset]);
                dialer.AutoAnswerFeedback.LinkComplementInputSig(trilist.BooleanInput[dialerJoinMap.AutoAnswerOff.JoinNumber + dialerLineOffset]);

                dialer.NameFeedback.LinkInputSig(trilist.StringInput[dialerJoinMap.Label.JoinNumber + dialerLineOffset]);
                dialer.DisplayNumberFeedback.LinkInputSig(trilist.StringInput[dialerJoinMap.DisplayNumber.JoinNumber + dialerLineOffset]);

                dialer.DialStringFeedback.LinkInputSig(trilist.StringInput[dialerJoinMap.DialString.JoinNumber + dialerLineOffset]);
                dialer.CallerIdNumberFeedback.LinkInputSig(trilist.StringInput[dialerJoinMap.CallerIdNumberFb.JoinNumber + dialerLineOffset]);
                dialer.CallerIdNameFeedback.LinkInputSig(trilist.StringInput[dialerJoinMap.CallerIdNameFb.JoinNumber + dialerLineOffset]);
                dialer.LastDialedFeedback.LinkInputSig(trilist.StringInput[dialerJoinMap.LastNumberDialerFb.JoinNumber + dialerLineOffset]);

                dialer.HoldCallFeedback.LinkInputSig(trilist.BooleanInput[dialerJoinMap.HoldCall.JoinNumber]);
                dialer.HoldCallFeedback.LinkComplementInputSig(trilist.BooleanInput[dialerJoinMap.ResumeCall.JoinNumber]);
                dialer.HoldCallFeedback.LinkInputSig(trilist.BooleanInput[dialerJoinMap.HoldToggle.JoinNumber]);


                dialer.CallStateFeedback.LinkInputSig(trilist.UShortInput[dialerJoinMap.CallState.JoinNumber + dialerLineOffset]);

                lineOffset += 50;
            }
        }

        private void LinkPresetsToApi(BasicTriList trilist, TesiraPresetJoinMapAdvanced presetJoinMap)
        {
            // string input executes preset recall using preset name
            trilist.SetStringSigAction(presetJoinMap.PresetName.JoinNumber, RunPreset);
            trilist.SetUShortSigAction(presetJoinMap.PresetName.JoinNumber, RunPresetNumber);
            // digital input executes preset reall using preset id (RunPresetNumber))
            foreach (var preset in Presets)
            {
                if (!(preset.Value is TesiraPreset p)) continue;
                var runPresetIndex = p.PresetIndex;
                var presetIndex = runPresetIndex;
                trilist.StringInput[(uint)(presetJoinMap.PresetNameFeedback.JoinNumber + presetIndex)].StringValue = p.Label;
                trilist.SetSigTrueAction((uint)(presetJoinMap.PresetSelection.JoinNumber + presetIndex),
                    () => RecallPreset(p.Key));
            }
        }

        private void LinkSourceSelectorsToApi(BasicTriList trilist, TesiraSwitcherJoinMapAdvanced switcherJoinMap)
        {
            this.LogVerbose("There are {0} SourceSelector Control Points", Switchers.Count());
            foreach (var item in SourceSelectors)
            {
                var switcher = item.Value;
                var data = switcher.BridgeIndex;
                if (data == null) continue;
                var y = (uint)data;
                var x = (ushort)(((y - 1) * 2) + 1);
                //3 switchers
                //((1 - 1) * 2) + 1 = 1
                //((2 - 1) * 2) + 1 = 3
                //((3 - 1) * 2) + 1 = 5

                this.LogVerbose("Tesira Switcher {0} connect to {1}", switcher.Key, y);

                if (!switcher.Enabled) continue;


                this.LogVerbose("Tesira Switcher {0} is Enabled", x);

                var s = switcher as IRoutingWithFeedback;
                s.SourceIndexFeedback.LinkInputSig(trilist.UShortInput[switcherJoinMap.Index.JoinNumber + x]);

                trilist.SetUShortSigAction(switcherJoinMap.Index.JoinNumber + x, u => switcher.SetSource(u));
                trilist.SetSigTrueAction(switcherJoinMap.Poll.JoinNumber + x, switcher.DoPoll);

                switcher.NameFeedback.LinkInputSig(trilist.StringInput[switcherJoinMap.Label.JoinNumber + x]);

                switcher.GetSourceNames();
            }
        }

        private void LinkSwitchersToApi(BasicTriList trilist, TesiraSwitcherJoinMapAdvanced switcherJoinMap)
        {
            this.LogVerbose("There are {0} SourceSelector Control Points", Switchers.Count());
            foreach (var item in Routers)
            {
                var switcher = item.Value;
                var data = switcher.BridgeIndex;
                if (data == null) continue;
                var y = (uint)data;
                var x = (ushort)(((y - 1) * 2) + 1);
                //3 switchers
                //((1 - 1) * 2) + 1 = 1
                //((2 - 1) * 2) + 1 = 3
                //((3 - 1) * 2) + 1 = 5

                this.LogVerbose("Tesira Switcher {0} connect to {1}", switcher.Key, y);

                if (!switcher.Enabled) continue;


                this.LogVerbose("Tesira Switcher {0} is Enabled", x);

                var s = switcher as IRoutingWithFeedback;
                s.SourceIndexFeedback.LinkInputSig(trilist.UShortInput[switcherJoinMap.Index.JoinNumber + x]);

                trilist.SetUShortSigAction(switcherJoinMap.Index.JoinNumber + x, u => switcher.SetSource(u));
                trilist.SetSigTrueAction(switcherJoinMap.Poll.JoinNumber + x, switcher.DoPoll);

                switcher.NameFeedback.LinkInputSig(trilist.StringInput[switcherJoinMap.Label.JoinNumber + x]);

                switcher.GetSourceNames();
            }
        }

        private void LinkLegacySwitchersToApi(BasicTriList trilist, TesiraSwitcherJoinMapAdvanced switcherJoinMap)
        {
            this.LogVerbose("There are {0} SourceSelector Control Points", Switchers.Count());
            foreach (var item in Switchers)
            {
                var switcher = item.Value;
                var data = switcher.BridgeIndex;
                if (data == null) continue;
                var y = (uint)data;
                var x = (ushort)(((y - 1) * 2) + 1);
                //3 switchers
                //((1 - 1) * 2) + 1 = 1
                //((2 - 1) * 2) + 1 = 3
                //((3 - 1) * 2) + 1 = 5

                this.LogVerbose("Tesira Switcher {0} connect to {1}", switcher.Key, y);

                if (!switcher.Enabled) continue;


                this.LogVerbose("Tesira Switcher {0} is Enabled", x);

                var s = switcher as IRoutingWithFeedback;
                s.SourceIndexFeedback.LinkInputSig(trilist.UShortInput[switcherJoinMap.Index.JoinNumber + x]);

                trilist.SetUShortSigAction(switcherJoinMap.Index.JoinNumber + x, u => switcher.SetSource(u));
                trilist.SetSigTrueAction(switcherJoinMap.Poll.JoinNumber + x, switcher.DoPoll);

                switcher.NameFeedback.LinkInputSig(trilist.StringInput[switcherJoinMap.Label.JoinNumber + x]);

                switcher.GetSourceNames();
            }
        }

        private void LinkStatesToApi(BasicTriList trilist, TesiraStateJoinMapAdvanced stateJoinMap)
        {
            this.LogVerbose("There are {0} State Control Points", States.Count());
            foreach (var item in States)
            {
                var state = item.Value;
                var data = state.BridgeIndex;
                if (data == null) continue;

                var x = (uint)data - 1;
                this.LogVerbose("Tesira State {0} connect to {1}", state.Key, x);

                if (!state.Enabled) continue;

                this.LogVerbose("Tesira State {0} at {1} is Enabled", state.Key, x);

                state.StateFeedback.LinkInputSig(trilist.BooleanInput[stateJoinMap.Toggle.JoinNumber + x]);
                state.StateFeedback.LinkInputSig(trilist.BooleanInput[stateJoinMap.On.JoinNumber + x]);
                state.StateFeedback.LinkComplementInputSig(trilist.BooleanInput[stateJoinMap.Off.JoinNumber + x]);
                state.NameFeedback.LinkInputSig(trilist.StringInput[stateJoinMap.Label.JoinNumber + x]);

                trilist.SetSigTrueAction(stateJoinMap.Toggle.JoinNumber + x, state.StateToggle);
                trilist.SetSigTrueAction(stateJoinMap.On.JoinNumber + x, state.StateOn);
                trilist.SetSigTrueAction(stateJoinMap.Off.JoinNumber + x, state.StateOff);
            }
        }

        private void LinkFadersToApi(BasicTriList trilist, TesiraFaderJoinMapAdvanced faderJoinMap)
        {
            this.LogVerbose("There are {0} Level Control Points", Faders.Count());
            foreach (var item in Faders)
            {
                var channel = item.Value;
                var data = channel.BridgeIndex;
                if (data == null) continue;
                var x = (uint)data;
                //var TesiraChannel = channel.Value as Tesira.DSP.EPI.TesiraDspLevelControl;
                this.LogVerbose("TesiraChannel {0} connect", x);

                var genericChannel = channel as IBasicVolumeWithFeedback;

                if (!channel.Enabled) continue;

                this.LogVerbose("TesiraChannel {0} Is Enabled", x);

                channel.NameFeedback.LinkInputSig(trilist.StringInput[faderJoinMap.Label.JoinNumber + x]);
                channel.TypeFeedback.LinkInputSig(trilist.UShortInput[faderJoinMap.Type.JoinNumber + x]);
                channel.ControlTypeFeedback.LinkInputSig(trilist.UShortInput[faderJoinMap.Status.JoinNumber + x]);
                channel.PermissionsFeedback.LinkInputSig(trilist.UShortInput[faderJoinMap.Permissions.JoinNumber + x]);
                channel.VisibleFeedback.LinkInputSig(trilist.BooleanInput[faderJoinMap.Visible.JoinNumber + x]);

                genericChannel.MuteFeedback.LinkInputSig(trilist.BooleanInput[faderJoinMap.MuteToggle.JoinNumber + x]);
                genericChannel.MuteFeedback.LinkInputSig(trilist.BooleanInput[faderJoinMap.MuteOn.JoinNumber + x]);
                genericChannel.MuteFeedback.LinkComplementInputSig(trilist.BooleanInput[faderJoinMap.MuteOff.JoinNumber + x]);
                genericChannel.VolumeLevelFeedback.LinkInputSig(trilist.UShortInput[faderJoinMap.Volume.JoinNumber + x]);

                trilist.SetSigTrueAction(faderJoinMap.MuteToggle.JoinNumber + x, genericChannel.MuteToggle);
                trilist.SetSigTrueAction(faderJoinMap.MuteOn.JoinNumber + x, genericChannel.MuteOn);
                trilist.SetSigTrueAction(faderJoinMap.MuteOff.JoinNumber + x, genericChannel.MuteOff);

                trilist.SetBoolSigAction(faderJoinMap.VolumeUp.JoinNumber + x, genericChannel.VolumeUp);
                trilist.SetBoolSigAction(faderJoinMap.VolumeDown.JoinNumber + x, genericChannel.VolumeDown);

                trilist.SetUShortSigAction(faderJoinMap.Volume.JoinNumber + x, u => { if (u > 0) { genericChannel.SetVolume(u); } });
                //channel.Value.DoPoll();
            }
        }

        public void UpdateDeviceInfo()
        {
            DevInfo.UpdateDeviceInfo();
        }
    }
}
