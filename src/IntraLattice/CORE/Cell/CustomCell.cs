﻿using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Drawing;
using Rhino;

namespace IntraLattice
{
    public class CustomCell : GH_Component
    {
        public CustomCell()
            : base("CustomCell", "CustomCell",
                "Use to define custom cells. Checks validity of cell and outputs topology.",
                "IntraLattice2", "Cell")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Custom Cell", "L", "Unit cell lines (curves must be linear).", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Topology", "Topo", "Verified unit cell topology", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Retrieve input
            var curves = new List<Curve>();
            if (!DA.GetDataList(0, curves)) { return; }

            // Convert curve input to line input
            var lines = new List<Line>();
            foreach (LineCurve curve in curves) lines.Add(curve.Line);

            // Set tolerance
            double tol = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

            // Explode lines at intersections
            CellTools.FixIntersections(ref lines);

            // Set bounding box
            BoundingBox bound = new BoundingBox();
            foreach (Line line in lines)
                bound.Union(line.BoundingBox); // combine bounding box to full cell box

            // Extract unique nodes and nodePairs (stored in the UnitCell object)
            var cell = new UnitCell();
            CellTools.ExtractTopology(ref lines, ref cell);

            // The check - Opposing faces must be identical
            // Set up the face planes
            Plane[] xy = new Plane[2];
            xy[0] = new Plane(bound.Corner(true, true, true), Plane.WorldXY.ZAxis);
            xy[1] = new Plane(bound.Corner(true, true, false), Plane.WorldXY.ZAxis);
            Plane[] yz = new Plane[2];
            yz[0] = new Plane(bound.Corner(true, true, true), Plane.WorldXY.XAxis);
            yz[1] = new Plane(bound.Corner(false, true, true), Plane.WorldXY.XAxis);
            Plane[] zx = new Plane[2];
            zx[0] = new Plane(bound.Corner(true, true, true), Plane.WorldXY.YAxis);
            zx[1] = new Plane(bound.Corner(true, false, true), Plane.WorldXY.YAxis);
            // Loop through nodes
            foreach (Point3d node in cell.Nodes)
            {
                // Essentially, for every node, we must find it's mirror node on the opposite face
                // First, check if node requires a mirror node, and where that mirror node should be (testPoint)
                Point3d testPoint = Point3d.Unset;
                
                if (Math.Abs(xy[0].DistanceTo(node)) < tol)
                    testPoint = new Point3d(node.X, node.Y, xy[1].OriginZ);
                if (Math.Abs(xy[1].DistanceTo(node)) < tol)
                    testPoint = new Point3d(node.X, node.Y, xy[0].OriginZ);
                if (Math.Abs(yz[0].DistanceTo(node)) < tol)
                    testPoint = new Point3d(yz[1].OriginX, node.Y, node.Z);
                if (Math.Abs(yz[1].DistanceTo(node)) < tol)
                    testPoint = new Point3d(yz[0].OriginX, node.Y, node.Z);
                if (Math.Abs(zx[0].DistanceTo(node)) < tol)
                    testPoint = new Point3d(node.X, zx[1].OriginY, node.Z);
                if (Math.Abs(zx[1].DistanceTo(node)) < tol)
                    testPoint = new Point3d(node.X, zx[0].OriginY, node.Z);
                // Now, check if the mirror node exists
                if (testPoint != Point3d.Unset)
                {
                    int testPointIndex = cell.Nodes.ClosestIndex(testPoint);
                    if (testPoint.DistanceTo(cell.Nodes[testPointIndex]) > tol)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Your unit cell failed the validity check - opposing faces must be identical.");
                        return;
                    }
                }

            }

            DA.SetDataList(0, lines);

        }

        /// <summary>
        /// Here we set the exposure of the component (i.e. the toolbar panel it is in)
        /// </summary>
        public override GH_Exposure Exposure
        {
            get
            {
                return GH_Exposure.tertiary;
            }
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{93998286-27d4-40a3-8f0e-043de932b931}"); }
        }
    }
}