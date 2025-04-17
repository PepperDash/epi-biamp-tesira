using System;
using System.Collections.Generic;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Devices.Common.AudioCodec;
using PepperDash.Essentials.Devices.Common.Codec;
using Tesira_DSP_EPI.Interfaces;

namespace Tesira_DSP_EPI
{
    public class TesiraDspDialerControlPoint : AudioCodecBase, ISubscribedComponent
    {
        public string InstanceTag1 { get; set; }
        public string InstanceTag2 { get; set; }
        public int Index1 { get; set; }
        public int Index2 { get; set; }
        public TesiraDsp Parent { get; private set; }
        public string Label { get; set; }
        public readonly uint? BridgeIndex;

        public List<string> CustomNames { get; set; } 

        private const string KeyFormatter = "{0}--{1}";

        public virtual bool IsSubscribed { get; protected set; }

		protected TesiraDspDialerControlPoint(string key, string instanceTag1, string instanceTag2, int index1, int index2, TesiraDsp parent, uint? bridgeIndex)
            : base(string.Format(KeyFormatter, parent.Key, key), key)
		{
		    BridgeIndex = bridgeIndex;
            InstanceTag1 = string.IsNullOrEmpty(instanceTag1) ? "" : instanceTag1;
            InstanceTag2 = string.IsNullOrEmpty(instanceTag2) ? "" : instanceTag2;
            Index1 = index1;
            Index2 = index2;
            Parent = parent;
            CustomNames = new List<string>();
        }


        virtual public void Subscribe() {}

        virtual public void Unsubscribe() {}
   
        /// <summary>
        /// Sends a command to the DSP for a specific control component
        /// </summary>
        /// <param name="command">Command to send</param>
        /// <param name="attributeCode">Attribute code for control</param>
        /// <param name="value">Value for command</param>
        /// <param name="instanceTag">Instance Tag of Control</param>
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
            string localInstanceTag;
            switch (instanceTag)
            {
                case 1:
                    localInstanceTag = InstanceTag1;
                    break;
                case 2:
                    localInstanceTag = InstanceTag2;
                    break;
                case 999:
                    localInstanceTag = "DEVICE";
                    break;
                default:
                    localInstanceTag = InstanceTag1;
                    break;
            }

            if (attributeCode == "level" || attributeCode == "mute" || attributeCode == "minLevel" ||
                attributeCode == "maxLevel" || attributeCode == "label" || attributeCode == "rampInterval" ||
                attributeCode == "rampStep" || attributeCode == "autoAnswer" || attributeCode == "dndEnable" ||
                attributeCode == "dtmf" || attributeCode == "state")
            {
                //Command requires Index
                if (String.IsNullOrEmpty(value))
                {
                    cmd = String.IsNullOrEmpty(command) ? 
                        string.Format("{0} {1} {2} ", localInstanceTag, attributeCode, Index1) : 
                        string.Format("{0} {1} {2} {3}", localInstanceTag, command, attributeCode, Index1);
                }
                else
                {
                    // format command with value
                    cmd = string.Format("{0} {1} {2} {3} {4}", localInstanceTag, command, attributeCode, Index1, value);
                }
            }


            else if ( attributeCode == "dial" || attributeCode == "end" || attributeCode == "onHook" ||
                attributeCode == "offHook" || attributeCode == "answer" || attributeCode == "hold" || 
                attributeCode == "resume")
            {
                //requires index, but does not require command
                cmd = String.IsNullOrEmpty(value) ? 
                    string.Format("{0} {1} {2} {3}", localInstanceTag, attributeCode, Index1, Index2) : 
                    string.Format("{0} {1} {2} {3} {4}", localInstanceTag, attributeCode, Index1, Index2, value);
            }

            else
            {
                //Command does not require Index
                cmd = String.IsNullOrEmpty(value) ? string.Format("{0} {1} {2}", localInstanceTag, command, attributeCode) : string.Format("{0} {1} {2} {3}", localInstanceTag, command, attributeCode, value);
            }

            if (command == "get")
            {
                // This command will generate a return value response so it needs to be queued
                if (!string.IsNullOrEmpty(cmd))
                    Parent.CommandQueue.EnqueueCommand(new QueuedCommand(cmd, attributeCode, this));
            }
            else
            {
                // This command will generate a simple "+OK" response and doesn't need to be queued
                if (!string.IsNullOrEmpty(cmd))
                    Parent.SendLine(cmd);
            }
        }

        virtual public void ParseGetMessage(string attributeCode, string message)
        {

        }

        public virtual void ParseSubscriptionMessage(string customName, string value)
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
			if (attributeCode == "callState" || attributeCode == "sourceSelection" || attributeCode == "hookState")
            {
                cmd = string.Format("\"{0}\" subscribe {1} {2} {3}", localInstanceTag, attributeCode, customName, responseRate);
            }

            else if (responseRate > 0)
            {
                cmd = string.Format("\"{0}\" subscribe {1} {2} {3} {4}", localInstanceTag, attributeCode, Index1, customName, responseRate);
            }
            else
            {
                cmd = string.Format("\"{0}\" subscribe {1} {2} {3}", localInstanceTag, attributeCode, Index1, customName);
            }

            //Parent.WatchDogList.Add(customName,cmd);
            //Parent.SendLine(cmd);
            Parent.SendLine(cmd);
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

            //Parent.WatchDogList.Add(customName,cmd);
            //Parent.SendLine(cmd);
            Parent.SendLine(cmd);
        }
        
        public virtual void DoPoll()
        {

        }

        public override void Dial(string number)
        {

        }

        public override void EndCall(CodecActiveCallItem activeCall) { }

        public override void EndAllCalls() { }

        public override void AcceptCall(CodecActiveCallItem item) { }

        public override void RejectCall(CodecActiveCallItem item) { }

        public override void SendDtmf(string digit) { }

        #region IBridgeAdvanced Members

        public void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region ISubscribedComponent Members


        

        #endregion
    }
}