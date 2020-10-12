using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.CrestronThread;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using PepperDash.Essentials.Core.Config;
using Crestron.SimplSharpPro.DeviceSupport;
using Tesira_DSP_EPI.Bridge.JoinMaps;
using PepperDash.Essentials.Core.Bridges;
using Feedback = PepperDash.Essentials.Core.Feedback;

namespace Tesira_DSP_EPI
{
    public class TesiraDsp : EssentialsBridgeableDevice
    {
        /// <summary>
        /// Collection of all Device Feedbacks
        /// </summary>
        public FeedbackCollection<Feedback> Feedbacks;

        /// <summary>
        /// Date Returning from Device
        /// </summary>
        public string DeviceRx { get; set; }

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
		public GenericCommunicationMonitor CommunicationMonitor { get; private set; }

        /// <summary>
        /// COmmand Responses from Device
        /// </summary>
		public StringFeedback CommandPassthruFeedback { get; set; }

        /// <summary>
        /// True when all components have been subscribed
        /// </summary>
        public bool IsSubscribed;

		private CTimer _watchDogTimer;

        private readonly CCriticalSection _subscriptionLock = new CCriticalSection();

        private Thread _subscribeThread;

        private readonly bool _isSerialComm;

        private Dictionary<string, TesiraDspFaderControl> Faders { get; set; }
        private Dictionary<string, TesiraDspDialer> Dialers { get; set; }
        private Dictionary<string, TesiraDspSwitcher> Switchers { get; set; }
        private Dictionary<string, TesiraDspStateControl> States { get; set; }
        private Dictionary<string, TesiraDspMeter> Meters { get; set; }
        private Dictionary<string, TesiraDspCrosspointState> CrosspointStates { get; set; }
        private Dictionary<string, TesiraDspRoomCombiner> RoomCombiners { get; set; }
        private Dictionary<string, TesiraDspPresets> Presets { get; set; }
        private List<ISubscribedComponent> ControlPointList { get; set; }

		private bool WatchDogSniffer { get; set; }

		readonly DeviceConfig _dc;

        readonly CrestronQueue _commandQueue;

		bool _commandQueueInProgress;

		public bool ShowHexResponse { get; set; }

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

			Debug.Console(0, this, "Made it to device constructor");

			_commandQueue = new CrestronQueue(100);
			Communication = comm;
			var socket = comm as ISocketStatus;

			if (socket != null)
			{
				// This instance uses IP control
				socket.ConnectionChange += socket_ConnectionChange;
				_isSerialComm = false;
			}
			else
			{
				// This instance uses RS-232 control
				_isSerialComm = true;
			}
			PortGather = new CommunicationGather(Communication, "\x0D\x0A");
			PortGather.LineReceived += Port_LineReceived;

			CommandPassthruFeedback = new StringFeedback(() => DeviceRx);

			CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, 20000, 120000, 300000, () => SendLine("SESSION set verbose false"));

			// Custom monitoring, will check the heartbeat tracker count every 20s and reset. Heartbeat sbould be coming in every 20s if subscriptions are valid
			DeviceManager.AddDevice(CommunicationMonitor);

            ControlPointList = new List<ISubscribedComponent>();

            //Initialize Dictionaries
            Feedbacks = new FeedbackCollection<Feedback>();
            Faders = new Dictionary<string, TesiraDspFaderControl>();
            Presets = new Dictionary<string, TesiraDspPresets>();
            Dialers = new Dictionary<string, TesiraDspDialer>();
            Switchers = new Dictionary<string, TesiraDspSwitcher>();
            States = new Dictionary<string, TesiraDspStateControl>();
            Meters = new Dictionary<string, TesiraDspMeter>();
            CrosspointStates = new Dictionary<string, TesiraDspCrosspointState>();
            RoomCombiners = new Dictionary<string, TesiraDspRoomCombiner>();

            CrestronEnvironment.ProgramStatusEventHandler += CrestronEnvironment_ProgramStatusEventHandler;


			CommunicationMonitor.StatusChange += CommunicationMonitor_StatusChange;
			CrestronConsole.AddNewConsoleCommand(SendLine, "send" + Key, "", ConsoleAccessLevelEnum.AccessOperator);
			CrestronConsole.AddNewConsoleCommand(s => Communication.Connect(), "con" + Key, "", ConsoleAccessLevelEnum.AccessOperator);

            _subscribeThread = new Thread(o => HandleAttributeSubscriptions(), null, Thread.eThreadStartOptions.CreateSuspended)
            {
                Priority = Thread.eThreadPriority.LowestPriority
            };

            Feedbacks.Add(CommunicationMonitor.IsOnlineFeedback);
            Feedbacks.Add(CommandPassthruFeedback);

            //Start CommnicationMonitor in PostActivation phase
            AddPostActivationAction(() =>
            {
                Communication.Connect();
                if (_isSerialComm)
                {
                    CommunicationMonitor.Start();
                }
            });

            CreateDspObjects();
        }

        void CrestronEnvironment_ProgramStatusEventHandler(eProgramStatusEventType programEventType)
        {
            if (programEventType != eProgramStatusEventType.Stopping) return;

            _watchDogTimer.Stop();
            _watchDogTimer.Dispose();
            CommunicationMonitor.Stop();
            Communication.Disconnect();
        }

        private void CreateDspObjects()
        {
            Debug.Console(2, "Creating DSP Objects");

            var props = JsonConvert.DeserializeObject<TesiraDspPropertiesConfig>(_dc.Properties.ToString());

            if (props == null) return;

            Debug.Console(2, this, "Props Exists");
            Debug.Console(2, this, "Here's the props string\n {0}", _dc.Properties.ToString());

            Faders.Clear();
            Presets.Clear();
            Dialers.Clear();
            States.Clear();
            Switchers.Clear();
            ControlPointList.Clear();
            Meters.Clear();
            RoomCombiners.Clear();

            if (props.FaderControlBlocks != null)
            {
                Debug.Console(2, this, "levelControlBlocks is not null - There are {0} of them", props.FaderControlBlocks.Count());
                foreach (var block in props.FaderControlBlocks)
                {
                    var key = block.Key;
                    Debug.Console(2, this, "LevelControlBlock Key - {0}", key);
                    var value = block.Value;

                    Faders.Add(key, new TesiraDspFaderControl(key, value, this));
                    Debug.Console(2, this, "Added LevelControlPoint {0} LevelTag: {1} MuteTag: {2}", key, value.LevelInstanceTag, value.MuteInstanceTag);
                    if (block.Value.Enabled)
                    {
                        //Add ControlPoint to the list for the watchdog
                        ControlPointList.Add(Faders[key]);
                    }
                }
            }

            if (props.SwitcherControlBlocks != null)
            {
                Debug.Console(2, this, "switcherControlBlocks is not null - There are {0} of them", props.FaderControlBlocks.Count());
                foreach (var block in props.SwitcherControlBlocks)
                {
                    var key = block.Key;
                    Debug.Console(2, this, "SwitcherControlBlock Key - {0}", key);
                    var value = block.Value;

                    Switchers.Add(key, new TesiraDspSwitcher(key, value, this));
                    Debug.Console(2, this, "Added TesiraSwitcher {0} InstanceTag {1}", key, value.SwitcherInstanceTag);

                    if (block.Value.Enabled)
                    {
                        //Add ControlPoint to the list for the watchdog

                        ControlPointList.Add(Switchers[key]);
                    }
                }
            }

            if (props.DialerControlBlocks != null)
            {
                Debug.Console(2, this, "DialerControlBlocks is not null - There are {0} of them", props.DialerControlBlocks.Count());
                foreach (var block in props.DialerControlBlocks)
                {

                    var key = block.Key;
                    Debug.Console(2, this, "LevelControlBlock Key - {0}", key);
                    var value = block.Value;
                    Dialers.Add(key, new TesiraDspDialer(key, value, this));
                    Debug.Console(2, this, "Added DspDialer {0} ControlStatusTag: {1} DialerTag: {2}", key, value.ControlStatusInstanceTag, value.DialerInstanceTag);

                    if (block.Value.Enabled)
                    {
                        ControlPointList.Add(Dialers[key]);
                    }

                }
            }

            if (props.StateControlBlocks != null)
            {
                Debug.Console(2, this, "stateControlBlocks is not null - There are {0} of them", props.StateControlBlocks.Count());
                foreach (var block in props.StateControlBlocks)
                {

                    var key = block.Key;
                    var value = block.Value;
                    States.Add(key, new TesiraDspStateControl(key, value, this));
                    Debug.Console(2, this, "Added DspState {0} InstanceTag: {1}", key, value.StateInstanceTag);

                    if (block.Value.Enabled)
                        ControlPointList.Add(States[key]);
                }
            }

            if (props.Presets != null)
            {
                foreach (var preset in props.Presets)
                {
                    var value = preset.Value;
                    var key = preset.Key;
                    Presets.Add(key, value);
                    Debug.Console(2, this, "Added Preset {0} {1}", value.Label, value.PresetName);
                }
            }

            if (props.MeterControlBlocks != null)
            {
                foreach (var meter in props.MeterControlBlocks)
                {
                    var key = meter.Key;
                    var value = meter.Value;
                    Meters.Add(key, new TesiraDspMeter(key, value, this));
                    Debug.Console(2, this, "Adding Meter {0} InstanceTag: {1}", key, value.MeterInstanceTag);

                    if (value.Enabled)
                    {
                        ControlPointList.Add(Meters[key]);
                    }
                }
            }

            if (props.CrosspointStateControlBlocks != null)
            {
                foreach (var mixer in props.CrosspointStateControlBlocks)
                {
                    var key = mixer.Key;
                    var value = mixer.Value;
                    CrosspointStates.Add(key, new TesiraDspCrosspointState(key, value, this));
                    Debug.Console(2, this, "Adding Mixer {0} InstanceTag: {1}", key, value.MatrixInstanceTag);

                    if (value.Enabled)
                    {
                        ControlPointList.Add(CrosspointStates[key]);
                    }
                }
            }
            if (props.RoomCombinerControlBlocks == null) return;
            foreach (var roomCombiner in props.RoomCombinerControlBlocks)
            {
                var key = roomCombiner.Key;
                var value = roomCombiner.Value;
                RoomCombiners.Add(key, new TesiraDspRoomCombiner(key, value, this));
                Debug.Console(2, this, "Adding Mixer {0} InstanceTag: {1}", key, value.RoomCombinerInstanceTag);

                if (value.Enabled)
                {
                    ControlPointList.Add(RoomCombiners[key]);
                }
            }

            //Keep me at the end of this method!
            DeviceManager.AddDevice(new TesiraDspDeviceInfo(String.Format("{0}--DeviceInfo", Key), String.Format("{0}--DeviceInfo", Name, Presets), this, Presets));
        }


        #region Communications

        void CommunicationMonitor_StatusChange(object sender, MonitorStatusChangeEventArgs e)
		{
			Debug.Console(2, this, "Communication monitor state: {0}", CommunicationMonitor.Status);
			if (e.Status == MonitorStatus.IsOk)
			{
                if (_subscribeThread.ThreadState != Thread.eThreadStates.ThreadRunning)
                {
                    _subscribeThread.Start();
                }
			}
			else if (e.Status != MonitorStatus.IsOk)
			{
				StopWatchDog();
			}
		}

		void socket_ConnectionChange(object sender, GenericSocketStatusChageEventArgs e)
		{
			Debug.Console(2, this, "Socket Status Change: {0}", e.Client.ClientStatus.ToString());

			if (e.Client.IsConnected)
			{
				//SubscribeToAttributes();
			}
			else
			{
				// Cleanup items from this session
				_commandQueue.Clear();
				_commandQueueInProgress = false;
			}
        }

        #endregion

        #region Watchdog

        private void StartWatchDog()
        {
            if (_watchDogTimer == null)
            {
                _watchDogTimer = new CTimer(o => CheckWatchDog(), null, 20000, 20000);
            }
            else
            {
                _watchDogTimer.Reset(20000, 20000);
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
            Debug.Console(2, this, "The Watchdog is on the hunt!");
            if (!WatchDogSniffer)
            {
                Debug.Console(2, this, "The Watchdog is picking up a scent!");
                var random = new Random(DateTime.Now.Millisecond + DateTime.Now.Second + DateTime.Now.Minute
                    + DateTime.Now.Hour + DateTime.Now.Day + DateTime.Now.Month + DateTime.Now.Year);

                var watchDogSubject = SelectWatchDogSubject(random);

                if (!watchDogSubject.IsSubscribed)
                {
                    Debug.Console(2, this, "The Watchdog was wrong - that's just an old shoe.  Nothing is subscribed.");
                    return;
                }
                Debug.Console(2, this, "The Watchdog is sniffing {0}", watchDogSubject.Key);

                WatchDogSniffer = true;

                watchDogSubject.Subscribe();
            }
            else
            {
                Debug.Console(2, this, "The WatchDog smells something foul....let's resubscribe!");
                Resubscribe();
            }
        }

        private ISubscribedComponent SelectWatchDogSubject(Random random)
        {
            var watchDogSubject = ControlPointList[random.Next(0, ControlPointList.Count)];
            while(watchDogSubject.IsSubscribed == false)
                watchDogSubject = ControlPointList[random.Next(0, ControlPointList.Count)];
            return watchDogSubject;
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

			Debug.Console(1, this, "TX: '{0}'", s);
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

			Debug.Console(1, this, "TX: '{0}'", s);
			Communication.SendText(s);
		}

        private void Port_LineReceived(object dev, GenericCommMethodReceiveTextArgs args)
        {
            if (Debug.Level == 2)
                Debug.Console(2, this, "RX: '{0}'",
                    ShowHexResponse ? ComTextHelper.GetEscapedText(args.Text) : args.Text);

            //Debug.Console(1, this, "RX: '{0}'", args.Text);

            try
            {

                DeviceRx = args.Text;

                CommandPassthruFeedback.FireUpdate();

                if (args.Text.IndexOf("Welcome to the Tesira Text Protocol Server...", StringComparison.Ordinal) > -1)
                {
                    // Indicates a new TTP session
                    // moved to CustomActivate() method
                    CommunicationMonitor.Start();
                    if (_subscribeThread.ThreadState != Thread.eThreadStates.ThreadRunning)
                    {
                        _subscribeThread.Start();
                    }
                }
                else if (args.Text.IndexOf("! ", StringComparison.Ordinal) > -1)
                {
                    // response is from a subscribed attribute

                    //(if(args.Text

                    const string pattern = "! [\\\"](.*?[^\\\\])[\\\"] (.*)";

                    var match = Regex.Match(args.Text, pattern);

                    if (!match.Success) return;

                    var customName = match.Groups[1].Value;
                    var value = match.Groups[2].Value;

                    AdvanceQueue(args.Text);

                    foreach (var controlPoint in Faders.Where(controlPoint => customName == controlPoint.Value.LevelCustomName || customName == controlPoint.Value.MuteCustomName))
                    {
                        controlPoint.Value.ParseSubscriptionMessage(customName, value);
                        return;
                    }
                    foreach (var controlPoint in Dialers.Where(controlPoint => customName == controlPoint.Value.AutoAnswerCustomName || customName == controlPoint.Value.ControlStatusCustomName || customName == controlPoint.Value.DialerCustomName))
                    {
                        controlPoint.Value.ParseSubscriptionMessage(customName, value);
                        return;
                    }
                    foreach (var controlPoint in States.Where(controlPoint => customName == controlPoint.Value.StateCustomName))
                    {
                        controlPoint.Value.ParseSubscriptionMessage(customName, value);
                        return;
                    }

                    foreach (var controlPoint in Switchers.Where(controlPoint => customName == controlPoint.Value.SelectorCustomName))
                    {
                        controlPoint.Value.ParseSubscriptionMessage(customName, value);
                        return;
                    }

                    foreach (var controlPoint in Meters.Where(controlPoint => customName == controlPoint.Value.MeterCustomName))
                    {
                        controlPoint.Value.ParseSubscriptionMessage(customName, value);
                        return;
                    }

                    // same for dialers
                    // same for switchers

                }
                else if (args.Text.IndexOf("+OK", StringComparison.Ordinal) > -1)
                {
                    if (args.Text == "+OK")       // Check for a simple "+OK" only 'ack' repsonse or a list response and ignore
                        return;
                    // response is not from a subscribed attribute.  From a get/set/toggle/increment/decrement command
                    //string pattern = "(?<=\" )(.*?)(?=\\+)";
                    //string data = Regex.Replace(args.Text, pattern, "");

                    AdvanceQueue(args.Text);

                }
                else if (args.Text.IndexOf("-ERR", StringComparison.Ordinal) > -1)
                {
                    // Error response
                    Debug.Console(2, this, "Error From DSP: '{0}'", args.Text);
                    switch (args.Text)
                    {
                        case "-ERR ALREADY_SUBSCRIBED":
                            {
                                WatchDogSniffer = false;
                                AdvanceQueue(args.Text);
                                break;
                            }


                        default:
                            {
                                WatchDogSniffer = false;

                                AdvanceQueue(args.Text);
                                break;
                            }
                    }

                }
            }
            catch (Exception e)
            {
                if (Debug.Level == 2)
                    Debug.Console(2, this, "Error parsing response: '{0}'\n{1}", args.Text, e);
            }

        }

        #endregion

        #region Queue Management

        /// <summary>
		/// Adds a command from a child module to the queue
		/// </summary>
        /// <param name="commandToEnqueue">Command object from child module</param>
		public void EnqueueCommand(QueuedCommand commandToEnqueue)
		{
			_commandQueue.Enqueue(commandToEnqueue);
			Debug.Console(1, this, "Command (QueuedCommand) Enqueued '{0}'.  CommandQueue has '{1}' Elements.", commandToEnqueue.Command, _commandQueue.Count);
			if (!_commandQueueInProgress)
				SendNextQueuedCommand();
		}

		/// <summary>
		/// Adds a raw string command to the queue
		/// </summary>
		/// <param name="command">String to enqueue</param>
		public void EnqueueCommand(string command)
		{
			_commandQueue.Enqueue(command);
			Debug.Console(1, this, "Command (string) Enqueued '{0}'.  CommandQueue has '{1}' Elements.", command, _commandQueue.Count);
			if (!_commandQueueInProgress)
				SendNextQueuedCommand();
		}

		/// <summary>
		/// Sends the next queued command to the DSP
		/// </summary>
		void SendNextQueuedCommand()
		{
            Debug.Console(2, this, "Attemption to send a queued commend");
		    if (!Communication.IsConnected || _commandQueue.IsEmpty) return;
		    _commandQueueInProgress = true;

		    if (_commandQueue.Peek() is QueuedCommand)
		    {
		        var nextCommand = (QueuedCommand)_commandQueue.Peek();
		        SendLine(nextCommand.Command);
		    }

		    else
		    {
		        var nextCommand = (string)_commandQueue.Peek();
		        SendLine(nextCommand);
		    }
		}

        private void AdvanceQueue(string cmd)
        {
            if (_commandQueue.IsEmpty) return;

            if (_commandQueue.Peek() is QueuedCommand)
            {
                // Expected response belongs to a child class
                var tempCommand = (QueuedCommand)_commandQueue.TryToDequeue();
                Debug.Console(1, this, "Command Dequeued. CommandQueue Size: {0} {1}", _commandQueue.Count, tempCommand.Command);
                tempCommand.ControlPoint.ParseGetMessage(tempCommand.AttributeCode, cmd);
            }

            Debug.Console(2, this, "Commmand queue {0}.", _commandQueue.IsEmpty ? "is empty" : "has entries");

            if (_commandQueue.IsEmpty)
                _commandQueueInProgress = false;
            else
                SendNextQueuedCommand();
        }

        /// <summary>
        /// Contains all data for a component command
        /// </summary>
        public class QueuedCommand
        {
            public string Command { get; set; }
            public string AttributeCode { get; set; }
            public ISubscribedComponent ControlPoint { get; set; }
        }


        #endregion

		#region Presets

		public void RunPresetNumber(ushort n)
		{
		    var presetValue = Presets.FirstOrDefault(o => o.Value.PresetIndex == n).Value;

			Debug.Console(2, this, "Attempting to run preset {0}", n);
			if (presetValue != null)
			{
			    if (!String.IsNullOrEmpty(presetValue.PresetName))
			    {
			        RunPreset(presetValue.PresetName);
			    }

                else
                    RunPreset(presetValue.PresetId);
			}
		}

		/// <summary>
		/// Sends a command to execute a preset
		/// </summary>
        /// <param name="name">Preset Name</param>
		public void RunPreset(string name)
		{
            SendLine(string.Format("DEVICE recallPresetByName \"{0}\"", name));
		}

        /// <summary>
        /// Sends a command to execute a preset
        /// </summary>
        /// <param name="id">Preset Id</param>
        public void RunPreset(int id)
        {
            SendLine(string.Format("DEVICE recallPreset {0}", id));
        }

		#endregion

		#region SubscriptionHandling        

        #region Unsubscribe

        private void UnsubscribeFromComponents()
		{
			foreach (var control in Dialers.Select(dialer => dialer.Value))
			{
			    UnsubscribeFromComponent(control);
			}

			foreach (var control in Switchers.Select(switcher => switcher.Value))
			{
			    UnsubscribeFromComponent(control);
			}

			foreach (var control in States.Select(state => state.Value))
			{
			    UnsubscribeFromComponent(control);
			}

            foreach (var control in Faders.Select(level => level.Value))
			{
			    UnsubscribeFromComponent(control);
			}

			foreach (var control in RoomCombiners.Select(roomCombiner => roomCombiner.Value))
			{
			    UnsubscribeFromComponent(control);
			}
		}

        private void UnsubscribeFromComponent(ISubscribedComponent data)
		{
            if (!data.Enabled) return;
            Debug.Console(2, this, "Unsubscribing From Object - {0}", data.InstanceTag1);
            data.Unsubscribe();
		}

		#endregion

		#region Subscribe

		private void SubscribeToComponents()
		{
			foreach (var control in Dialers.Select(dialer => dialer.Value))
			{
			    SubscribeToComponent(control);
			}

			foreach (var control in Switchers.Select(switcher => switcher.Value))
			{
                SubscribeToComponent(control);
            }

			foreach (var control in States.Select(state => state.Value))
			{
                SubscribeToComponent(control);
            }

            foreach (var control in Faders.Select(level => level.Value))
			{
                SubscribeToComponent(control);
            }

			foreach (var control in RoomCombiners.Select(roomCombiner => roomCombiner.Value))
			{
                SubscribeToComponent(control);
            }
		}

        private void SubscribeToComponent(ISubscribedComponent data)
        {
            if (data == null) return;
            if (!data.Enabled) return;
            Debug.Console(2, this, "Subscribing To Object - {0}", data.InstanceTag1);
            data.Subscribe();
		}

		#endregion

        private void Resubscribe()
        {
            Debug.Console(0, this, "Issue Detected with device subscriptions - resubscribing to all controls");
            StopWatchDog();
            if (_subscribeThread.ThreadState != Thread.eThreadStates.ThreadRunning)
            {
                _subscribeThread.Start();
            }
        }

        private object HandleAttributeSubscriptions()
        {
            _subscriptionLock.Enter();
            SendLine("SESSION set verbose false");
            try
            {
                if (_isSerialComm)
                    UnsubscribeFromComponents();

                //Subscribe
                SubscribeToComponents();

                StartWatchDog();
                if (!_commandQueueInProgress)
                    SendNextQueuedCommand();
            }
            catch (Exception ex)
            {
                Debug.ConsoleWithLog(2, this, "Error Subscribing: '{0}'", ex);
                _subscriptionLock.Leave();
                //_subscriptionLock.Leave();
            }
            finally
            {
                _subscriptionLock.Leave();
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
                bridge.AddJoinMap(String.Format("{0}--DeviceInfoJoinMap", Key), deviceJoinMap);
                bridge.AddJoinMap(String.Format("{0}--DialerJoinMap", Key), dialerJoinMap);
                bridge.AddJoinMap(String.Format("{0}--FaderJoinMap", Key), faderJoinMap);
                bridge.AddJoinMap(String.Format("{0}--StateJoinMap", Key), stateJoinMap);
                bridge.AddJoinMap(String.Format("{0}--SwitcherJoinMap", Key), switcherJoinMap);
                bridge.AddJoinMap(String.Format("{0}--PresetsJoinMap", Key), presetJoinMap);
                bridge.AddJoinMap(String.Format("{0}--MeterJoinMap", Key), meterJoinMap);
                bridge.AddJoinMap(String.Format("{0}--CrosspointStateJoinMap", Key), crosspointStateJoinMap);
                bridge.AddJoinMap(String.Format("{0}--RoomCombinerJoinMap", Key), roomCombinerJoinMap);
            }

            Debug.Console(1, this, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));

            //var comm = DspDevice as IBasicCommunication;


            CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[deviceJoinMap.IsOnline.JoinNumber]);
            CommandPassthruFeedback.LinkInputSig(trilist.StringInput[deviceJoinMap.CommandPassThru.JoinNumber]);
            trilist.SetStringSigAction(deviceJoinMap.DirectPreset.JoinNumber, RunPreset);

            trilist.SetStringSigAction(deviceJoinMap.CommandPassThru.JoinNumber, SendLineRaw);


            //Level and Mute Control
            Debug.Console(2, this, "There are {0} Level Control Points", Faders.Count());
            foreach (var item in Faders)
            {
                var channel = item.Value;
                var data = channel.BridgeIndex;
                if (data == null) continue;
                var x = (uint) data;
                //var TesiraChannel = channel.Value as Tesira.DSP.EPI.TesiraDspLevelControl;
                Debug.Console(2, "TesiraChannel {0} connect", x);

                var genericChannel = channel as IBasicVolumeWithFeedback;
                
                if (!channel.Enabled) continue;

                Debug.Console(2, this, "TesiraChannel {0} Is Enabled", x);

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
            Debug.Console(2, this, "There are {0} State Control Points", States.Count());
            foreach (var item in States)
            {
                var state = item.Value;
                var data = state.BridgeIndex;
                if (data == null) continue;

                var x = (uint)data - 1;
                Debug.Console(2, this, "Tesira State {0} connect to {1}", state.Key, x);

                if (!state.Enabled) continue;
                
                Debug.Console(2, this, "Tesira State {0} at {1} is Enabled", state.Key, x);

                state.StateFeedback.LinkInputSig(trilist.BooleanInput[stateJoinMap.Toggle.JoinNumber + x]);
                state.StateFeedback.LinkInputSig(trilist.BooleanInput[stateJoinMap.On.JoinNumber + x]);
                state.StateFeedback.LinkComplementInputSig(trilist.BooleanInput[stateJoinMap.Off.JoinNumber + x]);
                state.NameFeedback.LinkInputSig(trilist.StringInput[stateJoinMap.Label.JoinNumber + x]);

                trilist.SetSigTrueAction(stateJoinMap.Toggle.JoinNumber + x, state.StateToggle);
                trilist.SetSigTrueAction(stateJoinMap.On.JoinNumber + x, state.StateOn);
                trilist.SetSigTrueAction(stateJoinMap.Off.JoinNumber + x, state.StateOff);
            }


            //Source Selectors
            Debug.Console(2, this, "There are {0} SourceSelector Control Points", Switchers.Count());
            foreach (var item in Switchers)
            {
                var switcher = item.Value;
                var data = switcher.BridgeIndex;
                if (data == null) continue;
                var y = (uint) data;
                var x = (ushort)(((y - 1) * 2) + 1);
                //3 switchers
                //((1 - 1) * 2) + 1 = 1
                //((2 - 1) * 2) + 1 = 3
                //((3 - 1) * 2) + 1 = 5

                Debug.Console(2, this, "Tesira Switcher {0} connect to {1}", switcher.Key, y);

                if (!switcher.Enabled) continue;
                
                
                Debug.Console(2, this, "Tesira Switcher {0} is Enabled", x);

                var s = switcher as IRoutingWithFeedback;
                s.SourceIndexFeedback.LinkInputSig(trilist.UShortInput[switcherJoinMap.Index.JoinNumber + x]);

                trilist.SetUShortSigAction(switcherJoinMap.Index.JoinNumber + x, u => switcher.SetSource(u));

                switcher.NameFeedback.LinkInputSig(trilist.StringInput[switcherJoinMap.Label.JoinNumber + x]);
            }



            //Presets 

            trilist.SetStringSigAction(presetJoinMap.PresetName.JoinNumber, RunPreset);

            foreach (var preset in Presets)
            {
                var p = preset;
                var runPresetIndex = preset.Value.PresetIndex;
                var presetIndex = runPresetIndex - 1;
                trilist.StringInput[(uint)(presetJoinMap.PresetNameFeedback.JoinNumber + presetIndex)].StringValue = p.Value.Label;
                trilist.SetSigTrueAction((uint)(presetJoinMap.PresetSelection.JoinNumber + presetIndex), () => RunPresetNumber((ushort)runPresetIndex));
            }

            // VoIP Dialer

            uint lineOffset = 0;
            foreach (var line in Dialers)
            {
                var dialer = line.Value;
                var bridgeIndex = dialer.BridgeIndex;
                if (bridgeIndex == null) continue;

                var dialerLineOffset = lineOffset += 1;
                Debug.Console(2, "AddingDialerBRidge {0} {1} Offset", dialer.Key, dialerLineOffset);

                for (var i = 0; i < dialerJoinMap.KeyPadNumeric.JoinSpan; i++)
                {
                    trilist.SetSigTrueAction((dialerJoinMap.KeyPadNumeric.JoinNumber + (uint)i + dialerLineOffset), () => dialer.SendKeypad(TesiraDspDialer.EKeypadKeys.Num0));
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
                trilist.SetSigTrueAction(dialerJoinMap.EndCall.JoinNumber + dialerLineOffset, dialer.EndAllCalls);
                trilist.SetSigTrueAction(dialerJoinMap.OnHook.JoinNumber + dialerLineOffset, dialer.OnHook);
                trilist.SetSigTrueAction(dialerJoinMap.OffHook.JoinNumber + dialerLineOffset, dialer.OffHook);

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


                dialer.CallStateFeedback.LinkInputSig(trilist.UShortInput[dialerJoinMap.CallState.JoinNumber + dialerLineOffset]);

                lineOffset += 50;
            }

            Debug.Console(2, this, "There are {0} Meter Control Points", Meters.Count);
            foreach (var item in Meters)
            {
                var meter = item.Value;
                var data = meter.BridgeIndex;
                if (data == null) continue;
                var x = (uint)(data - 1);


                Debug.Console(2, this, "AddingMeterBridge {0} | Join:{1}", meter.Key, meterJoinMap.Label.JoinNumber);

                meter.MeterFeedback.LinkInputSig(trilist.UShortInput[meterJoinMap.Meter.JoinNumber + x]);
                meter.NameFeedback.LinkInputSig(trilist.StringInput[meterJoinMap.Label.JoinNumber + x]);
                meter.SubscribedFeedback.LinkInputSig(trilist.BooleanInput[meterJoinMap.Subscribe.JoinNumber + x]);

                trilist.SetSigTrueAction(meterJoinMap.Subscribe.JoinNumber, meter.Subscribe);
                trilist.SetSigFalseAction(meterJoinMap.Subscribe.JoinNumber, meter.UnSubscribe);

            }

            Debug.Console(2, this, "There are {0} Crosspoint State Control Points", CrosspointStates.Count);
            foreach (var item in CrosspointStates)
            {
                var xpointState = item.Value;
                var data = xpointState.BridgeIndex;
                if (data == null) continue;

                Debug.Console(2, this, "Adding Crosspoint State ControlPoint {0} | JoinStart:{1}", xpointState.Key, crosspointStateJoinMap.Label.JoinNumber);
                xpointState.CrosspointStateFeedback.LinkInputSig(trilist.BooleanInput[crosspointStateJoinMap.Toggle.JoinNumber]);
                xpointState.CrosspointStateFeedback.LinkInputSig(trilist.BooleanInput[crosspointStateJoinMap.On.JoinNumber]);

                trilist.SetSigTrueAction(crosspointStateJoinMap.Toggle.JoinNumber, xpointState.StateToggle);
                trilist.SetSigTrueAction(crosspointStateJoinMap.On.JoinNumber, xpointState.StateOn);
                trilist.SetSigTrueAction(crosspointStateJoinMap.Off.JoinNumber, xpointState.StateOff);

            }

            Debug.Console(2, this, "There are {0} Room Combiner Control Points", RoomCombiners.Count);
            //x = 0;
            foreach (var item in RoomCombiners)
            {
                var roomCombiner = item.Value;
                var data = roomCombiner.BridgeIndex;
                if (data == null) continue;
                var y = (uint) data;

                var x = y > 1 ? ((y - 1) * 6) : 0;

                Debug.Console(2, "Tesira Room Combiner {0} connect", x);

                var genericChannel = roomCombiner as IBasicVolumeWithFeedback;
                if (!roomCombiner.Enabled) continue;
                
                Debug.Console(2, this, "TesiraChannel {0} Is Enabled", x);

                roomCombiner.NameFeedback.LinkInputSig(trilist.StringInput[roomCombinerJoinMap.Label.JoinNumber + x]);
                roomCombiner.VisibleFeedback.LinkInputSig(trilist.BooleanInput[roomCombinerJoinMap.Visible.JoinNumber + x]);
                roomCombiner.ControlTypeFeedback.LinkInputSig(trilist.UShortInput[roomCombinerJoinMap.Type.JoinNumber + x]);
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

    }
}
