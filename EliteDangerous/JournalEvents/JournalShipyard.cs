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
using System.Linq;

namespace EliteDangerousCore.JournalEvents
{
    [JournalEntryType(JournalTypeEnum.Shipyard)]
    public class JournalShipyard : JournalEntry, IAdditionalFiles
    {
        public JournalShipyard(JObject evt) : base(evt, JournalTypeEnum.Shipyard)
        {
            Rescan(evt);
        }

        public JournalShipyard(DateTime utc, string sn, string sys, long mid, Tuple<long, string, long>[] list, int cmdrid, bool allowcobramkiv, bool horizons = true) :
              base(utc, JournalTypeEnum.Shipyard, false)
        {
            MarketID = mid;
            Horizons = horizons;
            AllowCobraMkIV = allowcobramkiv;
            var nlist = list.Select(x => new ShipYard.ShipyardItem { id = x.Item1, ShipType = x.Item2, ShipPrice = x.Item3 }).ToArray();
            Yard = new ShipYard(sn, sys, utc, nlist);
            SetCommander(cmdrid);
        }

        public void Rescan(JObject evt)
        {
            Yard = new ShipYard(evt["StationName"].Str(), evt["StarSystem"].Str(), EventTimeUTC, evt["PriceList"]?.ToObjectQ<ShipYard.ShipyardItem[]>());
            MarketID = evt["MarketID"].LongNull();
            Horizons = evt["Horizons"].BoolNull();
            AllowCobraMkIV = evt["AllowCobraMkIV"].BoolNull();
        }

        public void ReadAdditionalFiles(string directory)
        {
            JObject jnew = ReadAdditionalFile(System.IO.Path.Combine(directory, "Shipyard.json"), EventTypeStr);
            if (jnew != null)        // new json, rescan
            {
                Rescan(jnew);
                UpdateJson(jnew);
            }
        }

        public JObject ToJSON()
        {
            JArray itemlist = new JArray(Yard.Ships.Select(x => new JObject() { { "id", x.id }, { "ShipType", x.ShipType }, { "ShipType_Localised", x.ShipType_Localised },{ "ShipPrice", x.ShipPrice } }));

            JObject j = new JObject()
            {
                ["timestamp"] = EventTimeUTC.ToStringZuluInvariant(),
                ["event"] = EventTypeStr,
                ["StationName"] = Yard.StationName,
                ["StarSystem"] = Yard.StarSystem,
                ["MarketID"] = MarketID,
                ["Horizons"] = Horizons,
                ["AllowCobraMkIV"] = AllowCobraMkIV,
                ["PriceList"] = itemlist,
            };

            return j;
        }


        public ShipYard Yard { get; set; }
        public long? MarketID { get; set; }
        public bool? Horizons { get; set; }
        public bool? AllowCobraMkIV { get; set; }

        public override void FillInformation(ISystem sys, string whereami, out string info, out string detailed) 
        {
            info = "";
            detailed = "";

            if (Yard.Ships != null)
            {
                if (Yard.Ships.Length < 5)
                {
                    foreach (ShipYard.ShipyardItem m in Yard.Ships)
                        info = info.AppendPrePad(m.ShipType_Localised.Alt(m.FriendlyShipType), ", ");
                }
                else
                    info = Yard.Ships.Length.ToString() + " " + "Ships".T(EDCTx.JournalEntry_Ships);

                foreach (ShipYard.ShipyardItem m in Yard.Ships)
                {
                    detailed = detailed.AppendPrePad(BaseUtils.FieldBuilder.Build("",m.ShipType_Localised.Alt(m.FriendlyShipType), "; cr;N0", m.ShipPrice), System.Environment.NewLine);
                }
            }
        }
    }

    [JournalEntryType(JournalTypeEnum.ShipyardBuy)]
    public class JournalShipyardBuy : JournalEntry, ILedgerJournalEntry, IShipInformation
    {
        public JournalShipyardBuy(JObject evt) : base(evt, JournalTypeEnum.ShipyardBuy)
        {
            ShipTypeFD = JournalFieldNaming.NormaliseFDShipName(evt["ShipType"].Str());
            ShipType = JournalFieldNaming.GetBetterShipName(ShipTypeFD);
            ShipPrice = evt["ShipPrice"].Long();

            StoreOldShipFD = evt["StoreOldShip"].StrNull();
            if (StoreOldShipFD != null)
            {
                StoreOldShipFD = JournalFieldNaming.NormaliseFDShipName(StoreOldShipFD);
                StoreOldShip = JournalFieldNaming.GetBetterShipName(StoreOldShipFD);
            }

            StoreOldShipId = evt["StoreShipID"].ULongNull();

            SellOldShipFD = evt["SellOldShip"].StrNull();
            if (SellOldShipFD != null)
            {
                SellOldShipFD = JournalFieldNaming.NormaliseFDShipName(SellOldShipFD);
                SellOldShip = JournalFieldNaming.GetBetterShipName(SellOldShipFD);
            }

            SellOldShipId = evt["SellShipID"].ULongNull();

            SellPrice = evt["SellPrice"].LongNull();

            MarketID = evt["MarketID"].LongNull();
        }

        public string ShipTypeFD { get; set; }
        public string ShipType { get; set; }
        public long ShipPrice { get; set; }

        public string StoreOldShipFD { get; set; }      // may be null
        public string StoreOldShip { get; set; }        // may be null
        public ulong? StoreOldShipId { get; set; }      // may be null

        public string SellOldShipFD { get; set; }       // may be null         
        public string SellOldShip { get; set; }         // may be null
        public ulong? SellOldShipId { get; set; }       // may be null

        public long? SellPrice { get; set; }
        public long? MarketID { get; set; }

        public void Ledger(Ledger mcl)
        {
            mcl.AddEvent(Id, EventTimeUTC, EventTypeID, ShipType, -ShipPrice + (SellPrice ?? 0));
        }

        public void ShipInformation(ShipInformationList shp, string whereami, ISystem system)
        {                                   // new will come along and provide the new ship info
            //System.Diagnostics.Debug.WriteLine(EventTimeUTC + " Buy");
            if (StoreOldShipId != null && StoreOldShipFD != null)
                shp.Store(StoreOldShipFD, StoreOldShipId.Value, whereami, system.Name);

            if (SellOldShipId != null && SellOldShipFD != null)
                shp.Sell(SellOldShipFD, SellOldShipId.Value);
        }

        public override void FillInformation(ISystem sys, string whereami, out string info, out string detailed)
        {
            info = BaseUtils.FieldBuilder.Build("", ShipType, "Amount: ; cr;N0".T(EDCTx.JournalEntry_Amount), ShipPrice);
            if (StoreOldShip != null)
                info += ", " + BaseUtils.FieldBuilder.Build("Stored: ".T(EDCTx.JournalEntry_Stored), StoreOldShip);
            if (SellOldShip != null)
                info += ", " + BaseUtils.FieldBuilder.Build("Sold: ".T(EDCTx.JournalEntry_Sold), StoreOldShip, "Amount: ; cr;N0".T(EDCTx.JournalEntry_Amount), SellPrice);
            detailed = "";
        }

    }

    [JournalEntryType(JournalTypeEnum.ShipyardNew)]
    public class JournalShipyardNew : JournalEntry, IShipInformation
    {
        public JournalShipyardNew(JObject evt) : base(evt, JournalTypeEnum.ShipyardNew)
        {
            ShipFD = JournalFieldNaming.NormaliseFDShipName(evt["ShipType"].Str());
            ShipType = JournalFieldNaming.GetBetterShipName(ShipFD);
            ShipId = evt["NewShipID"].ULong();
        }

        public string ShipType { get; set; }
        public string ShipFD { get; set; }
        public ulong ShipId { get; set; }

        public void ShipInformation(ShipInformationList shp, string whereami, ISystem system)
        {
            //System.Diagnostics.Debug.WriteLine(EventTimeUTC + " NEW");
            shp.ShipyardNew(ShipType, ShipFD, ShipId);
        }

        public override void FillInformation(ISystem sys, string whereami, out string info, out string detailed)
        {
            info = ShipType;
            detailed = "";
        }
    }

    [JournalEntryType(JournalTypeEnum.ShipyardSell)]
    public class JournalShipyardSell : JournalEntry, ILedgerJournalEntry, IShipInformation
    {
        public JournalShipyardSell(JObject evt) : base(evt, JournalTypeEnum.ShipyardSell)
        {
            MarketID = evt["MarketID"].LongNull();
            ShipTypeFD = JournalFieldNaming.NormaliseFDShipName(evt["ShipType"].Str());
            ShipType = JournalFieldNaming.GetBetterShipName(ShipTypeFD);
            SellShipId = evt["SellShipID"].ULong();
            ShipPrice = evt["ShipPrice"].Long();
            System = evt["System"].Str();
        }

        public JournalShipyardSell(DateTime utc, string fdtype, ulong id, long price, int cmdrid) : base(utc, JournalTypeEnum.ShipyardSell, false)
        {
            ShipTypeFD = fdtype;
            SellShipId = id;
            ShipPrice = price;
            SetCommander(cmdrid);
        }

        public string ShipTypeFD { get; set; }
        public string ShipType { get; set; }
        public ulong SellShipId { get; set; }
        public long ShipPrice { get; set; }
        public string System { get; set; }      // may be empty
        public long? MarketID { get; set; }

        public void Ledger(Ledger mcl)
        {
            mcl.AddEvent(Id, EventTimeUTC, EventTypeID, ShipType, ShipPrice);
        }

        public void ShipInformation(ShipInformationList shp, string whereami, ISystem system)
        {
            //Debug.WriteLine(EventTimeUTC + " SELL");
            shp.Sell(ShipTypeFD, SellShipId);
        }

        public override void FillInformation(ISystem sys, string whereami, out string info, out string detailed)
        {
            info = BaseUtils.FieldBuilder.Build("", ShipType, "Amount: ; cr;N0".T(EDCTx.JournalEntry_Amount), ShipPrice, "At: ".T(EDCTx.JournalShipyardSell_At), System);
            detailed = "";
        }

        public JObject Json()            // create JSON of this record..
        {
            JObject evt = new JObject();
            evt["timestamp"] = EventTimeUTC;
            evt["event"] = EventTypeStr;
            if ( MarketID.HasValue )
                evt["MarketID"] = MarketID.Value;
            evt["ShipType"] = ShipTypeFD;
            evt["SellShipID"] = SellShipId;
            evt["ShipPrice"] = ShipPrice;
            if (System.HasChars())
                evt["System"] = System;

            return evt;
        }
    }


    [JournalEntryType(JournalTypeEnum.ShipyardSwap)]
    public class JournalShipyardSwap : JournalEntry, IShipInformation
    {
        public JournalShipyardSwap(JObject evt) : base(evt, JournalTypeEnum.ShipyardSwap)
        {
            ShipFD = JournalFieldNaming.NormaliseFDShipName(evt["ShipType"].Str());
            ShipType = JournalFieldNaming.GetBetterShipName(ShipFD);
            ShipId = evt["ShipID"].ULong();

            StoreOldShip = StoreOldShipFD = evt["StoreOldShip"].Str();      // set to evt value first
            
            if (StoreOldShip.HasChars())    // if we have something, normalise
            {
                StoreOldShipFD = JournalFieldNaming.NormaliseFDShipName(StoreOldShipFD);
                StoreOldShip = JournalFieldNaming.GetBetterShipName(StoreOldShipFD);
            }

            StoreShipId = evt["StoreShipID"].ULongNull();

            MarketID = evt["MarketID"].LongNull();
        }

        public string ShipFD { get; set; }
        public string ShipType { get; set; }
        public ulong ShipId { get; set; }

        public string StoreOldShipFD { get; set; }
        public string StoreOldShip { get; set; }
        public ulong? StoreShipId { get; set; }

        public long? MarketID { get; set; }

        public void ShipInformation(ShipInformationList shp, string whereami, ISystem system)
        {
            //System.Diagnostics.Debug.WriteLine(EventTimeUTC + " SWAP");
            shp.ShipyardSwap(this, whereami, system.Name);
        }

        public override void FillInformation(ISystem sys, string whereami, out string info, out string detailed)
        {
            info = BaseUtils.FieldBuilder.Build("Swap ".T(EDCTx.JournalShipyardSwap_Swap), StoreOldShip, "< for a ".T(EDCTx.JournalShipyardSwap_fora), ShipType);
            detailed = "";
        }
    }

    [JournalEntryType(JournalTypeEnum.ShipyardTransfer)]
    public class JournalShipyardTransfer : JournalEntry, ILedgerJournalEntry, IShipInformation
    {
        public JournalShipyardTransfer(JObject evt) : base(evt, JournalTypeEnum.ShipyardTransfer)
        {
            ShipTypeFD = JournalFieldNaming.NormaliseFDShipName(evt["ShipType"].Str());
            ShipType = JournalFieldNaming.GetBetterShipName(ShipTypeFD);
            ShipId = evt["ShipID"].ULong();

            FromSystem = evt["System"].Str();
            Distance = evt["Distance"].Double();
            TransferPrice = evt["TransferPrice"].Long();

            if (Distance > 100000.0)       // previously, it was in m, now they have changed it to LY per 2.3. So if its large (over 100k ly, impossible) convert
                Distance = Distance / 299792458.0 / 365 / 24 / 60 / 60;

            nTransferTime = evt["TransferTime"].IntNull();
            FriendlyTransferTime = nTransferTime.HasValue ? nTransferTime.Value.SecondsToString() : "";

            MarketID = evt["MarketID"].LongNull();
            ShipMarketID = evt["ShipMarketID"].LongNull();
        }

        public string ShipTypeFD { get; set; }
        public string ShipType { get; set; }
        public ulong ShipId { get; set; }
        public string FromSystem { get; set; }
        public double Distance { get; set; }
        public long TransferPrice { get; set; }
        public int? nTransferTime { get; set; }
        public string FriendlyTransferTime { get; set; }
        public long? MarketID { get; set; }
        public long? ShipMarketID { get; set; }

        public void Ledger(Ledger mcl)
        {
            mcl.AddEvent(Id, EventTimeUTC, EventTypeID, ShipType, -TransferPrice);
        }

        public void ShipInformation(ShipInformationList shp, string whereami, ISystem system)
        {
            DateTime arrival = EventTimeUTC.AddSeconds(nTransferTime ?? 0);
            //System.Diagnostics.Debug.WriteLine(EventTimeUTC + " Transfer");
            shp.Transfer(ShipType, ShipTypeFD, ShipId, FromSystem, system.Name, whereami, arrival);
        }

        public override void FillInformation(ISystem sys, string whereami, out string info, out string detailed)
        {
            info = BaseUtils.FieldBuilder.Build("Of ".T(EDCTx.JournalShipyardTransfer_Of), ShipType, "< from ".T(EDCTx.JournalEntry_from), FromSystem, "Distance: ; ly;0.0".T(EDCTx.JournalEntry_Distance), 
                            Distance, "Price: ; cr;N0".T(EDCTx.JournalEntry_Price), TransferPrice, "Transfer Time: ".T(EDCTx.JournalEntry_TransferTime), FriendlyTransferTime);
            detailed = "";
        }
    }


    [JournalEntryType(JournalTypeEnum.StoredShips)]
    public class JournalStoredShips : JournalEntry, IShipInformation
    {
        public JournalStoredShips(JObject evt) : base(evt, JournalTypeEnum.StoredShips)
        {
            StationName = evt["StationName"].Str();
            StarSystem = evt["StarSystem"].Str();
            MarketID = evt["MarketID"].LongNull();

            ShipsHere = evt["ShipsHere"]?.ToObjectQ<StoredShipInformation[]>();
            Normalise(ShipsHere);

            if (ShipsHere != null)
            {
                foreach (var x in ShipsHere)
                {
                    x.StarSystem = StarSystem;
                    x.StationName = StationName;
                }
            }

            ShipsRemote = evt["ShipsRemote"]?.ToObjectQ<StoredShipInformation[]>();
            Normalise(ShipsRemote);
        }

        public string StationName { get; set; }
        public string StarSystem { get; set; }
        public long? MarketID { get; set; }

        public StoredShipInformation[] ShipsHere { get; set; }
        public StoredShipInformation[] ShipsRemote { get; set; }

        public override void FillInformation(ISystem sys, string whereami, out string info, out string detailed)
        {

            info = BaseUtils.FieldBuilder.Build("At starport: ".T(EDCTx.JournalStoredShips_Atstarport), ShipsHere?.Count(), "Other locations: ".T(EDCTx.JournalStoredShips_Otherlocations), ShipsRemote?.Count());
            detailed = "";
            if (ShipsHere != null)
            {
                foreach (StoredShipInformation m in ShipsHere)
                    detailed = detailed.AppendPrePad(BaseUtils.FieldBuilder.Build("", m.ShipType, "; cr;N0".T(EDCTx.JournalStoredShips_SSP), m.Value, ";(Hot)".T(EDCTx.JournalEntry_Hot), m.Hot), System.Environment.NewLine);
            }
            if (ShipsRemote != null)
            {
                detailed = detailed.AppendPrePad("Remote: ".T(EDCTx.JournalStoredShips_Remote), System.Environment.NewLine + System.Environment.NewLine);

                foreach (StoredShipInformation m in ShipsRemote)
                {
                    if (m.InTransit)
                    {
                        detailed = detailed.AppendPrePad(BaseUtils.FieldBuilder.Build("; ", m.Name,
                                    "<; in transit".T(EDCTx.JournalStoredShips_intransit), m.ShipType,
                                    "Value: ; cr;N0".T(EDCTx.JournalEntry_Value), m.Value, ";(Hot)".T(EDCTx.JournalEntry_Hot), m.Hot), System.Environment.NewLine);

                    }
                    else
                    {
                        detailed = detailed.AppendPrePad(BaseUtils.FieldBuilder.Build(
                            "; ", m.Name,
                            "<", m.ShipType,
                            "< at ".T(EDCTx.JournalStoredShips_at), m.StarSystem,
                            "Transfer Cost: ; cr;N0".T(EDCTx.JournalEntry_TransferCost), m.TransferPrice, "Time: ".T(EDCTx.JournalEntry_Time), m.TransferTimeString,
                            "Value: ; cr;N0".T(EDCTx.JournalEntry_Value), m.Value, ";(Hot)".T(EDCTx.JournalEntry_Hot), m.Hot), System.Environment.NewLine);
                    }
                }
            }
        }

        public void Normalise(StoredShipInformation[] s)
        {
            if (s != null)
            {
                foreach (StoredShipInformation i in s)
                    i.Normalise();
            }
        }

        public void ShipInformation(ShipInformationList shp, string whereami, ISystem system)
        {
            //System.Diagnostics.Debug.WriteLine(EventTimeUTC + " StoredShips");
            if (ShipsHere != null)
                shp.StoredShips(ShipsHere);
            if (ShipsRemote != null)
                shp.StoredShips(ShipsRemote);
        }

    }


    [JournalEntryType(JournalTypeEnum.SellShipOnRebuy)]
    public class JournalSellShipOnRebuy : JournalEntry, ILedgerJournalEntry, IShipInformation
    {
        public JournalSellShipOnRebuy(JObject evt) : base(evt, JournalTypeEnum.SellShipOnRebuy)
        {
            ShipTypeFD = JournalFieldNaming.NormaliseFDShipName(evt["ShipType"].Str());
            ShipType = JournalFieldNaming.GetBetterShipName(ShipTypeFD);
            System = evt["System"].Str();
            SellShipId = evt["SellShipId"].ULong();
            ShipPrice = evt["ShipPrice"].Long();
        }

        public string ShipTypeFD { get; set; }
        public string ShipType { get; set; }
        public string System { get; set; }
        public ulong SellShipId { get; set; }
        public long ShipPrice { get; set; }

        public void Ledger(Ledger mcl)
        {
            mcl.AddEvent(Id, EventTimeUTC, EventTypeID, ShipType, ShipPrice);
        }

        public void ShipInformation(ShipInformationList shp, string whereami, ISystem system)
        {
            shp.Sell(ShipType, SellShipId);
        }

        public override void FillInformation(ISystem sys, string whereami, out string info, out string detailed)
        {
            info = BaseUtils.FieldBuilder.Build("Ship: ".T(EDCTx.JournalEntry_Ship), ShipType, "System: ".T(EDCTx.JournalEntry_System), System, "Price: ; cr;N0".T(EDCTx.JournalEntry_Price), ShipPrice);
            detailed = "";
        }
    }


}
