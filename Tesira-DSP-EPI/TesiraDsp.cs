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

		public bool isSubscribed;

		//public TesiraDspWatchdog Watchdog;

		private CTimer WatchdogTimer;

		public Dictionary<string, TesiraDspLevelControl> LevelControlPoints { get; private set; }
		public Dictionary<string, TesiraDspDialer> Dialers { get; private set; }
		public Dictionary<string, TesiraDspSwitcher> Switchers { get; private set; }
		public Dictionary<string, TesiraDspStateControl> States { get; private set; }
		public List<TesiraDspPresets> PresetList = new List<TesiraDspPresets>();

        public List<TesiraDspControlPoint> ControlPointList { get; private set; }

        private bool WatchDogSniffer { get; set; }

		DeviceConfig _Dc;

		CrestronQueue CommandQueue;

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

			CommandQueue = new CrestronQueue(100);
			Communication = comm;
			var socket = comm as ISocketStatus;
			if (socket != null)
			{
				// This instance uses IP control
				socket.ConnectionChange += new EventHandler<GenericSocketStatusChageEventArgs>(socket_ConnectionChange);
			}
			else
			{
				// This instance uses RS-232 control
			}
			PortGather = new CommunicationGather(Communication, "\x0D\x0A");
			PortGather.LineReceived += this.Port_LineReceived;

			CommandPassthruFeedback = new StringFeedback(() => DeviceRx);

			// Custom monitoring, will check the heartbeat tracker count every 20s and reset. Heartbeat sbould be coming in every 20s if subscriptions are valid
			CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, 20000, 120000, 300000, CheckWatchDog);
			//DeviceManager.AddDevice(CommunicationMonitor);

			LevelControlPoints = new Dictionary<string, TesiraDspLevelControl>();
			Dialers = new Dictionary<string, TesiraDspDialer>();
			Switchers = new Dictionary<string, TesiraDspSwitcher>();
			States = new Dictionary<string, TesiraDspStateControl>();
            ControlPointList = new List<TesiraDspControlPoint>();
			CreateDspObjects();
		}

		public override bool CustomActivate()
		{
			Communication.Connect();
			CommunicationMonitor.StatusChange += (o, a) => { Debug.Console(2, this, "Communication monitor state: {0}", CommunicationMonitor.Status); };
			CommunicationMonitor.Start();

			CrestronConsole.AddNewConsoleCommand(SendLine, "send" + Key, "", ConsoleAccessLevelEnum.AccessOperator);
			CrestronConsole.AddNewConsoleCommand(s => Communication.Connect(), "con" + Key, "", ConsoleAccessLevelEnum.AccessOperator);
			return true;
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
				CommandQueue.Clear();
				CommandQueueInProgress = false;
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



			if (props.levelControlBlocks != null)
			{
				Debug.Console(2, this, "levelControlBlocks is not null - There are {0} of them", props.levelControlBlocks.Count());
				foreach (KeyValuePair<string, TesiraLevelControlBlockConfig> block in props.levelControlBlocks)
				{
					string key = block.Key;
					Debug.Console(2, this, "LevelControlBlock Key - {0}", key);
					var value = block.Value;
					//value.levelInstanceTag = value.levelInstanceTag;
					//value.muteInstanceTag = value.muteInstanceTag;

					this.LevelControlPoints.Add(key, new TesiraDspLevelControl(key, value, this));
                    ControlPointList.Add(LevelControlPoints[key]);
					Debug.Console(2, this, "Added LevelControlPoint {0} LevelTag: {1} MuteTag: {2}", key, value.levelInstanceTag, value.muteInstanceTag);
                    DeviceManager.AddDevice(LevelControlPoints[key]);
				}
			}

			if (props.switcherControlBlocks != null)
			{
				Debug.Console(2, this, "switcherControlBlocks is not null - There are {0} of them", props.levelControlBlocks.Count());
				foreach (KeyValuePair<string, TesiraSwitcherControlBlockConfig> block in props.switcherControlBlocks)
				{
					string key = block.Key;
					Debug.Console(2, this, "SwitcherControlBlock Key - {0}", key);
					var value = block.Value;

					this.Switchers.Add(key, new TesiraDspSwitcher(key, value, this));
                    ControlPointList.Add(Switchers[key]);

					Debug.Console(2, this, "Added TesiraSwitcher {0} InstanceTag {1}", key, value.switcherInstanceTag);
				}
			}

			if (props.dialerControlBlocks != null)
			{
				Debug.Console(2, this, "dialerControlBlocks is not null - There are {0} of them", props.dialerControlBlocks.Count());
				foreach (KeyValuePair<string, TesiraDialerControlBlockConfig> block in props.dialerControlBlocks)
				{
					string key = block.Key;
					Debug.Console(2, this, "LevelControlBlock Key - {0}", key);
					var value = block.Value;
					//value.controlStatusInstanceTag = value.controlStatusInstanceTag;
					//value.dialerInstanceTag = value.dialerInstanceTag;

					this.Dialers.Add(key, new TesiraDspDialer(key, value, this));
                    DeviceManager.AddDevice(Dialers[key]);

					Debug.Console(2, this, "Added DspDialer {0} ControlStatusTag: {1} DialerTag: {2}", key, value.controlStatusInstanceTag, value.dialerInstanceTag);

				}
			}

			if (props.stateControlBlocks != null)
			{
				Debug.Console(2, this, "stateControlBlocks is not null - There are {0} of them", props.stateControlBlocks.Count());
				foreach (KeyValuePair<string, TesiraStateControlBlockConfig> block in props.stateControlBlocks)
				{
					string key = block.Key;
					var value = block.Value;
					//value.stateInstanceTag = value.stateInstanceTag;
					this.States.Add(key, new TesiraDspStateControl(key, value, this));
                    ControlPointList.Add(States[key]);

                    Debug.Console(2, this, "Added DspState {0} InstanceTag: {1}", key, value.stateInstanceTag);

				}
			}

			if (props.presets != null)
			{
				foreach (KeyValuePair<string, TesiraDspPresets> preset in props.presets)
				{
					var value = preset.Value;
					value.preset = value.preset;
					this.addPreset(value);
					Debug.Console(2, this, "Added Preset {0} {1}", value.label, value.preset);
				}
			}

		}

		void Port_LineReceived(object dev, GenericCommMethodReceiveTextArgs args)
		{
			if (Debug.Level == 2)
				Debug.Console(2, this, "RX: '{0}'",
					ShowHexResponse ? ComTextHelper.GetEscapedText(args.Text) : args.Text);

			Debug.Console(1, this, "RX: '{0}'", args.Text);

			try
			{

				DeviceRx = args.Text;

				this.CommandPassthruFeedback.FireUpdate();

				if (args.Text.IndexOf("Welcome to the Tesira Text Protocol Server...") > -1)
				{
					// Indicates a new TTP session
					// moved to CustomActivate() method
					//CommunicationMonitor.Start();

					SubscribeToAttributes();
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

						foreach (KeyValuePair<string, TesiraDspLevelControl> controlPoint in LevelControlPoints)
						{
							if (customName == controlPoint.Value.LevelCustomName || customName == controlPoint.Value.MuteCustomName)
							{
								controlPoint.Value.ParseSubscriptionMessage(customName, value);
								return;
							}
						}
						foreach (KeyValuePair<string, TesiraDspDialer> controlPoint in Dialers)
						{

							if (customName == controlPoint.Value.AutoAnswerCustomName || customName == controlPoint.Value.ControlStatusCustomName ||
								customName == controlPoint.Value.DialerCustomName)
							{
								controlPoint.Value.ParseSubscriptionMessage(customName, value);
								return;
							}

						}
						foreach (KeyValuePair<string, TesiraDspStateControl> controlPoint in States)
						{

							if (customName == controlPoint.Value.StateCustomName)
							{
								controlPoint.Value.ParseSubscriptionMessage(customName, value);
							}
						}

						foreach (KeyValuePair<string, TesiraDspSwitcher> controlPoint in Switchers)
						{

							if (customName == controlPoint.Value.SelectorCustomName)
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

					if (!CommandQueue.IsEmpty)
					{
						if (CommandQueue.Peek() is QueuedCommand)
						{
							// Expected response belongs to a child class
							QueuedCommand tempCommand = (QueuedCommand)CommandQueue.TryToDequeue();
							//Debug.Console(1, this, "Command Dequeued. CommandQueue Size: {0}", CommandQueue.Count);

							tempCommand.ControlPoint.ParseGetMessage(tempCommand.AttributeCode, args.Text);
						}
						else
						{
							// Expected response belongs to this class
							string temp = (string)CommandQueue.TryToDequeue();
							//Debug.Console(1, this, "Command Dequeued. CommandQueue Size: {0}", CommandQueue.Count);

						}

						if (CommandQueue.IsEmpty)
							CommandQueueInProgress = false;
						else
							SendNextQueuedCommand();

					}
				}
				else if (args.Text.IndexOf("-ERR") > -1)
				{
					// Error response

					switch (args.Text)
					{
						case "-ERR ALREADY_SUBSCRIBED":
							{
                                WatchDogSniffer = false;
								break;
							}
						default:
							{
								Debug.Console(0, this, "Error From DSP: '{0}'", args.Text);
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
			CreateDspObjects();
		}

        public void CheckWatchDog()
        {
            if (!WatchDogSniffer)
            {
                Random random = new Random(DateTime.Now.Millisecond);

                var WatchDogSubject = ControlPointList[random.Next(0, ControlPointList.Count - 1)];

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
			CommandQueue.Enqueue(commandToEnqueue);
			//Debug.Console(1, this, "Command (QueuedCommand) Enqueued '{0}'.  CommandQueue has '{1}' Elements.", commandToEnqueue.Command, CommandQueue.Count);

			if (!CommandQueueInProgress)
				SendNextQueuedCommand();
		}

		/// <summary>
		/// Adds a raw string command to the queue
		/// </summary>
		/// <param name="command"></param>
		public void EnqueueCommand(string command)
		{
			CommandQueue.Enqueue(command);
			Debug.Console(1, this, "Command (string) Enqueued '{0}'.  CommandQueue has '{1}' Elements.", command, CommandQueue.Count);

			if (!CommandQueueInProgress)
				SendNextQueuedCommand();
		}

		/// <summary>
		/// Sends the next queued command to the DSP
		/// </summary>
		void SendNextQueuedCommand()
		{
			if (Communication.IsConnected && !CommandQueue.IsEmpty)
			{
				CommandQueueInProgress = true;
				if (CommandQueue.Peek() is QueuedCommand)
				{
					QueuedCommand nextCommand = new QueuedCommand();
					nextCommand = (QueuedCommand)CommandQueue.Peek();
					SendLine(nextCommand.Command);
				}
				else
				{
					string nextCommand = (string)CommandQueue.Peek();
					SendLine(nextCommand);
				}
			}

		}

		/// <summary>
		/// Initiates the subscription process to the DSP
		/// </summary>
		void SubscribeToAttributes()
		{
			SendLine("SESSION set verbose false");

			foreach (KeyValuePair<string, TesiraDspDialer> dialer in Dialers)
			{
				Debug.Console(2, this, "Made it to Object - {0}", dialer.Value.InstanceTag1);
				if (dialer.Value.Enabled)
					dialer.Value.Subscribe();
			}

			foreach (KeyValuePair<string, TesiraDspSwitcher> switcher in Switchers)
			{
				Debug.Console(2, this, "Made it to Object - {0}", switcher.Value.InstanceTag1);
				if (switcher.Value.Enabled)
					switcher.Value.Subscribe();
			}

			foreach (KeyValuePair<string, TesiraDspStateControl > state in States)
			{
				Debug.Console(2, this, "Made it to Object - {0}", state.Value.InstanceTag1);
				if (state.Value.Enabled)
					state.Value.Subscribe();
			}

            Debug.Console(2, this, "There are {0} Level Objects", LevelControlPoints.Count());
            foreach (KeyValuePair<string, TesiraDspLevelControl> level in LevelControlPoints) {
                Debug.Console(2, this, "Made it to Object - {0}", level.Value.InstanceTag1);
                if (level.Value.Enabled)
                    level.Value.Subscribe();
            }

			if (!CommandQueueInProgress)
				SendNextQueuedCommand();

            if (WatchdogTimer == null) {
                WatchdogTimer = new CTimer(o => SubscribeToAttributes(), 90000, 90000);
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
				if (Debug.Level == 2)
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
	}
}