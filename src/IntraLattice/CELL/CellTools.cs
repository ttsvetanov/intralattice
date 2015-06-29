﻿using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Collections;
using Grasshopper.Kernel.Types.Transforms;
using Rhino.Geometry.Intersect;

// This is a set of methods used by the cell components
// =====================================================
//      FixIntersections - 
//      ExtractTopology - 
//      NormaliseTopology -
//      FormatTopology - 

// Written by Aidan Kurtz (http://aidankurtz.com)

namespace IntraLattice
{
    public class CellTools
    {

        /// <summary>
        /// This method fixes intersections (all nodes must be defined) and ensures that opposing faces are identical (for continuity)
        /// </summary>
        public static int FixIntersections(ref List<Curve> lines)
        {
            // Set tolerance
            double tol = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

            // Check 2 - Fix any intersections, all nodes must be defined
            List<int> linesToRemove = new List<int>();
            List<Curve> splitLines = new List<Curve>();
            for (int a=0; a<lines.Count; a++)
            {
                for (int b=a+1; b<lines.Count; b++)
                {
                    Line lineA = new Line(lines[a].PointAtStart, lines[a].PointAtEnd);
                    Line lineB = new Line(lines[b].PointAtStart, lines[b].PointAtEnd);
                    double paramA, paramB;
                    bool intersectionFound = Intersection.LineLine(lineA, lineB, out paramA, out paramB, tol, true);

                    // if intersection was found
                    if (intersectionFound)
                    {
                        // if intersection isn't start/end point, we split the line
                        if ((paramA > tol) && (1 - paramA > tol) && !linesToRemove.Contains(a))
                        {
                            splitLines.AddRange(new List<Curve>(lines[a].Split(paramA))); // create new struts
                            linesToRemove.Add(a); // remove old and add new
                        }
                        if ((paramB > tol) && (1-paramB > tol) && !linesToRemove.Contains(b))
                        {
                            splitLines.AddRange(new List<Curve>(lines[b].Split(paramB))); // create new struts
                            linesToRemove.Add(b); // remove old strut
                        }

                    }
                }
            }
            // remove lines that were split, and add the new lines
            linesToRemove.Reverse();
            foreach (int index in linesToRemove) lines.RemoveAt(index);
            lines.AddRange(splitLines);

            return 1;
        }


        /// <summary>
        /// Converts list of lines into a unique list of nodes, and struts as an adjacency list
        /// </summary>
        public static void ExtractTopology(ref List<Curve> lines, ref UnitCell cell)
        {
            // Iterate through list of lines
            foreach (Curve line in lines)
            {
                // Get line, and it's endpoints
                Point3d[] pts = new Point3d[] { line.PointAtStart, line.PointAtEnd };
                List<int> nodeIndex = new List<int>();

                // Loop over end points, being sure to not create the same node twice
                foreach (Point3d endPt in pts)
                {
                    int closestIndex = cell.Nodes.ClosestIndex(endPt);  // find closest node to current pt
                    // If node already exists
                    if (cell.Nodes.Count != 0 && cell.Nodes[closestIndex].DistanceTo(endPt) < RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)
                        nodeIndex.Add(closestIndex);
                    // If it doesn't exist, add it
                    else
                    {
                        cell.Nodes.Add(endPt);
                        nodeIndex.Add(cell.Nodes.Count - 1);
                    }
                }

                // Now we save the strut (as pair of node indices)
                cell.Struts.Add(new IndexPair(nodeIndex[0], nodeIndex[1]));
            }
        }

        /// <summary>
        /// Scales the unit cell down to unit size (1x1x1) and moves it to the origin
        /// </summary>
        public static void NormaliseTopology(ref UnitCell cell)
        {
            // We'll build the bounding box as well
            var xRange = new Interval();
            var yRange = new Interval();
            var zRange = new Interval();

            // Get the bounding box size (check for extreme values)
            foreach (Point3d node in cell.Nodes)
            {
                if (node.X < xRange.T0) xRange.T0 = node.X;
                if (node.X > xRange.T1) xRange.T1 = node.X;
                if (node.Y < yRange.T0) yRange.T0 = node.Y;
                if (node.Y > yRange.T1) yRange.T1 = node.Y;
                if (node.Z < zRange.T0) zRange.T0 = node.Z;
                if (node.Z > zRange.T1) zRange.T1 = node.Z;
            }

            // move bounding box to origin
            Vector3d toOrigin = new Vector3d(-xRange.T0, -yRange.T0, -zRange.T0);
            cell.Nodes.Transform(Transform.Translation(toOrigin));
            // normalise to 1x1x1 bounding box
            cell.Nodes.Transform(Transform.Scale(Plane.WorldXY, 1 / xRange.Length, 1 / yRange.Length, 1 / zRange.Length));

        }

        /// <summary>
        /// Converts to format that ensures no duplicate nodes or struts are created
        /// ASSUMES VALID TOPOLOGY!!!
        /// </summary>
        public static void FormatTopology(ref UnitCell cell)
        {
            // Set tolerance
            double tol = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

            // Set up boundary planes (no struts should exist on these planes, and nodes on these planes belong to other cells)
            Plane xy = Plane.WorldXY; xy.Translate(new Vector3d(0, 0, 1));
            Plane yz = Plane.WorldYZ; yz.Translate(new Vector3d(1, 0, 0));
            Plane zx = Plane.WorldZX; zx.Translate(new Vector3d(0, 1, 0));

            // Define node paths in the tree (as mentioned, nodes on the 3 boundary planes belong to other cells in the tree)
            foreach (Point3d node in cell.Nodes)
            {
                // check top plane first
                if (Math.Abs(xy.DistanceTo(node)) < tol)
                {
                    if (node.DistanceTo(new Point3d(1, 1, 1)) < tol)
                        cell.NodePaths.Add(new int[] { 1, 1, 1, cell.Nodes.ClosestIndex(new Point3d(0, 0, 0)) });
                    else if (Math.Abs(node.X - 1) < tol && Math.Abs(node.Z - 1) < tol)
                        cell.NodePaths.Add(new int[] { 1, 0, 1, cell.Nodes.ClosestIndex(new Point3d(0, node.Y, 0)) });
                    else if (Math.Abs(node.Y - 1) < tol && Math.Abs(node.Z - 1) < tol)
                        cell.NodePaths.Add(new int[] { 0, 1, 1, cell.Nodes.ClosestIndex(new Point3d(node.X, 0, 0)) });
                    else
                        cell.NodePaths.Add(new int[] { 0, 0, 1, cell.Nodes.ClosestIndex(new Point3d(node.X, node.Y, 0)) });
                }
                // check yz boundary plane
                else if (Math.Abs(yz.DistanceTo(node)) < tol)
                {
                    if (Math.Abs(node.X - 1) < tol && Math.Abs(node.Y - 1) < tol)
                        cell.NodePaths.Add(new int[] { 1, 1, 0, cell.Nodes.ClosestIndex(new Point3d(0, 0, node.Z)) });
                    else
                        cell.NodePaths.Add(new int[] { 1, 0, 0, cell.Nodes.ClosestIndex(new Point3d(0, node.Y, node.Z)) });
                }
                // check last boundary plane
                else if (Math.Abs(zx.DistanceTo(node)) < tol)
                    cell.NodePaths.Add(new int[] { 0, 1, 0, cell.Nodes.ClosestIndex(new Point3d(node.X, 0, node.Z)) });
                // if not on those planes, the node belongs to the current cell
                else
                    cell.NodePaths.Add(new int[] { 0, 0, 0, cell.Nodes.IndexOf(node) });
            }

            // now locate any struts that lie on the boundary planes
            List<int> strutsToRemove = new List<int>();
            for (int i = 0; i < cell.Struts.Count; i++)
            {
                Point3d node1 = cell.Nodes[cell.Struts[i].I];
                Point3d node2 = cell.Nodes[cell.Struts[i].J];

                bool toRemove = false;

                if (Math.Abs(xy.DistanceTo(node1)) < tol && Math.Abs(xy.DistanceTo(node2)) < tol) toRemove = true;
                if (Math.Abs(yz.DistanceTo(node1)) < tol && Math.Abs(yz.DistanceTo(node2)) < tol) toRemove = true;
                if (Math.Abs(zx.DistanceTo(node1)) < tol && Math.Abs(zx.DistanceTo(node2)) < tol) toRemove = true;

                if (toRemove) strutsToRemove.Add(i);
            }
            // discard them (reverse the list because when removing objects from list, all indices larger than the one being removed change by -1)
            strutsToRemove.Reverse();
            foreach (int strutToRemove in strutsToRemove) cell.Struts.RemoveAt(strutToRemove);

        }

    }

    // The UnitCell object
    public class UnitCell
    {
        public Point3dList Nodes = new Point3dList();   // List of unique nodes (as Point3d)
        public List<IndexPair> Struts = new List<IndexPair>();  // List of node index pairs
        public List<int[]> NodePaths = new List<int[]>();   // Relative path of node in tree (u+?, v+?, w+?, ?)
    }

}