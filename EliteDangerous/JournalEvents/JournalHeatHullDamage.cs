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
    [JournalEntryType(JournalTypeEnum.HeatDamage)]
    public class JournalHeatDamage : JournalEntry
    {
        public int? ID { get; set; }

        public JournalHeatDamage(JObject evt ) : base(evt, JournalTypeEnum.HeatDamage)
        {
            ID = evt["ID"].IntNull();
        }

        public override void FillInformation(ISystem sys, string whereami, out string info, out string detailed) 
        {
            info = "";
            detailed = "";
        }
    }

    [JournalEntryType(JournalTypeEnum.HeatWarning)]
    public class JournalHeatWarning : JournalEntry
    {
        public JournalHeatWarning(JObject evt) : base(evt, JournalTypeEnum.HeatWarning)
        {
        }

        public override void FillInformation(ISystem sys, string whereami, out string info, out string detailed)
        {
            info = "";
            detailed = "";
        }
    }


    [JournalEntryType(JournalTypeEnum.HullDamage)]
    public class JournalHullDamage : JournalEntry
    {
        public JournalHullDamage(JObject evt) : base(evt, JournalTypeEnum.HullDamage)
        {
            Health = evt["Health"].Double();
            PlayerPilot = evt["PlayerPilot"].BoolNull();
            Fighter = evt["Fighter"].BoolNull();
        }

        public double Health { get; set; }
        public bool? PlayerPilot { get; set; }      // 2.4+
        public bool? Fighter { get; set; }

        public override void FillInformation(ISystem sys, string whereami, out string info, out string detailed)
        {
            info = BaseUtils.FieldBuilder.Build(";%", (int)(Health * 100));
            detailed = "";
        }
    }

}
