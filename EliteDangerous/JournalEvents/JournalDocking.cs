﻿/*
 * Copyright © 2016-2021 EDDiscovery development team
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

namespace EliteDangerousCore.JournalEvents
{
    [JournalEntryType(JournalTypeEnum.Docked)]
    public class JournalDocked : JournalEntry, ISystemStationEntry
    {
        public JournalDocked(JObject evt ) : base(evt, JournalTypeEnum.Docked)
        {
            StationName = evt["StationName"].Str();
            StationType = evt["StationType"].Str().SplitCapsWord();
            StationState = evt["StationState"].StrNull();           // missed, added, nov 22, only on bad starports.  Null otherwise
            StarSystem = evt["StarSystem"].Str();
            SystemAddress = evt["SystemAddress"].LongNull();
            MarketID = evt["MarketID"].LongNull();
            CockpitBreach = evt["CockpitBreach"].Bool();

            JToken jk = (JToken)evt["StationFaction"];
            if (jk != null && jk.IsObject)     // new 3.03
            {
                Faction = jk["Name"].Str();                // system faction pick up
                FactionState = jk["FactionState"].Str();
            }
            else
            {
                // old pre 3.3.3 had this
                Faction = evt.MultiStr(new string[] { "StationFaction", "Faction" });
                FactionState = evt["FactionState"].Str();           // PRE 2.3 .. not present in newer files, fixed up in next bit of code (but see 3.3.2 as its been incorrectly reintroduced)
            }

            Allegiance = evt.MultiStr(new string[] { "StationAllegiance", "Allegiance" });

            Economy = evt.MultiStr(new string[] { "StationEconomy", "Economy" });
            Economy_Localised = JournalFieldNaming.CheckLocalisation(evt.MultiStr(new string[] { "StationEconomy_Localised", "Economy_Localised" }),Economy);
            EconomyList = evt["StationEconomies"]?.ToObjectQ<Economies[]>();

            Government = evt.MultiStr(new string[] { "StationGovernment", "Government" });
            Government_Localised = JournalFieldNaming.CheckLocalisation(evt.MultiStr(new string[] { "StationGovernment_Localised", "Government_Localised" }),Government);

            Wanted = evt["Wanted"].Bool();

            StationServices = evt["StationServices"]?.ToObjectQ<string[]>();

            ActiveFine = evt["ActiveFine"].BoolNull();

            // Government = None only happens in Training
            if (Government == "$government_None;")
            {
                IsTrainingEvent = true;
            }

            Taxi = evt["Taxi"].BoolNull();
            Multicrew = evt["Multicrew"].BoolNull();

            LandingPads = evt["LandingPads"]?.ToObjectQ<LandingPadList>();      // only from odyssey release 5
        }

        public string StationName { get; set; }
        public string StationType { get; set; }
        public string StationState { get; set; }            // only present in stations not normal - UnderAttack, Damaged, UnderRepairs. Null otherwise
        public string StarSystem { get; set; }
        public long? SystemAddress { get; set; }
        public long? MarketID { get; set; }
        public bool CockpitBreach { get; set; }
        public string Faction { get; set; }
        public string FactionState { get; set; }
        public string Allegiance { get; set; }
        public string Economy { get; set; }
        public string Economy_Localised { get; set; }
        public Economies[] EconomyList { get; set; }        // may be null
        public string Government { get; set; }
        public string Government_Localised { get; set; }
        public string[] StationServices { get; set; }
        public bool Wanted { get; set; }
        public bool? ActiveFine { get; set; }

        public bool? Taxi { get; set; }             //4.0 alpha 4
        public bool? Multicrew { get; set; }
        public LandingPadList LandingPads { get; set; } // 4.0 update 5

        public bool IsTrainingEvent { get; private set; }

        public class Economies
        {
            public string Name;
            public string Name_Localised;
            public double Proportion;
        }

        public class LandingPadList
        {
            public int Small;
            public int Medium;
            public int Large;
        };

        public override string SummaryName(ISystem sys) { return string.Format("At {0}".T(EDCTx.JournalDocked_At), StationName); }

        public override void FillInformation(ISystem sys, string whereami, out string info, out string detailed)      
        {
            info = "Docked".T(EDCTx.JournalTypeEnum_Docked) + ", ";
            info += BaseUtils.FieldBuilder.Build("Type: ".T(EDCTx.JournalEntry_Type), StationType, "< in system ".T(EDCTx.JournalEntry_insystem), StarSystem, 
                "State: ".TxID(EDCTx.JournalLocOrJump_State),StationState,
                ";(Wanted)".T(EDCTx.JournalEntry_Wanted), Wanted, 
                ";Active Fine".T(EDCTx.JournalEntry_ActiveFine),ActiveFine,
                "Faction: ".T(EDCTx.JournalEntry_Faction), Faction,  "< in state ".T(EDCTx.JournalEntry_instate), FactionState.SplitCapsWord());

            detailed = BaseUtils.FieldBuilder.Build("Allegiance: ".T(EDCTx.JournalEntry_Allegiance), Allegiance, "Economy: ".T(EDCTx.JournalEntry_Economy), Economy_Localised, "Government: ".T(EDCTx.JournalEntry_Government), Government_Localised);

            if (StationServices != null)
            {
                string l = "";
                foreach (string s in StationServices)
                    l = l.AppendPrePad(s.SplitCapsWord(), ", ");
                detailed += System.Environment.NewLine + "Station services: ".T(EDCTx.JournalEntry_Stationservices) + l;
            }

            if ( EconomyList != null )
            {
                string l = "";
                foreach (Economies e in EconomyList)
                    l = l.AppendPrePad(e.Name_Localised.Alt(e.Name) + " " + (e.Proportion * 100).ToString("0.#") + "%", ", ");
                detailed += System.Environment.NewLine + "Economies: ".T(EDCTx.JournalEntry_Economies) + l;
            }
        }
    }

    [JournalEntryType(JournalTypeEnum.DockingCancelled)]
    public class JournalDockingCancelled : JournalEntry
    {
        public JournalDockingCancelled(JObject evt) : base(evt, JournalTypeEnum.DockingCancelled)
        {
            StationName = evt["StationName"].Str();
            StationType = evt["StationType"].Str().SplitCapsWord();
            MarketID = evt["MarketID"].LongNull();
        }

        public string StationName { get; set; }
        public string StationType { get; set; }
        public long? MarketID { get; set; }

        public override void FillInformation(ISystem sys, string whereami, out string info, out string detailed)
        {
            info = StationName;
            detailed = "";
        }
    }

    [JournalEntryType(JournalTypeEnum.DockingDenied)]
    public class JournalDockingDenied : JournalEntry
    {
        public JournalDockingDenied(JObject evt) : base(evt, JournalTypeEnum.DockingDenied)
        {
            StationName = evt["StationName"].Str();
            Reason = evt["Reason"].Str().SplitCapsWord();
            StationType = evt["StationType"].Str().SplitCapsWord();
            MarketID = evt["MarketID"].LongNull();
        }

        public string StationName { get; set; }
        public string Reason { get; set; }
        public string StationType { get; set; }
        public long? MarketID { get; set; }

        public override void FillInformation(ISystem sys, string whereami, out string info, out string detailed)
        {
            info = BaseUtils.FieldBuilder.Build("", StationName, "", Reason);
            detailed = "";
        }
    }

    [JournalEntryType(JournalTypeEnum.DockingGranted)]
    public class JournalDockingGranted : JournalEntry
    {
        public JournalDockingGranted(JObject evt) : base(evt, JournalTypeEnum.DockingGranted)
        {
            StationName = evt["StationName"].Str();
            LandingPad = evt["LandingPad"].Int();
            StationType = evt["StationType"].Str().SplitCapsWord();
            MarketID = evt["MarketID"].LongNull();
        }

        public string StationName { get; set; }
        public int LandingPad { get; set; }
        public string StationType { get; set; }
        public long? MarketID { get; set; }

        public override void FillInformation(ISystem sys, string whereami, out string info, out string detailed)
        {
            info = BaseUtils.FieldBuilder.Build("", StationName, "< on pad ".T(EDCTx.JournalEntry_onpad), LandingPad, "Type: ".T(EDCTx.JournalEntry_Type), StationType);
            detailed = "";
        }
    }

    [JournalEntryType(JournalTypeEnum.DockingRequested)]
    public class JournalDockingRequested : JournalEntry
    {
        public JournalDockingRequested(JObject evt) : base(evt, JournalTypeEnum.DockingRequested)
        {
            StationName = evt["StationName"].Str();
            StationType = evt["StationType"].Str().SplitCapsWord();
            MarketID = evt["MarketID"].LongNull();
            LandingPads = evt["LandingPads"]?.ToObjectQ<JournalDocked.LandingPadList>();      // only from odyssey release 5
        }

        public string StationName { get; set; }
        public string StationType { get; set; }
        public long? MarketID { get; set; }
        public JournalDocked.LandingPadList LandingPads { get; set; } // 4.0 update 5

        public override void FillInformation(ISystem sys, string whereami, out string info, out string detailed)
        {
            info = StationName;
            detailed = "";
        }
    }

    [JournalEntryType(JournalTypeEnum.DockingTimeout)]
    public class JournalDockingTimeout : JournalEntry
    {
        public JournalDockingTimeout(JObject evt) : base(evt, JournalTypeEnum.DockingTimeout)
        {
            StationName = evt["StationName"].Str();
            StationType = evt["StationType"].Str().SplitCapsWord();
            MarketID = evt["MarketID"].LongNull();
        }

        public string StationName { get; set; }
        public string StationType { get; set; }
        public long? MarketID { get; set; }

        public override void FillInformation(ISystem sys, string whereami, out string info, out string detailed)
        {
            info = StationName;
            detailed = "";
        }
    }


    [JournalEntryType(JournalTypeEnum.Undocked)]
    public class JournalUndocked : JournalEntry
    {
        public JournalUndocked(JObject evt) : base(evt, JournalTypeEnum.Undocked)
        {
            StationName = evt["StationName"].Str();
            StationType = evt["StationType"].Str().SplitCapsWord();
            MarketID = evt["MarketID"].LongNull();
            Taxi = evt["Taxi"].BoolNull();
            Multicrew = evt["Multicrew"].BoolNull();
        }

        public string StationName { get; set; }
        public string StationType { get; set; }
        public long? MarketID { get; set; }

        public bool? Taxi { get; set; }             //4.0 alpha 4
        public bool? Multicrew { get; set; }

        public override void FillInformation(ISystem sys, string whereami, out string info, out string detailed)
        {
            info = BaseUtils.FieldBuilder.Build("", StationName, "Type: ".T(EDCTx.JournalEntry_Type), StationType);
            detailed = "";
        }
    }


}
