﻿/*
 * Copyright © 2016-2018 EDDiscovery development team
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this
 * file except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed under
 * the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF
 * ANY KIND, either express or implied. See the License for the specific language
 * governing permissions and limitations under the License.
 *
 * EDDiscovery is not affiliated with Frontier Developments plc.
 */
using QuickJSON;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace EliteDangerousCore.JournalEvents
{
    [JournalEntryType(JournalTypeEnum.SendText)]
    public class JournalSendText : JournalEntry
    {
        public JournalSendText(JObject evt) : base(evt, JournalTypeEnum.SendText)
        {
            To = evt["To"].Str();
            To_Localised = JournalFieldNaming.CheckLocalisation(evt["To_Localised"].Str(),To);
            Message = evt["Message"].Str();
            Command = Message.StartsWith("/") && To.Equals("Local", StringComparison.InvariantCultureIgnoreCase);
        }

        public string To { get; set; }
        public string To_Localised { get; set; }
        public string Message { get; set; }
        public bool Command { get; set; }

        public override void FillInformation(ISystem sys, string whereami, out string info, out string detailed) 
        {
            info = BaseUtils.FieldBuilder.Build("To: ".T(EDCTx.JournalSendText_To), To_Localised, "Msg: ".T(EDCTx.JournalSendText_Msg), Message);
            detailed = "";
        }
    }


    [JournalEntryType(JournalTypeEnum.ReceiveText)]
    public class JournalReceiveText : JournalEntry
    {
        public JournalReceiveText(JObject evt) : base(evt, JournalTypeEnum.ReceiveText)
        {
            From = evt["From"].Str();
            FromLocalised = evt["From_Localised"].Str().Alt(From);
            Message = evt["Message"].Str();
            MessageLocalised = evt["Message_Localised"].Str().Alt(Message);
            Channel = evt["Channel"].Str();

            string[] specials = new string[] { "$COMMS_entered:", "$CHAT_intro;", "$HumanoidEmote" };

            if ( specials.StartsWith(Message, System.StringComparison.InvariantCultureIgnoreCase)>=0)
            {
                Channel = "Info";
            }
        }

        public string From { get; set; }
        public string FromLocalised { get; set; }
        public string Message { get; set; }
        public string MessageLocalised { get; set; }
        public string Channel { get; set; }         // wing/local/voicechat/friend/player/npc : 3.3 adds squadron/starsystem

        public List<JournalReceiveText> MergedEntries { get; set; }    // if verbose.. doing it this way does not break action packs as the variables are maintained
                                                                       // This is second, third merge etc.  First one is in above variables

        public override void FillInformation(ISystem sys, string whereami, out string info, out string detailed)
        {
            detailed = "";
            if (MergedEntries == null)
                info = ToString();
            else
            {
                info = (MergedEntries.Count() + 1).ToString() + " Texts".T(EDCTx.JournalReceiveText_Text) + " " + "from ".T(EDCTx.JournalReceiveText_FC) + Channel;
                for (int i = MergedEntries.Count - 1; i >= 0; i--)
                    detailed = detailed.AppendPrePad(MergedEntries[i].ToStringNC(), System.Environment.NewLine);
                detailed = detailed.AppendPrePad(ToStringNC(), System.Environment.NewLine);   // ours is the last one
            }
        }

        public override string ToString()
        {
            if ( FromLocalised.HasChars() )
                return BaseUtils.FieldBuilder.Build("From: ".T(EDCTx.JournalReceiveText_From), FromLocalised, "< on ".T(EDCTx.JournalReceiveText_on), Channel, "<: ", MessageLocalised);
            else
                return BaseUtils.FieldBuilder.Build("", Channel, "<: ", MessageLocalised);
        }

        public string ToStringNC()
        {
            return BaseUtils.FieldBuilder.Build("From: ".T(EDCTx.JournalReceiveText_From), FromLocalised, "<: ", MessageLocalised);
        }

        public void Add(JournalReceiveText next)
        {
            if (MergedEntries == null)
                MergedEntries = new List<JournalReceiveText>();
            MergedEntries.Add(next);
        }

    }

}
