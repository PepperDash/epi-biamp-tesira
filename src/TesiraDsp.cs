using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.CrestronThread;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.DeviceInfo;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using PepperDash.Essentials.Core.Config;
using Crestron.SimplSharpPro.DeviceSupport;
using Tesira_DSP_EPI.Bridge.JoinMaps;
using PepperDash.Essentials.Core.Bridges;
using Tesira_DSP_EPI.Interfaces;
using Feedback = PepperDash.Essentials.Core.Feedback;
using IRoutingWithFeedback = Tesira_DSP_EPI.Interfaces.IRoutingWithFeedback;

#if SERIES4
using PepperDash.Core.Logging;
#endif

namespace Tesira_DSP_EPI
{
    public class TesiraDsp : EssentialsBridgeableDevice,
#if SERIES4
        IDspPresets,
#endif
        ICommunicationMonitor,
        IDeviceInfoProvider
    {
        /// <summary>
        /// Collection of all Device Feedbacks
        /// </summary>
        public FeedbackCollection<Feedback> Feedbacks;

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
        public bool IsSubscribed {
            get
            {
                var subscribeTracker = ControlPointList.All(subscribedComponent => subscribedComponent.IsSubscribed);
                if (_subscribeThread == null) return subscribeTracker;
                if (subscribeTracker && _subscribeThread.ThreadState == Thread.eThreadStates.ThreadRunning)
                    StopSubscriptionThread();
                return subscribeTracker;
            }
        }


		private CTimer _watchDogTimer;

        private CTimer _queueCheckTimer;

        private CTimer _unsubscribeTimer;
        private CTimer _subscribeTimer;
        private CTimer _expanderCheckTimer;
        private CTimer _pacer;
        private CTimer _paceTimer;
        private CTimer _getMaxTimer;
        private CTimer _getMinTimer;
        private CTimer _componentSubscribeTimer;

        private Thread _subscribeThread;

        private readonly bool _isSerialComm;

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
        private Dictionary<string, TesiraDspCrosspointState> CrosspointStates { get; set; }
        private Dictionary<string, TesiraDspRoomCombiner> RoomCombiners { get; set; }
        public List<TesiraPreset> TesiraPresets { get; private set; } 
        private List<ISubscribedComponent> ControlPointList { get; set; }
        public Dictionary<string, IKeyName> Presets { get; private set; }

        private TesiraExpanderTracker ExpanderTracker { get; set; }

        private bool _initalSubscription = true;



        //private TesiraDspDeviceInfo DeviceInfo { get; set; }

		private bool WatchDogSniffer { get; set; }
        public bool WatchdogSuspend { get; private set; }

		readonly DeviceConfig _dc;

		public bool ShowHexResponse { get; set; }

        public string ResubsriptionString { get; set; }

        public DeviceInfo DeviceInfo
        {
            get {
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
			_dc = dc;

            CommandQueue = new TesiraQueue(2000, this);

            CommandPassthruFeedback = new StringFeedback(() => DeviceRx);

            Communication = comm;


            CommandPassthruFeedback = new StringFeedback(() => DeviceRx);
            Communication = comm;
            ISocketStatus socket = comm as ISocketStatus;

            if (socket != null)
            {
#if SERIES4
                this.LogDebug("DEVICE IS CONTROLLED VIA NETWORK CONNECTION");
#else
                Debug.Console(1, "DEVICE IS CONTROLLED VIA NETWORK CONNECTION");
#endif

                // This instance uses IP control
                socket.ConnectionChange += Socket_ConnectionChange;
                _isSerialComm = false;


                GenericSshClient ssh = comm as GenericSshClient;
                if (ssh != null)
                {
                    DeviceInfo.IpAddress = ssh.Hostname;
                    DeviceInfo.HostName = ssh.Hostname;
                }


                GenericTcpIpClient tcp = comm as GenericTcpIpClient;
                if (tcp != null)
                {
                    DeviceInfo.IpAddress = tcp.Hostname;
                    DeviceInfo.HostName = tcp.Hostname;
                }
            }
            else
            {
#if SERIES4
                this.LogDebug("DEVICE IS CONTROLLED VIA RS232");
#else
                Debug.Console(1, "DEVICE IS CONTROLLED VIA RS232");
#endif
                // This instance uses RS-232 control
                _isSerialComm = true;
            }


            PortGather = new CommunicationGather(Communication, "\x0D\x0A");
			PortGather.LineReceived += Port_LineReceived;


            CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, 20000, 120000, 300000, () => SendLine("SESSION set verbose false"));

			// Custom monitoring, will check the heartbeat tracker count every 20s and reset. Heartbeat sbould be coming in every 20s if subscriptions are valid
			DeviceManager.AddDevice(CommunicationMonitor);

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

            //Start CommnicationMonitor in PostActivation phase
            AddPostActivationAction(() =>
            {
                Communication.Connect();
                if (!_isSerialComm) return;
                CommunicationMonitor.Start();
                OkayToSend = true;
                CheckSerialSendStatus();
            });

            CreateDspObjects();
        }

        private void CheckSerialSendStatus()
        {
#if SERIES4
            if (_isSerialComm) this.LogVerbose("CheckSerialSendStatus");
#else
            if (_isSerialComm) Debug.Console(2, "CheckSerialSendStatus");
#endif

            if (OkayToSend && ControlsAdded && _isSerialComm && InitialStart)
            {
                InitialStart = false;
#if SERIES4
                this.LogVerbose("CheckSerialStatus Ready");
#else
                Debug.Console(2, "CheckSerialStatus Ready");
#endif

                CrestronInvoke.BeginInvoke(o => StartSubsciptionThread());
                return;
            }
#if SERIES4
            if (_isSerialComm) this.LogVerbose("CheckSerialSendStatus NOT READY");
#else
            if (_isSerialComm) Debug.Console(2, "CheckSerialSendStatus NOT READY");
#endif

        }



        private void StartSubsciptionThread()
        {
#if SERIES4
            this.LogDebug("Start Subscription Thread");
#else
            Debug.Console(1, "Start Subscription Thread");
#endif
			if (_subscribeThread != null)
			{
				if(_subscribeThread.ThreadState == Thread.eThreadStates.ThreadRunning)
				{
					return;
				}	
			}
			_subscribeThread = null;
            _subscribeThread = new Thread(o => HandleAttributeSubscriptions(), null,
                Thread.eThreadStartOptions.CreateSuspended)
            {
                Name = string.Format("{0}-queue", Key),
                Priority = CrestronEnvironment.ProgramCompatibility.Equals(eCrestronSeries.Series4)
                    ? Thread.eThreadPriority.LowestPriority
                    : Thread.eThreadPriority.LowestPriority
            };
			/*{
				Priority = Thread.eThreadPriority.LowestPriority
			};*/

            _subscribeThread.Start();
        }

        private void StopSubscriptionThread()
        {
            if (_subscribeThread.ThreadState == Thread.eThreadStates.ThreadRunning)
            {
                _subscribeThread = null;
            }
        }

        void CrestronEnvironment_ProgramStatusEventHandler(eProgramStatusEventType programEventType)
        {
            if (programEventType != eProgramStatusEventType.Stopping) return;

			if (_watchDogTimer != null)
			{
				_watchDogTimer.Stop();
				_watchDogTimer.Dispose();
			}
			if (CommunicationMonitor != null)
			{
				CommunicationMonitor.Stop();
				Communication.Disconnect();
			}
        }

        private void CreateDspObjects()
        {
#if SERIES4
            this.LogVerbose("Creating DSP Objects");
#else
            Debug.Console(2, "Creating DSP Objects");
#endif

            var props = JsonConvert.DeserializeObject<TesiraDspPropertiesConfig>(_dc.Properties.ToString());

            ResubsriptionString = !string.IsNullOrEmpty(props.ResubscribeString)
                ? props.ResubscribeString
                : "hullabaloo";

#if SERIES4
            this.LogVerbose("Props Exists");
#else
            Debug.Console(2, "Props Exists");
#endif

            Faders.Clear();
            Presets.Clear();
            Dialers.Clear();
            States.Clear();
            Switchers.Clear();
            ControlPointList.Clear();
            Meters.Clear();
            RoomCombiners.Clear();

            CreateFaders(props);

            CreateSwitchers(props);

            CreateRouters(props);

            CreateSourceSelectors(props);

            CreateDialers(props);

            CreateStates(props);

            CreatePresets(props);

            CreateMeters(props);

            CreateCrosspoints(props);

            CreateRoomCombiners(props);

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
#if SERIES4
                this.LogVerbose("Added Preset {0} {1}", value.Label, value.PresetName);
#else
                Debug.Console(2, "Added Preset {0} {1}", value.Label, value.PresetName);
#endif
            }

            var presetDevice = new TesiraDspPresetDevice(this);
            DeviceManager.AddDevice(presetDevice);
        }

        private void CreateFaders(TesiraDspPropertiesConfig props)
        {
            if (props.FaderControlBlocks == null) return;
#if SERIES4
            this.LogVerbose("faderControlBlocks is not null - There are {0} of them",
                props.FaderControlBlocks.Count());
#else
            Debug.Console(2, "faderControlBlocks is not null - There are {0} of them",
                props.FaderControlBlocks.Count());
#endif
            foreach (var block in props.FaderControlBlocks)
            {
                var key = block.Key;
#if SERIES4
                this.LogVerbose("faderControlBlock Key - {0}", key);
#else
                Debug.Console(2, "faderControlBlock Key - {0}", key);
#endif
                var value = block.Value;

                Faders.Add(key, new TesiraDspFaderControl(key, value, this));
#if SERIES4
                this.LogVerbose("Added faderControlPoint {0} levelTag: {1} muteTag: {2}", key, value.LevelInstanceTag,
                    value.MuteInstanceTag);
#else
                Debug.Console(2, "Added faderControlPoint {0} levelTag: {1} muteTag: {2}", key, value.LevelInstanceTag,
                    value.MuteInstanceTag);
#endif
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
#if SERIES4
                this.LogVerbose("Adding Mixer {0} InstanceTag: {1}", key, value.RoomCombinerInstanceTag);
#else
                Debug.Console(2, "Adding Mixer {0} InstanceTag: {1}", key, value.RoomCombinerInstanceTag);
#endif

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
#if SERIES4
                this.LogVerbose("Adding CrosspointState {0} InstanceTag: {1}", key, value.MatrixInstanceTag);
#else
                Debug.Console(2, "Adding CrosspointState {0} InstanceTag: {1}", key, value.MatrixInstanceTag);
#endif

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
#if SERIES4
                this.LogVerbose("Adding Meter {0} InstanceTag: {1}", key, value.MeterInstanceTag);
#else
                Debug.Console(2, "Adding Meter {0} InstanceTag: {1}", key, value.MeterInstanceTag);
#endif

                if (value.Enabled)
                {
                    ControlPointList.Add(Meters[key]);
                }
                DeviceManager.AddDevice(Meters[key]);

            }
        }

        private void CreateStates(TesiraDspPropertiesConfig props)
        {
            if (props.StateControlBlocks == null) return;
#if SERIES4
            this.LogVerbose("stateControlBlocks is not null - There are {0} of them",
                props.StateControlBlocks.Count());
#else
            Debug.Console(2, "stateControlBlocks is not null - There are {0} of them",
                props.StateControlBlocks.Count());
#endif
            foreach (var block in props.StateControlBlocks)
            {
                var key = block.Key;
                var value = block.Value;
                States.Add(key, new TesiraDspStateControl(key, value, this));
#if SERIES4
                this.LogVerbose("Added DspState {0} InstanceTag: {1}", key, value.StateInstanceTag);
#else
                Debug.Console(2, "Added DspState {0} InstanceTag: {1}", key, value.StateInstanceTag);
#endif

                if (block.Value.Enabled)
                    ControlPointList.Add(States[key]);
                DeviceManager.AddDevice(States[key]);

            }
        }

        private void CreateDialers(TesiraDspPropertiesConfig props)
        {
            if (props.DialerControlBlocks == null) return;
#if SERIES4
            this.LogVerbose("DialerControlBlocks is not null - There are {0} of them",
                props.DialerControlBlocks.Count());
#else
            Debug.Console(2, "DialerControlBlocks is not null - There are {0} of them",
                props.DialerControlBlocks.Count());
#endif
            foreach (var block in props.DialerControlBlocks)
            {
                var key = block.Key;
#if SERIES4
                this.LogVerbose("DialerControlBlock Key - {0}", key);
#else
                Debug.Console(2, "DialerControlBlock Key - {0}", key);
#endif
                var value = block.Value;
                Dialers.Add(key, new TesiraDspDialer(key, value, this));
#if SERIES4
                this.LogVerbose("Added DspDialer {0} ControlStatusTag: {1} DialerTag: {2}", key,
                    value.ControlStatusInstanceTag, value.DialerInstanceTag);
#else
                Debug.Console(2, "Added DspDialer {0} ControlStatusTag: {1} DialerTag: {2}", key,
                    value.ControlStatusInstanceTag, value.DialerInstanceTag);
#endif

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
#if SERIES4
            this.LogVerbose("switcherControlBlocks is not null - There are {0} of them",
                props.SwitcherControlBlocks.Count());
#else
            Debug.Console(2, "switcherControlBlocks is not null - There are {0} of them",
                props.SwitcherControlBlocks.Count());
#endif
            foreach (var block in props.SwitcherControlBlocks)
            {
                var key = block.Key;
#if SERIES4
                this.LogVerbose("SwitcherControlBlock Key - {0}", key);
#else
                Debug.Console(2, "SwitcherControlBlock Key - {0}", key);
#endif
                var value = block.Value;

                Switchers.Add(key, new TesiraDspSwitcher(key, value, this));
#if SERIES4
                this.LogVerbose("Added TesiraSwitcher {0} InstanceTag {1}", key, value.SwitcherInstanceTag);
#else
                Debug.Console(2, "Added TesiraSwitcher {0} InstanceTag {1}", key, value.SwitcherInstanceTag);
#endif

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
#if SERIES4
            this.LogVerbose("sourceSelectorControlBlocks is not null - There are {0} of them",
                props.SourceSelectorControlBlocks.Count());
#else
            Debug.Console(2, "sourceSelectorControlBlocks is not null - There are {0} of them",
                props.SourceSelectorControlBlocks.Count());
#endif
            foreach (var block in props.SourceSelectorControlBlocks)
            {
                var key = block.Key;
#if SERIES4
                this.LogVerbose("Source Selector ControlBlock Key - {0}", key);
#else
                Debug.Console(2, "Source Selector ControlBlock Key - {0}", key);
#endif
                var value = block.Value;

                SourceSelectors.Add(key, new TesiraDspSourceSelector(key, value, this));
#if SERIES4
                this.LogVerbose("Added TesiraSwitcher {0} InstanceTag {1}", key, value.SourceSelectorInstanceTag);
#else
                Debug.Console(2, "Added TesiraSwitcher {0} InstanceTag {1}", key, value.SourceSelectorInstanceTag);
#endif

                ControlPointList.Add(SourceSelectors[key]);
                DeviceManager.AddDevice(SourceSelectors[key]);

            }
        }

        private void CreateRouters(TesiraDspPropertiesConfig props)
        {
            if (props.RouterControlBlocks == null) return;
#if SERIES4
            this.LogVerbose("routerControlBlocks is not null - There are {0} of them",
                props.RouterControlBlocks.Count());
#else
            Debug.Console(2, "routerControlBlocks is not null - There are {0} of them",
                props.RouterControlBlocks.Count());
#endif
            foreach (var block in props.RouterControlBlocks)
            {
                var key = block.Key;
#if SERIES4
                this.LogVerbose("RouterControlBlock Key - {0}", key);
#else
                Debug.Console(2, "RouterControlBlock Key - {0}", key);
#endif
                var value = block.Value;

                Routers.Add(key, new TesiraDspRouter(key, value, this));
#if SERIES4
                this.LogVerbose("Added Router {0} InstanceTag {1}", key, value.RouterInstanceTag);
#else
                Debug.Console(2, "Added Router {0} InstanceTag {1}", key, value.RouterInstanceTag);
#endif

                DeviceManager.AddDevice(Routers[key]);

            }
        }

        private void CreateExpanderTracker(TesiraDspPropertiesConfig props)
        {
            if (props.ExpanderBlocks == null) return;
#if SERIES4
            this.LogVerbose("ExpanderBlocks is not null - there are {0} of them", props.ExpanderBlocks.Count());
#else
            Debug.Console(2, "ExpanderBlocks is not null - there are {0} of them", props.ExpanderBlocks.Count());
#endif

            ExpanderTracker = new TesiraExpanderTracker(this, props.ExpanderBlocks);

            DeviceManager.AddDevice(ExpanderTracker);
            
        }

        #region Communications

        void CommunicationMonitor_StatusChange(object sender, MonitorStatusChangeEventArgs e)
		{
#if SERIES4
            this.LogVerbose("Communication monitor state: {0}", CommunicationMonitor.Status);
#else
            Debug.Console(2, "Communication monitor state: {0}", CommunicationMonitor.Status);
#endif
			if (e.Status == MonitorStatus.IsOk)
			{
			    //StartSubsciptionThread();
			}
			else if (e.Status != MonitorStatus.IsOk)
			{
				StopWatchDog();
			}
		}

		void Socket_ConnectionChange(object sender, GenericSocketStatusChageEventArgs e)
		{
#if SERIES4
            this.LogVerbose("Socket Status Change: {0}", e.Client.ClientStatus.ToString());
#else
            Debug.Console(2, "Socket Status Change: {0}", e.Client.ClientStatus.ToString());
#endif

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
            if (_watchDogTimer == null)
            {
                _watchDogTimer = new CTimer(o => CheckWatchDog(), null, 90000, 90000);
            }
            else
            {
                _watchDogTimer.Reset(90000, 90000);
            }
        }

        private void StopWatchDog()
        {
            if (_watchDogTimer == null) return;
            _watchDogTimer.Stop();
            _watchDogTimer.Dispose();
            _watchDogTimer = null;
        }

        private void CheckWatchDog()
        {
			try
			{
			    if (ControlPointList.Count == 0) return;
			    if (WatchdogSuspend)
			    {
			        WatchDogSniffer = false;
			        return;
			    }
#if SERIES4
                this.LogDebug("The Watchdog is on the hunt!");
#else
                Debug.Console(1, "The Watchdog is on the hunt!");
#endif
				if (!WatchDogSniffer)
				{
#if SERIES4
                    this.LogDebug("The Watchdog is picking up a scent!");
#else
                    Debug.Console(1, "The Watchdog is picking up a scent!");
#endif


					var random = new Random(DateTime.Now.Millisecond + DateTime.Now.Second + DateTime.Now.Minute
						+ DateTime.Now.Hour + DateTime.Now.Day + DateTime.Now.Month + DateTime.Now.Year);

					var watchDogSubject = ControlPointList[random.Next(0, ControlPointList.Count)];
					if (!watchDogSubject.IsSubscribed)
					{
#if SERIES4
                        this.LogDebug("The Watchdog was wrong - that's just an old shoe.  Nothing is subscribed.");
#else
                        Debug.Console(1, "The Watchdog was wrong - that's just an old shoe.  Nothing is subscribed.");
#endif
						return;
					}
#if SERIES4
                    this.LogDebug("The Watchdog is sniffing \"{0}\".", watchDogSubject.Key);
#else
                    Debug.Console(1, "The Watchdog is sniffing \"{0}\".", watchDogSubject.Key);
#endif

					WatchDogSniffer = true;

					watchDogSubject.Subscribe();
				}
				else
				{
#if SERIES4
                    this.LogDebug("The WatchDog smells something foul....let's resubscribe!");
#else
                    Debug.Console(1, "The WatchDog smells something foul....let's resubscribe!");
#endif
					Resubscribe();
				}

			}
			catch (Exception ex)
			{
#if SERIES4
                this.LogInformation(ex, "Watchdog Error");
#else
                Debug.Console(0, "Watchdog Error: {0}", ex);
#endif
			}
        }

        #endregion

        #region String Handling

        /// <summary>
		/// Sends a command to the DSP (with delimiter appended)
		/// </summary>
		/// <param name="s">Command to send</param>
		public void SendLine(string s)
		{
			if (string.IsNullOrEmpty(s))
				return;

			//this.LogDebug("TX: '{0}'", s);
            
			Communication.SendText(s + "\x0D");
		}

		/// <summary>
		/// Sends a command to the DSP (without delimiter appended)
		/// </summary>
		/// <param name="s">Command to send</param>
		public void SendLineRaw(string s)
		{
			if (string.IsNullOrEmpty(s))
				return;

			//this.LogDebug("TX: '{0}'", s);
			Communication.SendText(s);
		}

        const string SubscriptionPattern = "! [\\\"](.*?[^\\\\])[\\\"] (.*)";
        private readonly static Regex SubscriptionRegex = new Regex(SubscriptionPattern);

        public event DeviceInfoChangeHandler DeviceInfoChanged;

        private void Port_LineReceived(object dev, GenericCommMethodReceiveTextArgs args)
        {
            if (args == null) return;

            if (string.IsNullOrEmpty(args.Text)) return;


            try
            {

                //this.LogDebug("RX: '{0}'", ShowHexResponse ? ComTextHelper.GetEscapedText(args.Text) : args.Text);

                DeviceRx = args.Text;

                CommandPassthruFeedback.FireUpdate();

                if (args.Text.Length == 0) return;

                //if (args.Text.IndexOf("Welcome", StringComparison.Ordinal) > -1)
                if(args.Text.Contains("Welcome"))
                {
                    // Indicates a new TTP session
                    // moved to CustomActivate() method
                    if (!_isSerialComm)
                    {
                        CommunicationMonitor.Start();
                    }
                    CrestronInvoke.BeginInvoke(o => StartSubsciptionThread());
                  
                }

                //else if (args.Text.IndexOf(ResubsriptionString, StringComparison.Ordinal) > -1)
                else if (args.Text.Equals(ResubsriptionString, StringComparison.OrdinalIgnoreCase))
                {
                    if(!string.IsNullOrEmpty(ResubsriptionString))
                    CommandQueue.Clear();
                    Resubscribe();
                }

                else if (args.Text.IndexOf("! ", StringComparison.Ordinal) >= 0)
                {

                    var match = SubscriptionRegex.Match(args.Text);

                    if (!match.Success) return;

                    var customName = match.Groups[1].Value;
                    var value = match.Groups[2].Value;
#if SERIES4
                    this.LogVerbose("Subscription Message: 'Name: {0} Value:{1}'",customName, value);
#else
                    Debug.Console(2, "Subscription Message: 'Name: {0} Value:{1}'",customName, value);
#endif
                    //CommandQueue.AdvanceQueue(args.Text);

                    foreach (var component in from component in ControlPointList let item = component from n in item.CustomNames.Where(n => n == customName) select component)
                    {
                        if (component == null)
                        {
#if SERIES4
                            this.LogDebug("Unable to find matching Custom Name {0}", customName);
#else
                            Debug.Console(1, "Unable to find matching Custom Name {0}", customName);
#endif
                            return;
                        }
                        component.ParseSubscriptionMessage(customName, value);
                    }
                }

                else if (args.Text.IndexOf("+OK", StringComparison.Ordinal) == 0)
                {
                    if(InitialStart) CheckSerialSendStatus();
                    // if (args.Text == "+OK")       // Check for a simple "+OK" only 'ack' repsonse or a list response and ignore
                       // return;
                    
                    // response is not from a subscribed attribute.  From a get/set/toggle/increment/decrement command
                    //string pattern = "(?<=\" )(.*?)(?=\\+)";
                    //string data = Regex.Replace(args.Text, pattern, "");

                    CommandQueue.AdvanceQueue(args.Text);
                }

				else if (args.Text.IndexOf("DEVICE recallPresetByName", StringComparison.Ordinal) == 0)
				{
					CommandQueue.AdvanceQueue(args.Text);
				}
				else if (args.Text.IndexOf("-ERR", StringComparison.Ordinal) >= 0)
				{
					// Error response

					if (args.Text.IndexOf("ALREADY_SUBSCRIBED", StringComparison.Ordinal) >= 0)
					{
						if (WatchDogSniffer)
#if SERIES4
                            this.LogDebug("The Watchdog didn't find anything.  Good Boy!");
#else
                            Debug.Console(1, "The Watchdog didn't find anything.  Good Boy!");
#endif

						WatchDogSniffer = false;
						//CommandQueue.AdvanceQueue(args.Text);
					}

					else
					{
#if SERIES4
                        this.LogDebug("Error From DSP: '{0}'", args.Text);
#else
                        Debug.Console(1, "Error From DSP: '{0}'", args.Text);
#endif
						WatchDogSniffer = false;
						CommandQueue.AdvanceQueue(args.Text);
					}
				}
            }
            catch (Exception e)
            {
                if(args.Text.Length > 0)
#if SERIES4
                    this.LogInformation(e, "Error parsing response {response}", args.Text);
#else
                    Debug.Console(0, "Error parsing response {0}; {1}", args.Text, e);
#endif
            }

        }

        #endregion


		#region Presets

		public void RunPresetNumber(ushort n)
		{
#if SERIES4
            this.LogVerbose("Attempting to run preset {0}", n);
#else
            Debug.Console(2, "Attempting to run preset {0}", n);
#endif

            foreach (var preset in Presets.OfType<TesiraPreset>().Where(preset => preset.Index == n))
            {
#if SERIES4
                this.LogVerbose("Found a matching Preset - {0}", preset.PresetId);
#else
                Debug.Console(2, "Found a matching Preset - {0}", preset.PresetId);
#endif
                RecallPreset(preset.Key);
            }

		}

		/// <summary>
		/// Sends a command to execute a preset
		/// </summary>
        /// <param name="name">Preset Name</param>
		public void RunPreset(string name)
		{
#if SERIES4
            this.LogVerbose("Running Preset By Name - {0}", name);
#else
            Debug.Console(2, "Running Preset By Name - {0}", name);
#endif
            SendLine(string.Format("DEVICE recallPresetByName \"{0}\"", name));
            //CommandQueue.EnqueueCommand(string.Format("DEVICE recallPresetByName \"{0}\"", name));
		}

        /// <summary>
        /// Sends a command to execute a preset
        /// </summary>
        /// <param name="id">Preset id</param>
        public void RunPreset(int id)
        {
#if SERIES4
            this.LogVerbose("Running Preset By ID - {0}", id);
#else
            Debug.Console(2, "Running Preset By ID - {0}", id);
#endif
            SendLine(string.Format("DEVICE recallPreset {0}", id));
            //CommandQueue.EnqueueCommand(string.Format("DEVICE recallPreset {0}", id));
        }

        public void RecallPreset(string key)
        {
            var preset = Presets[key] as TesiraPreset;
#if SERIES4
            this.LogVerbose("Running preset {0}", preset.Name);
#else
            Debug.Console(2, "Running preset {0}", preset.Name);
#endif
            if (preset == null) return;            

#if SERIES4
            this.LogVerbose("Checking Preset {0} | presetIndex {1} | presetId {2} | presetName {3}", 
                preset.Name, preset.PresetIndex, preset.PresetId, preset.PresetName);
#else
            Debug.Console(2, "Checking Preset {0} | presetIndex {1} | presetId {2} | presetName {3}", 
                preset.Name, preset.PresetIndex, preset.PresetId, preset.PresetName);
#endif
            // - changed string check reference from 'tesiraPreset.PresetName' to 'tesiraPreset.PreetData.PresetName'
            if (!string.IsNullOrEmpty(preset.PresetName))
            {
                RunPreset(preset.PresetName);
            }
            else
            {
                if (preset.PresetId == 0)
                {
#if SERIES4
                    this.LogVerbose("Preset {0} has an invalid presetId {1}", preset.Name, preset.PresetId);
#else
                    Debug.Console(2, "Preset {0} has an invalid presetId {1}", preset.Name, preset.PresetId);
#endif
                    return;
                }
                RunPreset(preset.PresetId);
            }
        }

        public void SavePresetNumber(ushort n)
        {

#if SERIES4
            this.LogVerbose("Attempting to save preset {0}", n);
#else
            Debug.Console(2, "Attempting to save preset {0}", n);
#endif

            foreach (var preset in Presets.OfType<TesiraPreset>().Where(preset => preset.Index == n))
            {

#if SERIES4
                this.LogVerbose("Found a matching Preset - {0}", preset.PresetId);
#else
                Debug.Console(2, "Found a matching Preset - {0}", preset.PresetId);
#endif

                StorePreset(preset.Key);
            }
        }

        /// <summary>
        /// Saves a preset with the given name
        /// </summary>
        /// <param name="name">Preset Name</param>
        public void SavePreset(string name)
        {

#if SERIES4
            this.LogVerbose("Saving Preset By Name - {0}", name);
#else
            Debug.Console(2, "Saving Preset By Name - {0}", name);
#endif

            SendLine(string.Format("DEVICE savePresetByName \"{0}\"", name));
        }

        /// <summary>
        /// Saves a preset with the given ID
        /// </summary>
        /// <param name="id">Preset ID</param>
        public void SavePreset(int id)
        {

#if SERIES4
            this.LogVerbose("Saving Preset By ID - {0}", id);
#else
            Debug.Console(2, "Saving Preset By ID - {0}", id);
#endif

            SendLine(string.Format("DEVICE savePreset {0}", id));
        }

        public void StorePreset(string key)
        {
            var preset = Presets[key] as TesiraPreset;

#if SERIES4
            this.LogVerbose("Storing preset {0}", preset.Name);
#else
            Debug.Console(2, "Storing preset {0}", preset.Name);
#endif

            if (preset == null) return;

#if SERIES4
            this.LogVerbose("Checking Preset {0} | presetIndex {1} | presetId {2} | presetName {3}",
                preset.Name, preset.PresetIndex, preset.PresetId, preset.PresetName);
#else
            Debug.Console(2, "Checking Preset {0} | presetIndex {1} | presetId {2} | presetName {3}",
                preset.Name, preset.PresetIndex, preset.PresetId, preset.PresetName);
#endif


            if (!string.IsNullOrEmpty(preset.PresetName))
            {
                SavePreset(preset.PresetName);
            }
            else
            {
                if (preset.PresetId == 0)
                {

#if SERIES4
                    this.LogVerbose("Preset {0} has an invalid presetId {1}", preset.Name, preset.PresetId);
#else
                    Debug.Console(2, "Preset {0} has an invalid presetId {1}", preset.Name, preset.PresetId);
#endif

                    return;
                }
                SavePreset(preset.PresetId);
            }
        }
		#endregion

		#region SubscriptionHandling        

        #region Unsubscribe

        
        private void UnsubscribeFromComponents()
        {

            _pacer = new CTimer(o => UnsubscribeFromComponent(0), null, 250);
		}

        private void UnsubscribeFromComponent(int index )
        {
            var controlPoint = ControlPointList[index];
            if (controlPoint != null) UnsubscribeFromComponent(controlPoint);
            var newIndex = index + 1;
#if SERIES4
            this.LogVerbose("NewIndex == {0} and ControlPointListCount == {1}", newIndex, ControlPointList.Count() );
#else
            Debug.Console(2, "NewIndex == {0} and ControlPointListCount == {1}", newIndex, ControlPointList.Count() );
#endif
            if (newIndex < ControlPointList.Count())
            {
                _unsubscribeTimer = new CTimer(o => UnsubscribeFromComponent(newIndex), null, 250);
            }
            else
            {
#if SERIES4
                this.LogDebug("Subscribe To Components");
#else
                Debug.Console(1, "Subscribe To Components");
#endif
                if (_unsubscribeTimer != null)
                    _unsubscribeTimer.Dispose();
                _subscribeTimer = new CTimer(o => SubscribeToComponents(), null, 250);
            }
        }

        private void UnsubscribeFromComponent(ISubscribedComponent data)
		{
            if (!data.Enabled) return;
#if SERIES4
            this.LogVerbose("Unsubscribing From Object - {0}", data.InstanceTag1);
#else
            Debug.Console(2, "Unsubscribing From Object - {0}", data.InstanceTag1);
#endif
            data.Unsubscribe();
		}

		#endregion

		#region Subscribe

        private void SubscribeToComponents()
        {
#if SERIES4
            this.LogDebug("Subscribing to Components");
#else
            Debug.Console(1, "Subscribing to Components");
#endif

            if (_unsubscribeTimer != null)
                _unsubscribeTimer.Dispose();
            if (_subscribeTimer != null)
                _subscribeTimer.Dispose();

            _initalSubscription = false;
            if (DevInfo != null)
            {
#if SERIES4
                this.LogVerbose("DevInfo Not Null");
#else
                Debug.Console(2, "DevInfo Not Null");
#endif
                DevInfo.GetDeviceInfo();

            }

            _expanderCheckTimer = new CTimer(o => CheckExpanders(), null, 1000);
        }


        private void GetMinLevels()
        {
#if SERIES4
            this.LogDebug("GetMinLevels Started");
#else
            Debug.Console(1, "GetMinLevels Started");
#endif
            var newList = ControlPointList.OfType<IVolumeComponent>().ToList();

            if (newList.Any())
            {
                _paceTimer = new CTimer(o => GetMinLevel(newList, 0), null, 250);
            }
        }

        private void 
            
        GetMaxLevels()
        {
#if SERIES4
            this.LogDebug("GetMaxLevels Started");
#else
            Debug.Console(1, "GetMaxLevels Started");
#endif
            var newList = ControlPointList.OfType<IVolumeComponent>().ToList();

            if (newList.Any())
            {
                _paceTimer = new CTimer(o => GetMaxLevel(newList, 0), null, 250);
            }
        }

        private void GetMaxLevel(IList<IVolumeComponent> faders, int index)
        {
            var data = faders[index];
            if (data != null)
                data.GetMaxLevel();
            var indexerOutput = index + 1;
#if SERIES4
            this.LogVerbose("Indexer = {0} : Count = {1} : MaxLevel", indexerOutput, faders.Count());
#else
            Debug.Console(2, "Indexer = {0} : Count = {1} : MaxLevel", indexerOutput, faders.Count());
#endif
            if (indexerOutput < faders.Count)
            {
                _getMaxTimer = new CTimer(o => GetMaxLevel(faders, indexerOutput), null, 250);
                return;
            }
            if (_getMaxTimer != null)
                _getMaxTimer.Dispose();
            _pacer = new CTimer(o => QueueCheckDelayed(), null, 250);
        }
        private void GetMinLevel(IList<IVolumeComponent> faders, int index)
        {
            var data = faders[index];
            if (data != null)
                data.GetMinLevel();
            var indexerOutput = index + 1;
#if SERIES4
            this.LogVerbose("Indexer = {0} : Count = {1} : MinLevel", indexerOutput, faders.Count());
#else
            Debug.Console(2, "Indexer = {0} : Count = {1} : MinLevel", indexerOutput, faders.Count());
#endif
            if (indexerOutput < faders.Count)
            {
                _getMinTimer = new CTimer(o => GetMinLevel(faders, indexerOutput), null, 250);
                return;
            }
            if (_getMinTimer != null)
                _getMinTimer.Dispose();
            _pacer = new CTimer(o => GetMaxLevels(), null, 250);
        }

        
        private void QueueCheckDelayed()
        {
#if SERIES4
            this.LogVerbose("Queue Check Delayed Started");
#else
            Debug.Console(2, "Queue Check Delayed Started");
#endif

            if (_queueCheckTimer == null)
            {
                _queueCheckTimer = new CTimer(o => QueueCheckSubscribe(), null, 1000, 1000);
            }
            else
            {
                _queueCheckTimer.Reset(250, 250);
            }

        }
         

        private void CheckExpanders()
        {
            _expanderCheckTimer.Dispose();
#if SERIES4
            this.LogDebug("CheckExpanders Started");
#else
            Debug.Console(1, "CheckExpanders Started");
#endif

            if (ExpanderTracker != null)
                ExpanderTracker.Initialize();
            _pacer = new CTimer(o => GetMinLevels(), null, 250);
        }

        private void QueueCheckSubscribe()
        {
#if SERIES4
            this.LogVerbose("LocalQueue Size = {0} and Command Queue {1} in Progress", CommandQueue.LocalQueue.Count, CommandQueue.CommandQueueInProgress ? "is" : "is not");
#else
            Debug.Console(2, "LocalQueue Size = {0} and Command Queue {1} in Progress", CommandQueue.LocalQueue.Count, CommandQueue.CommandQueueInProgress ? "is" : "is not");
#endif
            if (!CommandQueue.LocalQueue.Any() && !CommandQueue.CommandQueueInProgress)
            {
                _queueCheckTimer.Stop();
                _queueCheckTimer = null;
                _pacer = new CTimer(o => SubscribeToComponentByIndex(0), null, 250);

            }
            else
            {
                CommandQueue.SendNextQueuedCommand();
                _queueCheckTimer.Reset(1000, 1000);
            }
        }


        private void SubscribeToComponent(ISubscribedComponent data)
        {
            if (data == null) return;
            if (!data.Enabled) return;
#if SERIES4
            this.LogDebug("Subscribing To Object - {0}", data.InstanceTag1);
#else
            Debug.Console(1, "Subscribing To Object - {0}", data.InstanceTag1);
#endif
            data.Subscribe();
		}

        private void SubscribeToComponentByIndex(int indexer)
        {
            if (indexer >= ControlPointList.Count)
            {
                EndSubscriptionProcess();
                return;
            }
#if SERIES4
            this.LogDebug("Subscribing to Component {0}", indexer);
#else
            Debug.Console(1, "Subscribing to Component {0}", indexer);
#endif
            var data = ControlPointList[indexer];
            SubscribeToComponent(data);
            var indexerOutput = indexer + 1;
#if SERIES4
            this.LogVerbose("Indexer = {0} : Count = {1} : ControlPointList", indexerOutput, ControlPointList.Count());
#else
            Debug.Console(2, "Indexer = {0} : Count = {1} : ControlPointList", indexerOutput, ControlPointList.Count());
#endif
            _componentSubscribeTimer = new CTimer(o => SubscribeToComponentByIndex(indexerOutput), null, 250);
        }

        private void EndSubscriptionProcess()
        {
            if (_componentSubscribeTimer != null)
                _componentSubscribeTimer.Dispose();

            if (_pacer != null)
                _pacer.Dispose();

            if (_paceTimer != null)
                _paceTimer.Dispose();

            foreach (var control in Switchers.Select(switcher => switcher.Value).Where(control => control.SelectorCustomName == string.Empty))
            {
                control.DoPoll();
            }
            foreach (var control in Routers.Select(router => router.Value))
            {
                control.DoPoll();
            }

        }




		#endregion

        /// <summary>
        /// Resubscribe to all controls
        /// </summary>
        public void Resubscribe()
        {
#if SERIES4
            this.LogDebug("Issue Detected with device subscriptions - resubscribing to all controls"); // Assuming LogInformation maps to Debug.Console(1)
#else
            Debug.Console(1, "Issue Detected with device subscriptions - resubscribing to all controls");
#endif
            StopWatchDog();
			StartSubsciptionThread();
       }

        private object HandleAttributeSubscriptions()
        {
#if SERIES4
            this.LogDebug("HandleAttributeSubscriptions - LIVE");
#else
            Debug.Console(1, "HandleAttributeSubscriptions - LIVE");
#endif
            //_subscriptionLock.Enter();
            if (Communication.IsConnected)
            {
                SendLine("SESSION set verbose false");
                try
                {
                    if (_isSerialComm && _initalSubscription)
                    {
                        _initalSubscription = false;
                        UnsubscribeFromComponents();
                    }
                    else
                    {
                        //Subscribe
                        SubscribeToComponents();
                    }

                }
                catch (Exception ex)
                {
#if SERIES4
                   this.LogInformation(ex, "Error Subscribing");
#else
                   Debug.Console(0, "Error Subscribing: {0}", ex);
#endif
                    //_subscriptionLock.Leave();
                    //_subscriptionLock.Leave();
                }
            }
            StartWatchDog();
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

#if SERIES4
            this.LogDebug("Linking to Trilist '{ipId}'", trilist.ID.ToString("X"));
#else
            Debug.Console(1, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));
#endif

            //var comm = DspDevice as IBasicCommunication;


            CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[deviceJoinMap.IsOnline.JoinNumber]);
            CommandPassthruFeedback.LinkInputSig(trilist.StringInput[deviceJoinMap.CommandPassThru.JoinNumber]);
            trilist.SetStringSigAction(presetJoinMap.PresetName.JoinNumber, RunPreset);

            trilist.SetStringSigAction(deviceJoinMap.CommandPassThru.JoinNumber, SendLineRaw);

            trilist.SetSigTrueAction(deviceJoinMap.Resubscribe.JoinNumber, Resubscribe);


            //Level and Mute Control
#if SERIES4
            this.LogVerbose("There are {0} Level Control Points", Faders.Count());
#else
            Debug.Console(2, "There are {0} Level Control Points", Faders.Count());
#endif
            foreach (var item in Faders)
            {
                var channel = item.Value;
                var data = channel.BridgeIndex;
                if (data == null) continue;
                var x = (uint) data;
                //var TesiraChannel = channel.Value as Tesira.DSP.EPI.TesiraDspLevelControl;
#if SERIES4
                this.LogVerbose("TesiraChannel {0} connect", x);
#else
                Debug.Console(2, "TesiraChannel {0} connect", x);
#endif

                var genericChannel = channel as IBasicVolumeWithFeedback;
                
                if (!channel.Enabled) continue;

#if SERIES4
                this.LogVerbose("TesiraChannel {0} Is Enabled", x);
#else
                Debug.Console(2, "TesiraChannel {0} Is Enabled", x);
#endif

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

            //states
#if SERIES4
            this.LogVerbose("There are {0} State Control Points", States.Count());
#else
            Debug.Console(2, "There are {0} State Control Points", States.Count());
#endif
            foreach (var item in States)
            {
                var state = item.Value;
                var data = state.BridgeIndex;
                if (data == null) continue;

                var x = (uint)data - 1;
#if SERIES4
                this.LogVerbose("Tesira State {0} connect to {1}", state.Key, x);
#else
                Debug.Console(2, "Tesira State {0} connect to {1}", state.Key, x);
#endif

                if (!state.Enabled) continue;
                
#if SERIES4
                this.LogVerbose("Tesira State {0} at {1} is Enabled", state.Key, x);
#else
                Debug.Console(2, "Tesira State {0} at {1} is Enabled", state.Key, x);
#endif

                state.StateFeedback.LinkInputSig(trilist.BooleanInput[stateJoinMap.Toggle.JoinNumber + x]);
                state.StateFeedback.LinkInputSig(trilist.BooleanInput[stateJoinMap.On.JoinNumber + x]);
                state.StateFeedback.LinkComplementInputSig(trilist.BooleanInput[stateJoinMap.Off.JoinNumber + x]);
                state.NameFeedback.LinkInputSig(trilist.StringInput[stateJoinMap.Label.JoinNumber + x]);

                trilist.SetSigTrueAction(stateJoinMap.Toggle.JoinNumber + x, state.StateToggle);
                trilist.SetSigTrueAction(stateJoinMap.On.JoinNumber + x, state.StateOn);
                trilist.SetSigTrueAction(stateJoinMap.Off.JoinNumber + x, state.StateOff);
            }


            //Legacy Switchers
#if SERIES4
            this.LogVerbose("There are {0} SourceSelector Control Points", Switchers.Count());
#else
            Debug.Console(2, "There are {0} SourceSelector Control Points", Switchers.Count());
#endif
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

#if SERIES4
                this.LogVerbose("Tesira Switcher {0} connect to {1}", switcher.Key, y);
#else
                Debug.Console(2, "Tesira Switcher {0} connect to {1}", switcher.Key, y);
#endif

                if (!switcher.Enabled) continue;

#if SERIES4
                this.LogVerbose("Tesira Switcher {0} is Enabled", x);
#else
                Debug.Console(2, "Tesira Switcher {0} is Enabled", x);
#endif

                var s = switcher as IRoutingWithFeedback;
                s.SourceIndexFeedback.LinkInputSig(trilist.UShortInput[switcherJoinMap.Index.JoinNumber + x]);

                trilist.SetUShortSigAction(switcherJoinMap.Index.JoinNumber + x, u => switcher.SetSource(u));
                trilist.SetSigTrueAction(switcherJoinMap.Poll.JoinNumber + x, switcher.DoPoll);

                switcher.NameFeedback.LinkInputSig(trilist.StringInput[switcherJoinMap.Label.JoinNumber + x]);

                switcher.GetSourceNames();
            }
            //Source Selectors
#if SERIES4
            this.LogVerbose("There are {0} SourceSelector Control Points", Routers.Count()); // Corrected from Switchers.Count()
#else
            Debug.Console(2, "There are {0} SourceSelector Control Points", Routers.Count()); // Corrected from Switchers.Count()
#endif
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

#if SERIES4
                this.LogVerbose("Tesira Switcher {0} connect to {1}", switcher.Key, y);
#else
                Debug.Console(2, "Tesira Switcher {0} connect to {1}", switcher.Key, y);
#endif

                if (!switcher.Enabled) continue;

#if SERIES4
                this.LogVerbose("Tesira Switcher {0} is Enabled", x);
#else
                Debug.Console(2, "Tesira Switcher {0} is Enabled", x);
#endif

                var s = switcher as IRoutingWithFeedback;
                s.SourceIndexFeedback.LinkInputSig(trilist.UShortInput[switcherJoinMap.Index.JoinNumber + x]);

                trilist.SetUShortSigAction(switcherJoinMap.Index.JoinNumber + x, u => switcher.SetSource(u));
                trilist.SetSigTrueAction(switcherJoinMap.Poll.JoinNumber + x, switcher.DoPoll);

                switcher.NameFeedback.LinkInputSig(trilist.StringInput[switcherJoinMap.Label.JoinNumber + x]);

                switcher.GetSourceNames();
            }
            //Source Selectors
#if SERIES4
            this.LogVerbose("There are {0} SourceSelector Control Points", SourceSelectors.Count()); // Corrected from Switchers.Count()
#else
            Debug.Console(2, "There are {0} SourceSelector Control Points", SourceSelectors.Count()); // Corrected from Switchers.Count()
#endif
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

#if SERIES4
                this.LogVerbose("Tesira Switcher {0} connect to {1}", switcher.Key, y);
#else
                Debug.Console(2, "Tesira Switcher {0} connect to {1}", switcher.Key, y);
#endif

                if (!switcher.Enabled) continue;

#if SERIES4
                this.LogVerbose("Tesira Switcher {0} is Enabled", x);
#else
                Debug.Console(2, "Tesira Switcher {0} is Enabled", x);
#endif

                var s = switcher as IRoutingWithFeedback;
                s.SourceIndexFeedback.LinkInputSig(trilist.UShortInput[switcherJoinMap.Index.JoinNumber + x]);

                trilist.SetUShortSigAction(switcherJoinMap.Index.JoinNumber + x, u => switcher.SetSource(u));
                trilist.SetSigTrueAction(switcherJoinMap.Poll.JoinNumber + x, switcher.DoPoll);

                switcher.NameFeedback.LinkInputSig(trilist.StringInput[switcherJoinMap.Label.JoinNumber + x]);

                switcher.GetSourceNames();
            }



            //Presets 
            // string input executes preset recall using preset name
            trilist.SetStringSigAction(presetJoinMap.PresetName.JoinNumber, RunPreset);
            trilist.SetUShortSigAction(presetJoinMap.PresetName.JoinNumber, RunPresetNumber);
            // digital input executes preset reall using preset id (RunPresetNumber))
            foreach (var preset in Presets)
            {
                var tesiraPreset = preset.Value as TesiraPreset;
                if (tesiraPreset == null) continue;

                var runPresetIndex = tesiraPreset.PresetIndex;
                var presetIndex = runPresetIndex;
                trilist.StringInput[(uint)(presetJoinMap.PresetNameFeedback.JoinNumber + presetIndex)].StringValue = tesiraPreset.Label;
                //trilist.SetSigTrueAction((uint) (presetJoinMap.PresetSelection.JoinNumber + presetIndex),
                //() => RecallPreset(tesiraPreset.Key));
                // recall & save (press&hold)
                trilist.SetSigHeldAction((uint)(presetJoinMap.PresetSelection.JoinNumber + presetIndex), 5000,
                    () => StorePreset(tesiraPreset.Key),
                    () => RecallPreset(tesiraPreset.Key));
            }

            // VoIP Dialer

            uint lineOffset = 0;
            foreach (var line in Dialers)
            {
                var dialer = line.Value;
                var bridgeIndex = dialer.BridgeIndex;
				if (bridgeIndex == null)
				{
#if SERIES4
                    this.LogVerbose("BridgeIndex is missing for Dialer {0}", dialer.Key);
#else
                    Debug.Console(2, "BridgeIndex is missing for Dialer {0}", dialer.Key);
#endif
					continue;
				}

				var dialerLineOffset = lineOffset;
#if SERIES4
                this.LogVerbose("AddingDialerBridge {0} {1} Offset", dialer.Key, dialerLineOffset);
#else
                Debug.Console(2, "AddingDialerBridge {0} {1} Offset", dialer.Key, dialerLineOffset);
#endif
                for (var i = 0; i < dialerJoinMap.KeyPadNumeric.JoinSpan; i++)
                {
					var tempi = i;
                    trilist.SetSigTrueAction((dialerJoinMap.KeyPadNumeric.JoinNumber + (uint)i + dialerLineOffset), () => dialer.SendKeypad((TesiraDspDialer.EKeypadKeys)(tempi)));
                }

                trilist.SetSigTrueAction((dialerJoinMap.KeyPadStar.JoinNumber + dialerLineOffset), () => dialer.SendKeypad(TesiraDspDialer.EKeypadKeys.Star));
                trilist.SetSigTrueAction((dialerJoinMap.KeyPadPound.JoinNumber + dialerLineOffset), () => dialer.SendKeypad(TesiraDspDialer.EKeypadKeys.Pound));
                trilist.SetSigTrueAction((dialerJoinMap.KeyPadClear.JoinNumber + dialerLineOffset), () => dialer.SendKeypad(TesiraDspDialer.EKeypadKeys.Clear));
                trilist.SetSigTrueAction((dialerJoinMap.KeyPadBackspace.JoinNumber + dialerLineOffset), () => dialer.SendKeypad(TesiraDspDialer.EKeypadKeys.Backspace));

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

#if SERIES4
            this.LogVerbose("There are {0} Meter Control Points", Meters.Count);
#else
            Debug.Console(2, "There are {0} Meter Control Points", Meters.Count);
#endif
            foreach (var item in Meters)
            {
                var meter = item.Value;
                var data = meter.BridgeIndex;
                if (data == null) continue;
                var x = (uint)(data - 1);

#if SERIES4
                this.LogVerbose("AddingMeterBridge {0} | Join:{1}", meter.Key, meterJoinMap.Label.JoinNumber);
#else
                Debug.Console(2, "AddingMeterBridge {0} | Join:{1}", meter.Key, meterJoinMap.Label.JoinNumber);
#endif

                meter.MeterFeedback.LinkInputSig(trilist.UShortInput[meterJoinMap.Meter.JoinNumber + x]);
                meter.NameFeedback.LinkInputSig(trilist.StringInput[meterJoinMap.Label.JoinNumber + x]);
                meter.SubscribedFeedback.LinkInputSig(trilist.BooleanInput[meterJoinMap.Subscribe.JoinNumber + x]);

                trilist.SetSigTrueAction(meterJoinMap.Subscribe.JoinNumber, meter.Subscribe);
                trilist.SetSigFalseAction(meterJoinMap.Subscribe.JoinNumber, meter.UnSubscribe);

            }

#if SERIES4
            this.LogVerbose("There are {0} Crosspoint State Control Points", CrosspointStates.Count);
#else
            Debug.Console(2, "There are {0} Crosspoint State Control Points", CrosspointStates.Count);
#endif
            foreach (var item in CrosspointStates)
            {
                var xpointState = item.Value;
				var joinOffset = ((xpointState.BridgeIndex - 1) * 3);
                if (joinOffset == null) continue;


				var channel = item.Value;
				var data = channel.BridgeIndex;
				if (data == null) continue;

#if SERIES4
                this.LogVerbose("Adding Crosspoint State ControlPoint {0} | JoinStart:{1}", xpointState.Key, (crosspointStateJoinMap.Toggle.JoinNumber + joinOffset));
#else
                Debug.Console(2, "Adding Crosspoint State ControlPoint {0} | JoinStart:{1}", xpointState.Key, (crosspointStateJoinMap.Toggle.JoinNumber + joinOffset));
#endif
                xpointState.CrosspointStateFeedback.LinkInputSig(trilist.BooleanInput[(uint)(crosspointStateJoinMap.Toggle.JoinNumber + joinOffset)]);
                xpointState.CrosspointStateFeedback.LinkInputSig(trilist.BooleanInput[(uint)(crosspointStateJoinMap.On.JoinNumber + joinOffset)]);

                trilist.SetSigTrueAction((uint)(crosspointStateJoinMap.Toggle.JoinNumber + joinOffset), xpointState.StateToggle);
                trilist.SetSigTrueAction((uint)(crosspointStateJoinMap.On.JoinNumber + joinOffset), xpointState.StateOn);
                trilist.SetSigTrueAction((uint)(crosspointStateJoinMap.Off.JoinNumber + joinOffset), xpointState.StateOff);
				

            }

#if SERIES4
            this.LogVerbose("There are {0} Room Combiner Control Points", RoomCombiners.Count);
#else
            Debug.Console(2, "There are {0} Room Combiner Control Points", RoomCombiners.Count);
#endif
            //x = 0;
            foreach (var item in RoomCombiners)
            {
                var roomCombiner = item.Value;
                var data = roomCombiner.BridgeIndex;
                if (data == null) continue;
                var y = (uint) data;

                var x = y > 1 ? ((y - 1) * 6) : 0;

#if SERIES4
                this.LogVerbose("Tesira Room Combiner {0} connect", x);
#else
                Debug.Console(2, "Tesira Room Combiner {0} connect", x);
#endif

                var genericChannel = roomCombiner as IBasicVolumeWithFeedback;
                if (!roomCombiner.Enabled) continue;
                
#if SERIES4
                this.LogVerbose("TesiraChannel {0} Is Enabled", x);
#else
                Debug.Console(2, "TesiraChannel {0} Is Enabled", x);
#endif

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

            trilist.OnlineStatusChange += (d, args) =>
            {
                if (!args.DeviceOnLine) return;

                foreach (var feedback in Feedbacks)
                {
                    feedback.FireUpdate();
                }

            };
        }

        public void UpdateDeviceInfo()
        {
            DevInfo.UpdateDeviceInfo();
        }
    }
}
