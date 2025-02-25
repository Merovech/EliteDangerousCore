﻿/*
 * Copyright © 2016 - 2022 EDDiscovery development team
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
using System.Text;

namespace EliteDangerousCore.JournalEvents
{
    public partial class JournalScan 
    {

        // has no trailing LF
        public string DisplayString(int indent = 0, List<MaterialCommodityMicroResource> historicmatlist = null,
                                                        List<MaterialCommodityMicroResource> currentmatlist = null,
                                                        bool includefront = true)
        {
            string inds = new string(' ', indent);

            StringBuilder scanText = new StringBuilder();

            scanText.Append(inds);

            if (includefront)
            {
                scanText.AppendFormat("{0} {1}" + Environment.NewLine, BodyName, IsEDSMBody ? " (EDSM)" : "");
                scanText.Append(Environment.NewLine);

                if (IsStar)
                {
                    scanText.AppendFormat(StarTypeText + " (" + StarClassification + ")\n");
                }
                else if (IsPlanet)
                {
                    scanText.AppendFormat("{0}", PlanetTypeText);

                    if (!GasWorld)      // all gas worlds have atmospheres, so don't add it on
                    {
                        scanText.AppendFormat(", " + (Atmosphere == "none" ? "No Atmosphere".T(EDCTx.JournalScan_NoAtmosphere) : Atmosphere));
                    }

                    if (IsLandable)
                        scanText.AppendFormat(", Landable".T(EDCTx.JournalScan_LandC));
                    scanText.AppendFormat("\n");
                }

                if (Terraformable)
                    scanText.Append("Candidate for terraforming\n".T(EDCTx.JournalScan_Candidateforterraforming));

                if (nAge.HasValue)
                    scanText.AppendFormat("Age: {0} my\n".T(EDCTx.JournalScan_AMY), nAge.Value.ToString("N0"));

                if (nStellarMass.HasValue)
                    scanText.AppendFormat("Solar Masses: {0:0.00}\n".T(EDCTx.JournalScan_SolarMasses), nStellarMass.Value);

                if (nMassEM.HasValue)
                    scanText.AppendFormat("Mass: ".T(EDCTx.JournalScan_MASS) + " " + MassEMText() + "\n");

                if (nRadius.HasValue)
                    scanText.AppendFormat("Radius: ".T(EDCTx.JournalScan_RS) + " " + RadiusText() + "\n");

                if (DistanceFromArrivalLS > 0)
                    scanText.AppendFormat("Distance from Arrival Point {0:N1}ls\n".T(EDCTx.JournalScan_DistancefromArrivalPoint), DistanceFromArrivalLS);

                if (HasAtmosphericComposition)
                    scanText.Append(DisplayAtmosphere(4));

                if (HasPlanetaryComposition)
                    scanText.Append(DisplayComposition(4));
            }

            if (nSurfaceTemperature.HasValue)
                scanText.AppendFormat("Surface Temp: {0}K\n".T(EDCTx.JournalScan_SurfaceTemp), nSurfaceTemperature.Value.ToString("N0"));

            if (nSurfaceGravity.HasValue)
                scanText.AppendFormat("Gravity: {0:0.00}g\n".T(EDCTx.JournalScan_GV), nSurfaceGravityG.Value);

            if (nSurfacePressure.HasValue && nSurfacePressure.Value > 0.00 && !GasWorld)        // don't print for gas worlds
            {
                if (nSurfacePressure.Value > 1000)
                {
                    scanText.AppendFormat("Surface Pressure: {0} Atmospheres\n".T(EDCTx.JournalScan_SPA), nSurfacePressureEarth.Value.ToString("N2"));
                }
                else
                {
                    scanText.AppendFormat("Surface Pressure: {0} Pa\n".T(EDCTx.JournalScan_SPP), (nSurfacePressure.Value).ToString("N2"));
                }
            }

            if (Volcanism.HasChars())
                scanText.AppendFormat("Volcanism: {0}\n".T(EDCTx.JournalScan_Volcanism), Volcanism.IsEmpty() ? "No Volcanism".T(EDCTx.JournalScan_NoVolcanism) : System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.
                                                                                            ToTitleCase(Volcanism.ToLowerInvariant()));


            if (nOrbitalPeriodDays.HasValue && nOrbitalPeriodDays > 0)
                scanText.AppendFormat("Orbital Period: {0} days\n".T(EDCTx.JournalScan_OrbitalPeriod), nOrbitalPeriodDays.Value.ToString("0.0####"));

            if (nSemiMajorAxis.HasValue)
            {
                if (IsStar || nSemiMajorAxis.Value > BodyPhysicalConstants.oneAU_m / 10)
                    scanText.AppendFormat("Semi Major Axis: {0:0.00}AU\n".T(EDCTx.JournalScan_SMA), nSemiMajorAxisAU.Value);
                else
                    scanText.AppendFormat("Semi Major Axis: {0}km\n".T(EDCTx.JournalScan_SMK), (nSemiMajorAxis.Value / 1000).ToString("N1"));
            }

            if (nEccentricity.HasValue)
            {
                if (nEccentricity < 0.9)
                    scanText.AppendFormat("Orbital Eccentricity: ".T(EDCTx.JournalScan_OrbitalEccentricity) + "{0:0.000}\n", nEccentricity.Value);
                else 
                    scanText.AppendFormat("Orbital Eccentricity: ".T(EDCTx.JournalScan_OrbitalEccentricity) + "{0:0.000000}\n", nEccentricity.Value);
            }

            if (nOrbitalInclination.HasValue)
                scanText.AppendFormat("Orbital Inclination: {0:0.000}°\n".T(EDCTx.JournalScan_OrbitalInclination), nOrbitalInclination.Value);

            if (nAscendingNode.HasValue)
                scanText.AppendFormat("Ascending Node: {0:0.000}°\n".T(EDCTx.JournalScan_AscendingNode), nAscendingNode.Value);

            if (nPeriapsis.HasValue)
                scanText.AppendFormat("Arg Of Periapsis: {0:0.000}°\n".T(EDCTx.JournalScan_ArgOfPeriapsis), nPeriapsis.Value);

            if (nMeanAnomaly.HasValue)
                scanText.AppendFormat("Mean Anomaly: {0:0.000}°\n".T(EDCTx.JournalScan_MeanAnomaly), nMeanAnomaly.Value);

            if (nAxialTiltDeg.HasValue)
                scanText.AppendFormat("Axial tilt: {0:0.00}°\n".T(EDCTx.JournalScan_Axialtilt), nAxialTiltDeg.Value);

            if (nRotationPeriodDays.HasValue)
                scanText.AppendFormat("Rotation Period: {0} days\n".T(EDCTx.JournalScan_RotationPeriod), nRotationPeriodDays.Value.ToString("0.0####"));


            if (nAbsoluteMagnitude.HasValue)
                scanText.AppendFormat("Absolute Magnitude: {0:0.00}\n".T(EDCTx.JournalScan_AbsoluteMagnitude), nAbsoluteMagnitude.Value);

            if (nTidalLock.HasValue && nTidalLock.Value)
                scanText.Append("Tidally locked\n".T(EDCTx.JournalScan_Tidallylocked));

            if (HasRingsOrBelts)
            {
                scanText.Append("\n");

                if (HasRings)
                    scanText.AppendFormat(Rings.Count() == 1 ? "Ring".T(EDCTx.JournalScan_Ring) : "Rings".T(EDCTx.JournalScan_Rings), ""); // OLD translator files had "Rings{0}" so supply an empty string just in case
                else
                    scanText.AppendFormat(Rings.Count() == 1 ? "Belt".T(EDCTx.JournalScan_Belt) : "Belts".T(EDCTx.JournalScan_Belts), ""); // OLD translator files had "Belt{0}" so supply an empty string just in case

                for (int i = 0; i < Rings.Length; i++)
                    scanText.Append("\n" + Rings[i].RingInformation());

                scanText.Append("\n");
            }

            if (HasMaterials)
            {
                scanText.Append(DisplayMaterials(4, historicmatlist, currentmatlist));
            }

            if (IsStar)
            {
                string czs = CircumstellarZonesString(true, CZPrint.CZAll);
                if (czs != null)
                    scanText.Append(czs);
            }

            if (Mapped)
            {
                scanText.Append("Mapped".T(EDCTx.JournalScan_MPI));
                if (EfficientMapped)
                    scanText.Append(" " + "Efficiently".T(EDCTx.JournalScan_MPIE));
                scanText.Append("\n");
            }

            ScanEstimatedValues ev = GetEstimatedValues();

            scanText.AppendFormat("Current value: {0:N0}".T(EDCTx.JournalScan_CV) + "\n", ev.EstimatedValue(WasDiscovered, WasMapped, Mapped, EfficientMapped, IsEDSMBody));

            if (ev.EstimatedValueFirstDiscoveredFirstMapped > 0 && (!WasDiscovered.HasValue || !WasDiscovered.Value))  // if we don't know, or its not discovered
            {
                string msg = "First Discovered+Mapped value: {0:N0}/{1:N0}e".T(EDCTx.JournalScan_EVFD) + "\n";
                scanText.AppendFormat(msg, ev.EstimatedValueFirstDiscoveredFirstMapped, ev.EstimatedValueFirstDiscoveredFirstMappedEfficiently);
            }

            if (ev.EstimatedValueFirstMapped > 0 && (!WasMapped.HasValue || !WasMapped.Value))    // if was not mapped
            {
                scanText.AppendFormat("First Mapped value: {0:N0}/{1:N0}e".T(EDCTx.JournalScan_EVFM) + "\n", ev.EstimatedValueFirstMapped, ev.EstimatedValueFirstMappedEfficiently);
            }

            if (ev.EstimatedValueFirstDiscovered > 0 && (!WasDiscovered.HasValue || !WasDiscovered.Value))  // if we don't know, or its not discovered
            {
                scanText.AppendFormat("First Discovered value: {0:N0}".T(EDCTx.JournalScan_FDV) + "\n", ev.EstimatedValueFirstDiscovered);
            }

            if (ev.EstimatedValueFirstDiscovered > 0) // if we have extra details, on planets, show the base value
            {
                scanText.AppendFormat("Mapped value: {0:N0}/{1:N0}e".T(EDCTx.JournalScan_EVM) + "\n", ev.EstimatedValueMapped, ev.EstimatedValueMappedEfficiently);
                scanText.AppendFormat("Base Estimated value: {0:N0}".T(EDCTx.JournalScan_EV) + "\n", ev.EstimatedValueBase);
            }

            if (WasDiscovered.HasValue && WasDiscovered.Value)
                scanText.AppendFormat("Already Discovered".T(EDCTx.JournalScan_EVAD) + "\n");
            if (WasMapped.HasValue && WasMapped.Value)
                scanText.AppendFormat("Already Mapped".T(EDCTx.JournalScan_EVAM) + "\n");

            if (EDSMDiscoveryCommander != null)
                scanText.AppendFormat("Discovered by {0} on {1}".T(EDCTx.JournalScan_DB) + "\n", EDSMDiscoveryCommander, EDSMDiscoveryUTC.ToStringZulu());

            if (Signals != null)
                scanText.AppendFormat("Signals".T(EDCTx.ScanDisplayUserControl_Signals) + ":\n" + JournalSAASignalsFound.SignalList(Signals, 4, "\n") + "\n");
            if (Genuses != null)
                scanText.AppendFormat("Genuses".T(EDCTx.ScanDisplayUserControl_Genuses) + ":\n" + JournalSAASignalsFound.GenusList(Genuses, 4, "\n") + "\n");
            if (Organics != null)
                scanText.AppendFormat("Organics".T(EDCTx.ScanDisplayUserControl_Organics) + ":\n" + JournalScanOrganic.OrganicList(Organics, 4, "\n") + "\n");

            scanText.AppendFormat("Scan Type: {0}".T(EDCTx.JournalScan_SCNT) + "\n", ScanType);

            if ( Parents!=null && Parents.Count > 0 && Parents[0].Barycentre != null)      // dec 22 bug here on edsm data received a empty parent array- be more defensive
            { 
                JournalScanBaryCentre barycentrejs = Parents[0].Barycentre;
                scanText.AppendLine();
                scanText.AppendLine("Barycentre: " + barycentrejs.BodyID.ToString());
                barycentrejs.FillInformation(null, null, out string info, out string detailed);
                scanText.AppendLine(info);
                scanText.AppendLine(detailed);
            }


#if DEBUG
            if ( Parents != null)
            {
                scanText.AppendLine();
                scanText.AppendLine($"Body ID {BodyID}");
                foreach (var x in Parents)
                    scanText.AppendLine($"Parent {x.BodyID} {x.Type}");
            }

#endif

            //scanText.AppendFormat("BID+Parents: {0} - {1}\n", BodyID ?? -1, ParentList());

            if (scanText.Length > 0 && scanText[scanText.Length - 1] == '\n')
                scanText.Remove(scanText.Length - 1, 1);

            return scanText.ToNullSafeString().Replace("\n", "\n" + inds);
        }


        // show material counts at the historic point and current.  Has trailing LF if text present.
        public string DisplayMaterials(int indent = 0, List<MaterialCommodityMicroResource> historicmatlist = null, List<MaterialCommodityMicroResource> currentmatlist = null)
        {
            StringBuilder scanText = new StringBuilder();

            if (HasMaterials)
            {
                string indents = new string(' ', indent);

                scanText.Append("Materials:\n".T(EDCTx.JournalScan_Materials));
                foreach (KeyValuePair<string, double> mat in Materials)
                {
                    scanText.Append(indents + DisplayMaterial(mat.Key, mat.Value, historicmatlist, currentmatlist));
                }
            }

            return scanText.ToNullSafeString();
        }

        public string DisplayMaterial(string fdname, double percent, List<MaterialCommodityMicroResource> historicmatlist = null,
                                                                      List<MaterialCommodityMicroResource> currentmatlist = null)  // has trailing LF
        {
            StringBuilder scanText = new StringBuilder();

            MaterialCommodityMicroResourceType mc = MaterialCommodityMicroResourceType.GetByFDName(fdname);

            if (mc != null && (historicmatlist != null || currentmatlist != null))
            {
                MaterialCommodityMicroResource historic = historicmatlist?.Find(x => x.Details == mc);
                MaterialCommodityMicroResource current = ReferenceEquals(historicmatlist, currentmatlist) ? null : currentmatlist?.Find(x => x.Details == mc);
                int? limit = mc.MaterialLimit();

                string matinfo = historic?.Count.ToString() ?? "0";
                if (limit != null)
                    matinfo += "/" + limit.Value.ToString();

                if (current != null && (historic == null || historic.Count != current.Count))
                    matinfo += " Cur " + current.Count.ToString();

                scanText.AppendFormat("{0} ({1}) {2} {3}% {4}\n", mc.Name, mc.Shortname, mc.TranslatedType, percent.ToString("N1"), matinfo);
            }
            else
                scanText.AppendFormat("{0} {1}%\n", System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(fdname.ToLowerInvariant()),
                                                            percent.ToString("N1"));

            return scanText.ToNullSafeString();
        }

        private string DisplayAtmosphere(int indent = 0)     // has trailing LF
        {
            StringBuilder scanText = new StringBuilder();
            string indents = new string(' ', indent);

            scanText.Append("Atmospheric Composition:\n".T(EDCTx.JournalScan_AtmosphericComposition));
            foreach (KeyValuePair<string, double> comp in AtmosphereComposition)
            {
                scanText.AppendFormat(indents + "{0} - {1}%\n", comp.Key, comp.Value.ToString("N2"));
            }

            return scanText.ToNullSafeString();
        }

        private string DisplayComposition(int indent = 0)   // has trailing LF
        {
            StringBuilder scanText = new StringBuilder();
            string indents = new string(' ', indent);

            scanText.Append("Planetary Composition:\n".T(EDCTx.JournalScan_PlanetaryComposition));
            foreach (KeyValuePair<string, double> comp in PlanetComposition)
            {
                if (comp.Value > 0)
                    scanText.AppendFormat(indents + "{0} - {1}%\n", comp.Key, comp.Value.ToString("N2"));
            }

            return scanText.ToNullSafeString();
        }


        public string RadiusText()  // null if not set, or the best representation
        {
            if (nRadius != null)
            {
                if (nRadius >= BodyPhysicalConstants.oneSolRadius_m / 5)
                    return nRadiusSols.Value.ToString("0.#" + " SR");
                else
                    return (nRadius.Value / 1000).ToString("0.#") + " km";
            }
            else
                return null;
        }

        public string MassEMText()
        {
            if (nMassEM.HasValue)
            {
                if (nMassEM.Value < 0.01)
                    return nMassMM.Value.ToString("0.####") + " MM";
                else
                    return nMassEM.Value.ToString("0.##") + " EM";
            }
            else
                return null;
        }

        public string SurveyorInfoLine(ISystem sys,
                            bool hasminingsignals, bool hasgeosignals, bool hasbiosignals, bool hasthargoidsignals, bool hasguardiansignals, bool hashumansignals, bool hasothersignals,
                            bool hasscanorganics,
                            bool showvolcanism, bool showvalues, bool shortinfo, bool showGravity, bool showAtmos, bool showRings,
                            int lowRadiusLimit, int largeRadiusLimit, double eccentricityLimit)
        {
            JournalScan js = this;            

            var information = new StringBuilder();

            string bodyname = js.BodyDesignationOrName.ReplaceIfStartsWith(sys.Name);

            // Name
            information.Append(bodyname);

            // symbols
            if (js.Mapped)  // let the cmdr see that this body is already mapped
                information.Append(" \u24C2");

            if (js.CountOrganicsScansAnalysed > 0) // if scanned
            {
                if (js.CountOrganicsScansAnalysed == js.CountBioSignals)   // and show organic scan situation - fully with a tick
                    information.Append(" \u232C\u2713");
                else
                    information.Append(" \u232C");  // partial
            }

            information.Append(" is a ".T(EDCTx.JournalScanInfo_isa));

            // Additional information
            information.Append((js.IsStar) ? Bodies.StarName(js.StarTypeID) + "." : null);
            information.Append((js.CanBeTerraformable) ? @"terraformable ".T(EDCTx.JournalScanInfo_terraformable) : null);
            information.Append((js.IsPlanet) ? Bodies.PlanetTypeName(js.PlanetTypeID) + "." : null);
            information.Append((js.nRadius < lowRadiusLimit && js.IsPlanet) ? @" Is tiny ".T(EDCTx.JournalScanInfo_LowRadius) + "(" + RadiusText() + ")." : null);
            information.Append((js.nRadius > largeRadiusLimit && js.IsPlanet && js.IsLandable) ? @" Is large ".T(EDCTx.JournalScanInfo_LargeRadius) + "(" + RadiusText() + ")." : null);
            information.Append((js.IsLandable) ? @" Is landable.".T(EDCTx.JournalScanInfo_islandable) : null);
            information.Append((js.IsLandable && showGravity && js.nSurfaceGravityG.HasValue) ? @" (" + Math.Round(js.nSurfaceGravityG.Value, 2, MidpointRounding.AwayFromZero) + "g)" : null);
            information.Append((js.HasAtmosphericComposition && showAtmos) ? @" Atmosphere: ".T(EDCTx.JournalScanInfo_Atmosphere) + (js.Atmosphere?.Replace(" atmosphere", "") ?? "unknown".T(EDCTx.JournalScanInfo_unknownAtmosphere)) + "." : null);
            information.Append((js.HasMeaningfulVolcanism && showvolcanism) ? @" Has ".T(EDCTx.JournalScanInfo_Has) + js.Volcanism + "." : null);
            information.Append((hasminingsignals) ? " Has mining signals.".T(EDCTx.JournalScanInfo_Signals) : null);
            information.Append((hasgeosignals) ? " Geological signals: ".T(EDCTx.JournalScanInfo_GeoSignals) + js.CountGeoSignals + "." : null);
            information.Append((hasbiosignals) ? " Biological signals: ".T(EDCTx.JournalScanInfo_BioSignals) + js.CountBioSignals + "." : null);
            information.Append((hasthargoidsignals) ? " Thargoid signals: ".T(EDCTx.JournalScanInfo_ThargoidSignals) + js.CountThargoidSignals + "." : null);
            information.Append((hasguardiansignals) ? " Guardian signals: ".T(EDCTx.JournalScanInfo_GuardianSignals) + js.CountGuardianSignals + ".": null);
            information.Append((hashumansignals) ? " Human signals: ".T(EDCTx.JournalScanInfo_HumanSignals) + js.CountHumanSignals + "." : null);
            information.Append((hasothersignals) ? " 'Other' signals: ".T(EDCTx.JournalScanInfo_OtherSignals) + js.CountOtherSignals + "." : null);
            information.Append((js.HasRingsOrBelts && showRings) ? @" Is ringed.".T(EDCTx.JournalScanInfo_Hasring) : null);
            information.Append((js.nEccentricity >= eccentricityLimit) ? @" Has an high eccentricity of ".T(EDCTx.JournalScanInfo_eccentricity) + js.nEccentricity + "." : null);
            information.Append(hasscanorganics ? " Has been scanned for organics.".T(EDCTx.JournalScanInfo_scanorganics) : null);

            var ev = js.GetEstimatedValues();

            if (js.WasMapped == true && js.WasDiscovered == true)
            {
                information.Append(" (Mapped & Discovered)".T(EDCTx.JournalScanInfo_MandD));
                if (showvalues)
                {
                    information.Append(' ').Append(ev.EstimatedValueMappedEfficiently.ToString("N0")).Append(" cr");
                }
            }
            else if (js.WasMapped == true && js.WasDiscovered == false)
            {
                information.Append(" (Mapped)".T(EDCTx.JournalScanInfo_MP));
                if (showvalues)
                {
                    information.Append(' ').Append(ev.EstimatedValueFirstMappedEfficiently.ToString("N0")).Append(" cr");
                }
            }
            else if (js.WasDiscovered == true && js.WasMapped == false)
            {
                information.Append(" (Discovered)".T(EDCTx.JournalScanInfo_DIS));
                if (showvalues)
                {
                    information.Append(' ').Append((ev.EstimatedValueFirstMappedEfficiently > 0 ? ev.EstimatedValueFirstMappedEfficiently : ev.EstimatedValueBase).ToString("N0")).Append(" cr");
                }
            }
            else if (js.WasDiscovered == false && js.IsStar)
            {
                if (showvalues)
                {
                    information.Append(' ').Append((ev.EstimatedValueFirstDiscovered > 0 ? ev.EstimatedValueFirstDiscovered : ev.EstimatedValueBase).ToString("N0")).Append(" cr");
                }
            }
            else
            {
                if (showvalues)
                {
                    information.Append(' ').Append((ev.EstimatedValueFirstDiscoveredFirstMappedEfficiently > 0 ? ev.EstimatedValueFirstDiscoveredFirstMappedEfficiently : ev.EstimatedValueBase).ToString("N0")).Append(" cr");
                }
            }

            if (shortinfo)
            {
                information.Append(' ').Append(js.ShortInformation());
            }
            else
                information.Append(' ').Append(js.DistanceFromArrivalText);

            return information.ToString();
        }

        public string ShortInformation()
        {
            if (IsStar)
            {

                return BaseUtils.FieldBuilder.Build("Mass: ;SM;0.00".T(EDCTx.JournalScan_MSM), nStellarMass,
                                                "Age: ;my;0.0".T(EDCTx.JournalScan_Age), nAge,
                                                "Radius: ".T(EDCTx.JournalScan_RS), RadiusText(),
                                                "Dist: ".T(EDCTx.JournalScan_DIST), DistanceFromArrivalLS > 0 ? DistanceFromArrivalText : null);
            }
            else
            {
                return BaseUtils.FieldBuilder.Build("Mass: ".T(EDCTx.JournalScan_MASS), MassEMText(),
                                                 "Radius: ".T(EDCTx.JournalScan_RS), RadiusText(),
                                                 "Dist: ".T(EDCTx.JournalScan_DIST), DistanceFromArrivalLS > 0 ? DistanceFromArrivalText : null);
            }
        }




    }
}


