using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Devices.Common.DSP;
using PepperDash.Essentials.Devices.Common.AudioCodec;
using PepperDash.Essentials.Devices.Common.Codec;

namespace Tesira_DSP_EPI
{
    public class TesiraDspDialerControlPoint : AudioCodecBase, IParseMessage
    {
        public string InstanceTag1 { get; set; }
        public string InstanceTag2 { get; set; }
        public int Index1 { get; set; }
        public int Index2 { get; set; }
        public TesiraDsp Parent { get; private set; }
        public string Label { get; set; }

        public virtual bool IsSubscribed { get; protected set; }

        protected TesiraDspDialerControlPoint(string key, string instanceTag1, string instanceTag2, int index1, int index2, TesiraDsp parent)
            : base(string.Format("{0}--{1}", parent.Key, key), key)
        {
            InstanceTag1 = string.IsNullOrEmpty(instanceTag1) ? "" : instanceTag1;
            InstanceTag2 = string.IsNullOrEmpty(instanceTag2) ? "" : instanceTag2;
            Index1 = index1;
            Index2 = index2;
            Parent = parent;
        }

        virtual public void Initialize()
        {

        }

        virtual public void Subscribe()
        {

        }

        /// <summary>
        /// Sends a command to the DSP
        /// </summary>
        /// <param name="command">command</param>
        /// <param name="attribute">attribute code</param>
        /// <param name="value">value (use "" if not applicable)</param>
        public virtual void SendFullCommand(string command, string attributeCode, string value, int InstanceTag)
        {
            if (string.IsNullOrEmpty(attributeCode))
            {
                Debug.Console(2, this, Debug.ErrorLogLevel.Error, "SendFullCommand({0}, {1}, {2}, {3}) Error: AttributeCode is null or empty", command, attributeCode, value, InstanceTag);
                return;
            }

            // Command Format: InstanceTag get/set/toggle/increment/decrement/subscribe/unsubscribe attributeCode [index] [value]
            // Ex: "RoomLevel set level 1.00"
            string cmd;
            string instanceTag;
            switch (InstanceTag)
            {
                case 1:
                    instanceTag = InstanceTag1;
                    break;
                case 2:
                    instanceTag = InstanceTag2;
                    break;
                case 999:
                    instanceTag = "DEVICE";
                    break;
                default:
                    instanceTag = InstanceTag1;
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
                    if (String.IsNullOrEmpty(command))
                    {
                        //format command without value OR command
                        cmd = string.Format("{0} {1} {2} ", instanceTag, attributeCode, Index1);
                    }
                    else
                    {
                        // format command without value
                        cmd = string.Format("{0} {1} {2} {3}", instanceTag, command, attributeCode, Index1);
                    }
                }
                else
                {
                    // format commadn with value
                    cmd = string.Format("{0} {1} {2} {3} {4}", instanceTag, command, attributeCode, Index1, value);
                }
            }


            else if (attributeCode == "dial" || attributeCode == "end" || attributeCode == "onHook" ||
                attributeCode == "offHook" || attributeCode == "answer")
            {
                //requires index, but does not require command
                if (String.IsNullOrEmpty(value))
                {
                    //format command without value
                    cmd = string.Format("{0} {1} {2} {3}", instanceTag, attributeCode, Index1, Index2);
                }
                else
                {
                    cmd = string.Format("{0} {1} {2} {3} {4}", instanceTag, attributeCode, Index1, Index2, value);
                }
            }

            else
            {
                //Command does not require Index
                if (String.IsNullOrEmpty(value))
                {
                    cmd = string.Format("{0} {1} {2}", instanceTag, command, attributeCode);
                }
                else
                {
                    cmd = string.Format("{0} {1} {2} {3}", instanceTag, command, attributeCode, value);
                }
            }

            if (command == "get")
            {
                // This command will generate a return value response so it needs to be queued
                if (!string.IsNullOrEmpty(cmd))
                    Parent.EnqueueCommand(new TesiraDsp.QueuedCommand { Command = cmd, AttributeCode = attributeCode, ControlPoint = this });
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

        public virtual void SendSubscriptionCommand(string customName, string attributeCode, int responseRate, int InstanceTag)
        {
            // Subscription string format: InstanceTag subscribe attributeCode Index1 customName responseRate
            // Ex: "RoomLevel subscribe level 1 MyRoomLevel 500"
            if (string.IsNullOrEmpty(customName) || string.IsNullOrEmpty(attributeCode))
            {
                Debug.Console(2, this, "SendSubscriptionCommand({0}, {1}, {2}, {3}) Error: CustomName or AttributeCode are null or empty", customName, attributeCode, responseRate, InstanceTag);
                return;
            }

            string cmd;
            string instanceTag;
            switch (InstanceTag)
            {
                case 1:
                    instanceTag = InstanceTag1;
                    break;
                case 2:
                    instanceTag = InstanceTag2;
                    break;

                default:
                    instanceTag = InstanceTag1;
                    break;
            }
            if (attributeCode == "callState" || attributeCode == "sourceSelection")
            {
                cmd = string.Format("\"{0}\" subscribe {1} {2} {3}", instanceTag, attributeCode, customName, responseRate);
            }

            else if (responseRate > 0)
            {
                cmd = string.Format("\"{0}\" subscribe {1} {2} {3} {4}", instanceTag, attributeCode, Index1, customName, responseRate);
            }
            else
            {
                cmd = string.Format("\"{0}\" subscribe {1} {2} {3}", instanceTag, attributeCode, Index1, customName);
            }

            //Parent.WatchDogList.Add(customName,cmd);
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
    }
}