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
using System.Collections.Generic;
using System.Linq;

namespace EliteDangerousCore.JournalEvents
{
    [JournalEntryType(JournalTypeEnum.EDDCommodityPrices)]
    public class JournalEDDCommodityPrices : JournalCommodityPricesBase
    {
        public JournalEDDCommodityPrices(JObject evt) : base(evt, JournalTypeEnum.EDDCommodityPrices)
        {
            Station = evt["station"].Str();         // these are the old names used in EDD CP, not aligned to market (introduced before). keep
            StarSystem = evt["starsystem"].Str();
            MarketID = evt["MarketID"].LongNull();
            Rescan(evt["commodities"].Array());
        }

        public void Rescan(JArray jcommodities)
        {
            Commodities = new List<CCommodities>();

            if (jcommodities != null)
            {
                foreach (JObject commodity in jcommodities)
                {
                    CCommodities com = new CCommodities(commodity, CCommodities.ReaderType.CAPI);
                    Commodities.Add(com);
                }

                Commodities.Sort((l, r) => l.locName.CompareTo(r.locName));
            }
        }

        public JournalEDDCommodityPrices(System.DateTime utc, long? marketid, string station, string starsystem, int cmdrid, JArray commds) : 
                                        base(utc, JournalTypeEnum.EDDCommodityPrices, marketid, station, starsystem, cmdrid)
        {
            Rescan(commds);
        }

        public JObject ToJSON()
        {
            JObject j = new JObject()
            {
                ["timestamp"] = EventTimeUTC.ToStringZuluInvariant(),
                ["event"] = EventTypeStr,
                ["starsystem"] = StarSystem,
                ["station"] = Station,
                ["MarketID"] = MarketID,
                ["commodities"] = JToken.FromObject(Commodities, true)
            };

            return j;
        }
    }

    public class JournalCommodityPricesBase : JournalEntry
    {
        public JournalCommodityPricesBase(JObject evt, JournalTypeEnum en) : base(evt, en)
        {
        }

        public JournalCommodityPricesBase(System.DateTime utc, JournalTypeEnum type, long? marketid, string station, string starsystem, int cmdrid) 
                                            : base(utc, type, false)
        {
            MarketID = marketid;
            Station = station;
            StarSystem = starsystem;
            Commodities = new List<CCommodities>(); // always made..
            SetCommander(cmdrid);
        }

        public string Station { get; protected set; }
        public string StarSystem { get; set; }
        public long? MarketID { get; set; }
        public List<CCommodities> Commodities { get; protected set; }   // never null

        public bool HasCommodity(string fdname) { return Commodities.FindIndex(x => x.fdname.Equals(fdname, System.StringComparison.InvariantCultureIgnoreCase)) >= 0; }
        public bool HasCommodityToBuy(string fdname) { return Commodities.FindIndex(x => x.fdname.Equals(fdname, System.StringComparison.InvariantCultureIgnoreCase) && x.CanBeBought) >= 0; }

        public override void FillInformation(ISystem sys, string whereami, out string info, out string detailed)
        {
            FillInformation(sys, whereami, out info, out detailed, Commodities.Count > 60 ? 2 : 1);
        }

        public void FillInformation(ISystem sys, string whereami, out string info, out string detailed, int maxcol)
        {

            info = BaseUtils.FieldBuilder.Build("Prices on ; items".T(EDCTx.JournalCommodityPricesBase_PON), Commodities.Count, 
                                                "< at ".T(EDCTx.JournalCommodityPricesBase_CPBat), Station , 
                                                "< in ".T(EDCTx.JournalCommodityPricesBase_CPBin), StarSystem);

            int col = 0;
            detailed = "Items to buy: ".T(EDCTx.JournalCommodityPricesBase_Itemstobuy) + System.Environment.NewLine;
            foreach (CCommodities c in Commodities)
            {
                if (c.CanBeBought)
                {
                    string name = MaterialCommodityMicroResourceType.GetNameByFDName(c.fdname);

                    if (c.CanBeSold)
                    {
                        detailed += string.Format("{0}: {1} sell {2} Diff {3} {4}%  ".T(EDCTx.JournalCommodityPricesBase_CPBBuySell),
                            name, c.buyPrice, c.sellPrice, c.buyPrice - c.sellPrice, 
                            ((double)(c.buyPrice - c.sellPrice) / (double)c.sellPrice * 100.0).ToString("0.#"));
                    }
                    else
                        detailed += string.Format("{0}: {1}  ".T(EDCTx.JournalCommodityPricesBase_CPBBuy), name, c.buyPrice);

                    if (++col == maxcol)
                    {
                        detailed += System.Environment.NewLine;
                        col = 0;
                    }
                }
            }

            if (col == maxcol - 1)
                detailed += System.Environment.NewLine;

            col = 0;
            detailed += "Sell only Items: ".T(EDCTx.JournalCommodityPricesBase_SO) + System.Environment.NewLine;
            foreach (CCommodities c in Commodities)
            {
                if (!c.CanBeBought)
                {
                    string name = MaterialCommodityMicroResourceType.GetNameByFDName(c.fdname);

                    detailed += string.Format("{0}: {1}  ".T(EDCTx.JournalCommodityPricesBase_CPBBuy), name, c.sellPrice);
                    if (++col == maxcol)
                    {
                        detailed += System.Environment.NewLine;
                        col = 0;
                    }
                }
            }
        }

    }


    //When written: by EDD when a user manually sets an item count (material or commodity)
    [JournalEntryType(JournalTypeEnum.EDDItemSet)]
    public class JournalEDDItemSet : JournalEntry, ICommodityJournalEntry, IMaterialJournalEntry
    {
        public JournalEDDItemSet(JObject evt) : base(evt, JournalTypeEnum.EDDItemSet)
        {
            Materials = new MaterialListClass(evt["Materials"]?.ToObjectQ<MaterialItem[]>().ToList());
            Commodities = new CommodityListClass(evt["Commodities"]?.ToObjectQ<CommodityItem[]>().ToList());
        }

        public MaterialListClass Materials { get; set; }             // FDNAMES
        public CommodityListClass Commodities { get; set; }

        public void UpdateMaterials(MaterialCommoditiesMicroResourceList mc)
        {
            if (Materials != null)
            {
                foreach (MaterialItem m in Materials.Materials)
                    mc.Change(EventTimeUTC, m.Category, m.Name, m.Count, 0, 0, true);
            }
        }

        public void UpdateCommodities(MaterialCommoditiesMicroResourceList mc, bool unusedinsrv)
        { 
            if (Commodities != null)
            {
                foreach (CommodityItem m in Commodities.Commodities)
                    mc.Change(EventTimeUTC, MaterialCommodityMicroResourceType.CatType.Commodity, m.Name, m.Count, (long)m.BuyPrice, 0, true);
            }
        }

        public override void FillInformation(ISystem sys, string whereami, out string info, out string detailed)
        {

            info = "";
            bool comma = false;
            if (Materials != null)
            {
                foreach (MaterialItem m in Materials.Materials)
                {
                    if (comma)
                        info += ", ";
                    comma = true;
                    info += BaseUtils.FieldBuilder.Build("Name: ".T(EDCTx.JournalEntry_Name), MaterialCommodityMicroResourceType.GetNameByFDName(m.Name), "", m.Count);
                }
            }

            if (Commodities != null)
            {
                foreach (CommodityItem m in Commodities.Commodities)
                {
                    if (comma)
                        info += ", ";
                    comma = true;
                    info += BaseUtils.FieldBuilder.Build("Name: ".T(EDCTx.JournalEntry_Name), MaterialCommodityMicroResourceType.GetNameByFDName(m.Name), "", m.Count);
                }
            }
            detailed = "";
        }

        public class MaterialItem
        {
            public string Name;     //FDNAME
            public string Category;
            public int Count;
        }

        public class CommodityItem
        {
            public string Name;     //FDNAME
            public int Count;
            public double BuyPrice;
        }

        public class MaterialListClass
        {
            public MaterialListClass(System.Collections.Generic.List<MaterialItem> ma)
            {
                Materials = ma ?? new System.Collections.Generic.List<MaterialItem>();
                foreach (MaterialItem i in Materials)
                    i.Name = JournalFieldNaming.FDNameTranslation(i.Name);
            }

            public System.Collections.Generic.List<MaterialItem> Materials { get; protected set; }
        }

        public class CommodityListClass
        {
            public CommodityListClass(System.Collections.Generic.List<CommodityItem> ma)
            {
                Commodities = ma ?? new System.Collections.Generic.List<CommodityItem>();
                foreach (CommodityItem i in Commodities)
                    i.Name = JournalFieldNaming.FDNameTranslation(i.Name);
            }

            public System.Collections.Generic.List<CommodityItem> Commodities { get; protected set; }
        }
    }

}


