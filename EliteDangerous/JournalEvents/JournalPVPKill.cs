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
    [JournalEntryType(JournalTypeEnum.PVPKill)]
    public class JournalPVPKill : JournalEntry
    {
        public JournalPVPKill(JObject evt) : base(evt, JournalTypeEnum.PVPKill)
        {
            Victim = evt["Victim"].Str();
            CombatRank = (CombatRank)evt["CombatRank"].Int();
        }

        public string Victim { get; set; }
        public CombatRank CombatRank { get; set; }

        public override void FillInformation(ISystem sys, string whereami, out string info, out string detailed)  
        {
            info = BaseUtils.FieldBuilder.Build("",Victim, "Rank: ".T(EDCTx.JournalEntry_Rank) , CombatRank.ToString().SplitCapsWord());
            detailed = "";
        }
    }
}
