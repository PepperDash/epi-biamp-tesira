using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Devices;
using PepperDash.Essentials.Devices.Common.Codec;
using PepperDash.Essentials.Devices.Common.DSP;
using System.Text.RegularExpressions;
using Crestron.SimplSharp.Reflection;
using Newtonsoft.Json;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Bridges;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.Diagnostics;
using Tesira_DSP_EPI.Bridge;
using Crestron.SimplSharpPro.CrestronThread;

namespace Tesira_DSP_EPI
{
	public class TesiraDsp : ReconfigurableDevice, IBridge
	{

		public static void LoadPlugin()
		{
			DeviceFactory.AddFactoryForType("tesiradsp", TesiraDsp.BuildDevice);
		}

		public static TesiraDsp BuildDevice(DeviceConfig dc)
		{
			Debug.Console(2, "TesiraDsp config is null: {0}", dc == null);
			var comm = CommFactory.CreateCommForDevice(dc);
			Debug.Console(2, "TesiraDsp comm is null: {0}", comm == null);
			var newMe = new TesiraDsp(dc.Key, dc.Name, comm, dc);

			return newMe;
		}

		public string DeviceRx { get; set; }

		public IBasicCommunication Communication { get; private set; }
		public CommunicationGather PortGather { get; private set; }
		public GenericCommunicationMonitor CommunicationMonitor { get; private set; }

		public StringFeedback CommandPassthruFeedback { get; set; }

		public bool _isSubscribed;

		private CTimer _WatchDogTimer;

		private readonly CCriticalSection _SubscriptionLock = new CCriticalSection();

		private bool _IsSerialComm = false;

		//public TesiraDspWatchdog Watchdog;

		public Dictionary<uint, TesiraDspLevelControl> LevelControlPoints { get; private set; }
		public Dictionary<uint, TesiraDspDialer> Dialers { get; private set; }
		public Dictionary<uint, TesiraDspSwitcher> Switchers { get; private set; }
		public Dictionary<uint, TesiraDspStateControl> States { get; private set; }
		public Dictionary<uint, TesiraDspMeter> Meters { get; private set; }
		public Dictionary<uint, TesiraDspMatrixMixer> MatrixMixers { get; private set; }
		public Dictionary<uint, TesiraDspRoomCombiner> RoomCombiners { get; private set; }
		public List<TesiraDspPresets> PresetList = new List<TesiraDspPresets>();

		public List<TesiraDspControlPoint> ControlPointList { get; private set; }

		private bool WatchDogSniffer { get; set; }

		DeviceConfig _Dc;

		CrestronQueue _CommandQueue;

		bool CommandQueueInProgress = false;
		//uint HeartbeatTracker = 0;
		public bool ShowHexResponse { get; set; }
		public TesiraDsp(string key, string name, IBasicCommunication comm, DeviceConfig dc)
			: base(dc)
		{
			_Dc = dc;
			TesiraDspPropertiesConfig props = JsonConvert.DeserializeObject<TesiraDspPropertiesConfig>(dc.Properties.ToString());
			Debug.Console(0, this, "Made it to device constructor");

			//WatchDogList = new Dictionary<string, string>();

			//Watchdog = new TesiraDspWatchdog("WatchDog", this);

			_CommandQueue = new CrestronQueue(100);
			Communication = comm;
			var socket = comm as ISocketStatus;
			if (socket != null)
			{
				// This instance uses IP control
				socket.ConnectionChange += new EventHandler<GenericSocketStatusChageEventArgs>(socket_ConnectionChange);
				_IsSerialComm = false;
			}
			else
			{
				// This instance uses RS-232 control
				_IsSerialComm = true;
			}
			PortGather = new CommunicationGather(Communication, "\x0D\x0A");
			PortGather.LineReceived += this.Port_LineReceived;

			CommandPassthruFeedback = new StringFeedback(() => DeviceRx);

			CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, 20000, 120000, 300000, () => SendLine("SESSION set verbose false"));

			// Custom monitoring, will check the heartbeat tracker count every 20s and reset. Heartbeat sbould be coming in every 20s if subscriptions are valid
			DeviceManager.AddDevice(CommunicationMonitor);

			LevelControlPoints = new Dictionary<uint, TesiraDspLevelControl>();
			Dialers = new Dictionary<uint, TesiraDspDialer>();
			Switchers = new Dictionary<uint, TesiraDspSwitcher>();
			States = new Dictionary<uint, TesiraDspStateControl>();
			ControlPointList = new List<TesiraDspControlPoint>();
			Meters = new Dictionary<uint, TesiraDspMeter>();
			MatrixMixers = new Dictionary<uint, TesiraDspMatrixMixer>();
			RoomCombiners = new Dictionary<uint, TesiraDspRoomCombiner>();
			CommunicationMonitor.StatusChange += new EventHandler<MonitorStatusChangeEventArgs>(CommunicationMonitor_StatusChange);
			CrestronConsole.AddNewConsoleCommand(SendLine, "send" + Key, "", ConsoleAccessLevelEnum.AccessOperator);
			CrestronConsole.AddNewConsoleCommand(s => Communication.Connect(), "con" + Key, "", ConsoleAccessLevelEnum.AccessOperator);
			CreateDspObjects();
		}

		public override bool CustomActivate()
		{
			AddPostActivationAction(() =>
			{
				Communication.Connect();
				if (_IsSerialComm)
				{
					CommunicationMonitor.Start();
				}
			});
			return true;
		}

		void CommunicationMonitor_StatusChange(object sender, MonitorStatusChangeEventArgs e)
		{
			Debug.Console(2, this, "Communication monitor state: {0}", CommunicationMonitor.Status);
			if (e.Status == MonitorStatus.IsOk)
			{
				CrestronInvoke.BeginInvoke((o) => HandleAttributeSubscriptions());
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
				_CommandQueue.Clear();
				CommandQueueInProgress = false;
			}
		}

		private void StartWatchDog()
		{
			if (_WatchDogTimer != null)
			{
				_WatchDogTimer = new CTimer((o) => CheckWatchDog(), null, 20000, 20000);
			}
		}

		private void StopWatchDog()
		{
			if (_WatchDogTimer != null)
			{
				_WatchDogTimer.Stop();
				_WatchDogTimer.Dispose();
				_WatchDogTimer = null;
			}
		}

		public void CreateDspObjects()
		{
			Debug.Console(2, "Creating DSP Objects");
			TesiraDspPropertiesConfig props = JsonConvert.DeserializeObject<TesiraDspPropertiesConfig>(_Dc.Properties.ToString());

			if (props != null)
			{
				Debug.Console(2, this, "Props Exists");
				Debug.Console(2, this, "Here's the props string\n {0}", _Dc.Properties.ToString());
			}

			LevelControlPoints.Clear();
			PresetList.Clear();
			Dialers.Clear();
			States.Clear();
			Switchers.Clear();
			ControlPointList.Clear();
			Meters.Clear();
			RoomCombiners.Clear();



			if (props.levelControlBlocks != null)
			{
				Debug.Console(2, this, "levelControlBlocks is not null - There are {0} of them", props.levelControlBlocks.Count());
				foreach (KeyValuePair<uint, TesiraLevelControlBlockConfig> block in props.levelControlBlocks)
				{
					var key = block.Key;
					//string key = string.Format("{0}-{1}", block.Key, block.Value.levelInstanceTag);
					Debug.Console(2, this, "LevelControlBlock Key - {0}", key);
					var value = block.Value;

					//value.levelInstanceTag = value.levelInstanceTag;
					//value.muteInstanceTag = value.muteInstanceTag;

					this.LevelControlPoints.Add(key, new TesiraDspLevelControl(key, value, this));
					Debug.Console(2, this, "Added LevelControlPoint {0} LevelTag: {1} MuteTag: {2}", key, value.levelInstanceTag, value.muteInstanceTag);
					if (block.Value.enabled)
					{
						//DeviceManager.AddDevice(LevelControlPoints[key]);
						ControlPointList.Add(LevelControlPoints[key]);
					}
				}
			}

			if (props.switcherControlBlocks != null)
			{
				Debug.Console(2, this, "switcherControlBlocks is not null - There are {0} of them", props.levelControlBlocks.Count());
				foreach (KeyValuePair<uint, TesiraSwitcherControlBlockConfig> block in props.switcherControlBlocks)
				{
					var key = block.Key;
					Debug.Console(2, this, "SwitcherControlBlock Key - {0}", key);
					var value = block.Value;

					this.Switchers.Add(key, new TesiraDspSwitcher(key, value, this));
					Debug.Console(2, this, "Added TesiraSwitcher {0} InstanceTag {1}", key, value.switcherInstanceTag);

					if (block.Value.enabled)
						ControlPointList.Add(Switchers[key]);
				}
			}

			if (props.dialerControlBlocks != null)
			{
				Debug.Console(2, this, "dialerControlBlocks is not null - There are {0} of them", props.dialerControlBlocks.Count());
				foreach (KeyValuePair<uint, TesiraDialerControlBlockConfig> block in props.dialerControlBlocks)
				{

					var key = block.Key;
					Debug.Console(2, this, "LevelControlBlock Key - {0}", key);
					var value = block.Value;
					//value.controlStatusInstanceTag = value.controlStatusInstanceTag;
					//value.dialerInstanceTag = value.dialerInstanceTag;

					this.Dialers.Add(key, new TesiraDspDialer(key, value, this));
					Debug.Console(2, this, "Added DspDialer {0} ControlStatusTag: {1} DialerTag: {2}", key, value.controlStatusInstanceTag, value.dialerInstanceTag);
				}
			}

			if (props.stateControlBlocks != null)
			{
				Debug.Console(2, this, "stateControlBlocks is not null - There are {0} of them", props.stateControlBlocks.Count());
				foreach (KeyValuePair<uint, TesiraStateControlBlockConfig> block in props.stateControlBlocks)
				{

					var key = block.Key;
					var value = block.Value;
					//value.stateInstanceTag = value.stateInstanceTag;
					this.States.Add(key, new TesiraDspStateControl(key, value, this));
					Debug.Console(2, this, "Added DspState {0} InstanceTag: {1}", key, value.stateInstanceTag);

					if (block.Value.enabled)
						ControlPointList.Add(States[key]);
				}
			}

			if (props.presets != null)
			{
				foreach (KeyValuePair<uint, TesiraDspPresets> preset in props.presets)
				{
					var value = preset.Value;
					value.preset = value.preset;
					this.addPreset(value);
					Debug.Console(2, this, "Added Preset {0} {1}", value.label, value.preset);
				}
			}

			if (props.meterControlBlocks != null)
			{
				foreach (KeyValuePair<uint, TesiraMeterBlockConfig> meter in props.meterControlBlocks)
				{
					var key = meter.Key;
					var value = meter.Value;
					Meters.Add(key, new TesiraDspMeter(key, value, this));
					Debug.Console(2, this, "Adding Meter {0} InstanceTag: {1}", key, value.meterInstanceTag);

					if (value.enabled)
					{
						ControlPointList.Add(Meters[key]);
					}
				}
			}

			if (props.matrixMixerControlBlocks != null)
			{
				foreach (KeyValuePair<uint, TesiraMatrixMixerBlockConfig> mixer in props.matrixMixerControlBlocks)
				{
					var key = mixer.Key;
					var value = mixer.Value;
					MatrixMixers.Add(key, new TesiraDspMatrixMixer(key, value, this));
					Debug.Console(2, this, "Adding Mixer {0} InstanceTag: {1}", key, value.matrixInstanceTag);

					if (value.enabled)
					{
						ControlPointList.Add(MatrixMixers[key]);
					}
				}
			}
			if (props.roomCombinerControlBlocks != null)
			{
				foreach (KeyValuePair<uint, TesiraRoomCombinerBlockConfig> roomCombiner in props.roomCombinerControlBlocks)
				{
					var key = roomCombiner.Key;
					var value = roomCombiner.Value;
					RoomCombiners.Add(key, new TesiraDspRoomCombiner(key, value, this));
					Debug.Console(2, this, "Adding Mixer {0} InstanceTag: {1}", key, value.roomCombinerInstanceTag);

					if (value.enabled)
					{
						ControlPointList.Add(RoomCombiners[key]);
					}
				}
			}
		}


		private void AdvanceQueue(string cmd)
		{
			if (!_CommandQueue.IsEmpty)
			{
				if (_CommandQueue.Peek() is QueuedCommand)
				{
					// Expected response belongs to a child class
					QueuedCommand tempCommand = (QueuedCommand)_CommandQueue.TryToDequeue();
					Debug.Console(1, this, "Command Dequeued. CommandQueue Size: {0} {1}", _CommandQueue.Count, tempCommand.Command);
					tempCommand.ControlPoint.ParseGetMessage(tempCommand.AttributeCode, cmd);
				}
				else
				{
					// Expected response belongs to this class
					string temp = (string)_CommandQueue.TryToDequeue();
				}

				if (_CommandQueue.IsEmpty)
					CommandQueueInProgress = false;
				else
					SendNextQueuedCommand();

			}
		}


		void Port_LineReceived(object dev, GenericCommMethodReceiveTextArgs args)
		{
			if (Debug.Level == 2)
				Debug.Console(2, this, "RX: '{0}'",
					ShowHexResponse ? ComTextHelper.GetEscapedText(args.Text) : args.Text);

			//Debug.Console(1, this, "RX: '{0}'", args.Text);

			try
			{

				DeviceRx = args.Text;

				this.CommandPassthruFeedback.FireUpdate();

				if (args.Text.IndexOf("Welcome to the Tesira Text Protocol Server...") > -1)
				{
					// Indicates a new TTP session
					// moved to CustomActivate() method
					CommunicationMonitor.Start();
				}
				else if (args.Text.IndexOf("! ") > -1)
				{
					// response is from a subscribed attribute

					//(if(args.Text

					string pattern = "! [\\\"](.*?[^\\\\])[\\\"] (.*)";

					Match match = Regex.Match(args.Text, pattern);

					if (match.Success)
					{

						string key;
						string customName;
						string value;

						customName = match.Groups[1].Value;

						// Finds the key (everything before the '~' character
						key = customName.Substring(0, customName.IndexOf("~", 0) - 1);

						value = match.Groups[2].Value;
						AdvanceQueue(args.Text);

						foreach (KeyValuePair<uint, TesiraDspLevelControl> controlPoint in LevelControlPoints)
						{
							if (customName == controlPoint.Value.LevelCustomName || customName == controlPoint.Value.MuteCustomName)
							{
								controlPoint.Value.ParseSubscriptionMessage(customName, value);
								return;
							}
						}
						foreach (KeyValuePair<uint, TesiraDspDialer> controlPoint in Dialers)
						{

							if (customName == controlPoint.Value.AutoAnswerCustomName || customName == controlPoint.Value.ControlStatusCustomName ||
								customName == controlPoint.Value.DialerCustomName)
							{
								controlPoint.Value.ParseSubscriptionMessage(customName, value);
								return;
							}

						}
						foreach (KeyValuePair<uint, TesiraDspStateControl> controlPoint in States)
						{

							if (customName == controlPoint.Value.StateCustomName)
							{
								controlPoint.Value.ParseSubscriptionMessage(customName, value);
							}
						}

						foreach (KeyValuePair<uint, TesiraDspSwitcher> controlPoint in Switchers)
						{

							if (customName == controlPoint.Value.SelectorCustomName)
							{
								controlPoint.Value.ParseSubscriptionMessage(customName, value);
							}
						}

						foreach (KeyValuePair<uint, TesiraDspMeter> controlPoint in Meters)
						{
							if (customName == controlPoint.Value.MeterCustomName)
							{
								controlPoint.Value.ParseSubscriptionMessage(customName, value);
							}
						}

					}

					/// same for dialers
					/// same for switchers

				}
				else if (args.Text.IndexOf("+OK") > -1)
				{
					if (args.Text == "+OK")       // Check for a simple "+OK" only 'ack' repsonse or a list response and ignore
						return;
					// response is not from a subscribed attribute.  From a get/set/toggle/increment/decrement command
					//string pattern = "(?<=\" )(.*?)(?=\\+)";
					//string data = Regex.Replace(args.Text, pattern, "");

					AdvanceQueue(args.Text);

				}
				else if (args.Text.IndexOf("-ERR") > -1)
				{
					// Error response
					Debug.Console(2, this, "Error From DSP: '{0}'", args.Text);
					switch (args.Text)
					{
						case "-ERR ALREADY_SUBSCRIBED":
							{
								WatchDogSniffer = false;
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

		public void Resubscribe()
		{
			Debug.Console(0, this, "Issue Detected with device subscriptions - resubscribing to all controls");
			StopWatchDog();
			CrestronInvoke.BeginInvoke((o) => HandleAttributeSubscriptions());

			//SubscribeToAttributes();
		}

		public void CheckWatchDog()
		{
			Debug.Console(2, this, "Checking Watchdog!");
			if (!WatchDogSniffer)
			{
				Random random = new Random(DateTime.Now.Millisecond + DateTime.Now.Second + DateTime.Now.Minute
					+ DateTime.Now.Hour + DateTime.Now.Day + DateTime.Now.Month + DateTime.Now.Year);

				var WatchDogSubject = ControlPointList[random.Next(0, ControlPointList.Count)];

				Debug.Console(2, this, "Watchdog is checking {0}", WatchDogSubject.Key);

				WatchDogSniffer = true;

				WatchDogSubject.Subscribe();
			}
			else
			{
				CommunicationMonitor.Stop();
				Resubscribe();
			}
		}





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

		/// <summary>
		/// Adds a command from a child module to the queue
		/// </summary>
		/// <param name="command">Command object from child module</param>
		public void EnqueueCommand(QueuedCommand commandToEnqueue)
		{
			_CommandQueue.Enqueue(commandToEnqueue);
			Debug.Console(1, this, "Command (QueuedCommand) Enqueued '{0}'.  CommandQueue has '{1}' Elements.", commandToEnqueue.Command, _CommandQueue.Count);

			if (!CommandQueueInProgress)
				SendNextQueuedCommand();
		}

		/// <summary>
		/// Adds a raw string command to the queue
		/// </summary>
		/// <param name="command"></param>
		public void EnqueueCommand(string command)
		{
			_CommandQueue.Enqueue(command);
			Debug.Console(1, this, "Command (string) Enqueued '{0}'.  CommandQueue has '{1}' Elements.", command, _CommandQueue.Count);

			if (!CommandQueueInProgress)
				SendNextQueuedCommand();
		}

		/// <summary>
		/// Sends the next queued command to the DSP
		/// </summary>
		void SendNextQueuedCommand()
		{
			if (Communication.IsConnected && !_CommandQueue.IsEmpty)
			{
				CommandQueueInProgress = true;
				if (_CommandQueue.Peek() is QueuedCommand)
				{
					QueuedCommand nextCommand = new QueuedCommand();
					nextCommand = (QueuedCommand)_CommandQueue.Peek();
					SendLine(nextCommand.Command);
				}
				else
				{
					string nextCommand = (string)_CommandQueue.Peek();
					SendLine(nextCommand);
				}
			}

		}

		/// <summary>
		/// Initiates the subscription process to the DSP
		/// </summary>
		void HandleAttributeSubscriptions()
		{
			_SubscriptionLock.Enter();

			SendLine("SESSION set verbose false");
			try
			{
				//Unsubscribe
				UnsubscribeFromAttributes();

				//Subscribe
				SubscribeToAttributes();

				StartWatchDog();
				if (!CommandQueueInProgress)
					SendNextQueuedCommand();
			}
			catch (Exception ex)
			{
				Debug.ConsoleWithLog(2, this, "Error Subscribing: '{0}'", ex);
				_SubscriptionLock.Leave();
			}
			finally
			{
				_SubscriptionLock.Leave();
			}
		}


		public class QueuedCommand
		{
			public string Command { get; set; }
			public string AttributeCode { get; set; }
			public IParseMessage ControlPoint { get; set; }
		}

		protected override void CustomSetConfig(DeviceConfig config)
		{
			ConfigWriter.UpdateDeviceConfig(config);
		}

		public void SetIpAddress(string hostname)
		{
			try
			{
				if (hostname.Length > 2 & _Dc.Properties["control"]["tcpSshProperties"]["address"].ToString() != hostname)
				{
					Debug.Console(2, this, "Changing IPAddress: {0}", hostname);
					Communication.Disconnect();

					(Communication as GenericTcpIpClient).Hostname = hostname;

					_Dc.Properties["control"]["tcpSshProperties"]["address"] = hostname;
					CustomSetConfig(_Dc);
					Communication.Connect();
				}
			}
			catch (Exception e)
			{
				Debug.Console(2, this, "Error SetIpAddress: '{0}'", e);
			}
		}

		public void WriteConfig()
		{
			CustomSetConfig(_Dc);
		}


		#region Presets

		public void RunPresetNumber(ushort n)
		{
			Debug.Console(2, this, "Attempting to run preset {0}", n);
			if (n < PresetList.Count() && n >= 0)
			{
				RunPreset(PresetList[n].preset);
			}
		}

		public void addPreset(TesiraDspPresets s)
		{
			PresetList.Add(s);
		}

		/// <summary>
		/// Sends a command to execute a preset
		/// </summary>
		/// <param name="name">Preset Name</param>
		public void RunPreset(string name)
		{
			SendLine(string.Format("DEVICE recallPresetByName \"{0}\"", name));
		}

		#endregion


		#region IBridge Members

		public void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey)
		{
			foreach (var item in States)
			{
				Debug.Console(2, this, "State {0} has an InstanceTag of {1}", item.Key, item.Value.InstanceTag1);
			}
			this.LinkToApiExt(trilist, joinStart, joinMapKey);
		}

		#endregion

		#region SubscriptionHandling

		#region Unsubscribe

		private void UnsubscribeFromAttributes()
		{
			foreach (KeyValuePair<uint, TesiraDspDialer> dialer in Dialers)
			{
				var control = dialer.Value;
				UnsubscribeFromDialer(control);
			}

			foreach (KeyValuePair<uint, TesiraDspSwitcher> switcher in Switchers)
			{
				var control = switcher.Value;
				UnsubscribeFromSwitcher(control);
			}

			foreach (KeyValuePair<uint, TesiraDspStateControl> state in States)
			{
				var control = state.Value;
				UnsubscribeFromState(control);
			}

			foreach (KeyValuePair<uint, TesiraDspLevelControl> level in LevelControlPoints)
			{
				var control = level.Value;
				UnsubscribedFromLevel(control);
			}

			/*
			foreach(KeyValuePair<string, TesiraDspMeter> meter in Meters) {
				var control = meter.Value;
				UnsubscribeFromMeter(control);
			}
			*/

			foreach (KeyValuePair<uint, TesiraDspRoomCombiner> roomCombiner in RoomCombiners)
			{
				var control = roomCombiner.Value;
				UnsubscribeFromRoomCombiner(control);
			}
		}


		private void UnsubscribeFromDialer(TesiraDspDialer data)
		{
			if (data.Enabled)
			{
				Debug.Console(2, this, "Unsubscribing From Object - {0}", data.InstanceTag1);
				data.UnSubscribe();
			}
		}

		private void UnsubscribeFromSwitcher(TesiraDspSwitcher data)
		{
			if (data.Enabled)
			{
				Debug.Console(2, this, "Unsubscribing From Object - {0}", data.InstanceTag1);
				data.Unsubscribe();
			}
		}

		private void UnsubscribeFromState(TesiraDspStateControl data)
		{
			if (data.Enabled)
			{
				Debug.Console(2, this, "Unsubscribing From Object - {0}", data.InstanceTag1);
				data.Unsubscribe();
			}
		}

		private void UnsubscribedFromLevel(TesiraDspLevelControl data)
		{
			if (data.Enabled)
			{
				Debug.Console(2, this, "Unsubscribing From Object - {0}", data.InstanceTag1);
				data.Unsubscribe();
			}
		}

		private void UnsubscribeFromMeter(TesiraDspMeter data)
		{
			if (data.Enabled)
			{
				Debug.Console(2, this, "Subscribing To Object - {0}", data.InstanceTag1);
				data.Unsubscribe();
			}
		}

		private void UnsubscribeFromRoomCombiner(TesiraDspRoomCombiner data)
		{
			if (data.Enabled)
			{
				Debug.Console(2, this, "Subscribing To Object - {0}", data.InstanceTag1);
				data.Unsubscribe();
			}
		}

		#endregion

		#region Subscribe

		private void SubscribeToAttributes()
		{
			foreach (KeyValuePair<uint, TesiraDspDialer> dialer in Dialers)
			{
				var control = dialer.Value;
				control.Subscribe();
			}

			foreach (KeyValuePair<uint, TesiraDspSwitcher> switcher in Switchers)
			{
				var control = switcher.Value;
				control.Subscribe();
			}

			foreach (KeyValuePair<uint, TesiraDspStateControl> state in States)
			{
				var control = state.Value;
				control.Subscribe();
			}

			foreach (KeyValuePair<uint, TesiraDspLevelControl> level in LevelControlPoints)
			{
				var control = level.Value;
				control.Subscribe();
			}

			/*
			foreach (KeyValuePair<string, TesiraDspMeter> meter in Meters)
			{
				var control = meter.Value;
				control.Subscribe();
			}
			*/

			foreach (KeyValuePair<uint, TesiraDspRoomCombiner> roomCombiner in RoomCombiners)
			{
				var control = roomCombiner.Value;
				control.Subscribe();
			}
		}

		private void SubscribeToDialer(TesiraDspDialer data)
		{
			if (data.Enabled)
			{
				Debug.Console(2, this, "Subscribing To Object - {0}", data.InstanceTag1);
				data.Subscribe();
			}
		}

		private void SubscribeToSwitcher(TesiraDspSwitcher data)
		{
			if (data.Enabled)
			{
				Debug.Console(2, this, "Subscribing To Object - {0}", data.InstanceTag1);
				data.Subscribe();
			}
		}

		private void SubscribeToState(TesiraDspStateControl data)
		{
			if (data.Enabled)
			{
				Debug.Console(2, this, "Subscribing To Object - {0}", data.InstanceTag1);
				data.Subscribe();
			}
		}

		private void SubscribeToLevel(TesiraDspLevelControl data)
		{
			if (data.Enabled)
			{
				Debug.Console(2, this, "Subscribing To Object - {0}", data.InstanceTag1);
				data.Subscribe();
			}
		}

		private void SubscribeToMeters(TesiraDspMeter data)
		{
			if (data.Enabled)
			{
				Debug.Console(2, this, "Subscribing To Object - {0}", data.InstanceTag1);
				data.Subscribe();
			}
		}

		private void SubscribeToRoomCombiners(TesiraDspRoomCombiner data)
		{
			if (data.Enabled)
			{
				Debug.Console(2, this, "Subscribing To Object - {0}", data.InstanceTag1);
				data.Subscribe();
			}
		}


		#endregion


		#endregion
	}
}