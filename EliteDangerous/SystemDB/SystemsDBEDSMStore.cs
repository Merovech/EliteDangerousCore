﻿/*
 * Copyright 2015 - 2021 EDDiscovery development team
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
using System.IO;
using System.Data.Common;
using System.Data;
using System.IO.Compression;

namespace EliteDangerousCore.DB
{
    public partial class SystemsDB
    {
        #region Table Update from JSON FILE

        // All of these needs the systems DB to be in write mode. Make sure it is
        // and check the system db rebuilding flag before using them - its above this level so can't logically be checked here

        // store single system to DB

        public static long StoreSystems(List<ISystem> systems)
        {
            JArray jlist = new JArray();

            foreach (var sys in systems)
            {
                if (sys.EDSMID > 0 && sys.HasCoordinate)
                {
                    JObject jo = new JObject
                    {
                        ["name"] = sys.Name,
                        ["id"] = sys.EDSMID,
                        ["date"] = DateTime.UtcNow,
                        ["coords"] = new JObject { ["x"] = sys.X, ["y"] = sys.Y, ["z"] = sys.Z }
                    };

                    jlist.Add(jo);
                }
            }

            if ( jlist.Count>0)
            { 
                DateTime unusedate = DateTime.UtcNow;
                // we need rewrite access, and run it with the cn passed to us
                return SystemsDB.ParseEDSMJSONString(jlist.ToString(), null, ref unusedate, () => false, (t) => { }, "");
            }

            return 0;
        }
        public static long ParseEDSMJSONFile(string filename, bool[] grididallow, ref DateTime date, Func<bool> cancelRequested, Action<string> reportProgress, string tableposfix, bool presumeempty = false, string debugoutputfile = null)
        {
            // if the filename ends in .gz, then decompress it on the fly
            if (filename.EndsWith("gz"))
            {
                using (FileStream originalFileStream = new FileStream(filename, FileMode.Open, FileAccess.Read))
                {
                    using (GZipStream gz = new GZipStream(originalFileStream, CompressionMode.Decompress))
                    {
                        using (StreamReader sr = new StreamReader(gz))
                        {
                            return ParseEDSMJSON(sr, grididallow, ref date, cancelRequested, reportProgress, tableposfix, presumeempty, debugoutputfile);
                        }
                    }
                }
            }
            else
            {
                using (StreamReader sr = new StreamReader(filename))         // read directly from file..
                    return ParseEDSMJSON(sr, grididallow, ref date, cancelRequested, reportProgress, tableposfix, presumeempty, debugoutputfile);
            }
        }

        public static long ParseEDSMJSONString(string data, bool[] grididallow, ref DateTime date, Func<bool> cancelRequested, Action<string> reportProgress, string tableposfix, bool presumeempty = false, string debugoutputfile = null)
        {
            using (StringReader sr = new StringReader(data))         // read directly from file..
                return ParseEDSMJSON(sr, grididallow, ref date, cancelRequested, reportProgress, tableposfix, presumeempty, debugoutputfile);
        }

        // set tempostfix to use another set of tables

        public static long ParseEDSMJSON(TextReader textreader, 
                                        bool[] grididallowed,       // null = all, else grid bool value
                                        ref DateTime maxdate,       // updated with latest date
                                        Func<bool> cancelRequested,
                                        Action<string> reportProgress,
                                        string tablepostfix,        // set to add on text to table names to redirect to another table
                                        bool tablesareempty = false,     // set to presume table is empty, so we don't have to do look up queries
                                        string debugoutputfile = null
                                        )
        {
            var cache = new SectorCache();

            long updates = 0;

            int nextsectorid = GetNextSectorID();
            StreamWriter sw = null;

            try
            {
#if DEBUG
                try
                {
                    if (debugoutputfile != null) sw = new StreamWriter(debugoutputfile);
                }
                catch
                {
                }
#endif
                var parser = new QuickJSON.Utils.StringParserQuickTextReader(textreader, 32768);
                var enumerator = JToken.ParseToken(parser, JToken.ParseOptions.None).GetEnumerator();       // Parser may throw note

                while (true)
                {
                    if (cancelRequested())
                    {
                        updates = -1;
                        break;
                    }

                    int recordstostore = ProcessBlock(cache, enumerator, grididallowed, tablesareempty, tablepostfix, ref maxdate, ref nextsectorid, out bool jr_eof);

                    System.Diagnostics.Debug.WriteLine($"{Environment.TickCount} Process {BaseUtils.AppTicks.TickCountLap("L1")}  {updates}");

                    if (recordstostore > 0)
                    {
                        updates += StoreNewEntries(cache, tablepostfix, sw);

                        reportProgress?.Invoke("EDSM Star database updated " + recordstostore + " total so far " + updates);
                    }

                    if (jr_eof)
                        break;

                    System.Threading.Thread.Sleep(20);      // just sleepy for a bit to let others use the db
                }
            }
            catch ( Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Exception during EDSM parse " + ex);
            }
            finally
            {
                if (sw != null)
                {
                    sw.Close();
                }
            }

            System.Diagnostics.Debug.WriteLine("Process " + BaseUtils.AppTicks.TickCountLap("L1") + "   " + updates);
            reportProgress?.Invoke("EDSM Star database updated " + updates);

            PutNextSectorID(nextsectorid);    // and store back

            return updates;
        }

        #endregion

        #region Table Update Helpers
        private static int ProcessBlock(SectorCache cache,
                                         IEnumerator<JToken> enumerator,
                                         bool[] grididallowed,       // null = all, else grid bool value
                                         bool tablesareempty,
                                         string tablepostfix,
                                         ref DateTime maxdate,       // updated with latest date
                                         ref int nextsectorid,
                                         out bool jr_eof)
        {
            int recordstostore = 0;
            DbCommand selectSectorCmd = null;
            DateTime cpmaxdate = maxdate;
            int cpnextsectorid = nextsectorid;
            const int BlockSize = 1000000;      // for 66mil stars, 20000 = 38.66m, 100000=34.67m, 1e6 = 28.02m
            int Limit = int.MaxValue;
            var unknownsectorentries = new List<TableWriteData>();
            jr_eof = false;

            while (true)
            {
                if ( !enumerator.MoveNext())        // get next token, if not, stop eof
                {
                    jr_eof = true;
                    break;
                }

                JToken t = enumerator.Current;

                if ( t.IsObject )                   // if start of object..
                {
                    EDSMFileEntry d = new EDSMFileEntry();

                    if (d.Deserialize(enumerator) && d.id >= 0 && d.name.HasChars() && d.z != int.MinValue)     // if we have a valid record
                    {
                        int gridid = GridId.Id128(d.x, d.z);
                        if (grididallowed == null || (grididallowed.Length > gridid && grididallowed[gridid]))    // allows a null or small grid
                        {
                            TableWriteData data = new TableWriteData() { edsm = d, classifier = new EliteNameClassifier(d.name), gridid = gridid };

                            // try and add data to sector
                            // if sector is not in cache, do not make it, return false, instead add to entries
                            // if sector is in cache, add it to the sector update list, return false,
                            // so this accumulates entries which need new sectors.
                            if (!TryCreateNewUpdate(cache, data, tablesareempty, ref cpmaxdate, ref cpnextsectorid, out Sector sector , false))
                            {
                                unknownsectorentries.Add(data); // unknown sector, process below
                            }

                            recordstostore++;
                        }
                    }

                    if (--Limit == 0)
                    {
                        jr_eof = true;
                        break;
                    }

                    if (recordstostore >= BlockSize)
                        break;
                }
            }

            // for unknownsectorentries, create sectors in cache for them

            SystemsDatabase.Instance.DBRead( db =>
            {
                try
                {
                    var cn = db;

                    selectSectorCmd = cn.CreateSelect("Sectors" + tablepostfix, "id", "name = @sname AND gridid = @gid", null,
                                                            new string[] { "sname", "gid" }, new DbType[] { DbType.String, DbType.Int32 });

                    foreach (var entry in unknownsectorentries)
                    {
                        CreateSectorInCacheIfRequired(cache, selectSectorCmd, entry, tablesareempty, ref cpmaxdate, ref cpnextsectorid);
                    }
                }
                finally
                {
                    if (selectSectorCmd != null)
                    {
                        selectSectorCmd.Dispose();
                    }
                }
            });

            maxdate = cpmaxdate;
            nextsectorid = cpnextsectorid;

            return recordstostore;
        }


        // create a new entry for insert in the sector tables 
        // tablesareempty means the tables are fresh and this is the first read
        // makenewiftablesarepresent allows new sectors to be made
        // false means tables are not empty , not making new, and sector not found in cache.. 
        // true means sector is found, and entry is added to sector update list
        private static bool TryCreateNewUpdate(SectorCache cache, TableWriteData data, bool tablesareempty, ref DateTime maxdate, ref int nextsectorid, 
                                                out Sector t, bool makenewiftablesarepresent = false)
        {
            if (data.edsm.date > maxdate)                                   // for all, record last recorded date processed
                maxdate = data.edsm.date;

            Sector prev = null;

            t = null;

            if (!cache.SectorNameCache.ContainsKey(data.classifier.SectorName))   // if unknown to cache
            {
                if (!tablesareempty && !makenewiftablesarepresent)        // if the tables are NOT empty and we can't make new..
                {
                    return false;
                }

                cache.SectorNameCache[data.classifier.SectorName] = t = new Sector(data.classifier.SectorName, gridid: data.gridid);   // make a sector of sectorname and with gridID n , id == -1
            }
            else
            {
                t = cache.SectorNameCache[data.classifier.SectorName];        // find the first sector of name
                while (t != null && t.GId != data.gridid)        // if GID of sector disagrees
                {
                    prev = t;                          // go thru list
                    t = t.NextSector;
                }

                if (t == null)      // still not got it, its a new one.
                {
                    if (!tablesareempty && !makenewiftablesarepresent)
                    {
                        return false;
                    }

                    prev.NextSector = t = new Sector(data.classifier.SectorName, gridid: data.gridid);   // make a sector of sectorname and with gridID n , id == -1
                }
            }

            if (t.Id == -1)   // if unknown sector ID..
            {
                if (tablesareempty)     // if tables are empty, we can just presume its id
                {
                    t.Id = nextsectorid++;      // insert the sector with the guessed ID
                    t.insertsec = true;
                    cache.SectorIDCache[t.Id] = t;    // and cache
                    //System.Diagnostics.Debug.WriteLine("Made sector " + t.Name + ":" + t.GId);
                }
            }

            if (t.edsmdatalist == null)
                t.edsmdatalist = new List<TableWriteData>(5000);

            t.edsmdatalist.Add(data);                       // add to list of systems to process for this sector

            return true;
        }

        // add the data to the sector cache, making it if required.
        // If it was made (id==-1) then find sector, and if not there, assign an ID
        private static void CreateSectorInCacheIfRequired(SectorCache cache, DbCommand selectSectorCmd, TableWriteData data, bool tablesareempty, ref DateTime maxdate, ref int nextsectorid)
        {
            // force the entry into the sector cache.
            TryCreateNewUpdate(cache, data, tablesareempty, ref maxdate, ref nextsectorid, out Sector t, true);

            if (t.Id == -1)   // if unknown sector ID..
            {
                selectSectorCmd.Parameters[0].Value = t.Name;   
                selectSectorCmd.Parameters[1].Value = t.GId;

                using (DbDataReader reader = selectSectorCmd.ExecuteReader())       // find name:gid
                {
                    if (reader.Read())      // if found name:gid
                    {
                        t.Id = (long)reader[0];
                    }
                    else
                    {
                        t.Id = nextsectorid++;      // insert the sector with the guessed ID
                        t.insertsec = true;
                    }

                    cache.SectorIDCache[t.Id] = t;                // and cache
                    //  System.Diagnostics.Debug.WriteLine("Made sector " + t.Name + ":" + t.GId);
                }
            }
        }

        private static long StoreNewEntries(SectorCache cache, string tablepostfix = "",        // set to add on text to table names to redirect to another table
                                           StreamWriter sw = null
                                        )
        {
            ////////////////////////////////////////////////////////////// push all new data to the db without any selects

            return SystemsDatabase.Instance.DBWrite(db =>
            {
                long updates = 0;

                DbTransaction txn = null;
                DbCommand replaceSectorCmd = null;
                DbCommand replaceSysCmd = null;
                DbCommand replaceNameCmd = null;
                try
                {
                    var cn = db;
                    txn = cn.BeginTransaction();

                    replaceSectorCmd = cn.CreateReplace("Sectors" + tablepostfix, new string[] { "name", "gridid", "id" }, new DbType[] { DbType.String, DbType.Int32, DbType.Int64 }, txn);

                    replaceSysCmd = cn.CreateReplace("Systems" + tablepostfix, new string[] { "sectorid", "nameid", "x", "y", "z", "edsmid" },
                                        new DbType[] { DbType.Int64, DbType.Int64, DbType.Int32, DbType.Int32, DbType.Int32, DbType.Int64 }, txn);

                    replaceNameCmd = cn.CreateReplace("Names" + tablepostfix, new string[] { "name", "id" }, new DbType[] { DbType.String, DbType.Int64 }, txn);

                    foreach (var kvp in cache.SectorIDCache)                  // all sectors cached, id is unique so its got all sectors                           
                    {
                        Sector t = kvp.Value;

                        if (t.insertsec)         // if we have been told to insert the sector, do it
                        {
                            replaceSectorCmd.Parameters[0].Value = t.Name;     // make a new one so we can get the ID
                            replaceSectorCmd.Parameters[1].Value = t.GId;
                            replaceSectorCmd.Parameters[2].Value = t.Id;        // and we insert with ID, managed by us, and replace in case there are any repeat problems (which there should not be)
                            replaceSectorCmd.ExecuteNonQuery();
                            //System.Diagnostics.Debug.WriteLine("Written sector " + t.GId + " " +t.Name);
                            t.insertsec = false;
                        }

                        if (t.edsmdatalist != null)       // if updated..
                        {
#if DEBUG
                            t.edsmdatalist.Sort(delegate (TableWriteData left, TableWriteData right) { return left.edsm.id.CompareTo(right.edsm.id); });
#endif

                            foreach (var data in t.edsmdatalist)            // now write the star list in this sector
                            {
                                try
                                {
                                    if (data.classifier.IsNamed)    // if its a named entry, we need a name
                                    {
                                        data.classifier.NameIdNumeric = data.edsm.id;           // name is the edsm id
                                        replaceNameCmd.Parameters[0].Value = data.classifier.StarName;       // insert a new name
                                        replaceNameCmd.Parameters[1].Value = data.edsm.id;      // we use edsmid as the nameid, and use replace to ensure that if a prev one is there, its replaced
                                        replaceNameCmd.ExecuteNonQuery();
                                        // System.Diagnostics.Debug.WriteLine("Make name " + data.classifier.NameIdNumeric);
                                    }

                                    replaceSysCmd.Parameters[0].Value = t.Id;
                                    replaceSysCmd.Parameters[1].Value = data.classifier.ID;
                                    replaceSysCmd.Parameters[2].Value = data.edsm.x;
                                    replaceSysCmd.Parameters[3].Value = data.edsm.y;
                                    replaceSysCmd.Parameters[4].Value = data.edsm.z;
                                    replaceSysCmd.Parameters[5].Value = data.edsm.id;       // in the event a new entry has the same edsmid, the system table edsmid is replace with new data
                                    replaceSysCmd.ExecuteNonQuery();

                                    if (sw != null)
                                        sw.WriteLine(data.edsm.name + " " + data.edsm.x + "," + data.edsm.y + "," + data.edsm.z + ", EDSM:" + data.edsm.id + " Grid:" + data.gridid);

                                    updates++;
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine("general exception during insert - ignoring " + ex.ToString());
                                }

                            }
                        }

                        t.edsmdatalist = null;     // and delete back
                    }

                    txn.Commit();

                    return updates;
                }
                finally
                {
                    replaceSectorCmd?.Dispose();
                    replaceSysCmd?.Dispose();
                    replaceNameCmd?.Dispose();
                    txn?.Dispose();
                }
            },warnthreshold:5000);
        }

        #endregion

        #region Internal Vars and Classes

        private static int GetNextSectorID() { return SystemsDatabase.Instance.GetEDSMSectorIDNext(); }
        private static void PutNextSectorID(int v) { SystemsDatabase.Instance.SetEDSMSectorIDNext(v); }  

        private class SectorCache
        {
            public Dictionary<long, Sector> SectorIDCache { get; set; } = new Dictionary<long, Sector>();          // only used during store operation
            public Dictionary<string, Sector> SectorNameCache { get; set; } = new Dictionary<string, Sector>();
        }

        private class Sector
        {
            public long Id;
            public int GId;
            public string Name;

            public Sector NextSector;       // memory only field, link to next in list

            public Sector(string name, long id = -1, int gridid = -1 )
            {
                this.Name = name;
                this.GId = gridid;
                this.Id = id;
                this.NextSector = null;
            }

            // for write table purposes only

            public List<TableWriteData> edsmdatalist;
            public bool insertsec = false;
        };

        private class TableWriteData
        {
            public EDSMFileEntry edsm;
            public EliteNameClassifier classifier;
            public int gridid;
        }

        public class EDSMFileEntry
        {
            public bool Deserialize(IEnumerator<JToken> enumerator)
            {
                while( enumerator.MoveNext() && enumerator.Current.IsProperty)   // while more tokens, and JProperty
                {
                    var p = enumerator.Current;
                    string field = p.Name;

                    switch (field)
                    {
                        case "name":
                            name = p.StrNull();
                            break;
                        case "id":
                            id = p.Int();
                            break;
                        case "date":
                            date = p.DateTimeUTC();
                            break;
                        case "coords":
                            {
                                while (enumerator.MoveNext() && enumerator.Current.IsProperty)   // while more tokens, and JProperty
                                {
                                    var cp = enumerator.Current;
                                    field = cp.Name;
                                    double? v = cp.DoubleNull();
                                    if (v == null)
                                        return false;
                                    int vi = (int)(v * SystemClass.XYZScalar);

                                    switch (field)
                                    {
                                        case "x":
                                            x = vi;
                                            break;
                                        case "y":
                                            y = vi;
                                            break;
                                        case "z":
                                            z = vi;
                                            break;
                                    }
                                }

                                break;
                            }
                        default:        // any other, ignore
                            break;
                    }
                }

                return true;
            }

            public string name;
            public long id = -1;
            public DateTime date;
            public int x = int.MinValue;
            public int y = int.MinValue;
            public int z = int.MinValue;
        }

        #endregion
    }
}


