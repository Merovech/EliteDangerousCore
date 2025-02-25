﻿/*
 * Copyright © 2015 - 2020 EDDiscovery development team
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Globalization;
using EliteDangerousCore.DB;
using System.IO;
using EliteDangerousCore.JournalEvents;
using QuickJSON;

namespace EliteDangerousCore
{
    public class NetLogFileReader : TravelLogUnitLogReader
    {
        // Header line regular expression
        private static Regex netlogHeaderRe = new Regex(@"^(?<Localtime>\d\d-\d\d-\d\d-\d\d:\d\d) (?<Timezone>.*) [(](?<GMT>\d\d:\d\d) GMT[)]");
        private static Regex netlogHeaderRe23 = new Regex(@"^(?<Localtime>\d\d\d\d-\d\d-\d\d \d\d:\d\d) (?<Timezone>.*)");

        // Close Quarters Combat
        public bool CQC { get; set; }

        // Time and timezone
        public DateTime LastLogTime { get; set; }
        public TimeZoneInfo TimeZone { get; set; }
        public TimeSpan TimeZoneOffset { get; set; }

        public bool Is23NetLog { get; set; }

        public NetLogFileReader(string filename) : base(filename)
        {
        }

        public NetLogFileReader(TravelLogUnit tlu) : base(tlu)
        {
        }

        private bool ParseTime(string time)
        {
            TimeSpan logtime;
            TimeSpan lasttime = (LastLogTime + TimeZoneOffset).TimeOfDay;
            if (TimeSpan.TryParseExact(time, "h\\:mm\\:ss", CultureInfo.InvariantCulture, out logtime))
            {
                if (logtime.TotalHours >= 0 && logtime.TotalHours < 24)
                {
                    TimeSpan timedelta = logtime - lasttime;
                    if (timedelta < TimeSpan.FromHours(-4))
                    {
                        // Midnight crossing - add 1 day
                        timedelta += TimeSpan.FromHours(24);
                    }
                    else if (timedelta < TimeSpan.Zero)
                    {
                        // Computer time jumped backwards - decrease timezone offset
                        TimeZoneOffset += timedelta;
                    }
                    else if (timedelta > TimeSpan.FromHours(20))
                    {
                        // Computer time jumped backwards - decrease timezone offset
                        timedelta -= TimeSpan.FromHours(24);
                        TimeZoneOffset += timedelta;
                    }

                    LastLogTime += timedelta;

                    return true;
                }
            }

            return false;
        }

        private bool ParseLineTime(string line)
        {
            if (line.Length > 11 && line[0] == '{' && line[3] == ':' && line[6] == ':' && line[9] == '}' && line[10] == ' ')
            {
                return ParseTime(line.Substring(1, 8));
            }
            else if (line.Length > 14 && line[0] == '{' && line[3] == ':' && line[6] == ':' && line[9] == 'G' && line[10] == 'M' && line[11] == 'T' && line[12] == ' ')
            {
                return ParseTime(line.Substring(1, 8));
            }

            return false;
        }

        private bool ParseVisitedSystem(DateTime time, TimeSpan tzoffset, string line, out JObject jo, out JournalLocOrJump je)
        {
            jo = new JObject();
            jo["timestamp"] = time.ToStringZuluInvariant();
            jo["event"] = "FSDJump";
            je = null;

            try
            {
                Regex pattern;

                /* MKW: Use regular expressions to parse the log; much more readable and robust.
                 * Example log entry:

                From  ED  2.1 /1.6
                   {19:21:15} System:"Ooscs Fraae JR-L d8-112" StarPos:(-11609.469,639.594,20141.875)ly  NormalFlight
                string rgexpstr = "{(?<Hour>\\d+):(?<Minute>\\d+):(?<Second>\\d+)} System:\"(?<SystemName>[^\"]+)\" StarPos:\\((?<Pos>.*?)\\)ly( +(?<TravelMode>\\w+))?";

                new from beta3?
                {18:15:14} System:"Pleiades Sector HR-W d1-41" StarPos:(-83.969,-146.156,-334.219)ly Body:0 RelPos:(-1.19887e+07,-9.95573e+06,2.55124e+06)km Supercruise
                string rgexpstr = "{(?<Hour>\\d+):(?<Minute>\\d+):(?<Second>\\d+)} System:\"(?<SystemName>[^\"]+)\" StarPos:\\((?<Pos>.*?)\\)ly Body:(?<Body>\d+) StarPos:\\((?<Pos>.*?)\\)ly( +(?<TravelMode>\\w+))?";

                From ED 2.3
                {12:55:06GMT 96.066s} System:"Stuemeae KM-W c1-342" StarPos:(26.344,-20.563,25899.250)ly Body:9 RelPos:(244.784,1133.47,732.025)km NormalFlight
                string rgexpstr = "{{(?<Hour>\\d+):(?<Minute>\\d+):(?<Second>\\d+)GMT \\d+\\.\\d+s} System:\"(?<SystemName>[^\"]+)\" StarPos:\\((?<Pos>.*?)\\)ly Body:(?<Body>\d+) StarPos:\\((?<Pos>.*?)\\)ly( +(?<TravelMode>\\w+))?";

                Pre ED 2.1/1.6
                    {09:36:16} System:0(Thuechea JE-O b11-0) Body:1 Pos:(-6.67432e+009,7.3151e+009,-1.19125e+010) Supercruise

                 * Also, please note that due to E:D bugs, these entries can be at the end of a line as well, not just on a line of their own.
                 * The RegExp below actually just finds the pattern somewhere in the line, so it caters for rubbish at the end too.
                 */

                if (line.Contains("StarPos:")) // new  ED 2.1 format
                {

                    //{(?<Hour>\d+):(?<Minute>\d+):(?<Second>\d+)} System:"(?<SystemName>[^"]+)" StarPos:\((?<Pos>.*?)\)ly( +(?<TravelMode>\w+))?
                    //{(?<Hour>\d+):(?<Minute>\d+):(?<Second>\d+)} System:"(?<SystemName>[^"]+)" StarPos:\((?<Pos>.*?)\)ly( +(?<TravelMode>\w+))?
                    //string rgexpstr = "{(?<Hour>\\d+):(?<Minute>\\d+):(?<Second>\\d+)} System:\"(?<SystemName>[^\"]+)\" StarPos:\\((?<Pos>.*?)\\)ly( +(?<TravelMode>\\w+))?";
                    string rgexpstr;

                    if (line.Contains("Body:"))
                        rgexpstr = "System:\"(?<SystemName>[^\"]+)\" StarPos:\\((?<Pos>.*?)\\)ly Body:(?<Body>\\d+) RelPos:\\((?<RelPos>.*?)\\)km( +(?<TravelMode>\\w+))?";
                    else
                        rgexpstr = "System:\"(?<SystemName>[^\"]+)\" StarPos:\\((?<Pos>.*?)\\)ly( +(?<TravelMode>\\w+))?";

                    pattern = new Regex(rgexpstr);

                    Match match = pattern.Match(line);

                    if (match != null && match.Success)
                    {
                        //sp.Nr = int.Parse(match.Groups["Body"].Value);
                        jo["StarSystem"] = match.Groups["SystemName"].Value;
                        string pos = match.Groups["Pos"].Value;
                        try
                        {
                            string[] xyzpos = pos.Split(',');
                            jo["StarPos"] = new JArray(
                                double.Parse(xyzpos[0], CultureInfo.InvariantCulture),
                                double.Parse(xyzpos[1], CultureInfo.InvariantCulture),
                                double.Parse(xyzpos[2], CultureInfo.InvariantCulture)
                            );
                        }
                        catch
                        {
                            System.Diagnostics.Trace.WriteLine("System parse error 1:" + line);
                            return false;
                        }
                    }
                    else
                    {
                        System.Diagnostics.Trace.WriteLine("System parse error 1:" + line);
                        return false;
                    }
                }
                else
                {
                    pattern = new Regex(@"System:\d+\((?<SystemName>.*?)\) Body:(?<Body>\d+) Pos:\(.*?\)( (?<TravelMode>\w+))?");
                    Match match = pattern.Match(line);

                    if (match != null && match.Success)
                    {
                        //sp.Nr = int.Parse(match.Groups["Body"].Value);

                        jo["StarSystem"] = match.Groups["SystemName"].Value;
                    }
                    else
                    {
                        System.Diagnostics.Trace.WriteLine("System parse error 2:" + line);
                        return false;
                    }
                }

                je = new JournalFSDJump(jo);

                return true;
            }
            catch
            {
                // MKW TODO: should we log bad lines?
                return false;
            }
        }

        private bool ReadNetLogSystem(out JObject jo, out JournalLocOrJump je, Func<bool> cancelRequested = null, Stream stream = null, bool ownstream = false)
        {
            if (cancelRequested == null)
                cancelRequested = () => false;

            try
            {
                if (stream == null)
                {
                    stream = File.Open(this.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    ownstream = true;
                }

                while (!cancelRequested() )
                {
                    string line = ReadLine();         // read line from TLU. Tell it if its in full read mode or play mode

                    if (line == null)                       // null means finished
                        break;

                    ParseLineTime(line);

                    if (line.Contains("[PG]"))
                    {
                        if (line.Contains("[PG] [Notification] Left a playlist lobby"))
                            this.CQC = false;

                        if (line.Contains("[PG] Destroying playlist lobby."))
                            this.CQC = false;

                        if (line.Contains("[PG] [Notification] Joined a playlist lobby"))
                            this.CQC = true;
                        if (line.Contains("[PG] Created playlist lobby"))
                            this.CQC = true;
                        if (line.Contains("[PG] Found matchmaking lobby object"))
                            this.CQC = true;
                    }

                    int offset = Is23NetLog ? (line.IndexOf("GMT ") - 8) : (line.IndexOf("} System:") - 8);
                    if (offset >= 1 && ParseTime(line.Substring(offset, 8)) && this.CQC == false)
                    {
                        //Console.WriteLine(" RD:" + line );
                        if (line.Contains("ProvingGround"))
                            continue;

                        if (ParseVisitedSystem(this.LastLogTime, this.TimeZoneOffset, line.Substring(offset + 10), out jo, out je))
                        {   // Remove some training systems
                            if (je.StarSystem.Equals("Training", StringComparison.CurrentCultureIgnoreCase))
                                continue;
                            if (je.StarSystem.Equals("Destination", StringComparison.CurrentCultureIgnoreCase))
                                continue;
                            if (je.StarSystem.Equals("Altiris", StringComparison.CurrentCultureIgnoreCase))
                                continue;
                            return true;
                        }
                    }
                }
            }
            finally
            {
                if (ownstream)
                {
                    stream.Dispose();
                }
            }

            jo = null;
            je = null;
            return false;
        }

        private bool ReadHeader()
        {
            DateTime lastlog;
            TimeZoneInfo tzinfo;
            TimeSpan tzoffset;
            bool is23netlog;
            bool ret = ReadLogTimeInfo(TravelLogUnit, out lastlog, out tzinfo, out tzoffset, out is23netlog);
            if (ret)
            {
                LastLogTime = lastlog;
                TimeZone = tzinfo;
                TimeZoneOffset = tzoffset;
                TravelLogUnit.Type = TravelLogUnit.NetLogType;
                Is23NetLog = is23netlog;
            }
            return ret;
        }

        private static bool ReadLogTimeInfo(TravelLogUnit tlu, out DateTime lastlog, out TimeZoneInfo tzi, out TimeSpan tzoffset, out bool is23netlog)
        {
            string line = null;
            string filename = tlu.FullName;

            lastlog = DateTime.UtcNow;
            tzi = TimeZoneInfo.Local;
            tzoffset = tzi.GetUtcOffset(lastlog);
            is23netlog = false;

            if (!File.Exists(filename))
            {
                return false;
            }

            // Try to read the first line of the log file
            try
            {
                using (Stream stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (TextReader reader = new StreamReader(stream))
                    {
                        line = reader.ReadLine();

                        if (line == null)
                        {
                            return false;
                        }

                        Regex re = netlogHeaderRe;
                        string timefmt = "yy-MM-dd-HH:mm";
                        string gmtimefmt = "h\\:mm";

                        if (line == "============================================")
                        {
                            re = netlogHeaderRe23;
                            is23netlog = true;
                            reader.ReadLine();
                            line = reader.ReadLine();
                            timefmt = "yyyy-MM-dd HH:mm";
                        }

                        // Extract the start time from the first line
                        Match match = re.Match(line);
                        if (match != null && match.Success)
                        {
                            string localtimestr = match.Groups["Localtime"].Value;
                            string timezonename = match.Groups["Timezone"].Value.Trim();
                            string gmtimestr = is23netlog ? null : match.Groups["GMT"].Value;

                            if (is23netlog)
                            {
                                reader.ReadLine();
                                line = reader.ReadLine();
                                if (line == null || line.Length < 13 || line[0] != '{' || line[3] != ':' || line[6] != ':' || line[9] != 'G')
                                {
                                    return false;
                                }
                                gmtimestr = line.Substring(1, 5);
                            }


                            DateTime localtime = DateTime.MinValue;
                            TimeSpan gmtime = TimeSpan.MinValue;
                            tzi = TimeZoneInfo.GetSystemTimeZones().FirstOrDefault(t => t.DaylightName.Trim() == timezonename || t.StandardName.Trim() == timezonename);

                            if (tzi != null)
                            {
                                tzoffset = tzi.GetUtcOffset(lastlog);
                            }

                            if (DateTime.TryParseExact(localtimestr, timefmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out localtime) &&
                                TimeSpan.TryParseExact(gmtimestr, gmtimefmt, CultureInfo.InvariantCulture, out gmtime))
                            {
                                // Grab the timezone offset
                                tzoffset = localtime.TimeOfDay - gmtime;
                                if (tzoffset.Minutes == 59 || tzoffset.Minutes == 44 || tzoffset.Minutes == 29 || tzoffset.Minutes == 14)
                                {
                                    tzoffset = tzoffset + new TimeSpan(0, 1, 0);
                                }

                                if (tzi != null)
                                {
                                    // Correct for wildly inaccurate values
                                    if (tzoffset > tzi.BaseUtcOffset + TimeSpan.FromHours(18))
                                    {
                                        tzoffset -= TimeSpan.FromHours(24);
                                    }
                                    else if (tzoffset < tzi.BaseUtcOffset - TimeSpan.FromHours(18))
                                    {
                                        tzoffset += TimeSpan.FromHours(24);
                                    }
                                }
                                else
                                {
                                    // No timezone specified - try to make the timezone offset make sense
                                    // Unfortunately anything east of Tonga (GMT+13) or west of Hawaii (GMT-10)
                                    // will be a day off.

                                    if (tzoffset <= TimeSpan.FromHours(-10.5))
                                    {
                                        tzoffset += TimeSpan.FromHours(24);
                                    }
                                    else if (tzoffset > TimeSpan.FromHours(13.5))
                                    {
                                        tzoffset -= TimeSpan.FromHours(24);
                                    }

                                    double tzhrs = tzoffset.TotalHours;
                                    bool tzneg = tzhrs < 0;
                                    if (tzneg) tzhrs = -tzhrs;
                                    int tzmins = (int)Math.Truncate(tzhrs * 60) % 60;
                                    tzhrs = Math.Truncate(tzhrs);

                                    string tzname = tzhrs == 0 ? "GMT" : $"GMT{(tzneg ? "-" : "+")}{tzhrs.ToString("00", CultureInfo.InvariantCulture)}{tzmins.ToString("00", CultureInfo.InvariantCulture)}";
                                    tzi = TimeZoneInfo.CreateCustomTimeZone(tzname, tzoffset, tzname, tzname);
                                }

                                // Set the start time, timezone info and timezone offset
                                lastlog = localtime - tzoffset;
                            }

                            if (is23netlog)
                            {
                                tzoffset = TimeSpan.Zero;
                                tzi = TimeZoneInfo.Utc;
                            }

                            return true;
                        }

                    }
                }
            }
            catch
            {
                return false;
            }


            return false;
        }

        public IEnumerable<JObject> ReadSystems(JournalLocOrJump last, Func<bool> cancelRequested = null, int cmdrid = -1)
        {
            if (cancelRequested == null)
                cancelRequested = () => false;

            if (cmdrid < 0)
            {
                cmdrid = EDCommander.CurrentCmdrID;
            }

            long startpos = Pos;

            if (TimeZone == null)
            {
                if (!ReadHeader())  // may be empty if we read it too fast.. don't worry, monitor will pick it up
                {
                    System.Diagnostics.Trace.WriteLine("File was empty (for now) " + FullName);
                    yield break;
                }
            }

            JObject jo;
            JournalLocOrJump je;
            using (Stream stream = File.Open(this.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                System.Diagnostics.Debug.WriteLine("ReadData " + FullName + " from " + startpos + " to " + Pos);

                while (!cancelRequested() && ReadNetLogSystem(out jo, out je, cancelRequested, stream))
                {
                    if (last != null && je.StarSystem.Equals(last.StarSystem, StringComparison.InvariantCultureIgnoreCase)
                                     && (!je.HasCoordinate || !last.HasCoordinate || (je.StarPos - last.StarPos).LengthSquared < 0.001))
                        continue;

                    if (je.EventTimeUTC.Subtract(EliteReleaseDates.GammaStart).TotalMinutes > 0)  // Ta bara med efter gamma.
                    {
                        yield return jo;
                        last = je;
                    }
                }
            }

            if ( startpos != Pos )
                System.Diagnostics.Debug.WriteLine("Parse ReadData " + FullName + " from " + startpos + " to " + Pos);
        }
    }
}
