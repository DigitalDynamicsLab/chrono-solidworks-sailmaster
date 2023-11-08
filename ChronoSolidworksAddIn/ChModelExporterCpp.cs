﻿using ChronoEngine_SwAddin;
using SolidWorks.Interop.sldworks;
using System.Globalization;
using System;
using System.Windows.Media.Media3D;
using SolidWorks.Interop.swconst;
using System.IO;

/// Derived class for exporting a Solidworks assembly to a Chrono::Engine C++ model.

namespace ChronoEngineAddin
{
    internal class ChModelExporterCpp : ChModelExporter
    {
        private string m_asciiText = "";
        private int num_link = 0;
        private int nbody = 0; // -1 ??


        public ChModelExporterCpp(ChronoEngine_SwAddin.SWIntegration swIntegration) 
            : base(swIntegration) { }


        public void Export()
        {
            CultureInfo bz = new CultureInfo("en-BZ");

            ModelDoc2 swModel;
            ConfigurationManager swConfMgr;
            Configuration swConf;
            Component2 swRootComp;

            this.m_savedParts.Clear();
            this.m_savedShapes.Clear();
            this.m_savedCollisionMeshes.Clear();

            swModel = (ModelDoc2)m_swIntegration.m_swApplication.ActiveDoc;
            if (swModel == null) return;
            swConfMgr = (ConfigurationManager)swModel.ConfigurationManager;
            swConf = (Configuration)swConfMgr.ActiveConfiguration;
            swRootComp = (Component2)swConf.GetRootComponent3(true);

            m_swIntegration.m_swApplication.GetUserProgressBar(out m_swIntegration.m_taskpaneHost.GetProgressBar());
            if (m_swIntegration.m_taskpaneHost.GetProgressBar() != null)
                m_swIntegration.m_taskpaneHost.GetProgressBar().Start(0, 5, "Exporting to C++");

            num_comp = 0;

            m_asciiText = "// C++ multibody system automatically generated using Chrono::SolidWorks add-in\n" +
                        "// Assembly: " + swModel.GetPathName() + "\n\n\n";

            m_asciiText += "#include <string>\n";
            m_asciiText += "#include \"chrono/assets/ChModelFileShape.h\"\n";
            m_asciiText += "#include \"chrono/collision/ChCollisionSystemBullet.h\"\n";
            m_asciiText += "#include \"chrono/physics/ChMaterialSurfaceNSC.h\"\n";
            m_asciiText += "#include \"chrono/physics/ChLinkMotorRotationAngle.h\"\n";
            m_asciiText += "#include \"chrono/physics/ChLinkMotorRotationSpeed.h\"\n";
            m_asciiText += "#include \"chrono/physics/ChLinkMotorRotationTorque.h\"\n";
            m_asciiText += "#include \"chrono/physics/ChLinkMotorLinearPosition.h\"\n";
            m_asciiText += "#include \"chrono/physics/ChLinkMotorLinearSpeed.h\"\n";
            m_asciiText += "#include \"chrono/physics/ChLinkMotorLinearForce.h\"\n";

            m_asciiText += "#include \"" + System.IO.Path.GetFileNameWithoutExtension(this.save_filename) + ".h\"\n";

            m_asciiText += "\n\n/// Function to import Solidworks assembly directly into Chrono ChSystem.\n";
            m_asciiText += "void ImportSolidworksSystemCpp(chrono::ChSystem& system, std::unordered_map<std::string, std::shared_ptr<chrono::ChFunction>>* motfun_map) {\n";
            m_asciiText += "std::vector<std::shared_ptr<chrono::ChBodyAuxRef>> bodylist;\n";
            m_asciiText += "std::vector<std::shared_ptr<chrono::ChLinkBase>> linklist;\n";
            m_asciiText += "ImportSolidworksSystemCpp(bodylist, linklist, motfun_map);\n";
            m_asciiText += "for (auto& body : bodylist)\n";
            m_asciiText += "    system.Add(body);\n";
            m_asciiText += "for (auto& link : linklist)\n";
            m_asciiText += "    system.Add(link);\n";
            m_asciiText += "}\n";

            m_asciiText += "\n\n/// Function to import Solidworks bodies and mates into dedicated containers.\n";
            m_asciiText += "void ImportSolidworksSystemCpp(std::vector<std::shared_ptr<chrono::ChBodyAuxRef>>& bodylist, std::vector<std::shared_ptr<chrono::ChLinkBase>>& linklist, std::unordered_map<std::string, std::shared_ptr<chrono::ChFunction>>* motfun_map) {\n\n";
            m_asciiText += "// Some global settings\n" +
                         "double sphereswept_r = " + m_swIntegration.m_taskpaneHost.GetNumericSphereSwept().Value.ToString(bz) + ";\n" +
                         "chrono::collision::ChCollisionModel::SetDefaultSuggestedEnvelope(" + ((double)m_swIntegration.m_taskpaneHost.GetNumericEnvelope().Value * ChScale.L).ToString(bz) + ");\n" +
                         "chrono::collision::ChCollisionModel::SetDefaultSuggestedMargin(" + ((double)m_swIntegration.m_taskpaneHost.GetNumericMargin().Value * ChScale.L).ToString(bz) + ");\n" +
                         "chrono::collision::ChCollisionSystemBullet::SetContactBreakingThreshold(" + ((double)m_swIntegration.m_taskpaneHost.GetNumericContactBreaking().Value * ChScale.L).ToString(bz) + ");\n\n";

            m_asciiText += "std::string shapes_dir = \"" + System.IO.Path.GetFileNameWithoutExtension(this.save_filename) + "_shapes/\";\n\n";

            m_asciiText += "// Prepare some data for later use\n";
            m_asciiText += "std::shared_ptr<chrono::ChModelFileShape> body_shape;\n";
            m_asciiText += "chrono::ChMatrix33<> mr;\n";
            m_asciiText += "std::shared_ptr<chrono::ChLinkBase> link;\n";
            m_asciiText += "chrono::ChVector<> cA;\n";
            m_asciiText += "chrono::ChVector<> cB;\n";
            m_asciiText += "chrono::ChVector<> dA;\n";
            m_asciiText += "chrono::ChVector<> dB;\n\n";

            m_asciiText += "// Assembly ground body\n";
            m_asciiText += "auto body_0 = chrono_types::make_shared<chrono::ChBodyAuxRef>();\n" +
                         "body_0->SetName(\"ground\");\n" +
                         "body_0->SetBodyFixed(true);\n" +
                         "bodylist.push_back(body_0);\n\n";


            if (swModel.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                // Write down all parts
                TraverseComponentForBodies(swRootComp, 1);


                // Write down all constraints
                MathTransform roottrasf = swRootComp.GetTotalTransform(true);
                if (roottrasf == null)
                {
                    IMathUtility swMath = (IMathUtility)m_swIntegration.m_swApplication.GetMathUtility();
                    double[] nulltr = new double[] { 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0 };
                    roottrasf = (MathTransform)swMath.CreateTransform(nulltr);
                }

                Feature swFeat = (Feature)swModel.FirstFeature();
                TraverseFeaturesForLinks(swFeat, 1, ref roottrasf, ref swRootComp);

                TraverseComponentForLinks(swRootComp, 1, ref roottrasf);

                // Write down all markers in assembly (that are not in sub parts, so they belong to 'ground' object)
                swFeat = (Feature)swModel.FirstFeature();
                TraverseFeaturesForMarkers(swFeat, 1, roottrasf);
            }

            m_asciiText += "\n\n} // end function\n";

            System.Windows.Forms.MessageBox.Show("Export to C++ completed.");

            if (m_swIntegration.m_taskpaneHost.GetProgressBar() != null)
                m_swIntegration.m_taskpaneHost.GetProgressBar().End();
        }


        // ============================================================================================================
        // Override base class methods
        // ============================================================================================================


        public override bool ConvertMate(in Feature swMateFeature, in MathTransform roottrasf, in Component2 assemblyofmates)
        {

            LinkParams link_params;
            GetLinkParameters(swMateFeature, out link_params, roottrasf, assemblyofmates);

            // TODO: redundant part of code
            Mate2 swMate = (Mate2)swMateFeature.GetSpecificFeature2();
            // Fetch the python names using hash map (python names added when scanning parts)
            ModelDocExtension swModelDocExt = default(ModelDocExtension);
            ModelDoc2 swModel = (ModelDoc2)m_swIntegration.m_swApplication.ActiveDoc;
            swModelDocExt = swModel.Extension;

            // Add some comment in C++, to list the referenced SW items
            m_asciiText += "\n// Mate constraint: " + swMateFeature.Name + " [" + swMateFeature.GetTypeName2() + "]" + " type:" + swMate.Type + " align:" + swMate.Alignment + " flip:" + swMate.Flipped + "\n";
            for (int e = 0; e < swMate.GetMateEntityCount(); e++)
            {
                MateEntity2 swEntityN = swMate.MateEntity(e);
                Component2 swCompN = swEntityN.ReferenceComponent;
                string ce_nameN = (string)m_savedParts[swModelDocExt.GetPersistReference3(swCompN)];
                if (swEntityN.ReferenceType2 == 4)
                    ce_nameN = "body_0"; // reference assembly
                m_asciiText += "//   Entity " + e + ": C::E name: " + ce_nameN + " , SW name: " + swCompN.Name2 + " ,  SW ref.type:" + swEntityN.Reference.GetType() + " (" + swEntityN.ReferenceType2 + ")\n";
            }
            m_asciiText += "\n";

            //// 
            //// WRITE CPP CODE CORRESPONDING TO CONSTRAINTS
            ////
            CultureInfo bz = new CultureInfo("en-BZ");


            if (link_params.ref1 == null)
                link_params.ref1 = "body_0";

            if (link_params.ref2 == null)
                link_params.ref2 = "body_0";

            if (link_params.do_ChLinkMateXdistance)
            {
                num_link++;
                String linkname = "link_" + num_link;
                m_asciiText += String.Format(bz, "link = chrono_types::make_shared<chrono::ChLinkMateXdistance>();\n", linkname);

                m_asciiText += String.Format(bz, "cA = chrono::ChVector<>({1:g},{2:g},{3:g});\n",
                          linkname,
                          link_params.cA.X * ChScale.L,
                          link_params.cA.Y * ChScale.L,
                          link_params.cA.Z * ChScale.L);
                m_asciiText += String.Format(bz, "cB = chrono::ChVector<>({1:g},{2:g},{3:g});\n",
                          linkname,
                          link_params.cB.X * ChScale.L,
                          link_params.cB.Y * ChScale.L,
                          link_params.cB.Z * ChScale.L);
                if (!link_params.entity_0_as_VERTEX)
                    m_asciiText += String.Format(bz, "dA = chrono::ChVector<>({1:g},{2:g},{3:g});\n",
                             linkname,
                             link_params.dA.X,
                             link_params.dA.Y,
                             link_params.dA.Z);
                if (!link_params.entity_1_as_VERTEX)
                    m_asciiText += String.Format(bz, "dB = chrono::ChVector<>({1:g},{2:g},{3:g});\n",
                             linkname,
                             link_params.dB.X,
                             link_params.dB.Y,
                             link_params.dB.Z);

                // Initialize link, by setting the two csys, in absolute space,
                if (!link_params.swapAB_1)
                    m_asciiText += String.Format(bz, "std::dynamic_pointer_cast<chrono::ChLinkMateXdistance>(link)->Initialize({1},{2},false,cA,cB,dB);\n", linkname, link_params.ref1, link_params.ref2);
                else
                    m_asciiText += String.Format(bz, "std::dynamic_pointer_cast<chrono::ChLinkMateXdistance>(link)->Initialize({1},{2},false,cB,cA,dA);\n", linkname, link_params.ref2, link_params.ref1);

                //if (link_params.do_distance_val!=0)
                m_asciiText += String.Format(bz, "std::dynamic_pointer_cast<chrono::ChLinkMateXdistance>(link)->SetDistance({1});\n", linkname,
                    link_params.do_distance_val * ChScale.L * -1);

                m_asciiText += String.Format(bz, "std::dynamic_pointer_cast<chrono::ChLinkMateXdistance>(link)->SetName(\"{1}\");\n", linkname, swMateFeature.Name);
                // Insert to a list of exported items
                m_asciiText += String.Format(bz, "linklist.push_back(link);\n\n", linkname);
            }

            if (link_params.do_ChLinkMateParallel)
            {
                if (Math.Abs(Vector3D.DotProduct(link_params.dA, link_params.dB)) > 0.98)
                {
                    num_link++;
                    String linkname = "link_" + num_link;
                    m_asciiText += String.Format(bz, "link = chrono_types::make_shared<chrono::ChLinkMateParallel>();\n", linkname);

                    m_asciiText += String.Format(bz, "cA = chrono::ChVector<>({1:g},{2:g},{3:g});\n",
                              linkname,
                              link_params.cA.X * ChScale.L,
                              link_params.cA.Y * ChScale.L,
                              link_params.cA.Z * ChScale.L);
                    m_asciiText += String.Format(bz, "dA = chrono::ChVector<>({1:g},{2:g},{3:g});\n",
                              linkname, link_params.dA.X, link_params.dA.Y, link_params.dA.Z);
                    m_asciiText += String.Format(bz, "cB = chrono::ChVector<>({1:g},{2:g},{3:g});\n",
                              linkname,
                              link_params.cB.X * ChScale.L,
                              link_params.cB.Y * ChScale.L,
                              link_params.cB.Z * ChScale.L);
                    m_asciiText += String.Format(bz, "dB = chrono::ChVector<>({1:g},{2:g},{3:g});\n",
                              linkname, link_params.dB.X, link_params.dB.Y, link_params.dB.Z);

                    if (link_params.do_parallel_flip)
                        m_asciiText += String.Format(bz, "std::dynamic_pointer_cast<chrono::ChLinkMateParallel>(link)->SetFlipped(true);\n", linkname);

                    // Initialize link, by setting the two csys, in absolute space,
                    if (!link_params.swapAB_1)
                        m_asciiText += String.Format(bz, "std::dynamic_pointer_cast<chrono::ChLinkMateParallel>(link)->Initialize({1},{2},false,cA,cB,dA,dB);\n", linkname, link_params.ref1, link_params.ref2);
                    else
                        m_asciiText += String.Format(bz, "std::dynamic_pointer_cast<chrono::ChLinkMateParallel>(link)->Initialize({1},{2},false,cB,cA,dB,dA);\n", linkname, link_params.ref2, link_params.ref1);

                    m_asciiText += String.Format(bz, "std::dynamic_pointer_cast<chrono::ChLinkMateParallel>(link)->SetName(\"{1}\");\n", linkname, swMateFeature.Name);
                    // Insert to a list of exported items
                    m_asciiText += String.Format(bz, "linklist.push_back(link);\n\n", linkname);
                }
                else
                {
                    m_asciiText += "\n// chrono_types::make_shared<ChLinkMateParallel> skipped because directions not parallel!\n";
                }
            }

            if (link_params.do_ChLinkMateOrthogonal)
            {
                if (Math.Abs(Vector3D.DotProduct(link_params.dA, link_params.dB)) < 0.02)
                {
                    num_link++;
                    String linkname = "link_" + num_link;
                    m_asciiText += String.Format(bz, "link = chrono_types::make_shared<chrono::ChLinkMateOrthogonal>();\n", linkname);

                    m_asciiText += String.Format(bz, "cA = chrono::ChVector<>({1:g},{2:g},{3:g});\n",
                              linkname,
                              link_params.cA.X * ChScale.L,
                              link_params.cA.Y * ChScale.L,
                              link_params.cA.Z * ChScale.L);
                    m_asciiText += String.Format(bz, "dA = chrono::ChVector<>({1:g},{2:g},{3:g});\n",
                              linkname, link_params.dA.X, link_params.dA.Y, link_params.dA.Z);
                    m_asciiText += String.Format(bz, "cB = chrono::ChVector<>({1:g},{2:g},{3:g});\n",
                              linkname,
                              link_params.cB.X * ChScale.L,
                              link_params.cB.Y * ChScale.L,
                              link_params.cB.Z * ChScale.L);
                    m_asciiText += String.Format(bz, "dB = chrono::ChVector<>({1:g},{2:g},{3:g});\n",
                              linkname, link_params.dB.X, link_params.dB.Y, link_params.dB.Z);

                    // Initialize link, by setting the two csys, in absolute space,
                    if (!link_params.swapAB_1)
                        m_asciiText += String.Format(bz, "std::dynamic_pointer_cast<chrono::ChLinkMateOrthogonal>(link)->Initialize({1},{2},false,cA,cB,dA,dB);\n", linkname, link_params.ref1, link_params.ref2);
                    else
                        m_asciiText += String.Format(bz, "std::dynamic_pointer_cast<chrono::ChLinkMateOrthogonal>(link)->Initialize({1},{2},false,cB,cA,dB,dA);\n", linkname, link_params.ref2, link_params.ref1);

                    m_asciiText += String.Format(bz, "std::dynamic_pointer_cast<chrono::ChLinkMateOrthogonal>(link)->SetName(\"{1}\");\n", linkname, swMateFeature.Name);
                    // Insert to a list of exported items
                    m_asciiText += String.Format(bz, "linklist.push_back(link);\n\n", linkname);
                }
                else
                {
                    m_asciiText += ";\n// chrono_types::make_shared<chrono::ChLinkMateOrthogonal> skipped because directions not orthogonal! ;\n";
                }
            }

            if (link_params.do_ChLinkMateSpherical)
            {
                num_link++;
                String linkname = "link_" + num_link;
                m_asciiText += String.Format(bz, "link = chrono_types::make_shared<chrono::ChLinkMateSpherical>();\n", linkname);

                m_asciiText += String.Format(bz, "cA = chrono::ChVector<>({1:g},{2:g},{3:g});\n",
                          linkname,
                          link_params.cA.X * ChScale.L,
                          link_params.cA.Y * ChScale.L,
                          link_params.cA.Z * ChScale.L);
                m_asciiText += String.Format(bz, "cB = chrono::ChVector<>({1:g},{2:g},{3:g});\n",
                          linkname,
                          link_params.cB.X * ChScale.L,
                          link_params.cB.Y * ChScale.L,
                          link_params.cB.Z * ChScale.L);

                // Initialize link, by setting the two csys, in absolute space,
                if (!link_params.swapAB_1)
                    m_asciiText += String.Format(bz, "std::dynamic_pointer_cast<chrono::ChLinkMateSpherical>(link)->Initialize({1},{2},false,cA,cB);\n", linkname, link_params.ref1, link_params.ref2);
                else
                    m_asciiText += String.Format(bz, "std::dynamic_pointer_cast<chrono::ChLinkMateSpherical>(link)->Initialize({1},{2},false,cB,cA);\n", linkname, link_params.ref2, link_params.ref1);

                m_asciiText += String.Format(bz, "std::dynamic_pointer_cast<chrono::ChLinkMateSpherical>(link)->SetName(\"{1}\");\n", linkname, swMateFeature.Name);
                // Insert to a list of exported items
                m_asciiText += String.Format(bz, "linklist.push_back(link);\n\n", linkname);
            }

            if (link_params.do_ChLinkMatePointLine)
            {
                num_link++;
                String linkname = "link_" + num_link;
                m_asciiText += String.Format(bz, "link = chrono_types::make_shared<chrono::ChLinkMateGeneric>();\n", linkname);
                m_asciiText += String.Format(bz, "std::dynamic_pointer_cast<chrono::ChLinkMateGeneric>(link)->SetConstrainedCoords(false, true, true, false, false, false);\n", linkname);

                m_asciiText += String.Format(bz, "cA = chrono::ChVector<>({1:g},{2:g},{3:g});\n",
                          linkname,
                          link_params.cA.X * ChScale.L,
                          link_params.cA.Y * ChScale.L,
                          link_params.cA.Z * ChScale.L);
                m_asciiText += String.Format(bz, "cB = chrono::ChVector<>({1:g},{2:g},{3:g});\n",
                          linkname,
                          link_params.cB.X * ChScale.L,
                          link_params.cB.Y * ChScale.L,
                          link_params.cB.Z * ChScale.L);
                if (!link_params.entity_0_as_VERTEX)
                    m_asciiText += String.Format(bz, "dA = chrono::ChVector<>({1:g},{2:g},{3:g});\n", linkname, link_params.dA.X, link_params.dA.Y, link_params.dA.Z);
                else
                    m_asciiText += String.Format(bz, "dA = VNULL;\n");
                if (!link_params.entity_1_as_VERTEX)
                    m_asciiText += String.Format(bz, "dB = chrono::ChVector<>({1:g},{2:g},{3:g});\n", linkname, link_params.dB.X, link_params.dB.Y, link_params.dB.Z);
                else
                    m_asciiText += String.Format(bz, "dB = VNULL;\n");

                // Initialize link, by setting the two csys, in absolute space,
                if (!link_params.swapAB_1)
                    m_asciiText += String.Format(bz, "std::dynamic_pointer_cast<chrono::ChLinkMateGeneric>(link)->Initialize({1},{2},false,cA,cB,dA,dB);\n", linkname, link_params.ref1, link_params.ref2);
                else
                    m_asciiText += String.Format(bz, "std::dynamic_pointer_cast<chrono::ChLinkMateGeneric>(link)->Initialize({1},{2},false,cB,cA,dB,dA);\n", linkname, link_params.ref2, link_params.ref1);

                m_asciiText += String.Format(bz, "std::dynamic_pointer_cast<chrono::ChLinkMateGeneric>(link)->SetName(\"{1}\");\n", linkname, swMateFeature.Name);
                // Insert to a list of exported items
                m_asciiText += String.Format(bz, "linklist.push_back(link);\n\n", linkname);
            }



            // Now, do some other special mate type that did not fall in combinations
            // of link_params.do_ChLinkMatePointLine, link_params.do_ChLinkMateSpherical, etc etc

            if (swMateFeature.GetTypeName2() == "MateHinge")
            {
                // auto flip direction if anti aligned (seems that this is assumed automatically in MateHinge in SW)
                if (Vector3D.DotProduct(link_params.dA, link_params.dB) < 0)
                    link_params.dB.Negate();

                // Hinge constraint must be splitted in two C::E constraints: a coaxial and a point-vs-plane
                num_link++;
                String linkname = "link_" + num_link;
                m_asciiText += String.Format(bz, "link = chrono_types::make_shared<chrono::ChLinkMateCoaxial>();\n", linkname);

                m_asciiText += String.Format(bz, "cA = chrono::ChVector<>({1:g},{2:g},{3:g});\n",
                          linkname,
                          link_params.cA.X * ChScale.L,
                          link_params.cA.Y * ChScale.L,
                          link_params.cA.Z * ChScale.L);
                m_asciiText += String.Format(bz, "dA = chrono::ChVector<>({1:g},{2:g},{3:g});\n",
                          linkname, link_params.dA.X, link_params.dA.Y, link_params.dA.Z);
                m_asciiText += String.Format(bz, "cB = chrono::ChVector<>({1:g},{2:g},{3:g});\n",
                          linkname,
                          link_params.cB.X * ChScale.L,
                          link_params.cB.Y * ChScale.L,
                          link_params.cB.Z * ChScale.L);
                m_asciiText += String.Format(bz, "dB = chrono::ChVector<>({1:g},{2:g},{3:g});\n",
                          linkname, link_params.dB.X, link_params.dB.Y, link_params.dB.Z);

                m_asciiText += String.Format(bz, "std::dynamic_pointer_cast<chrono::ChLinkMateCoaxial>(link)->SetName(\"{1}\");\n", linkname, swMateFeature.Name);


                // Initialize link, by setting the two csys, in absolute space,
                m_asciiText += String.Format(bz, "std::dynamic_pointer_cast<chrono::ChLinkMateCoaxial>(link)->Initialize({1},{2},false,cA,cB,dA,dB);\n", linkname, link_params.ref1, link_params.ref2);

                // Insert to a list of exported items
                m_asciiText += String.Format(bz, "linklist.push_back(link);\n", linkname);




                num_link++;
                linkname = "link_" + num_link;
                m_asciiText += String.Format(bz, "link = chrono_types::make_shared<chrono::ChLinkMateXdistance>();\n", linkname);

                m_asciiText += String.Format(bz, "cA = chrono::ChVector<>({1:g},{2:g},{3:g});\n",
                          linkname,
                          link_params.cC.X * ChScale.L,
                          link_params.cC.Y * ChScale.L,
                          link_params.cC.Z * ChScale.L);
                m_asciiText += String.Format(bz, "dA = chrono::ChVector<>({1:g},{2:g},{3:g});\n",
                          linkname, link_params.dC.X, link_params.dC.Y, link_params.dC.Z);
                m_asciiText += String.Format(bz, "cB = chrono::ChVector<>({1:g},{2:g},{3:g});\n",
                          linkname,
                          link_params.cD.X * ChScale.L,
                          link_params.cD.Y * ChScale.L,
                          link_params.cD.Z * ChScale.L);
                m_asciiText += String.Format(bz, "dB = chrono::ChVector<>({1:g},{2:g},{3:g});\n",
                          linkname, link_params.dD.X, link_params.dD.Y, link_params.dD.Z);

                m_asciiText += String.Format(bz, "std::dynamic_pointer_cast<chrono::ChLinkMateXdistance>(link)->SetName(\"{1}\");\n", linkname, swMateFeature.Name);


                // Initialize link, by setting the two csys, in absolute space,
                if (link_params.entity_2_as_VERTEX)
                    m_asciiText += String.Format(bz, "std::dynamic_pointer_cast<chrono::ChLinkMateXdistance>(link)->Initialize({1},{2},false,cA,cB,dA);\n", linkname, link_params.ref3, link_params.ref4);
                else
                    m_asciiText += String.Format(bz, "std::dynamic_pointer_cast<chrono::ChLinkMateXdistance>(link)->Initialize({1},{2},false,cA,cB,dB);\n", linkname, link_params.ref3, link_params.ref4);

                // Insert to a list of exported items
                m_asciiText += String.Format(bz, "linklist.push_back(link);\n", linkname);
            }


            return true;
        }

        public override void TraverseComponentForVisualShapes(Component2 swComp, long nLevel, ref int nVisShape, Component2 chBodyComp) 
        {
            CultureInfo bz = new CultureInfo("en-BZ");
            object[] bodies;
            object bodyInfo;
            bodies = (object[])swComp.GetBodies3((int)swBodyType_e.swAllBodies, out bodyInfo);

            if (bodies != null)
                if (bodies.Length > 0)
                {
                    // Export the component shape to a .OBJ file representing its SW body(s)
                    nVisShape += 1;
                    string bodyname = "body_" + nbody;
                    string shapename = "body_" + nbody + "_" + nVisShape;
                    string obj_filename = this.save_dir_shapes + "\\" + shapename + ".obj";

                    ModelDoc2 swCompModel = (ModelDoc2)swComp.GetModelDoc();
                    if (!this.m_savedShapes.ContainsKey(swCompModel.GetPathName()))
                    {
                        try
                        {
                            FileStream ostream = new FileStream(obj_filename, FileMode.Create, FileAccess.ReadWrite);
                            StreamWriter writer = new StreamWriter(ostream); //, new UnicodeEncoding());
                            string asciiobj = "";
                            if (m_swIntegration.m_taskpaneHost.GetProgressBar() != null)
                                m_swIntegration.m_taskpaneHost.GetProgressBar().UpdateTitle("Exporting " + swComp.Name2 + " (tesselate) ..."); 
                            // Write the OBJ converted visualization shapes:
                            TesselateToObj.Convert(swComp, ref asciiobj, m_swIntegration.m_taskpaneHost.GetCheckboxSaveUV().Checked, ref m_swIntegration.m_taskpaneHost.GetProgressBar(), true, false);
                            writer.Write(asciiobj);
                            writer.Flush();
                            ostream.Close();

                            this.m_savedShapes.Add(swCompModel.GetPathName(), shapename);
                        }
                        catch (Exception)
                        {
                            System.Windows.Forms.MessageBox.Show("Cannot write to file: " + obj_filename + "\n for component: " + swComp.Name2 + " for path name: " + swCompModel.GetPathName());
                        }
                    }
                    else
                    {
                        // reuse the already-saved shape name
                        shapename = (String)m_savedShapes[swCompModel.GetPathName()];
                    }

                    m_asciiText += String.Format(bz, "\n// Visualization shape\n");
                    m_asciiText += String.Format(bz, "body_shape = chrono_types::make_shared<chrono::ChModelFileShape>();\n");
                    m_asciiText += String.Format(bz, "body_shape->SetFilename(shapes_dir + \"{0}.obj\");\n", shapename);

                    object foo = null;
                    double[] vMatProperties = (double[])swComp.GetMaterialPropertyValues2((int)swInConfigurationOpts_e.swThisConfiguration, foo);

                    if (vMatProperties != null)
                        if (vMatProperties[0] != -1)
                        {
                            m_asciiText += String.Format(bz, "body_shape->SetColor(chrono::ChColor((float){1},(float){2},(float){3}));\n", shapename, vMatProperties[0], vMatProperties[1], vMatProperties[2]);
                            m_asciiText += String.Format(bz, "body_shape->SetOpacity({1});\n", shapename, 1.0 - vMatProperties[7]);
                        }

                    MathTransform absframe_chbody = chBodyComp.GetTotalTransform(true);
                    MathTransform absframe_shape = swComp.GetTotalTransform(true);
                    MathTransform absframe_chbody_inv = absframe_chbody.IInverse();
                    MathTransform relframe_shape = absframe_shape.IMultiply(absframe_chbody_inv);  // row-ordered transf. -> reverse mult.order!
                    double[] amatr = (double[])relframe_shape.ArrayData;
                    double[] quat = GetQuaternionFromMatrix(ref relframe_shape);

                    m_asciiText += String.Format(bz, "{0}->AddVisualShape(body_shape, chrono::ChFrame<>(", bodyname, shapename);
                    m_asciiText += String.Format(bz, "chrono::ChVector<>({0},{1},{2}), ", amatr[9] * ChScale.L, amatr[10] * ChScale.L, amatr[11] * ChScale.L);
                    m_asciiText += String.Format(bz, "chrono::ChQuaternion<>({0},{1},{2},{3})", quat[0], quat[1], quat[2], quat[3]);
                    m_asciiText += String.Format(bz, "));\n");


                    //m_asciiText += String.Format(bz, "\n// Visualization shape\n");
                    //m_asciiText += String.Format(bz, "auto {0}_shape = chrono_types::make_shared<chrono::ChModelFileShape>();\n", shapename);
                    //m_asciiText += String.Format(bz, "{0}_shape->SetFilename(shapes_dir + \"{0}.obj\");\n", shapename);

                    //object foo = null;
                    //double[] vMatProperties = (double[])swComp.GetMaterialPropertyValues2((int)swInConfigurationOpts_e.swThisConfiguration, foo);

                    //if (vMatProperties != null)
                    //    if (vMatProperties[0] != -1)
                    //    {
                    //        m_asciiText += String.Format(bz, "{0}_shape->SetColor(chrono::ChColor({1},{2},{3}));\n", shapename, vMatProperties[0], vMatProperties[1], vMatProperties[2]);
                    //        m_asciiText += String.Format(bz, "{0}_shape->SetOpacity({1});\n", shapename, 1.0 - vMatProperties[7]);
                    //    }

                    //MathTransform absframe_chbody = chBodyComp.GetTotalTransform(true);
                    //MathTransform absframe_shape = swComp.GetTotalTransform(true);
                    //MathTransform absframe_chbody_inv = absframe_chbody.IInverse();
                    //MathTransform relframe_shape = absframe_shape.IMultiply(absframe_chbody_inv);  // row-ordered transf. -> reverse mult.order!
                    //double[] amatr = (double[])relframe_shape.ArrayData;
                    //double[] quat = GetQuaternionFromMatrix(ref relframe_shape);

                    //m_asciiText += String.Format(bz, "{0}->AddVisualShape({1}_shape, chrono::ChFrame<>(", bodyname, shapename);
                    //m_asciiText += String.Format(bz, "chrono::ChVector<>({0},{1},{2}), ", amatr[9] * ChScale.L, amatr[10] * ChScale.L, amatr[11] * ChScale.L);
                    //m_asciiText += String.Format(bz, "chrono::ChQuaternion<>({0},{1},{2},{3})", quat[0], quat[1], quat[2], quat[3]);
                    //m_asciiText += String.Format(bz, "));\n");
                }



            // Recursive scan of subcomponents

            Component2 swChildComp;
            object[] vChildComp = (object[])swComp.GetChildren();

            for (long i = 0; i < vChildComp.Length; i++)
            {
                swChildComp = (Component2)vChildComp[i];

                if (swChildComp.Visible == (int)swComponentVisibilityState_e.swComponentVisible)
                    TraverseComponentForVisualShapes(swChildComp, nLevel + 1, ref nVisShape, chBodyComp);
            }
        }

        public override void TraverseFeaturesForCollisionShapes(Component2 swComp, long nLevel, ref MathTransform chbodytransform, ref bool found_collisionshapes, Component2 swCompBase, ref int ncollshape)
        {
            CultureInfo bz = new CultureInfo("en-BZ");
            Feature swFeat;
            swFeat = (Feature)swComp.FirstFeature();

            String bodyname = "body_" + nbody;
            String matname = "mat_" + nbody;

            MathTransform subcomp_transform = swComp.GetTotalTransform(true);
            MathTransform invchbody_trasform = (MathTransform)chbodytransform.Inverse();
            MathTransform collshape_subcomp_transform = subcomp_transform.IMultiply(invchbody_trasform); // row-ordered transf. -> reverse mult.order!
            
            // Export collision shapes
            if (m_swIntegration.m_taskpaneHost.GetCheckboxCollisionShapes().Checked) 
            {
                object[] bodies;
                object bodyInfo;
                bodies = (object[])swComp.GetBodies3((int)swBodyType_e.swAllBodies, out bodyInfo);

                if (bodies != null)
                {
                    // see if it contains some collision shape
                    bool build_collision_model = false;
                    for (int ib = 0; ib < bodies.Length; ib++)
                    {
                        Body2 swBody = (Body2)bodies[ib];
                        if (swBody.Name.StartsWith("COLL.") || swBody.Name.StartsWith("COLLMESH"))
                            build_collision_model = true;
                    }

                    if (build_collision_model)
                    {
                        if (!found_collisionshapes)
                        {
                            found_collisionshapes = true;

                            // fetch SW attribute with Chrono parameters
                            SolidWorks.Interop.sldworks.Attribute myattr = (SolidWorks.Interop.sldworks.Attribute)swCompBase.FindAttribute(m_swIntegration.defattr_chbody, 0);


                            m_asciiText += "\n// Collision material\n";

                            m_asciiText += String.Format(bz, "auto {0} = chrono_types::make_shared<chrono::ChMaterialSurfaceNSC>();\n", matname);



                            if (myattr != null)
                            {

                                m_asciiText += "\n// Collision parameters ;\n";
                                double param_friction = ((Parameter)myattr.GetParameter("friction")).GetDoubleValue();
                                double param_restitution = ((Parameter)myattr.GetParameter("restitution")).GetDoubleValue();
                                double param_rolling_friction = ((Parameter)myattr.GetParameter("rolling_friction")).GetDoubleValue();
                                double param_spinning_friction = ((Parameter)myattr.GetParameter("spinning_friction")).GetDoubleValue();
                                double param_collision_envelope = ((Parameter)myattr.GetParameter("collision_envelope")).GetDoubleValue();
                                double param_collision_margin = ((Parameter)myattr.GetParameter("collision_margin")).GetDoubleValue();
                                int param_collision_family = (int)((Parameter)myattr.GetParameter("collision_family")).GetDoubleValue();

                                m_asciiText += String.Format(bz, "{0}->SetFriction({1:g});\n", matname, param_friction);
                                if (param_restitution != 0)
                                    m_asciiText += String.Format(bz, "{0}->SetRestitution({1:g});\n", matname, param_restitution);
                                if (param_rolling_friction != 0)
                                    m_asciiText += String.Format(bz, "{0}->SetRollingFriction({1:g});\n", matname, param_rolling_friction);
                                if (param_spinning_friction != 0)
                                    m_asciiText += String.Format(bz, "{0}->SetSpinningFriction({1:g});\n", matname, param_spinning_friction);
                                //if (param_collision_envelope != 0.03)
                                m_asciiText += String.Format(bz, "{0}->GetCollisionModel()->SetEnvelope({1:g});\n", bodyname, param_collision_envelope * ChScale.L);
                                //if (param_collision_margin != 0.01)
                                m_asciiText += String.Format(bz, "{0}->GetCollisionModel()->SetSafeMargin({1:g});\n", bodyname, param_collision_margin * ChScale.L);
                                if (param_collision_family != 0)
                                    m_asciiText += String.Format(bz, "{0}->GetCollisionModel()->SetFamily({1});\n", bodyname, param_collision_family);
                            }

                            // clear model only at 1st subcomponent where coll shapes are found in features:
                            m_asciiText += "\n// Collision shapes\n";
                            m_asciiText += String.Format(bz, "{0}->GetCollisionModel()->ClearModel();\n", bodyname);
                        }

                        bool has_coll_mesh = false;

                        for (int ib = 0; ib < bodies.Length; ib++)
                        {
                            Body2 swBody = (Body2)bodies[ib];

                            if (swBody.Name.StartsWith("COLLMESH"))
                            {
                                has_coll_mesh = true;
                            }

                            if (swBody.Name.StartsWith("COLL."))
                            {
                                bool rbody_converted = false;
                                if (ConvertToCollisionShapes.SWbodyToSphere(swBody))
                                {
                                    Point3D center_l = new Point3D(); // in local subcomponent
                                    double rad = 0;
                                    ConvertToCollisionShapes.SWbodyToSphere(swBody, ref rad, ref center_l);
                                    Point3D center = PointTransform(center_l, ref collshape_subcomp_transform);
                                    m_asciiText += String.Format(bz, "{0}->GetCollisionModel()->AddSphere({1}, {2}, chrono::ChVector<>({3},{4},{5}));\n",
                                        bodyname, matname,
                                        rad * ChScale.L,
                                        center.X * ChScale.L,
                                        center.Y * ChScale.L,
                                        center.Z * ChScale.L);
                                    rbody_converted = true;
                                }
                                if (ConvertToCollisionShapes.SWbodyToBox(swBody))
                                {
                                    Point3D vC_l = new Point3D();
                                    Vector3D eX_l = new Vector3D(); Vector3D eY_l = new Vector3D(); Vector3D eZ_l = new Vector3D();
                                    ConvertToCollisionShapes.SWbodyToBox(swBody, ref vC_l, ref eX_l, ref eY_l, ref eZ_l);
                                    Point3D vC = PointTransform(vC_l, ref collshape_subcomp_transform);
                                    Vector3D eX = DirTransform(eX_l, ref collshape_subcomp_transform);
                                    Vector3D eY = DirTransform(eY_l, ref collshape_subcomp_transform);
                                    Vector3D eZ = DirTransform(eZ_l, ref collshape_subcomp_transform);
                                    Point3D vO = vC + 0.5 * eX + 0.5 * eY + 0.5 * eZ;
                                    Vector3D Dx = eX; Dx.Normalize();
                                    Vector3D Dy = eY; Dy.Normalize();
                                    Vector3D Dz = Vector3D.CrossProduct(Dx, Dy);
                                    m_asciiText += String.Format(bz, "mr(0,0)={0}; mr(1,0)={1}; mr(2,0)={2};\n", Dx.X, Dx.Y, Dx.Z, bodyname);
                                    m_asciiText += String.Format(bz, "mr(0,1)={0}; mr(1,1)={1}; mr(2,1)={2};\n", Dy.X, Dy.Y, Dy.Z, bodyname);
                                    m_asciiText += String.Format(bz, "mr(0,2)={0}; mr(1,2)={1}; mr(2,2)={2};\n", Dz.X, Dz.Y, Dz.Z, bodyname);
                                    m_asciiText += String.Format(bz, "{0}->GetCollisionModel()->AddBox({1},{2},{3},{4},chrono::ChVector<>({5},{6},{7}),mr);\n",
                                        bodyname, matname,
                                        eX.Length * ChScale.L,
                                        eY.Length * ChScale.L,
                                        eZ.Length * ChScale.L,
                                        vO.X * ChScale.L,
                                        vO.Y * ChScale.L,
                                        vO.Z * ChScale.L);
                                    rbody_converted = true;
                                }
                                if (ConvertToCollisionShapes.SWbodyToCylinder(swBody))
                                {
                                    Point3D p1_l = new Point3D();
                                    Point3D p2_l = new Point3D();
                                    double rad = 0;
                                    ConvertToCollisionShapes.SWbodyToCylinder(swBody, ref p1_l, ref p2_l, ref rad);
                                    Point3D p1 = PointTransform(p1_l, ref collshape_subcomp_transform);
                                    Point3D p2 = PointTransform(p2_l, ref collshape_subcomp_transform);
                                    m_asciiText += String.Format(bz, "chrono::ChVector<> p1_{3}({0},{1},{2});\n", p1.X * ChScale.L, p1.Y * ChScale.L, p1.Z * ChScale.L, bodyname);
                                    m_asciiText += String.Format(bz, "chrono::ChVector<> p2_{3}({0},{1},{2});\n", p2.X * ChScale.L, p2.Y * ChScale.L, p2.Z * ChScale.L, bodyname);
                                    m_asciiText += String.Format(bz, "{0}->GetCollisionModel()->AddCylinder({1},{2},p1_{0},p2_{0});\n", bodyname, matname, rad * ChScale.L);
                                    rbody_converted = true;
                                }

                                if (ConvertToCollisionShapes.SWbodyToConvexHull(swBody, 30) && !rbody_converted)
                                {
                                    Point3D[] vertexes = new Point3D[1]; // will be resized by SWbodyToConvexHull
                                    ConvertToCollisionShapes.SWbodyToConvexHull(swBody, ref vertexes, 30);
                                    if (vertexes.Length > 0)
                                    {
                                        m_asciiText += String.Format(bz, "std::vector<ChVector<>> pt_vect_{0};\n", bodyname);
                                        for (int iv = 0; iv < vertexes.Length; iv++)
                                        {
                                            Point3D vert_l = vertexes[iv];
                                            Point3D vert = PointTransform(vert_l, ref collshape_subcomp_transform);
                                            m_asciiText += String.Format(bz, "pt_vect_{3}.push_back(chrono::ChVector<>({0},{1},{2}));\n",
                                                vert.X * ChScale.L,
                                                vert.Y * ChScale.L,
                                                vert.Z * ChScale.L,
                                                bodyname);
                                        }
                                        m_asciiText += String.Format(bz, "{0}->GetCollisionModel()->AddConvexHull({1},pt_vect_{0});\n", bodyname, matname);
                                    }
                                    rbody_converted = true;
                                }


                            } // end dealing with a collision shape

                        } // end solid bodies traversal for converting to coll.shapes



                        if (has_coll_mesh)
                        {
                            // fallback if no primitive collision shape found: use concave trimesh collision model (although inefficient)
                            ncollshape += 1;
                            string shapename = "body_" + nbody + "_" + ncollshape + "_collision";
                            string obj_filename = this.save_dir_shapes + "\\" + shapename + ".obj";

                            ModelDoc2 swCompModel = (ModelDoc2)swComp.GetModelDoc();
                            if (!m_savedCollisionMeshes.ContainsKey(swCompModel.GetPathName()))
                            {
                                try
                                {
                                    FileStream ostream = new FileStream(obj_filename, FileMode.Create, FileAccess.ReadWrite);
                                    StreamWriter writer = new StreamWriter(ostream); //, new UnicodeEncoding());
                                    string asciiobj = "";
                                    if (m_swIntegration.m_taskpaneHost.GetProgressBar() != null)
                                        m_swIntegration.m_taskpaneHost.GetProgressBar().UpdateTitle("Exporting collision shape" + swComp.Name2 + " (tesselate) ...");
                                    // Write the OBJ converted visualization shapes:
                                    TesselateToObj.Convert(swComp, ref asciiobj, m_swIntegration.m_taskpaneHost.GetCheckboxSaveUV().Checked, ref m_swIntegration.m_taskpaneHost.GetProgressBar(), false, true);
                                    writer.Write(asciiobj);
                                    writer.Flush();
                                    ostream.Close();

                                    m_savedCollisionMeshes.Add(swCompModel.GetPathName(), shapename);
                                }
                                catch (Exception)
                                {
                                    System.Windows.Forms.MessageBox.Show("Cannot write to file: " + obj_filename + ";\n for component: " + swComp.Name2 + " for path name: " + swCompModel.GetPathName());
                                }
                            }
                            else
                            {
                                // reuse the already-saved shape name
                                shapename = (String)m_savedCollisionMeshes[swCompModel.GetPathName()];
                            }

                            double[] amatr = (double[])collshape_subcomp_transform.ArrayData;
                            double[] quat = GetQuaternionFromMatrix(ref collshape_subcomp_transform);

                            m_asciiText += String.Format(bz, ";\n// Triangle mesh collision shape\n", bodyname);
                            m_asciiText += String.Format(bz, "std::shared_ptr<chrono::ChTriangleMeshConnected> {0}_mesh;\n", shapename);
                            m_asciiText += String.Format(bz, "{0}_mesh->CreateFromWavefrontFile(shapes_dir + \"{0}.obj\", false, true);\n", shapename);
                            m_asciiText += String.Format(bz, "chrono::ChMatrix33<> mr;\n");
                            m_asciiText += String.Format(bz, "mr(0,0)={0}; mr(1,0)={1}; mr(2,0)={2};\n", amatr[0] * ChScale.L, amatr[1] * ChScale.L, amatr[2] * ChScale.L, shapename);
                            m_asciiText += String.Format(bz, "mr(0,1)={0}; mr(1,1)={1}; mr(2,1)={2};\n", amatr[3] * ChScale.L, amatr[4] * ChScale.L, amatr[5] * ChScale.L, shapename);
                            m_asciiText += String.Format(bz, "mr(0,2)={0}; mr(1,2)={1}; mr(2,2)={2};\n", amatr[6] * ChScale.L, amatr[7] * ChScale.L, amatr[8] * ChScale.L, shapename);
                            m_asciiText += String.Format(bz, "{0}_mesh->Transform(chrono::ChVector<>({1},{2},{3}),mr);\n", shapename, amatr[9] * ChScale.L, amatr[10] * ChScale.L, amatr[11] * ChScale.L);
                            m_asciiText += String.Format(bz, "{0}->GetCollisionModel()->AddTriangleMesh({1},{2}_mesh,false,false,", bodyname, matname, shapename);
                            m_asciiText += String.Format(bz, "chrono::ChVector<>(0,0,0), chrono::ChMatrix33<>(chrono::ChQuaternion<>(1,0,0,0)), sphereswept_r);\n");
                            //rbody_converted = true;
                        }


                    } // end if build_collision_model
                }

            } // end collision shapes export

        }

        public override void TraverseComponentForBodies(Component2 swComp, long nLevel)
        {
            CultureInfo bz = new CultureInfo("en-BZ");
            object[] vmyChildComp = (object[])swComp.GetChildren();
            //bool found_chbody_equivalent = false;

            if (nLevel > 1)
                if (nbody == -1)
                    if (!swComp.IsSuppressed()) // skip body if marked as 'suppressed'
                    {
                        if ((swComp.Solving == (int)swComponentSolvingOption_e.swComponentRigidSolving) || (vmyChildComp.Length == 0))
                        {
                            // OK! this is a 'leaf' of the tree of ChBody equivalents (a SDW subassebly or part)

                            //found_chbody_equivalent = true;

                            this.num_comp++;

                            nbody = this.num_comp;  // mark the rest of recursion as 'n-th body found'

                            if (m_swIntegration.m_taskpaneHost.GetProgressBar() != null)
                            {
                                m_swIntegration.m_taskpaneHost.GetProgressBar().UpdateTitle("Exporting " + swComp.Name2 + " ...");
                                m_swIntegration.m_taskpaneHost.GetProgressBar().UpdateProgress(this.num_comp % 5);
                            }

                            // fetch SW attribute with Chrono parameters
                            SolidWorks.Interop.sldworks.Attribute myattr = (SolidWorks.Interop.sldworks.Attribute)swComp.FindAttribute(m_swIntegration.defattr_chbody, 0);

                            MathTransform chbodytransform = swComp.GetTotalTransform(true);
                            double[] amatr;
                            amatr = (double[])chbodytransform.ArrayData;
                            string bodyname = "body_" + this.num_comp;

                            // Write create body
                            m_asciiText += "// Rigid body part\n";
                            m_asciiText += "auto " + bodyname + " = chrono_types::make_shared<chrono::ChBodyAuxRef>();\n";

                            m_exportNamesMap[swComp.Name2] = bodyname;

                            // Write name
                            m_asciiText += bodyname + "->SetName(\"" + swComp.Name2 + "\");\n";

                            // Write position
                            m_asciiText += bodyname + "->SetPos(chrono::ChVector<>("
                                       + (amatr[9] * ChScale.L).ToString("g", bz) + ","
                                       + (amatr[10] * ChScale.L).ToString("g", bz) + ","
                                       + (amatr[11] * ChScale.L).ToString("g", bz) + "));\n";

                            // Write rotation
                            double[] quat = GetQuaternionFromMatrix(ref chbodytransform);
                            m_asciiText += String.Format(bz, "{0}->SetRot(chrono::ChQuaternion<>({1:g},{2:g},{3:g},{4:g}));\n",
                                       bodyname, quat[0], quat[1], quat[2], quat[3]);

                            // Compute mass
                            int nvalid_bodies = 0;
                            TraverseComponent_for_countingmassbodies(swComp, ref nvalid_bodies);

                            int addedb = 0;
                            object[] bodies_nocollshapes = new object[nvalid_bodies];
                            TraverseComponent_for_massbodies(swComp, ref bodies_nocollshapes, ref addedb);

                            MassProperty swMass;
                            swMass = (MassProperty)swComp.IGetModelDoc().Extension.CreateMassProperty();
                            bool boolstatus = false;
                            boolstatus = swMass.AddBodies((object[])bodies_nocollshapes);
                            swMass.SetCoordinateSystem(chbodytransform);
                            swMass.UseSystemUnits = true;
                            //note: do not set here the COG-to-REF position because here SW express it in absolute coords
                            // double cogX = ((double[])swMass.CenterOfMass)[0];
                            // double cogY = ((double[])swMass.CenterOfMass)[1];
                            // double cogZ = ((double[])swMass.CenterOfMass)[2];
                            double mass = swMass.Mass;
                            double[] Itensor = (double[])swMass.GetMomentOfInertia((int)swMassPropertyMoment_e.swMassPropertyMomentAboutCenterOfMass);
                            double Ixx = Itensor[0];
                            double Iyy = Itensor[4];
                            double Izz = Itensor[8];
                            double Ixy = Itensor[1];
                            double Izx = Itensor[2];
                            double Iyz = Itensor[5];

                            MassProperty swMassb;
                            swMassb = (MassProperty)swComp.IGetModelDoc().Extension.CreateMassProperty();
                            bool boolstatusb = false;
                            boolstatusb = swMassb.AddBodies(bodies_nocollshapes);
                            swMassb.UseSystemUnits = true;
                            double cogXb = ((double[])swMassb.CenterOfMass)[0];
                            double cogYb = ((double[])swMassb.CenterOfMass)[1];
                            double cogZb = ((double[])swMassb.CenterOfMass)[2];

                            m_asciiText += String.Format(bz, "{0}->SetMass({1:g});\n",
                                       bodyname,
                                       mass * ChScale.M);

                            // Write inertia tensor 
                            m_asciiText += String.Format(bz, "{0}->SetInertiaXX(chrono::ChVector<>({1:g},{2:g},{3:g}));\n",
                                       bodyname,
                                       Ixx * ChScale.M * ChScale.L * ChScale.L,
                                       Iyy * ChScale.M * ChScale.L * ChScale.L,
                                       Izz * ChScale.M * ChScale.L * ChScale.L);
                            // Note: C::E assumes that's up to you to put a 'minus' sign in values of Ixy, Iyz, Izx
                            m_asciiText += String.Format(bz, "{0}->SetInertiaXY(chrono::ChVector<>({1:g},{2:g},{3:g}));\n",
                                       bodyname,
                                       -Ixy * ChScale.M * ChScale.L * ChScale.L,
                                       -Izx * ChScale.M * ChScale.L * ChScale.L,
                                       -Iyz * ChScale.M * ChScale.L * ChScale.L);

                            // Write the position of the COG respect to the REF
                            m_asciiText += String.Format(bz, "{0}->SetFrame_COG_to_REF(chrono::ChFrame<>(chrono::ChVector<>({1:g},{2:g},{3:g}),chrono::ChQuaternion<>(1,0,0,0)));\n",
                                        bodyname,
                                        cogXb * ChScale.L,
                                        cogYb * ChScale.L,
                                        cogZb * ChScale.L);

                            // Write 'fixed' state
                            if (swComp.IsFixed())
                                m_asciiText += String.Format(bz, "{0}->SetBodyFixed(true);\n", bodyname);

                            
                            // Write shapes (saving also Wavefront files .obj)
                            if (m_swIntegration.m_taskpaneHost.GetCheckboxSurfaces().Checked)
                            {
                                int nvisshape = 0;

                                if (swComp.Visible == (int)swComponentVisibilityState_e.swComponentVisible)
                                    TraverseComponentForVisualShapes(swComp, nLevel, ref nvisshape, swComp);
                            }

                            // Write markers (SW coordsystems) contained in this component or subcomponents
                            // if any.
                            TraverseComponentForMarkers(swComp, nLevel);

                            // Write collision shapes (customized SW solid bodies) contained in this component or subcomponents
                            // if any.
                            bool param_collide = true;
                            if (myattr != null)
                                param_collide = Convert.ToBoolean(((Parameter)myattr.GetParameter("collision_on")).GetDoubleValue());

                            if (param_collide)
                            {
                                bool found_collisionshapes = false;
                                int ncollshapes = 0;

                                TraverseComponentForCollisionShapes(swComp, nLevel, ref chbodytransform, ref found_collisionshapes, swComp, ref ncollshapes);
                                if (found_collisionshapes)
                                {
                                    m_asciiText += String.Format(bz, "{0}->GetCollisionModel()->BuildModel();\n", bodyname);
                                    m_asciiText += String.Format(bz, "{0}->SetCollide(true);\n", bodyname);
                                }
                            }

                            // Insert to a list of exported items
                            m_asciiText += String.Format(bz, "\nbodylist.push_back({0});\n", bodyname);

                            // End writing body in 
                            m_asciiText += "\n\n\n";


                        } // end if ChBody equivalent (tree leaf or non-flexible assembly)
                    }


            // Things to do also for sub-components of 'non flexible' assemblies: 
            //

            // store in hashtable, will be useful later when adding constraints
            if ((nLevel > 1) && (nbody != -1))
                try
                {
                    string bodyname = "body_" + this.num_comp;

                    ModelDocExtension swModelDocExt = default(ModelDocExtension);
                    ModelDoc2 swModel = (ModelDoc2)m_swIntegration.m_swApplication.ActiveDoc;
                    //if (swModel != null)
                    swModelDocExt = swModel.Extension;
                    m_savedParts.Add(swModelDocExt.GetPersistReference3(swComp), bodyname);
                }
                catch
                {
                    System.Windows.Forms.MessageBox.Show("Cannot add part to hashtable?");
                }


            // Traverse all children, proceeding to subassemblies and parts, if any
            // 

            object[] vChildComp;
            Component2 swChildComp;

            vChildComp = (object[])swComp.GetChildren();

            for (long i = 0; i < vChildComp.Length; i++)
            {
                swChildComp = (Component2)vChildComp[i];

                TraverseComponentForBodies(swChildComp, nLevel + 1);
            }


        }

        public override void TraverseComponentForMarkers(Component2 swComp, long nLevel)
        {
            // Look if component contains markers
            Feature swFeat = (Feature)swComp.FirstFeature();
            MathTransform swCompTotalTrasf = swComp.GetTotalTransform(true);
            TraverseFeaturesForMarkers(swFeat, nLevel, swCompTotalTrasf);

            // Recursive scan of subcomponents

            Component2 swChildComp;
            object[] vChildComp = (object[])swComp.GetChildren();

            for (long i = 0; i < vChildComp.Length; i++)
            {
                swChildComp = (Component2)vChildComp[i];

                TraverseComponentForMarkers(swChildComp, nLevel + 1);
            }
        }

        public override void TraverseFeaturesForMarkers(Feature swFeat, long nLevel, MathTransform swCompTotalTrasf)
        {
            CultureInfo bz = new CultureInfo("en-BZ");

            int nmarker = 0;

            String bodyname = "body_" + nbody;

            while ((swFeat != null))
            {
                // m_asciiText += "# feature: " + swFeat.Name + " [" + swFeat.GetTypeName2() + "]" + "\n";

                // Export markers, if any (as coordinate systems)
                if (swFeat.GetTypeName2() == "CoordSys")
                {
                    nmarker++;
                    CoordinateSystemFeatureData swCoordSys = (CoordinateSystemFeatureData)swFeat.GetDefinition();
                    MathTransform tr = swCoordSys.Transform;

                    MathTransform tr_part = swCompTotalTrasf;
                    MathTransform tr_abs = tr.IMultiply(tr_part);  // row-ordered transf. -> reverse mult.order!

                    double[] quat = GetQuaternionFromMatrix(ref tr_abs);
                    double[] amatr = (double[])tr_abs.ArrayData;
                    String markername = "marker_" + nbody + "_" + nmarker;
                    m_asciiText += "\n// Auxiliary marker (coordinate system feature)\n";
                    m_asciiText += String.Format(bz, "auto {0} = chrono_types::make_shared<chrono::ChMarker>();\n", markername);
                    m_asciiText += String.Format(bz, "{0}->SetName(\"{1}\");\n", markername, swFeat.Name);
                    m_asciiText += String.Format(bz, "{0}->AddMarker({1});\n", bodyname, markername);
                    m_asciiText += String.Format(bz, "{0}->Impose_Abs_Coord(chrono::ChCoordsys<>(chrono::ChVector<>({1},{2},{3}),chrono::ChQuaternion<>({4},{5},{6},{7})));\n",
                        markername,
                        amatr[9] * ChScale.L,
                        amatr[10] * ChScale.L,
                        amatr[11] * ChScale.L,
                        quat[0], quat[1], quat[2], quat[3]);

                    m_exportNamesMap[swFeat.Name] = markername;

                    // Export ChMotor from attributes embedded in marker, if any
                    if ((SolidWorks.Interop.sldworks.Attribute)((Entity)swFeat).FindAttribute(m_swIntegration.defattr_chlink, 0) != null)
                    {
                        SolidWorks.Interop.sldworks.Attribute motorAttribute = (SolidWorks.Interop.sldworks.Attribute)((Entity)swFeat).FindAttribute(m_swIntegration.defattr_chlink, 0);

                        string motorName = ((Parameter)motorAttribute.GetParameter("motor_name")).GetStringValue();
                        string motorType = ((Parameter)motorAttribute.GetParameter("motor_type")).GetStringValue();
                        string motorMotionlaw = ((Parameter)motorAttribute.GetParameter("motor_motionlaw")).GetStringValue();
                        string motorConstraints = ((Parameter)motorAttribute.GetParameter("motor_constraints")).GetStringValue();
                        string motorMarker = ((Parameter)motorAttribute.GetParameter("motor_marker")).GetStringValue();
                        string motorBody1 = ((Parameter)motorAttribute.GetParameter("motor_body1")).GetStringValue();
                        string motorBody2 = ((Parameter)motorAttribute.GetParameter("motor_body2")).GetStringValue();

                        ModelDoc2 swModel = (ModelDoc2)m_swIntegration.m_swApplication.ActiveDoc;
                        byte[] selMarkerRef = (byte[])EditChMotor.GetIDFromString(swModel, motorMarker);
                        byte[] selBody1Ref = (byte[])EditChMotor.GetIDFromString(swModel, motorBody1);
                        byte[] selBody2Ref = (byte[])EditChMotor.GetIDFromString(swModel, motorBody2);

                        Feature selectedMarker = (Feature)EditChMotor.GetObjectFromID(swModel, selMarkerRef); // actually, already selected through current traverse
                        SolidWorks.Interop.sldworks.Component2 selectedBody1 = (Component2)EditChMotor.GetObjectFromID(swModel, selBody1Ref);
                        SolidWorks.Interop.sldworks.Component2 selectedBody2 = (Component2)EditChMotor.GetObjectFromID(swModel, selBody2Ref);

                        string chMotorClassName = "ChLinkMotor" + motorType;
                        string chMotorConstraintName = "";
                        string chFunctionClassName = "ChFunction_" + motorMotionlaw;
                        string motorQuaternion = "";

                        if (motorType == "LinearPosition" || motorType == "LinearSpeed" || motorType == "LinearForce")
                        {
                            motorQuaternion = "chrono::Q_ROTATE_X_TO_Z";
                            chMotorConstraintName = "GuideConstraint";
                        }
                        else
                        {
                            motorQuaternion = "chrono::QUNIT";
                            chMotorConstraintName = "SpindleConstraint";
                        }

                        String motorInstanceName = "motor_" + nbody + "_" + nmarker;
                        m_asciiText += "\n// Motor from Solidworks marker\n";
                        m_asciiText += String.Format(bz, "auto {0} = chrono_types::make_shared<chrono::" + chMotorClassName + ">();\n", motorInstanceName);
                        m_asciiText += String.Format(bz, "{0}->SetName(\"{1}\");\n", motorInstanceName, motorName);
                        m_asciiText += String.Format(bz,
                            "{0}->Initialize({1},{2},chrono::ChFrame<>(" + m_exportNamesMap[swFeat.Name] + "->GetAbsFrame().GetPos()," + m_exportNamesMap[swFeat.Name] + "->GetAbsFrame().GetRot()*" + motorQuaternion + "));\n", motorInstanceName,
                            m_exportNamesMap[selectedBody1.Name],
                            m_exportNamesMap[selectedBody2.Name]);
                        if (motorConstraints == "False")
                        {
                            m_asciiText += String.Format(bz, "{0}->Set" + chMotorConstraintName + "(false, false, false, false, false);\n", motorInstanceName);
                        }
                        m_asciiText += String.Format(bz, "linklist.push_back(" + motorInstanceName + ");\n");
                        m_asciiText += String.Format(bz, "//\n");
                        String motfunInstanceName = "motfun_" + nbody + "_" + nmarker;
                        m_asciiText += String.Format(bz, "auto {0} = chrono_types::make_shared<chrono::{1}>();\n", motfunInstanceName, chFunctionClassName);
                        m_asciiText += String.Format(bz, "{0}->SetMotorFunction({1});\n", motorInstanceName, motfunInstanceName);
                        m_asciiText += String.Format(bz, "//\n");
                        m_asciiText += String.Format(bz, "(*motfun_map)[\"" + motorName + "\"] = " + motfunInstanceName + ";\n");
                    }

                }

                swFeat = (Feature)swFeat.GetNextFeature();
            }
        }

    }
}
