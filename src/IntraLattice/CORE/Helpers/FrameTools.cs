﻿using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Rhino;
using Rhino.DocObjects;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry.Intersect;
using Grasshopper;
using Rhino.Collections;
using IntraLattice.CORE.Data;


// Summary:     This class contains a set of static methods used by the frame components.
// ======================================================================================
// Author(s):   Aidan Kurtz (http://aidankurtz.com)

namespace IntraLattice.CORE.Helpers
{
    public class FrameTools
    {
        /// <summary>
        /// Removes duplicate/invalid/tiny curves and outputs the cleaned list.
        /// </summary>
        public static List<Curve> CleanNetwork(List<Curve> inputStruts)
        {
            var nodes = new Point3dList();
            var nodePairs = new List<IndexPair>();

            return CleanNetwork(inputStruts, out nodes, out nodePairs);
        }
        /// <summary>
        /// Removes duplicate/invalid/tiny curves and outputs the cleaned list, and a list of unique nodes.
        /// </summary>
        public static List<Curve> CleanNetwork(List<Curve> inputStruts, out Point3dList nodes)
        {
            nodes = new Point3dList();
            var nodePairs = new List<IndexPair>();

            return CleanNetwork(inputStruts, out nodes, out nodePairs);
        }
        /// <summary>
        /// Removes duplicate/invalid/tiny curves and outputs the cleaned list, a list of unique nodes and a list of node pairs.
        /// </summary>
        public static List<Curve> CleanNetwork(List<Curve> inputStruts, out Point3dList nodes, out List<IndexPair> nodePairs)
        {
            nodes = new Point3dList();
            nodePairs = new List<IndexPair>();

            var struts = new List<Curve>();

            double tol = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

            // Loop over list of struts
            for (int i = 0; i < inputStruts.Count; i++)
            {
                Curve strut = inputStruts[i];
                strut.Domain = new Interval(0, 1); // unitize domain
                // if strut is invalid, ignore it
                if (strut == null || !strut.IsValid || strut.IsShort(100*tol)) continue;

                Point3d[] pts = new Point3d[2] { strut.PointAtStart, strut.PointAtEnd };
                List<int> nodeIndices = new List<int>();
                // Loop over end points of strut
                // Check if node is already in nodes list, if so, we find its index instead of creating a new node
                for (int j = 0; j < 2; j++)
                {
                    Point3d pt = pts[j];
                    int closestIndex = nodes.ClosestIndex(pt);  // find closest node to current pt

                    // If node already exists (within tolerance), set the index
                    if (nodes.Count != 0 && pt.EpsilonEquals(nodes[closestIndex], tol))
                        nodeIndices.Add(closestIndex);
                    // If node doesn't exist
                    else
                    {
                        // update lookup list
                        nodes.Add(pt);
                        nodeIndices.Add(nodes.Count - 1);
                    }
                }

                // We must ignore duplicate struts
                bool isDuplicate = false;
                IndexPair nodePair = new IndexPair(nodeIndices[0], nodeIndices[1]);

                int dupIndex = nodePairs.IndexOf(nodePair);
                // dupIndex equals -1 if nodePair not found, i.e. if it doesn't equal -1, a match was found
                if (nodePairs.Count != 0 && dupIndex != -1)
                {
                    // Check the curve midpoint to make sure it's a duplicate
                    Curve testStrut = struts[dupIndex];
                    Point3d ptA = strut.PointAt(0.5);
                    Point3d ptB = testStrut.PointAt(0.5);
                    if (ptA.EpsilonEquals(ptB, tol)) isDuplicate = true;
                }

                // So we only create the strut if it doesn't exist yet (check nodePairLookup list)
                if (!isDuplicate)
                {
                    // update the lookup list
                    nodePairs.Add(nodePair);
                    strut.Domain = new Interval(0, 1);
                    struts.Add(strut);
                }
            }

            return struts;
        }

        /// <summary>
        /// Casts a GeometryBase design space to a brep or a mesh.
        /// </summary>
        public static int ValidateSpace(ref GeometryBase designSpace)
        {
            // Types: 0-invalid, 1-brep, 2-mesh, 3-solid surface
            int type = 0;

            if (designSpace.ObjectType == ObjectType.Brep)
                type = 1;
            else if (designSpace.ObjectType == ObjectType.Mesh && ((Mesh)designSpace).IsClosed)
                type = 2;
            else if (designSpace.ObjectType == ObjectType.Surface && ((Surface)designSpace).IsSolid)
                type = 3;

            return type;
        }
        /// <summary>
        /// Determines if a point is inside a geometry. (Brep, Mesh or closed Surface)
        /// </summary>
        public static bool IsPointInside(GeometryBase geometry, Point3d testPoint, int spaceType, double tol)
        {
            bool isInside = false;

            switch (spaceType)
            {
                case 1: // Brep design space
                    isInside = ((Brep)geometry).IsPointInside(testPoint, tol, false);
                    break;
                case 2: // Mesh design space
                    isInside = ((Mesh)geometry).IsPointInside(testPoint, tol, false);
                    break;
                case 3: // Solid surface design space (must be converted to brep)
                    isInside = ((Surface)geometry).ToBrep().IsPointInside(testPoint, tol, false);
                    break;
            }

            return isInside;
        }
        /// <summary>
        /// Computes the distance of a point to a given geometry. (Brep, Mesh or closed Surface)
        /// </summary>
        public static double DistanceTo(GeometryBase geometry, Point3d testPoint, int spaceType)
        {
            double distanceTo = 0;
            Point3d closestPoint;

            switch (spaceType)
            {
                case 1: // Brep design space
                    closestPoint = ((Brep)geometry).ClosestPoint(testPoint);
                    distanceTo = testPoint.DistanceTo(closestPoint);
                    break;
                case 2: // Mesh design space
                    closestPoint = ((Mesh)geometry).ClosestPoint(testPoint);
                    distanceTo = testPoint.DistanceTo(closestPoint);
                    break;
                case 3: // Solid surface design space (must be converted to brep)
                    closestPoint = ((Surface)geometry).ToBrep().ClosestPoint(testPoint);
                    distanceTo = testPoint.DistanceTo(closestPoint);
                    break;
            }

            return distanceTo;
        }

    }
}