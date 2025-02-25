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
        public enum ScanNodeType 
        {  
            star,            // used for top level stars - stars around stars are called a body.
            barycentre,      // only used for top level barycentres (AB)
            body,            // all levels >0 except for below are called a body
            belt,            // used on level 1 under the star : HIP 23759 A Belt Cluster 4 -> elements Main Star,A Belt,Cluster 4
            beltcluster,     // each cluster under it gets this name at level 2
            ring             // rings at the last level : Selkana 9 A Ring : MainStar,9,A Ring
        };

        [DebuggerDisplay("SN {FullName} {NodeType} lv {Level} bid {BodyID}")]
        public partial class ScanNode
        {
            public ScanNodeType NodeType;
            public string FullName;                 // full name including star system
            public string OwnName;                  // own name excluding star system
            public string CustomName;               // if we can extract from the journals a custom name of it, this holds it. Mostly null
            public SortedList<string, ScanNode> Children;         // kids
            public ScanNode Parent;                 // Parent node 
            public int Level;                       // level within SystemNode
            public int? BodyID;
            public bool IsMapped;                   // recorded here since the scan data can be replaced by a better version later.
            public bool WasMappedEfficiently;

            public string CustomNameOrOwnname { get { return CustomName ?? OwnName; } }

            private JournalScan scandata;            // can be null if no scan, its a place holder, else its a journal scan
            private JournalScan.StarPlanetRing beltdata;    // can be null if not belt. if its a type==belt, it is populated with belt data
            private List<JournalSAASignalsFound.SAASignal> signals; // can be null if no signals for this node, else its a list of signals.
            private List<JournalSAASignalsFound.SAAGenus> genusus; // can be null if no signals for this node, else its a list of signals.
            private List<JournalScanOrganic> organics;  // can be null if nothing for this node, else a list of organics

            public ScanNode() { }
            public ScanNode(string name, ScanNodeType node, int? bodyid) { FullName = OwnName = name; NodeType = node; BodyID = bodyid; 
                        Children = new SortedList<string, ScanNode>(new CollectionStaticHelpers.BasicLengthBasedNumberComparitor<string>()); }
            public ScanNode(ScanNode other) // copy constructor, but not children
            {
                NodeType = other.NodeType; FullName = other.FullName; OwnName = other.OwnName; CustomName = other.CustomName;
                Level = other.Level; BodyID = other.BodyID; IsMapped = other.IsMapped; WasMappedEfficiently = other.WasMappedEfficiently;
                scandata = other.scandata; beltdata = other.beltdata; signals = other.signals; organics = other.organics;
                Children = new SortedList<string, ScanNode>(new CollectionStaticHelpers.BasicLengthBasedNumberComparitor<string>());
            }

            public JournalScan ScanData
            {
                get
                {
                    return scandata;
                }

                set
                {
                    if (value == null)
                        return;

                    if (scandata == null)
                        scandata = value;
                    else if ((!value.IsEDSMBody && value.ScanType != "Basic") || scandata.ScanType == "Basic") // Always overwrtite if its a journalscan (except basic scans)
                    {
                        //System.Diagnostics.Debug.WriteLine(".. overwrite " + scandata.ScanType + " with " + value.ScanType + " for " + scandata.BodyName);
                        scandata = value;
                    }
                }
            }

            public JournalScan.StarPlanetRing BeltData
            {
                get
                {
                    return beltdata;
                }

                set
                {
                    if (value == null)
                        return;

                    beltdata = value;
                }
            }

            public List<JournalSAASignalsFound.SAASignal> Signals       // can be null
            {
                get
                {
                    return signals;
                }
                set
                {
                    if (value == null)
                        return;

                    signals = value;
                }
            }
            public List<JournalSAASignalsFound.SAAGenus> Genuses       // can be null
            {
                get
                {
                    return genusus;
                }
                set
                {
                    if (value == null)
                        return;

                    genusus = value;
                }
            }

            public int CountGeoSignals { get { return Signals?.Where(x => x.IsGeo).Sum(y => y.Count) ?? 0; } }
            public int CountBioSignals { get { return Signals?.Where(x => x.IsBio).Sum(y => y.Count) ?? 0; } }
            public int CountThargoidSignals { get { return Signals?.Where(x => x.IsThargoid).Sum(y => y.Count) ?? 0; } }
            public int CountGuardianSignals { get { return Signals?.Where(x => x.IsGuardian).Sum(y => y.Count) ?? 0; } }
            public int CountHumanSignals { get { return Signals?.Where(x => x.IsHuman).Sum(y => y.Count) ?? 0; } }
            public int CountOtherSignals { get { return Signals?.Where(x => x.IsOther).Sum(y => y.Count) ?? 0; } }
            public int CountUncategorisedSignals { get { return Signals?.Where(x => x.IsUncategorised).Sum(y => y.Count) ?? 0; } }

            public List<JournalScanOrganic> Organics        // can be null
            {
                get
                {
                    return organics;
                }
                set
                {
                    if (value == null)
                        return;

                    organics = value;
                }
            }

            public bool DoesNodeHaveNonEDSMScansBelow()
            {
                if (ScanData != null && ScanData.IsEDSMBody == false)
                    return true;

                if (Children != null)
                {
                    foreach (KeyValuePair<string, ScanNode> csn in Children)
                    {
                        if (csn.Value.DoesNodeHaveNonEDSMScansBelow())
                            return true;
                    }
                }

                return false;
            }

            public bool IsBodyInFilter(string[] filternames, bool checkchildren)
            {
                if (IsBodyInFilter(filternames))
                    return true;

                if (checkchildren)
                {
                    foreach (var body in Descendants)
                    {
                        if (body.IsBodyInFilter(filternames))
                            return true;
                    }
                }
                return false;
            }

            public bool IsBodyInFilter(string[] filternames)    // stars/bodies use the xID type, others use the type
            {
                if (filternames.Contains("All"))
                    return true;
                string name = NodeType.ToString();      // star etc..
                if (scandata != null)
                {
                    if (NodeType == ScanNodeType.star)
                        name = scandata.StarTypeID.ToString();
                    else if (NodeType == ScanNodeType.body)
                        name = scandata.PlanetTypeID.ToString();
                }

                return filternames.Contains(name, StringComparer.InvariantCultureIgnoreCase);
            }

            public IEnumerable<ScanNode> Descendants
            {
                get
                {
                    if (Children != null)
                    {
                        foreach (ScanNode sn in Children.Values)
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

            // Using info here, and the indicated journal scan node, return suryeyor info on this node
            public string SurveyorInfoLine(ISystem sys, bool showsignals, bool showorganics, bool showvolcanism, bool showvalues, bool shortinfo, bool showGravity, bool showAtmos, bool showRings,
                            int lowRadiusLimit, int largeRadiusLimit, double eccentricityLimit)
            {
                if (scandata != null)
                {
                    bool hasthargoidsignals = Signals?.Find(x => x.IsThargoid) != null && showsignals;
                    bool hasguardiansignals = Signals?.Find(x => x.IsGuardian) != null && showsignals;
                    bool hashumansignals = Signals?.Find(x => x.IsHuman) != null && showsignals;
                    bool hasothersignals = Signals?.Find(x => x.IsOther) != null && showsignals;
                    bool hasminingsignals = Signals?.Find(x => x.IsUncategorised) != null && showsignals;
                    bool hasgeosignals = Signals?.Find(x => x.IsGeo) != null && showsignals;
                    bool hasbiosignals = Signals?.Find(x => x.IsBio) != null && showsignals;
                    bool hasscanorganics = Organics != null && showorganics;

                    return scandata.SurveyorInfoLine(sys, hasminingsignals, hasgeosignals, hasbiosignals,
                                hasthargoidsignals, hasguardiansignals, hashumansignals, hasothersignals, hasscanorganics,
                                showvolcanism, showvalues, shortinfo, showGravity, showAtmos, showRings,
                                lowRadiusLimit,largeRadiusLimit, eccentricityLimit);
                }
                else
                    return string.Empty;
            }

        };
    }
}
