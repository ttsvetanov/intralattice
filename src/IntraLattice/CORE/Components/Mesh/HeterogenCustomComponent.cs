﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Grasshopper.Kernel;
using Rhino;
using Rhino.Geometry;
using Rhino.Collections;
using IntraLattice.CORE.Data;
using IntraLattice.CORE.Components;
using IntraLattice.CORE.Helpers;

// Summary:     This component generates a solid mesh of a curve network, with custom strut radii.
//              Approach based on Exoskeleton by David Stasiuk.
// ===============================================================================
// Details:     - Strut radii specified as a list of start/end radii. (parallel to curves list)
// ===============================================================================
// Author(s):   Aidan Kurtz (http://aidankurtz.com)

namespace IntraLattice.CORE.MeshModule
{
    public class HeterogenCustom : GH_Component
    {
        public HeterogenCustom()
            : base("Heterogen Custom", "HeterogenCustom",
                "Heterogeneous solidification of lattice wireframe",
                "IntraLattice2", "Mesh")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Struts", "Struts", "Wireframe to thicken.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Start Radii", "StartRadii", "Radius at the start of each strut.", GH_ParamAccess.list);
            pManager.AddNumberParameter("End Radii", "EndRadii", "Radius at the end of each strut.", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "Thickened wireframe.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // 0. Declare placeholder variables
            var struts = new List<Curve>();
            var startRadius = new List<double>();
            var endRadius = new List<double>();

            // 1. Attempt to fetch data inputs
            if (!DA.GetDataList(0, struts)) { return; }
            if (!DA.GetData(1, ref startRadius)) { return; }
            if (!DA.GetData(2, ref endRadius)) { return; }

            // 2. Validate data
            if (struts == null || struts.Count == 0) { return; }
            if (startRadius != null || startRadius.Count == 0) { return; }
            if (endRadius != null || endRadius.Count == 0) { return; }
            if (startRadius.Count != struts.Count || endRadius.Count != struts.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Number of radii in each list must have same number of elements as the struts list.");
                return;
            }

            // 3. Set some variables
            int sides = 6;  // Number of sides on each strut
            double tol = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

            // 4. Initialize lattice object
            // This constructor cleans the curve network (removes duplicates), and formats it as an ExoMesh.
            ExoMesh exoMesh = new ExoMesh(struts);

            //====================================================================================
            // PART A - Compute radii
            // Set the start/end radii of each sleeve, based on user input.
            //====================================================================================
            for (int i = 0; i < exoMesh.Sleeves.Count; i++ )
            {
                exoMesh.Sleeves[i].StartRadius = startRadius[i];
                exoMesh.Sleeves[i].EndRadius = endRadius[i];
            }

            //====================================================================================
            // PART B - Compute plate offsets
            // Each plate is offset from its parent node, to avoid mesh overlaps.
            // We also ensure that the no plates are engulfed by the hulls, so we're looking for
            // a convex plate layout. If any plate vertex gets engulfed, meshing will fail.
            //====================================================================================

            // B0. Loop over nodes
            for (int i = 0; i < exoMesh.Hulls.Count; i++)
            {
                // if node has only 1 strut, skip it
                if (exoMesh.Hulls[i].StrutIndices.Count < 2) continue;

                // compute the offsets required to avoid plate overlaps
                exoMesh.ComputeOffsets(i, tol);
                exoMesh.FixSharpNodes(i, sides);
            }

            // IDEA : add a new loop here that adjusts radii to avoid overlapping struts

            //====================================================================================
            // PART C - Construct sleeve meshes and hull points
            // 
            //====================================================================================

            // C0. Loop over all sleeves
            for (int i = 0; i < exoMesh.Sleeves.Count; i++)
            {
                Mesh sleeveMesh = exoMesh.MakeSleeve(i, sides);
                // append the new sleeve mesh to the full lattice mesh
                exoMesh.Mesh.Append(sleeveMesh);
            }

            //====================================================================================
            // PART D - Construct hull meshes
            // Generates convex hulls, then removes the faces that lie on the plates.
            //====================================================================================

            // D0. Loop over all hulls
            for (int i = 0; i < exoMesh.Hulls.Count; i++)
            {
                ExoHull node = exoMesh.Hulls[i];

                int plateCount = exoMesh.Hulls[i].PlateIndices.Count;
                // If node has a single plate, create an endmesh
                if (plateCount < 2)
                {
                    Mesh endMesh = exoMesh.MakeEndFace(i, sides);
                    exoMesh.Mesh.Append(endMesh);
                }
                // If node has more than 1 plate, create a hullmesh
                else
                {
                    Mesh hullMesh = exoMesh.MakeConvexHull(i, sides, tol, true);
                    exoMesh.Mesh.Append(hullMesh);
                }
            }

            // Post-process the final mesh.
            exoMesh.Mesh.Vertices.CombineIdentical(true, true);
            exoMesh.Mesh.FaceNormals.ComputeFaceNormals();
            exoMesh.Mesh.UnifyNormals();
            exoMesh.Mesh.Normals.ComputeNormals();


            DA.SetData(0, exoMesh.Mesh);

        }

        public override GH_Exposure Exposure
        {
            get
            {
                return GH_Exposure.secondary;
            }
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //return Exoskeleton.Properties.Resources.exoskel;
                return null;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("{5fa648cd-af7e-41e5-ac9c-f81bc19466bb}"); }
        }

    }
}