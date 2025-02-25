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
using System.Linq;

namespace EliteDangerousCore.JournalEvents
{
    [JournalEntryType(JournalTypeEnum.DatalinkScan)]
    public class JournalDatalinkScan : JournalEntry
    {
        public JournalDatalinkScan(JObject evt ) : base(evt, JournalTypeEnum.DatalinkScan)
        {
            Message = evt["Message"].Str();
            MessageLocalised = JournalFieldNaming.CheckLocalisation(evt["Message_Localised"].Str(), Message);

        }
        public string Message { get; set; }
        public string MessageLocalised { get; set; }

        public override void FillInformation(ISystem sys, string whereami, out string info, out string detailed) 
        {            
            info = MessageLocalised;
            detailed = "";
        }
    }

    [JournalEntryType(JournalTypeEnum.DatalinkVoucher)]
    public class JournalDatalinkVoucher : JournalEntry
    {
        public JournalDatalinkVoucher(JObject evt) : base(evt, JournalTypeEnum.DatalinkVoucher)
        {
            VictimFaction = evt["VictimFaction"].Str();
            Reward = evt["Reward"].Long();
            PayeeFaction = evt["PayeeFaction"].Str();
        }

        public string PayeeFaction { get; set; }
        public long Reward { get; set; }
        public string VictimFaction { get; set; }

        public override void FillInformation(ISystem sys, string whereami, out string info, out string detailed)
        {

            info = BaseUtils.FieldBuilder.Build("Reward: ; cr;N0".T(EDCTx.JournalEntry_Reward), Reward, "< from faction ".T(EDCTx.JournalEntry_fromfaction), PayeeFaction, "Against ".T(EDCTx.JournalEntry_Against), VictimFaction);
            detailed = "";
        }
    }

    [JournalEntryType(JournalTypeEnum.DataScanned)]
    public class JournalDataScanned : JournalEntry
    {
        public JournalDataScanned(JObject evt) : base(evt, JournalTypeEnum.DataScanned)
        {
            Type = evt["Type"].Str().SplitCapsWordFull();
            TypeLocalised = JournalFieldNaming.CheckLocalisation(evt["Type_Localised"].Str(), Type);
        }

        public string Type { get; set; }
        public string TypeLocalised { get; set; }

        public override void FillInformation(ISystem sys, string whereami, out string info, out string detailed)
        {
            info = TypeLocalised;
            detailed = "";
        }
    }


}
