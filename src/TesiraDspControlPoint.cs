using System;
using System.Collections.Generic;
using System.Globalization;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using Tesira_DSP_EPI.Interfaces;
using Feedback = PepperDash.Essentials.Core.Feedback;

namespace Tesira_DSP_EPI
{
    public abstract class TesiraDspControlPoint : EssentialsBridgeableDevice, ISubscribedComponent, IHasFeedback
    {
        public string InstanceTag1 { get; set; }
        public string InstanceTag2 { get; set; }
        public int Index1 { get; set; }
        public int Index2 { get; set; }
        public TesiraDsp Parent { get; private set; }
        public string Label { get; set; }
        public readonly uint? BridgeIndex;

        public List<string> CustomNames { get; set; }

        public StringFeedback NameFeedback;

        public FeedbackCollection<Feedback> Feedbacks { get; protected set; }

        public virtual bool IsSubscribed { get; protected set; }

        protected TesiraDspControlPoint(string instanceTag1, string instanceTag2, int index1, int index2, TesiraDsp parent, string key, string name, uint? bridgeIndex)
            : base(key, name)
        {
            if (bridgeIndex != null)
                BridgeIndex = bridgeIndex;
            Feedbacks = new FeedbackCollection<Feedback>();
            InstanceTag1 = string.IsNullOrEmpty(instanceTag1) ? "" : instanceTag1;
            InstanceTag2 = string.IsNullOrEmpty(instanceTag2) ? "" : instanceTag2;
            Index1 = index1;
            Index2 = index2;
            Parent = parent;
            NameFeedback = new StringFeedback(key + "-NameFeedback", () => Name);
            CustomNames = new List<string>();
        }
        protected TesiraDspControlPoint(string instanceTag1, string instanceTag2, TesiraDsp parent, string key, string name, uint? bridgeIndex)
            : base(key, name)
        {
            if (bridgeIndex != null)
                BridgeIndex = bridgeIndex;
            Feedbacks = new FeedbackCollection<Feedback>();
            InstanceTag1 = string.IsNullOrEmpty(instanceTag1) ? "" : instanceTag1;
            InstanceTag2 = string.IsNullOrEmpty(instanceTag2) ? "" : instanceTag2;
            Index1 = 0;
            Index2 = 0;
            Parent = parent;
            NameFeedback = new StringFeedback(key + "-NameFeedback", () => Name);
            CustomNames = new List<string>();
        }

        public virtual void Subscribe()
        {
            //If component does not override subscription, it sets the subscription value to false.
            IsSubscribed = false;
        }

        public virtual void Unsubscribe()
        {

        }

        /// <summary>
        /// Sends a command to the DSP
        /// </summary>
        /// <param name="command">command</param>
        /// <param name="attributeCode">attribute code</param>
        /// <param name="value">value (use "" if not applicable)</param>
        /// <param name="instanceTag">value (Instance Tag of the control</param>
        public virtual void SendFullCommand(string command, string attributeCode, string value, int instanceTag)
        {
            if (string.IsNullOrEmpty(attributeCode))
            {
                Debug.Console(2, this, Debug.ErrorLogLevel.Error, "SendFullCommand({0}, {1}, {2}, {3}) Error: AttributeCode is null or empty", command, attributeCode, value, instanceTag);
                return;
            }

            // Command Format: InstanceTag get/set/toggle/increment/decrement/subscribe/unsubscribe attributeCode [index] [value]
            // Ex: "RoomLevel set level 1.00"
            string cmd;
            string instanceTagLocal;
            switch (instanceTag)
            {
                case 1:
                    instanceTagLocal = InstanceTag1;
                    break;
                case 2:
                    instanceTagLocal = InstanceTag2;
                    break;
                case 999:
                    instanceTagLocal = "DEVICE";
                    break;
                default:
                    instanceTagLocal = InstanceTag1;
                    break;
            }

            if (attributeCode == "level" || attributeCode == "mute" || attributeCode == "minLevel" ||
                attributeCode == "maxLevel" || attributeCode == "label" || attributeCode == "rampInterval" ||
                attributeCode == "rampStep" || attributeCode == "autoAnswer" || attributeCode == "dndEnable" ||
                attributeCode == "dtmf" || attributeCode == "state" || attributeCode == "levelOut" ||
                attributeCode == "maxLevelOut" || attributeCode == "minLevelOut" || attributeCode == "muteOut" ||
                attributeCode == "group" && command == "set")
            {
                //Command requires Index
                if (String.IsNullOrEmpty(value))
                {
                    cmd = String.IsNullOrEmpty(command) ? string.Format("\"{0}\" {1} {2} ",
                        instanceTagLocal, attributeCode, Index1) : string.Format("\"{0}\" {1} {2} {3}", instanceTagLocal, command, attributeCode, Index1);
                }
                else
                {
                    // format command with value
                    cmd = string.Format("\"{0}\" {1} {2} {3} {4}", instanceTagLocal, command, attributeCode, Index1, value);
                }
            }
            else if (attributeCode == "crosspointLevelState")
            {
                cmd = string.Format("\"{0}\" {1} {2} {3} {4}", instanceTagLocal, command, attributeCode, Index1, Index2);
            }
            else if (attributeCode == "input")
            {
                //requires Index for both get and set commands
                cmd = String.IsNullOrEmpty(value) ? string.Format("\"{0}\" {1} {2} {3}",
                    instanceTagLocal, command, attributeCode, Index1 == 0 ? "" : Index1.ToString(CultureInfo.InvariantCulture)) : string.Format("\"{0}\" {1} {2} {3} {4}",
                    instanceTagLocal, command, attributeCode, Index1 == 0 ? "" : Index1.ToString(CultureInfo.InvariantCulture), value);
            }


            else if (attributeCode == "dial" || attributeCode == "end" || attributeCode == "onHook" ||
                attributeCode == "offHook" || attributeCode == "answer")
            {
                //requires index, but does not require command
                cmd = String.IsNullOrEmpty(value) ? string.Format("\"{0}\" {1} {2} {3}",
                    instanceTagLocal, attributeCode, Index1, Index2) : string.Format("\"{0}\" {1} {2} {3} {4}",
                    instanceTagLocal, attributeCode, Index1, Index2, value);
            }

            else
            {
                //Command does not require Index
                cmd = String.IsNullOrEmpty(value) ?
                    string.Format("\"{0}\" {1} {2}", instanceTagLocal, command, attributeCode) :
                    string.Format("\"{0}\" {1} {2} {3}", instanceTagLocal, command, attributeCode, value);
            }


            //if (command == "get")
            //{
            //    // This command will generate a return value response so it needs to be queued
            //    if (!string.IsNullOrEmpty(cmd))
            //        Parent.CommandQueue.EnqueueCommand(new QueuedCommand(cmd, attributeCode, this));
            //}
            //else
            //{
            //    // This command will generate a simple "+OK" response and doesn't need to be queued
            //    if (!string.IsNullOrEmpty(cmd))
            //        Parent.SendLine(cmd);
            //}

            Parent.CommandQueue.EnqueueCommand(new QueuedCommand(cmd, attributeCode, this));
        }

        virtual public void ParseGetMessage(string attributeCode, string message)
        {

        }

        public virtual void AddCustomName(string customName)
        {
            if (CustomNames.Contains(customName)) return;
            CustomNames.Add(customName);
        }

        public virtual void SendSubscriptionCommand(string customName, string attributeCode, int responseRate, int instanceTag)
        {
            // Subscription string format: InstanceTag subscribe attributeCode Index1 customName responseRate
            // Ex: "RoomLevel subscribe level 1 MyRoomLevel 500"
            if (string.IsNullOrEmpty(customName) || string.IsNullOrEmpty(attributeCode))
            {
                Debug.Console(2, this, "SendSubscriptionCommand({0}, {1}, {2}, {3}) Error: CustomName or AttributeCode are null or empty", customName, attributeCode, responseRate, instanceTag);
                return;
            }

            string cmd;
            string instanceTagLocal;
            switch (instanceTag)
            {
                case 1:
                    instanceTagLocal = InstanceTag1;
                    break;
                case 2:
                    instanceTagLocal = InstanceTag2;
                    break;
                default:
                    instanceTagLocal = InstanceTag1;
                    break;
            }
            if (attributeCode == "callState" || attributeCode == "sourceSelection")
            {
                cmd = string.Format("\"{0}\" subscribe {1} {2} {3}", instanceTagLocal, attributeCode, customName, responseRate);
            }

            else if (responseRate > 0)
            {
                cmd = string.Format("\"{0}\" subscribe {1} {2} {3} {4}", instanceTagLocal, attributeCode, Index1, customName, responseRate);
            }
            else
            {
                cmd = string.Format("\"{0}\" subscribe {1} {2} {3}", instanceTagLocal, attributeCode, Index1, customName);
            }

            //Parent.WatchDogList.Add(customName,cmd);
            //Parent.SendLine(cmd);
            Parent.SendLine(cmd);
            //Parent.CommandQueue.EnqueueCommand(new QueuedCommand(cmd, attributeCode, this));

        }

        public virtual void ParseSubscriptionMessage(string customName, string value)
        {

        }

        public virtual void SendUnSubscriptionCommand(string customName, string attributeCode, int instanceTag)
        {
            // Subscription string format: InstanceTag subscribe attributeCode Index1 customName responseRate
            // Ex: "RoomLevel subscribe level 1 MyRoomLevel 500"
            if (string.IsNullOrEmpty(customName) || string.IsNullOrEmpty(attributeCode))
            {
                Debug.Console(2, this, "SendUnSubscriptionCommand({0}, {1}, {2}) Error: CustomName or AttributeCode are null or empty", customName, attributeCode, instanceTag);
                return;
            }

            string cmd;
            string localInstanceTag;
            switch (instanceTag)
            {
                case 1:
                    localInstanceTag = InstanceTag1;
                    break;
                case 2:
                    localInstanceTag = InstanceTag2;
                    break;

                default:
                    localInstanceTag = InstanceTag1;
                    break;
            }
            if (attributeCode == "callState" || attributeCode == "sourceSelection")
            {
                cmd = string.Format("\"{0}\" unsubscribe {1} {2}", localInstanceTag, attributeCode, customName);
            }

            else
            {
                cmd = string.Format("\"{0}\" unsubscribe {1} {2} {3}", localInstanceTag, attributeCode, Index1, customName);
            }
            Debug.Console(1, this, "SendingUnsub - {0}", cmd);
            //Parent.WatchDogList.Add(customName,cmd);
            Parent.SendLine(cmd);
            //Parent.CommandQueue.EnqueueCommand(new QueuedCommand(cmd, attributeCode, this));
        }

        public virtual void DoPoll()
        {

        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            // throw new NotImplementedException();
        }
    }
}