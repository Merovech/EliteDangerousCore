﻿/*
 * Copyright © 2015 - 2022 EDDiscovery development team
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
 */

using EliteDangerousCore.JournalEvents;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace EliteDangerousCore
{
    public partial class StarScan
    {
        [DebuggerDisplay("SN {System.Name} {System.SystemAddress}")]
        public partial class SystemNode
        {
            public ISystem System { get; set; }

            public SortedList<string, ScanNode> StarNodes { get; private set; } = new SortedList<string, ScanNode>(new CollectionStaticHelpers.BasicLengthBasedNumberComparitor<string>());   // node list
            public SortedList<int, ScanNode> NodesByID { get; private set; } = new SortedList<int, ScanNode>();               // by ID list
            public SortedList<int, JournalScanBaryCentre> Barycentres { get; private set; } = new SortedList<int, JournalScanBaryCentre>();
            public int? FSSTotalBodies { get; set; }         // if we have FSSDiscoveryScan, this will be set
            public List<JournalFSSSignalDiscovered> FSSSignalList { get; private set; } = new List<JournalFSSSignalDiscovered>();       // we keep the FSS signal discovered not the signal list as the signals get merged, and we don't get a chance to see the merged signals
            public List<JournalCodexEntry> CodexEntryList { get; private set; } = new List<JournalCodexEntry>();

            public SystemNode(ISystem sys)
            {
                System = sys;
            }

            public void ClearChildren()
            {
                StarNodes = new SortedList<string, ScanNode>(new CollectionStaticHelpers.BasicLengthBasedNumberComparitor<string>());
                NodesByID = new SortedList<int, ScanNode>();
            }

            public IEnumerable<ScanNode> Bodies
            {
                get
                {
                    if (StarNodes != null)
                    {
                        foreach (ScanNode sn in StarNodes.Values)
                        {
                            yield return sn;

                            foreach (ScanNode c in sn.Descendants)
                            {
                                yield return c;
                            }
                        }
                    }
                }
            }

            public long ScanValue(bool includeedsmvalue)
            {
                long value = 0;

                foreach (var body in Bodies)
                {
                    if (body?.ScanData != null)
                    {
                        if (includeedsmvalue || !body.ScanData.IsEDSMBody)
                        {
                            value += body.ScanData.EstimatedValue;
                        }
                    }
                }

                return value;
            }

            // first is primary star. longform means full text, else abbreviation
            public string StarTypesFound(bool bracketit = true, bool longform = false)
            {
                var sortedset = (from x in Bodies where x.ScanData != null && x.NodeType == ScanNodeType.star orderby x.ScanData.DistanceFromArrivalLS select longform ? x.ScanData.StarTypeText : x.ScanData.StarClassificationAbv).ToList();
                string s = string.Join("; ", sortedset);
                if (bracketit && s.HasChars())
                    s = "(" + s + ")";
                return s;
            }

            public int StarPlanetsScanned()      // not include anything but these.  This corresponds to FSSDiscoveryScan
            {
                return Bodies.Where(b => (b.NodeType == ScanNodeType.star || b.NodeType == ScanNodeType.body) && b.ScanData != null).Count();
            }
            public int StarPlanetsScannednonEDSM()      // not include anything but these.  This corresponds to FSSDiscoveryScan
            {
                return Bodies.Where(b => (b.NodeType == ScanNodeType.star || b.NodeType == ScanNodeType.body) && b.ScanData != null && !b.ScanData.IsEDSMBody).Count();
            }
            public int StarsScanned()      // only stars
            {
                return Bodies.Where(b => b.NodeType == ScanNodeType.star && b.ScanData != null).Count();
            }

            public ScanNode Find(string bodyname)
            {
                foreach (var b in Bodies)
                {
                    if (b.FullName.Equals(bodyname, StringComparison.InvariantCultureIgnoreCase))
                        return b;
                }
                return null;
            }

            public ScanNode Find(JournalScan s)
            {
                foreach (var b in Bodies)
                {
                    if (object.ReferenceEquals(b.ScanData,s))
                        return b;
                }

                return null;
            }
        }
    }
}
