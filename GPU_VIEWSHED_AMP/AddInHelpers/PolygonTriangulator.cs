using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Myriax.Eonfusion.API.Helpers
{

    /// <summary>
    /// A Seidel triangulator based on the SGI open source OpenGL
    /// implementation (in particular the GLU library) found at
    /// <href=http://oss.sgi.com/projects/ogl-sample>.
    /// </summary>
    public class PolygonTriangulator
    {

        #region Public enumerations.

        public enum WindingRule { Odd, NonZero, Positive, Negative, AbsoluteGreaterThanOne };

        #endregion

        #region Public constructors.

        public PolygonTriangulator()
        {
            m_vertexQueue = new List<Vertex>();
            m_regionList = new OrderedList<Region>(EdgeLessOrEqual);
        }

        #endregion

        #region Tesselation method.

        public void Tesselate(List<List<Vertex>> contours, WindingRule windingRule, out List<Triangle> triangleList, out List<Vertex> vertexList)
        {
            m_windingRule = windingRule;

            SetContours(contours);

            ComputeInterior();

            //  Tessellate all the regions marked "inside".
            MeshTessellateInterior();
            m_mesh.MeshCheckMesh();

            RenderMesh(out triangleList, out vertexList);
        }

        public void Simplify(List<List<Vertex>> contours, WindingRule windingRule, out List<List<Vertex>> simplifiedContours)
        {
            m_windingRule = windingRule;

            SetContours(contours);

            //  ComputeInterior() computes the planar arrangement specified
            //  by the given contours, and further subdivides this arrangement
            //  into regions.  Each region is marked "inside" if it belongs
            //  to the polygon, according to the rule given by windingRule.
            //  Each interior region is guaranteed be monotone.
            ComputeInterior();

            //  We throw away all edges except those which separate the
            //  interior from the exterior.
            MeshSetWindingNumber(1, true);
            m_mesh.MeshCheckMesh();

            RenderBoundary(out simplifiedContours);
        }

        #endregion

        #region Comparison methods.

        /// <summary>
        /// Given three vertices u, v, w such that VertLeq(u, v) && VertLeq(v, w),
        /// evaluates the Y-coord of the edge uw at the X-coord of the vertex v.
        /// Returns v.Y - (uw)(v.X), ie. the signed distance from uw to v.
        /// If uw is vertical (and thus passes thru v), the result is zero.
        ///
        /// The calculation is extremely accurate and stable, even when v
        /// is very close to u or w.  In particular if we set v.Y = 0 and
        /// let r be the negated result (this evaluates (uw)(v.X)), then
        /// r is guaranteed to satisfy MIN(u.Y, w.Y) leq r leq MAX(u.Y, w.Y).
        /// </summary>
        private double EdgeEval(Vertex u, Vertex v, Vertex w)
        {
            Debug.Assert(Vertex.LessOrEqual(u, v) && Vertex.LessOrEqual(v, w));

            double gapL = v.X - u.X;
            double gapR = w.X - v.X;

            if (gapL + gapR > 0.0) {
                if (gapL < gapR) {
                    return (v.Y - u.Y) + (u.Y - w.Y) * (gapL / (gapL + gapR));
                } else {
                    return (v.Y - w.Y) + (w.Y - u.Y) * (gapR / (gapL + gapR));
                }
            }

            //  Vertical line.
            return 0.0;
        }

        /// <summary>
        /// Returns a number whose sign matches EdgeEval(u, v, w) but which
        /// is cheaper to evaluate.  Returns +, 0, or -
        /// as v is above, on, or below the edge uw.
        /// </summary>
        private double EdgeSign(Vertex u, Vertex v, Vertex w)
        {
            Debug.Assert(Vertex.LessOrEqual(u, v) && Vertex.LessOrEqual(v, w));

            double gapL = v.X - u.X;
            double gapR = w.X - v.X;

            if (gapL + gapR > 0.0) {
                return (v.Y - w.Y) * gapL + (v.Y - u.Y) * gapR;
            }

            //  Vertical line.
            return 0.0;
        }

        /// <summary>
        /// Given three vertices u, v, w such that TransLeq(u, v) && TransLeq(v, w),
        /// evaluates the Y-coord of the edge uw at the X-coord of the vertex v.
        /// Returns v.X - (uw)(v.Y), ie. the signed distance from uw to v.
        /// If uw is vertical (and thus passes thru v), the result is zero.
        ///
        /// The calculation is extremely accurate and stable, even when v
        /// is very close to u or w.  In particular if we set v.X = 0 and
        /// let r be the negated result (this evaluates (uw)(v.Y)), then
        /// r is guaranteed to satisfy MIN(u.X, w.X) leq r leq MAX(u.X, w.X).
        /// </summary>
        private double EdgeEvalTrans(Vertex u, Vertex v, Vertex w)
        {
            Debug.Assert(Vertex.LessOrEqualTrans(u, v) && Vertex.LessOrEqualTrans(v, w));

            double gapL = v.Y - u.Y;
            double gapR = w.Y - v.Y;

            if (gapL + gapR > 0.0) {
                if (gapL < gapR) {
                    return (v.X - u.X) + (u.X - w.X) * (gapL / (gapL + gapR));
                } else {
                    return (v.X - w.X) + (w.X - u.X) * (gapR / (gapL + gapR));
                }
            }

            //  Vertical line.
            return 0.0;
        }

        /// <summary>
        /// Returns a number whose sign matches TransEval(u, v, w) but which
        /// is cheaper to evaluate.  Returns +, 0, or -
        /// as v is above, on, or below the edge uw.
        /// </summary>
        private double EdgeSignTrans(Vertex u, Vertex v, Vertex w)
        {
            Debug.Assert(Vertex.LessOrEqualTrans(u, v) && Vertex.LessOrEqualTrans(v, w));

            double gapL = v.Y - u.Y;
            double gapR = w.Y - v.Y;

            if (gapL + gapR > 0.0) {
                return (v.X - w.X) * gapL + (v.X - u.X) * gapR;
            }

            //  Vertical line.
            return 0.0;
        }

        private double VertL1Dist(Vertex u, Vertex v)
        {
            return Math.Abs(u.X - v.X) + Math.Abs(u.Y - v.Y);
        }

        private bool VertCCW(Vertex u, Vertex v, Vertex w)
        {
            //  For almost-degenerate situations, the results are not reliable.
            //  Unless the floating-point arithmetic can be performed without
            //  rounding errors, *any* implementation will give incorrect results
            //  on some degenerate inputs, so the client must have some way to
            //  handle this situation.
            return (u.X * (v.Y - w.Y) + v.X * (w.Y - u.Y) + w.X * (u.Y - v.Y)) >= 0.0;
        }

        private bool EdgeLessOrEqual(Region reg1, Region reg2)
        {
            //  Both edges must be directed from right to left (this is the canonical
            //  direction for the upper edge of each region).
            // 
            //  The strategy is to evaluate a "Y" value for each edge at the
            //  current sweep line position, given by tess->event.  The calculations
            //  are designed to be very stable, but of course they are not perfect.
            // 
            //  Special case: if both edge destinations are at the sweep event,
            //  we sort the edges by slope (they would otherwise compare equally).

            HalfEdge e1 = reg1.UpperEdge;
            HalfEdge e2 = reg2.UpperEdge;

            if (e1.Dest == m_currentEvent) {
                if (e2.Dest == m_currentEvent) {
                    //  Two edges right of the sweep line which meet at the sweep event.
                    //  Sort them by slope.
                    if (Vertex.LessOrEqual(e1.Orig, e2.Orig)) {
                        return EdgeSign(e2.Dest, e1.Orig, e2.Orig) <= 0;
                    } else {
                        return EdgeSign(e1.Dest, e2.Orig, e1.Orig) >= 0;
                    }
                }

                return EdgeSign(e2.Dest, m_currentEvent, e2.Orig) <= 0;
            }

            if (e2.Dest == m_currentEvent) {
                return EdgeSign(e1.Dest, m_currentEvent, e1.Orig) >= 0;
            }

            //  General case - compute signed distance *from* e1, e2 to event.
            double t1 = EdgeEval(e1.Dest, m_currentEvent, e1.Orig);
            double t2 = EdgeEval(e2.Dest, m_currentEvent, e2.Orig);
            return (t1 >= t2);
        }

        #endregion

        #region Sweep methods.

        private void SetContours(List<List<Vertex>> contours)
        {
            m_mesh = new Mesh();

            foreach (List<Vertex> contour in contours) {
                HalfEdge lastEdge = null;
                foreach (Vertex vertex in contour) {
                    HalfEdge e;
                    if (lastEdge == null) {
                        //  Make a self-loop (one vertex, one edge).

                        e = m_mesh.MeshMakeEdge();
                        m_mesh.MeshSplice(e, e.Sym);
                    } else {
                        //  Create a new vertex and edge which immediately follow e
                        //  in the ordering around the left face.
                        m_mesh.MeshSplitEdge(lastEdge);
                        e = lastEdge.LeftNext;
                    }

                    //  The new vertex is now e.Org.
                    e.Orig.VertexIndex = vertex.VertexIndex;
                    e.Orig.X = vertex.X;
                    e.Orig.Y = vertex.Y;

                    //  The winding of an edge says how the winding number changes as we
                    //  cross from the edge's right face to its left face.  We add the
                    //  vertices in such an order that a CCW contour will add +1 to
                    //  the winding number of the region inside the contour.
                    e.Winding = 1;
                    e.Sym.Winding = -1;

                    lastEdge = e;
                }
            }

            //  Each vertex defines an event for our sweep line.  Start by inserting
            //  all the vertices in a priority queue.  Events are processed in
            //  lexicographic order, ie.
            //  
            //  e1 < e2  iff  e1.x < e2.x || (e1.x == e2.x && e1.y < e2.y)

            RemoveDegenerateEdges();
            InitPriorityQ();
            InitEdgeDict();
        }

        private void DeleteRegion(Region reg)
        {
            if (reg.FixUpperEdge) {
                //  It was created with zero winding number, so it better be
                //  deleted with zero winding number (ie. it better not get merged
                //  with a real edge).
                Debug.Assert(reg.UpperEdge.Winding == 0);
            }

            reg.UpperEdge.Region = null;
            m_regionList.Remove(reg.UpperNode);
        }

        /// <summary>
        /// Replace an upper edge which needs fixing (see ConnectRightVertex).
        /// </summary>
        private void FixUpperEdge(Region reg, HalfEdge newEdge)
        {
            Debug.Assert(reg.FixUpperEdge);
            m_mesh.MeshDelete(reg.UpperEdge);
            reg.FixUpperEdge = false;
            reg.UpperEdge = newEdge;
            newEdge.Region = reg;
        }

        /// <summary>
        /// Find the region above the uppermost edge with the same origin.
        /// </summary>
        private Region TopLeftRegion(Region reg)
        {
            Vertex org = reg.UpperEdge.Orig;

            do {
                reg = reg.RegionAbove;
            } while (reg.UpperEdge.Orig == org);

            //  If the edge above was a temporary edge introduced by ConnectRightVertex,
            //  now is the time to fix it.
            if (reg.FixUpperEdge) {
                HalfEdge e = m_mesh.MeshConnect(reg.RegionBelow.UpperEdge.Sym, reg.UpperEdge.LeftNext);
                if (e == null)
                    return null;
                FixUpperEdge(reg, e);
                reg = reg.RegionAbove;
            }

            return reg;
        }

        /// <summary>
        /// Find the region above the uppermost edge with the same destination.
        /// </summary>
        private Region TopRightRegion(Region reg)
        {
            Vertex dst = reg.UpperEdge.Dest;

            do {
                reg = reg.RegionAbove;
            } while (reg.UpperEdge.Dest == dst);

            return reg;
        }

        /// <summary>
        /// Add a new active region to the sweep line, *somewhere* below "regAbove"
        /// (according to where the new edge belongs in the sweep-line dictionary).
        /// The upper edge of the new region will be "eNewUp".
        /// Winding number and "inside" flag are not updated.
        /// </summary>
        private Region AddRegionBelow(Region regAbove, HalfEdge eNewUp)
        {
            Region regNew = new Region();

            regNew.UpperEdge = eNewUp;
            regNew.UpperNode = m_regionList.InsertBefore(regAbove.UpperNode, regNew);
            regNew.FixUpperEdge = false;
            regNew.Sentinel = false;
            regNew.Dirty = false;

            eNewUp.Region = regNew;
            return regNew;
        }

        private bool IsWindingInside(int n)
        {
            switch (m_windingRule) {
                case WindingRule.Odd:
                    return n % 2 == 1;
                case WindingRule.NonZero:
                    return n != 0;
                case WindingRule.Positive:
                    return n > 0;
                case WindingRule.Negative:
                    return n < 0;
                case WindingRule.AbsoluteGreaterThanOne:
                    return Math.Abs(n) > 1;
                default:
                    throw new InvalidOperationException("Invalid value of WindingRule.");
            }
        }

        private void ComputeWinding(Region reg)
        {
            reg.WindingNumber = reg.RegionAbove.WindingNumber + reg.UpperEdge.Winding;
            reg.Inside = IsWindingInside(reg.WindingNumber);
        }

        /// <summary>
        /// Delete a region from the sweep line.  This happens when the upper
        /// and lower chains of a region meet (at a vertex on the sweep line).
        /// The "inside" flag is copied to the appropriate mesh face (we could
        /// not do this before -- since the structure of the mesh is always
        /// changing, this face may not have even existed until now).
        /// </summary>
        private void FinishRegion(Region reg)
        {
            HalfEdge e = reg.UpperEdge;
            Face f = e.LeftFace;

            f.Inside = reg.Inside;
            f.AnEdge = e;   //  Optimization for glMeshTessellateMonoRegion().
            DeleteRegion(reg);
        }

        /// <summary>
        /// We are given a vertex with one or more left-going edges.  All affected
        /// edges should be in the edge dictionary.  Starting at regFirst.eUp,
        /// we walk down deleting all regions where both edges have the same
        /// origin vOrg.  At the same time we copy the "inside" flag from the
        /// active region to the face, since at this point each face will belong
        /// to at most one region (this was not necessarily true until this point
        /// in the sweep).  The walk stops at the region above regLast; if regLast
        /// is null we walk as far as possible.  At the same time we relink the
        /// mesh if necessary, so that the ordering of edges around vOrg is the
        /// same as in the dictionary.
        /// </summary>
        private HalfEdge FinishLeftRegions(Region regFirst, Region regLast)
        {
            Region regPrev = regFirst;
            HalfEdge ePrev = regFirst.UpperEdge;
            while (regPrev != regLast) {
                regPrev.FixUpperEdge = false;   //  Placement was OK.
                Region reg = regPrev.RegionBelow;
                HalfEdge e = reg.UpperEdge;
                if (e.Orig != ePrev.Orig) {
                    if (!reg.FixUpperEdge) {
                        //  Remove the last left-going edge.  Even though there are no further
                        //  edges in the dictionary with this origin, there may be further
                        //  such edges in the mesh (if we are adding left edges to a vertex
                        //  that has already been processed).  Thus it is important to call
                        //  FinishRegion rather than just DeleteRegion.
                        FinishRegion(regPrev);
                        break;
                    }
                    //  If the edge below was a temporary edge introduced by
                    //  ConnectRightVertex, now is the time to fix it.
                    e = m_mesh.MeshConnect(ePrev.LeftPrev, e.Sym);
                    FixUpperEdge(reg, e);
                }

                //  Relink edges so that ePrev.ONext == e.
                if (ePrev.OrigNext != e) {
                    m_mesh.MeshSplice(e.OrigPrev, e);
                    m_mesh.MeshSplice(ePrev, e);
                }
                FinishRegion(regPrev);  //  May change reg.eUp.
                ePrev = reg.UpperEdge;
                regPrev = reg;
            }

            return ePrev;
        }

        /// <summary>
        /// Purpose: insert right-going edges into the edge dictionary, and update
        /// winding numbers and mesh connectivity appropriately.  All right-going
        /// edges share a common origin vOrg.  Edges are inserted CCW starting at
        /// eFirst; the last edge inserted is eLast.OPrev.  If vOrg has any
        /// left-going edges already processed, then eTopLeft must be the edge
        /// such that an imaginary upward vertical segment from vOrg would be
        /// contained between eTopLeft.OPrev and eTopLeft; otherwise eTopLeft
        /// should be null.
        /// </summary>
        private void AddRightEdges(Region regUp, HalfEdge eFirst, HalfEdge eLast, HalfEdge eTopLeft, bool cleanUp)
        {
            //  Insert the new right-going edges in the dictionary.
            HalfEdge e = eFirst;
            do {
                Debug.Assert(Vertex.LessOrEqual(e.Orig, e.Dest));
                AddRegionBelow(regUp, e.Sym);
                e = e.OrigNext;
            } while (e != eLast);

            //  Walk *all* right-going edges from e.Org, in the dictionary order,
            //  updating the winding numbers of each region, and re-linking the mesh
            //  edges to match the dictionary ordering (if necessary).
            if (eTopLeft == null) {
                eTopLeft = regUp.RegionBelow.UpperEdge.RightPrev;
            }

            Region reg;
            Region regPrev = regUp;
            HalfEdge ePrev = eTopLeft;
            bool firstTime = true;
            while (true) {
                reg = regPrev.RegionBelow;
                e = reg.UpperEdge.Sym;
                if (e.Orig != ePrev.Orig)
                    break;

                if (e.OrigNext != ePrev) {
                    //  Unlink e from its current position, and relink below ePrev.
                    m_mesh.MeshSplice(e.OrigPrev, e);
                    m_mesh.MeshSplice(ePrev.OrigPrev, e);
                }
                //  Compute the winding number and "inside" flag for the new regions.
                reg.WindingNumber = regPrev.WindingNumber - e.Winding;
                reg.Inside = IsWindingInside(reg.WindingNumber);

                //  Check for two outgoing edges with same slope -- process these
                //  before any intersection tests (see example in ComputeInterior).
                regPrev.Dirty = true;
                if (!firstTime && CheckForRightSplice(regPrev)) {
                    AddWinding(e, ePrev);
                    DeleteRegion(regPrev);
                    m_mesh.MeshDelete(ePrev);
                }
                firstTime = false;
                regPrev = reg;
                ePrev = e;
            }

            regPrev.Dirty = true;
            Debug.Assert(regPrev.WindingNumber - e.Winding == reg.WindingNumber);

            if (cleanUp) {
                //  Check for intersections between newly adjacent edges.
                WalkDirtyRegions(regPrev);
            }
        }

        /// <summary>
        /// Check the upper and lower edge of "regUp", to make sure that the
        /// eUp.Org is above eLo, or eLo.Org is below eUp (depending on which
        /// origin is leftmost).
        ///
        /// The main purpose is to splice right-going edges with the same
        /// dest vertex and nearly identical slopes (ie. we can't distinguish
        /// the slopes numerically).  However the splicing can also help us
        /// to recover from numerical errors.  For example, suppose at one
        /// point we checked eUp and eLo, and decided that eUp.Org is barely
        /// above eLo.  Then later, we split eLo into two edges (eg. from
        /// a splice operation like this one).  This can change the result of
        /// our test so that now eUp.Org is incident to eLo, or barely below it.
        /// We must correct this condition to maintain the dictionary invariants.
        ///
        /// One possibility is to check these edges for intersection again
        /// (ie. CheckForIntersect).  This is what we do if possible.  However
        /// CheckForIntersect requires that tess->event lies between eUp and eLo,
        /// so that it has something to fall back on when the intersection
        /// calculation gives us an unusable answer.  So, for those cases where
        /// we can't check for intersection, this routine fixes the problem
        /// by just splicing the offending vertex into the other edge.
        /// This is a guaranteed solution, no matter how degenerate things get.
        /// Basically this is a combinatorial solution to a numerical problem.
        /// </summary>
        private bool CheckForRightSplice(Region regUp)
        {
            Region regLo = regUp.RegionBelow;
            HalfEdge eUp = regUp.UpperEdge;
            HalfEdge eLo = regLo.UpperEdge;

            if (Vertex.LessOrEqual(eUp.Orig, eLo.Orig)) {
                if (EdgeSign(eLo.Dest, eUp.Orig, eLo.Orig) > 0)
                    return false;

                //  eUp.Org appears to be below eLo.
                if (!Vertex.Equal(eUp.Orig, eLo.Orig)) {
                    //  Splice eUp.Org into eLo.
                    m_mesh.MeshSplitEdge(eLo.Sym);
                    m_mesh.MeshSplice(eUp, eLo.OrigPrev);
                    regUp.Dirty = regLo.Dirty = true;

                } else if (eUp.Orig != eLo.Orig) {
                    //  Merge the two vertices, discarding eUp.Org.
                    RemoveVertexEvent(eUp.Orig);
                    SpliceMergeVertices(eLo.OrigPrev, eUp);
                }
            } else {
                if (EdgeSign(eUp.Dest, eLo.Orig, eUp.Orig) < 0)
                    return false;

                //  eLo.Org appears to be above eUp, so splice eLo.Org into eUp.
                regUp.RegionAbove.Dirty = regUp.Dirty = true;
                m_mesh.MeshSplitEdge(eUp.Sym);
                m_mesh.MeshSplice(eLo.OrigPrev, eUp);
            }

            return true;
        }

        /// <summary>
        /// Check the upper and lower edge of "regUp", to make sure that the
        /// eUp.Dst is above eLo, or eLo.Dst is below eUp (depending on which
        /// destination is rightmost).
        ///
        /// Theoretically, this should always be true.  However, splitting an edge
        /// into two pieces can change the results of previous tests.  For example,
        /// suppose at one point we checked eUp and eLo, and decided that eUp.Dst
        /// is barely above eLo.  Then later, we split eLo into two edges (eg. from
        /// a splice operation like this one).  This can change the result of
        /// the test so that now eUp.Dst is incident to eLo, or barely below it.
        /// We must correct this condition to maintain the dictionary invariants
        /// (otherwise new edges might get inserted in the wrong place in the
        /// dictionary, and bad stuff will happen).
        ///
        /// We fix the problem by just splicing the offending vertex into the
        /// other edge.
        /// </summary>
        private bool CheckForLeftSplice(Region regUp)
        {
            Region regLo = regUp.RegionBelow;
            HalfEdge eUp = regUp.UpperEdge;
            HalfEdge eLo = regLo.UpperEdge;

            Debug.Assert(!Vertex.Equal(eUp.Dest, eLo.Dest));

            if (Vertex.LessOrEqual(eUp.Dest, eLo.Dest)) {
                if (EdgeSign(eUp.Dest, eLo.Dest, eUp.Orig) < 0)
                    return false;

                //  eLo.Dst is above eUp, so splice eLo.Dst into eUp.
                regUp.RegionAbove.Dirty = regUp.Dirty = true;
                HalfEdge e = m_mesh.MeshSplitEdge(eUp);
                m_mesh.MeshSplice(eLo.Sym, e);
                e.LeftFace.Inside = regUp.Inside;
            } else {
                if (EdgeSign(eLo.Dest, eUp.Dest, eLo.Orig) > 0)
                    return false;

                //  eUp.Dst is below eLo, so splice eUp.Dst into eLo.
                regUp.Dirty = regLo.Dirty = true;
                HalfEdge e = m_mesh.MeshSplitEdge(eLo);
                m_mesh.MeshSplice(eUp.LeftNext, eLo.Sym);
                e.RightFace.Inside = regUp.Inside;
            }

            return true;
        }

        /// <summary>
        /// Check the upper and lower edges of the given region to see if
        /// they intersect.  If so, create the intersection and add it
        /// to the data structures.
        ///
        /// Returns true if adding the new intersection resulted in a recursive
        /// call to AddRightEdges(); in this case all "dirty" regions have been
        /// checked for intersections, and possibly regUp has been deleted.
        /// </summary>
        private bool CheckForIntersect(Region regUp)
        {
            Region regLo = regUp.RegionBelow;
            HalfEdge eUp = regUp.UpperEdge;
            HalfEdge eLo = regLo.UpperEdge;
            Vertex orgUp = eUp.Orig;
            Vertex orgLo = eLo.Orig;
            Vertex dstUp = eUp.Dest;
            Vertex dstLo = eLo.Dest;

            Debug.Assert(!Vertex.Equal(dstLo, dstUp));
            Debug.Assert(EdgeSign(dstUp, m_currentEvent, orgUp) <= 0);
            Debug.Assert(EdgeSign(dstLo, m_currentEvent, orgLo) >= 0);
            Debug.Assert(orgUp != m_currentEvent && orgLo != m_currentEvent);
            Debug.Assert(!regUp.FixUpperEdge && !regLo.FixUpperEdge);

            if (orgUp == orgLo)
                return false; //  Right endpoints are the same.

            double tMinUp = Math.Min(orgUp.Y, dstUp.Y);
            double tMaxLo = Math.Max(orgLo.Y, dstLo.Y);
            if (tMinUp > tMaxLo)
                return false; //  Y ranges do not overlap.

            if (Vertex.LessOrEqual(orgUp, orgLo)) {
                if (EdgeSign(dstLo, orgUp, orgLo) > 0)
                    return false;
            } else {
                if (EdgeSign(dstUp, orgLo, orgUp) < 0)
                    return false;
            }

            //  At this point the edges intersect, at least marginally.
            Vertex isect;
            EdgeIntersect(dstUp, orgUp, dstLo, orgLo, out isect);

            //  The following properties are guaranteed:
            Debug.Assert(Math.Min(orgUp.Y, dstUp.Y) <= isect.Y);
            Debug.Assert(isect.Y <= Math.Max(orgLo.Y, dstLo.Y));
            Debug.Assert(Math.Min(dstLo.X, dstUp.X) <= isect.X);
            Debug.Assert(isect.X <= Math.Max(orgLo.X, orgUp.X));

            if (Vertex.LessOrEqual(isect, m_currentEvent)) {
                //  The intersection point lies slightly to the left of the sweep line,
                //  so move it until it's slightly to the right of the sweep line.
                //  (If we had perfect numerical precision, this would never happen
                //  in the first place).  The easiest and safest thing to do is
                //  replace the intersection by CurrentEvent.
                isect.X = m_currentEvent.X;
                isect.Y = m_currentEvent.Y;
            }

            //  Similarly, if the computed intersection lies to the right of the
            //  rightmost origin (which should rarely happen), it can cause
            //  unbelievable inefficiency on sufficiently degenerate inputs.
            //  (If you have the test program, try running test54.d with the
            //  "X zoom" option turned on).
            Vertex orgMin = Vertex.LessOrEqual(orgUp, orgLo) ? orgUp : orgLo;
            if (Vertex.LessOrEqual(orgMin, isect)) {
                isect.X = orgMin.X;
                isect.Y = orgMin.Y;
            }

            if (Vertex.Equal(isect, orgUp) || Vertex.Equal(isect, orgLo)) {
                //  Easy case -- intersection at one of the right endpoints.
                CheckForRightSplice(regUp);
                return false;
            }

            if ((!Vertex.Equal(dstUp, m_currentEvent) && EdgeSign(dstUp, m_currentEvent, isect) >= 0)
                || (!Vertex.Equal(dstLo, m_currentEvent) && EdgeSign(dstLo, m_currentEvent, isect) <= 0)) {
                //  Very unusual -- the new upper or lower edge would pass on the
                //  wrong side of the sweep event, or through it.  This can happen
                //  due to very small numerical errors in the intersection calculation.
                if (dstLo == m_currentEvent) {
                    //  Splice dstLo into eUp, and process the new region(s).
                    m_mesh.MeshSplitEdge(eUp.Sym);
                    m_mesh.MeshSplice(eLo.Sym, eUp);
                    regUp = TopLeftRegion(regUp);
                    eUp = regUp.RegionBelow.UpperEdge;
                    FinishLeftRegions(regUp.RegionBelow, regLo);
                    AddRightEdges(regUp, eUp.OrigPrev, eUp, eUp, true);
                    return true;
                }
                if (dstUp == m_currentEvent) {
                    //  Splice dstUp into eLo, and process the new region(s).
                    m_mesh.MeshSplitEdge(eLo.Sym);
                    m_mesh.MeshSplice(eUp.LeftNext, eLo.OrigPrev);
                    regLo = regUp;
                    regUp = TopRightRegion(regUp);
                    HalfEdge e = regUp.RegionBelow.UpperEdge.RightPrev;
                    regLo.UpperEdge = eLo.OrigPrev;
                    eLo = FinishLeftRegions(regLo, null);
                    AddRightEdges(regUp, eLo.OrigNext, eUp.RightPrev, e, true);
                    return true;
                }

                //  Special case: called from ConnectRightVertex.  If either
                //  edge passes on the wrong side of tess->event, split it
                //  (and wait for ConnectRightVertex to splice it appropriately).
                if (EdgeSign(dstUp, m_currentEvent, isect) >= 0) {
                    regUp.RegionAbove.Dirty = regUp.Dirty = true;
                    m_mesh.MeshSplitEdge(eUp.Sym);
                    eUp.Orig.X = m_currentEvent.X;
                    eUp.Orig.Y = m_currentEvent.Y;
                }
                if (EdgeSign(dstLo, m_currentEvent, isect) <= 0) {
                    regUp.Dirty = regLo.Dirty = true;
                    m_mesh.MeshSplitEdge(eLo.Sym);
                    eLo.Orig.X = m_currentEvent.X;
                    eLo.Orig.Y = m_currentEvent.Y;
                }

                //  Leave the rest for ConnectRightVertex.
                return false;
            }

            //  General case -- split both edges, splice into new vertex.
            //  When we do the splice operation, the order of the arguments is
            //  arbitrary as far as correctness goes.  However, when the operation
            //  creates a new face, the work done is proportional to the size of
            //  the new face.  We expect the faces in the processed part of
            //  the mesh (ie. eUp->Lface) to be smaller than the faces in the
            //  unprocessed original contours (which will be eLo->Oprev->Lface).
            m_mesh.MeshSplitEdge(eUp.Sym);
            m_mesh.MeshSplitEdge(eLo.Sym);
            m_mesh.MeshSplice(eLo.OrigPrev, eUp);
            eUp.Orig.X = isect.X;
            eUp.Orig.Y = isect.Y;
            AddVertexEvent(eUp.Orig);
            GetIntersectData(eUp.Orig, orgUp, dstUp, orgLo, dstLo);
            regUp.RegionAbove.Dirty = regUp.Dirty = regLo.Dirty = true;
            return false;
        }

        /// <summary>
        /// When the upper or lower edge of any region changes, the region is
        /// marked "dirty".  This routine walks through all the dirty regions
        /// and makes sure that the dictionary invariants are satisfied
        /// (see the comments at the beginning of this file).  Of course
        /// new dirty regions can be created as we make changes to restore
        /// the invariants.
        /// </summary>
        private void WalkDirtyRegions(Region regUp)
        {
            Region regLo = regUp.RegionBelow;

            while (true) {
                //  Find the lowest dirty region (we walk from the bottom up).
                while (regLo.Dirty) {
                    regUp = regLo;
                    regLo = regLo.RegionBelow;
                }
                if (!regUp.Dirty) {
                    regLo = regUp;
                    regUp = regUp.RegionAbove;
                    if (regUp == null || !regUp.Dirty) {
                        //  We've walked all the dirty regions.
                        return;
                    }
                }
                regUp.Dirty = false;
                HalfEdge eUp = regUp.UpperEdge;
                HalfEdge eLo = regLo.UpperEdge;

                if (eUp.Dest != eLo.Dest) {
                    //  Check that the edge ordering is obeyed at the Dst vertices.
                    if (CheckForLeftSplice(regUp)) {

                        //  If the upper or lower edge was marked fixUpperEdge, then
                        //  we no longer need it (since these edges are needed only for
                        //  vertices which otherwise have no right-going edges).
                        if (regLo.FixUpperEdge) {
                            DeleteRegion(regLo);
                            m_mesh.MeshDelete(eLo);
                            regLo = regUp.RegionBelow;
                            eLo = regLo.UpperEdge;
                        } else if (regUp.FixUpperEdge) {
                            DeleteRegion(regUp);
                            m_mesh.MeshDelete(eUp);
                            regUp = regLo.RegionAbove;
                            eUp = regUp.UpperEdge;
                        }
                    }
                }
                if (eUp.Orig != eLo.Orig) {
                    if (eUp.Dest != eLo.Dest && !regUp.FixUpperEdge && !regLo.FixUpperEdge && (eUp.Dest == m_currentEvent || eLo.Dest == m_currentEvent)) {
                        //  When all else fails in CheckForIntersect(), it uses CurrentEvent
                        //  as the intersection location.  To make this possible, it requires
                        //  that CurrentEvent lie between the upper and lower edges, and also
                        //  that neither of these is marked FixUpperEdge (since in the worst
                        //  case it might splice one of these edges into CurrentEvent, and
                        //  violate the invariant that fixable edges are the only right-going
                        //  edge from their associated vertex).
                        if (CheckForIntersect(regUp)) {
                            //  WalkDirtyRegions() was called recursively, we're done.
                            return;
                        }
                    } else {
                        //  Even though we can't use CheckForIntersect(), the Org vertices
                        //  may violate the dictionary edge ordering.  Check and correct this.
                        CheckForRightSplice(regUp);
                    }
                }
                if (eUp.Orig == eLo.Orig && eUp.Dest == eLo.Dest) {
                    //  A degenerate loop consisting of only two edges -- delete it.
                    AddWinding(eLo, eUp);
                    DeleteRegion(regUp);
                    m_mesh.MeshDelete(eUp);
                    regUp = regLo.RegionAbove;
                }
            }
        }

        /// <summary>
        /// Purpose: connect a "right" vertex vEvent (one where all edges go left)
        /// to the unprocessed portion of the mesh.  Since there are no right-going
        /// edges, two regions (one above vEvent and one below) are being merged
        /// into one.  "regUp" is the upper of these two regions.
        ///
        /// There are two reasons for doing this (adding a right-going edge):
        ///  - if the two regions being merged are "inside", we must add an edge
        ///    to keep them separated (the combined region would not be monotone).
        ///  - in any case, we must leave some record of vEvent in the dictionary,
        ///    so that we can merge vEvent with features that we have not seen yet.
        ///    For example, maybe there is a vertical edge which passes just to
        ///    the right of vEvent; we would like to splice vEvent into this edge.
        ///
        /// However, we don't want to connect vEvent to just any vertex.  We don't
        /// want the new edge to cross any other edges; otherwise we will create
        /// intersection vertices even when the input data had no self-intersections.
        /// (This is a bad thing; if the user's input data has no intersections,
        /// we don't want to generate any false intersections ourselves.)
        ///
        /// Our eventual goal is to connect vEvent to the leftmost unprocessed
        /// vertex of the combined region (the union of regUp and regLo).
        /// But because of unseen vertices with all right-going edges, and also
        /// new vertices which may be created by edge intersections, we don't
        /// know where that leftmost unprocessed vertex is.  In the meantime, we
        /// connect vEvent to the closest vertex of either chain, and mark the region
        /// as "fixUpperEdge".  This flag says to delete and reconnect this edge
        /// to the next processed vertex on the boundary of the combined region.
        /// Quite possibly the vertex we connected to will turn out to be the
        /// closest one, in which case we won't need to make any changes.
        /// </summary>
        private void ConnectRightVertex(Region regUp, HalfEdge eBottomLeft)
        {
            HalfEdge eTopLeft = eBottomLeft.OrigNext;
            Region regLo = regUp.RegionBelow;
            HalfEdge eUp = regUp.UpperEdge;
            HalfEdge eLo = regLo.UpperEdge;

            if (eUp.Dest != eLo.Dest) {
                CheckForIntersect(regUp);
            }

            //  Possible new degeneracies: upper or lower edge of regUp may pass
            //  through vEvent, or may coincide with new intersection vertex
            bool degenerate = false;
            if (Vertex.Equal(eUp.Orig, m_currentEvent)) {
                m_mesh.MeshSplice(eTopLeft.OrigPrev, eUp);
                regUp = TopLeftRegion(regUp);
                eTopLeft = regUp.RegionBelow.UpperEdge;
                FinishLeftRegions(regUp.RegionBelow, regLo);
                degenerate = true;
            }
            if (Vertex.Equal(eLo.Orig, m_currentEvent)) {
                m_mesh.MeshSplice(eBottomLeft, eLo.OrigPrev);
                eBottomLeft = FinishLeftRegions(regLo, null);
                degenerate = true;
            }
            if (degenerate) {
                AddRightEdges(regUp, eBottomLeft.OrigNext, eTopLeft, eTopLeft, true);
                return;
            }

            //  Non-degenerate situation -- need to add a temporary, fixable edge.
            //  Connect to the closer of eLo.Org, eUp.Org.
            HalfEdge eNew;
            if (Vertex.LessOrEqual(eLo.Orig, eUp.Orig)) {
                eNew = eLo.OrigPrev;
            } else {
                eNew = eUp;
            }
            eNew = m_mesh.MeshConnect(eBottomLeft.LeftPrev, eNew);

            //  Prevent cleanup, otherwise eNew might disappear before we've even
            //  had a chance to mark it as a temporary edge.
            AddRightEdges(regUp, eNew, eNew.OrigNext, eNew.OrigNext, false);
            eNew.Sym.Region.FixUpperEdge = true;
            WalkDirtyRegions(regUp);
        }

        private void ConnectLeftDegenerate(Region regUp, Vertex vEvent)
        {
            //  The event vertex lies exacty on an already-processed edge or vertex.
            //  Adding the new vertex involves splicing it into the already-processed
            //  part of the mesh.

            HalfEdge e = regUp.UpperEdge;
            Debug.Assert(Vertex.Equal(e.Orig, vEvent) == false);

            //  Splice vEvent into edge e which passes through it.
            m_mesh.MeshSplitEdge(e.Sym);
            if (regUp.FixUpperEdge) {
                //  This edge was fixable -- delete unused portion of original edge.
                m_mesh.MeshDelete(e.OrigNext);
                regUp.FixUpperEdge = false;
            }
            m_mesh.MeshSplice(vEvent.AnEdge, e);
            SweepEvent(vEvent); //  Recurse.
        }

        /// <summary>
        /// Purpose: connect a "left" vertex (one where both edges go right)
        /// to the processed portion of the mesh.  Let R be the active region
        /// containing vEvent, and let U and L be the upper and lower edge
        /// chains of R.  There are two possibilities:
        ///
        /// - the normal case: split R into two regions, by connecting vEvent to
        ///   the rightmost vertex of U or L lying to the left of the sweep line
        ///
        /// - the degenerate case: if vEvent is close enough to U or L, we
        ///   merge vEvent into that edge chain.  The subcases are:
        ///	- merging with the rightmost vertex of U or L
        ///	- merging with the active edge of U or L
        ///	- merging with an already-processed portion of U or L
        /// </summary>
        private void ConnectLeftVertex(Vertex vEvent)
        {
            //  Debug.Assert( vEvent.AnEdge.ONext.ONext == vEvent.AnEdge );

            //  Get a pointer to the active region containing vEvent.
            Region tmp = new Region();
            tmp.UpperEdge = vEvent.AnEdge.Sym;
            Region regUp = m_regionList.Search(tmp).Value;
            Region regLo = regUp.RegionBelow;
            HalfEdge eUp = regUp.UpperEdge;
            HalfEdge eLo = regLo.UpperEdge;

            //  Try merging with U or L first.
            if (EdgeSign(eUp.Dest, vEvent, eUp.Orig) == 0) {
                ConnectLeftDegenerate(regUp, vEvent);
                return;
            }

            //  Connect vEvent to rightmost processed vertex of either chain.
            //  e.Dst is the vertex that we will connect to vEvent.
            Region reg = Vertex.LessOrEqual(eLo.Dest, eUp.Dest) ? regUp : regLo;

            if (regUp.Inside || reg.FixUpperEdge) {
                HalfEdge eNew;
                if (reg == regUp) {
                    eNew = m_mesh.MeshConnect(vEvent.AnEdge.Sym, eUp.LeftNext);
                } else {
                    HalfEdge tempHalfEdge = m_mesh.MeshConnect(eLo.DestNext, vEvent.AnEdge);

                    eNew = tempHalfEdge.Sym;
                }
                if (reg.FixUpperEdge) {
                    FixUpperEdge(reg, eNew);
                } else {
                    ComputeWinding(AddRegionBelow(regUp, eNew));
                }
                SweepEvent(vEvent);
            } else {
                //  The new vertex is in a region which does not belong to the polygon.
                //  We don't need to connect this vertex to the rest of the mesh.
                AddRightEdges(regUp, vEvent.AnEdge, vEvent.AnEdge, null, true);
            }
        }

        /// <summary>
        /// Does everything necessary when the sweep line crosses a vertex.
        /// Updates the mesh and the edge dictionary.
        /// </summary>
        private void SweepEvent(Vertex vEvent)
        {
            m_currentEvent = vEvent;    //  For access in EdgeLessOrEqual().

            //  Check if this vertex is the right endpoint of an edge that is
            //  already in the dictionary.  In this case we don't need to waste
            //  time searching for the location to insert new edges.
            HalfEdge e = vEvent.AnEdge;
            while (e.Region == null) {
                e = e.OrigNext;
                if (e == vEvent.AnEdge) {
                    //  All edges go right -- not incident to any processed edges.
                    ConnectLeftVertex(vEvent);
                    return;
                }
            }

            //  Processing consists of two phases: first we "finish" all the
            //  active regions where both the upper and lower edges terminate
            //  at vEvent (ie. vEvent is closing off these regions).
            //  We mark these faces "inside" or "outside" the polygon according
            //  to their winding number, and delete the edges from the dictionary.
            //  This takes care of all the left-going edges from vEvent.
            Region regUp = TopLeftRegion(e.Region);
            Region reg = regUp.RegionBelow;
            HalfEdge eTopLeft = reg.UpperEdge;
            HalfEdge eBottomLeft = FinishLeftRegions(reg, null);

            //  Next we process all the right-going edges from vEvent.  This
            //  involves adding the edges to the dictionary, and creating the
            //  associated "active regions" which record information about the
            //  regions between adjacent dictionary edges.
            if (eBottomLeft.OrigNext == eTopLeft) {
                //  No right-going edges -- add a temporary "fixable" edge.
                ConnectRightVertex(regUp, eBottomLeft);
            } else {
                AddRightEdges(regUp, eBottomLeft.OrigNext, eTopLeft, eTopLeft, true);
            }
        }

        private const double SENTINEL_COORD = 4.0 * 1e+100;

        /// <summary>
        /// We add two sentinel edges above and below all other edges,
        /// to avoid special cases at the top and bottom.
        /// </summary>
        private void AddSentinel(double y)
        {
            Region reg = new Region();

            HalfEdge e = m_mesh.MeshMakeEdge();

            e.Orig.X = +SENTINEL_COORD;
            e.Orig.Y = y;
            e.Dest.X = -SENTINEL_COORD;
            e.Dest.Y = y;
            m_currentEvent = e.Dest;

            reg.UpperEdge = e;
            reg.WindingNumber = 0;
            reg.Inside = false;
            reg.FixUpperEdge = false;
            reg.Sentinel = true;
            reg.Dirty = false;
            reg.UpperNode = m_regionList.Insert(reg);
        }

        /// <summary>
        /// We maintain an ordering of edge intersections with the sweep line.
        /// This order is maintained in a dynamic dictionary.
        /// </summary>
        private void InitEdgeDict()
        {
            AddSentinel(-SENTINEL_COORD);
            AddSentinel(+SENTINEL_COORD);
        }

        private void RemoveVertexEvent(Vertex v)
        {
            Debug.Assert(Vertex.Compare(m_currentEvent, v) < 0);

            bool found = false;
            for (int vertexNum = m_currentVertexNum + 1; vertexNum < m_vertexQueue.Count; ++vertexNum) {
                if (m_vertexQueue[vertexNum] == v) {
                    m_vertexQueue.RemoveAt(vertexNum);
                    found = true;
                    break;
                }
            }

            Debug.Assert(found);
        }

        private void AddVertexEvent(Vertex v)
        {
            Debug.Assert(Vertex.Compare(m_currentEvent, v) < 0);

            int insertVertexNum;
            for (insertVertexNum = m_currentVertexNum + 1; insertVertexNum < m_vertexQueue.Count; ++insertVertexNum) {
                if (Vertex.LessOrEqual(v, m_vertexQueue[insertVertexNum])) {
                    break;
                }
            }

            m_vertexQueue.Insert(insertVertexNum, v);
        }

        private void DoneEdgeDict()
        {
            int fixedEdges = 0;

            while (true) {
                Region reg = m_regionList.ListMin.Value;
                if (reg == null)
                    break;

                //  At the end of all processing, the dictionary should contain
                //  only the two sentinel edges, plus at most one "fixable" edge
                //  created by ConnectRightVertex().
                if (!reg.Sentinel) {
                    Debug.Assert(reg.FixUpperEdge);
                    Debug.Assert(++fixedEdges == 1);
                }
                Debug.Assert(reg.WindingNumber == 0);
                DeleteRegion(reg);
            }
        }

        /// <summary>
        /// Remove zero-length edges, and contours with fewer than 3 vertices.
        /// </summary>
        private void RemoveDegenerateEdges()
        {
            HalfEdge eNext;
            for (HalfEdge e = m_mesh.EdgeHead.Next; e != m_mesh.EdgeHead; e = eNext) {
                eNext = e.Next;
                HalfEdge eLnext = e.LeftNext;

                if (Vertex.Equal(e.Orig, e.Dest) && e.LeftNext.LeftNext != e) {
                    //  Zero-length edge, contour has at least 3 edges.

                    SpliceMergeVertices(eLnext, e); //  Deletes e.Org.
                    m_mesh.MeshDelete(e); //  e is a self-loop.
                    e = eLnext;
                    eLnext = e.LeftNext;
                }
                if (eLnext.LeftNext == e) {
                    //  Degenerate contour (one or two edges).

                    if (eLnext != e) {
                        if (eLnext == eNext || eLnext == eNext.Sym) { eNext = eNext.Next; }
                        m_mesh.MeshDelete(eLnext);
                    }
                    if (e == eNext || e == eNext.Sym) { eNext = eNext.Next; }
                    m_mesh.MeshDelete(e);
                }
            }
        }

        /// <summary>
        /// Insert all vertices into the priority queue which determines the
        /// order in which vertices cross the sweep line.
        /// </summary>
        private void InitPriorityQ()
        {
            m_vertexQueue.Clear();

            for (Vertex v = m_mesh.VertexHead.Next; v != m_mesh.VertexHead; v = v.Next) {
                m_vertexQueue.Add(v);
            }

            m_vertexQueue.Sort(Vertex.Compare);
        }

        /// <summary>
        /// Delete any degenerate faces with only two edges.  WalkDirtyRegions()
        /// will catch almost all of these, but it won't catch degenerate faces
        /// produced by splice operations on already-processed edges.
        /// The two places this can happen are in FinishLeftRegions(), when
        /// we splice in a "temporary" edge produced by ConnectRightVertex(),
        /// and in CheckForLeftSplice(), where we splice already-processed
        /// edges to ensure that our dictionary invariants are not violated
        /// by numerical errors.
        ///
        /// In both these cases it is *very* dangerous to delete the offending
        /// edge at the time, since one of the routines further up the stack
        /// will sometimes be keeping a pointer to that edge.
        /// </summary>
        private void RemoveDegenerateFaces()
        {
            Face fNext;
            for (Face f = m_mesh.FaceHead.Next; f != m_mesh.FaceHead; f = fNext) {
                fNext = f.Next;
                HalfEdge e = f.AnEdge;
                Debug.Assert(e.LeftNext != e);

                if (e.LeftNext.LeftNext == e) {
                    //  A face with only two edges.
                    AddWinding(e.OrigNext, e);
                    m_mesh.MeshDelete(e);
                }
            }
        }

        /// <summary>
        /// ComputeInterior() computes the planar arrangement specified
        /// by the given contours, and further subdivides this arrangement
        /// into regions.  Each region is marked "inside" if it belongs
        /// to the polygon, according to the rule given by WindingRule.
        /// Each interior region is guaranteed to be monotone.
        /// </summary>
        private void ComputeInterior()
        {
            m_currentVertexNum = 0;
            while (m_currentVertexNum < m_vertexQueue.Count) {
                Vertex v = m_vertexQueue[m_currentVertexNum];
                int nextVertexNum = m_currentVertexNum + 1;

                while (true) {
                    Vertex vNext = (nextVertexNum < m_vertexQueue.Count) ? m_vertexQueue[nextVertexNum] : null;
                    if (vNext == null || !Vertex.Equal(vNext, v))
                        break;

                    //  Merge together all vertices at exactly the same location.
                    //  This is more efficient than processing them one at a time,
                    //  simplifies the code (see ConnectLeftDegenerate), and is also
                    //  important for correct handling of certain degenerate cases.
                    //  For example, suppose there are two identical edges A and B
                    //  that belong to different contours (so without this code they would
                    //  be processed by separate sweep events).  Suppose another edge C
                    //  crosses A and B from above.  When A is processed, we split it
                    //  at its intersection point with C.  However this also splits C,
                    //  so when we insert B we may compute a slightly different
                    //  intersection point.  This might leave two edges with a small
                    //  gap between them.  This kind of error is especially obvious
                    //  when using boundary extraction.
                    ++nextVertexNum;
                    SpliceMergeVertices(v.AnEdge, vNext.AnEdge);
                }
                SweepEvent(v);

                m_currentVertexNum = nextVertexNum;
            }

            //  Set CurrentEvent for debugging purposes.
            m_currentEvent = m_regionList.ListMin.Value.UpperEdge.Orig;

            DoneEdgeDict();

            RemoveDegenerateFaces();
            m_mesh.MeshCheckMesh();
        }

        private void AddWinding(HalfEdge eDst, HalfEdge eSrc)
        {
            eDst.Winding += eSrc.Winding;
            eDst.Sym.Winding += eSrc.Sym.Winding;
        }

        #endregion

        #region Tesselate monotone region methods.

        /// <summary>
        /// MeshTessellateMonoRegion() tessellates a monotone region
        /// (what else would it do??)  The region must consist of a single
        /// loop of half-edges (see mesh.h) oriented CCW.  "Monotone" in this
        /// case means that any vertical line intersects the interior of the
        /// region in a single interval.  
        ///
        /// Tessellation consists of adding interior edges (actually pairs of
        /// half-edges), to split the region into non-overlapping triangles.
        ///
        /// The basic idea is explained in Preparata and Shamos (which I don't
        /// have handy right now), although their implementation is more
        /// complicated than this one.  The are two edge chains, an upper chain
        /// and a lower chain.  We process all vertices from both chains in order,
        /// from right to left.
        ///
        /// The algorithm ensures that the following invariant holds after each
        /// vertex is processed: the untessellated region consists of two
        /// chains, where one chain (say the upper) is a single edge, and
        /// the other chain is concave.  The left vertex of the single edge
        /// is always to the left of all vertices in the concave chain.
        ///
        /// Each step consists of adding the rightmost unprocessed vertex to one
        /// of the two chains, and forming a fan of triangles from the rightmost
        /// of two chain endpoints.  Determining whether we can add each triangle
        /// to the fan is a simple orientation test.  By making the fan as large
        /// as possible, we restore the invariant (check it yourself).
        /// </summary>
        private void MeshTessellateMonoRegion(Face face)
        {
            //  All edges are oriented CCW around the boundary of the region.
            //  First, find the half-edge whose origin vertex is rightmost.
            //  Since the sweep goes from left to right, face.AnEdge should
            //  be close to the edge we want.
            HalfEdge up = face.AnEdge;
            Debug.Assert(up.LeftNext != up && up.LeftNext.LeftNext != up);

            while (Vertex.LessOrEqual(up.Dest, up.Orig)) {
                up = up.LeftPrev;
            }
            while (Vertex.LessOrEqual(up.Orig, up.Dest)) {
                up = up.LeftNext;
            }
            HalfEdge lo = up.LeftPrev;

            while (up.LeftNext != lo) {
                if (Vertex.LessOrEqual(up.Dest, lo.Orig)) {
                    //  up.Dst is on the left.  It is safe to form triangles from lo.Org.
                    //  The EdgeGoesLeft test guarantees progress even when some triangles
                    //  are CW, given that the upper and lower chains are truly monotone.
                    while (lo.LeftNext != up && (lo.LeftNext.EdgeGoesLeft || EdgeSign(lo.Orig, lo.Dest, lo.LeftNext.Dest) <= 0)) {
                        HalfEdge tempHalfEdge = m_mesh.MeshConnect(lo.LeftNext, lo);
                        lo = tempHalfEdge.Sym;
                    }
                    lo = lo.LeftPrev;
                } else {
                    //  lo.Org is on the left.  We can make CCW triangles from up.Dst.
                    while (lo.LeftNext != up && (up.LeftPrev.EdgeGoesRight || EdgeSign(up.Dest, up.Orig, up.LeftPrev.Orig) >= 0)) {
                        HalfEdge tempHalfEdge = m_mesh.MeshConnect(up, up.LeftPrev);
                        up = tempHalfEdge.Sym;
                    }
                    up = up.LeftNext;
                }
            }

            //  Now lo.Org == up.Dst == the leftmost vertex.  The remaining region
            //  can be tessellated in a fan from this leftmost vertex.
            Debug.Assert(lo.LeftNext != up);
            while (lo.LeftNext.LeftNext != up) {
                HalfEdge tempHalfEdge = m_mesh.MeshConnect(lo.LeftNext, lo);
                lo = tempHalfEdge.Sym;
            }
        }

        /// <summary>
        /// MeshTessellateInterior() tessellates each region of
        /// the mesh which is marked "inside" the polygon.  Each such region
        /// must be monotone.
        /// </summary>
        private void MeshTessellateInterior()
        {
            Face next;
            for (Face f = m_mesh.FaceHead.Next; f != m_mesh.FaceHead; f = next) {
                //  Make sure we don't try to tessellate the new triangles.
                next = f.Next;
                if (f.Inside) {
                    MeshTessellateMonoRegion(f);
                }
            }
        }

        /// <summary>
        /// MeshDiscardExterior() zaps (ie. sets to null) all faces
        /// which are not marked "inside" the polygon.  Since further mesh operations
        /// on null faces are not allowed, the main purpose is to clean up the
        /// mesh so that exterior loops are not represented in the data structure.
        /// </summary>
        private void MeshDiscardExterior()
        {
            Face next;
            for (Face f = m_mesh.FaceHead.Next; f != m_mesh.FaceHead; f = next) {
                //  Since f will be destroyed, save its next pointer.
                next = f.Next;
                if (!f.Inside) {
                    m_mesh.MeshZapFace(f);
                }
            }
        }

        /// <summary>
        /// MeshSetWindingNumber(value, keepOnlyBoundary) resets the
        /// winding numbers on all edges so that regions marked "inside" the
        /// polygon have a winding number of "value", and regions outside
        /// have a winding number of 0.
        ///
        /// If keepOnlyBoundary is true, it also deletes all edges which do not
        /// separate an interior region from an exterior one.
        /// </summary>
        private void MeshSetWindingNumber(int value, bool keepOnlyBoundary)
        {
            HalfEdge eNext;
            for (HalfEdge e = m_mesh.EdgeHead.Next; e != m_mesh.EdgeHead; e = eNext) {
                eNext = e.Next;
                if (e.RightFace.Inside != e.LeftFace.Inside) {

                    //  This is a boundary edge (one side is interior, one is exterior).
                    e.Winding = (e.LeftFace.Inside) ? value : -value;
                } else {

                    //  Both regions are interior, or both are exterior.
                    if (!keepOnlyBoundary) {
                        e.Winding = 0;
                    } else {
                        m_mesh.MeshDelete(e);
                    }
                }
            }
        }

        #endregion

        #region Intersection methods.

        private void CallCombine(Vertex isect, double[] weights, bool needed)
        {
            //CALL_COMBINE_OR_COMBINE_DATA( isect.Coords, weights );
        }

        /// <summary>
        /// Two vertices with idential coordinates are combined into one.
        /// e1.Org is kept, while e2.Org is discarded.
        /// </summary>
        private void SpliceMergeVertices(HalfEdge e1, HalfEdge e2)
        {
            double[] weights = new double[] { 0.5, 0.5, 0.0, 0.0 };

            CallCombine(e1.Orig, weights, false);
            m_mesh.MeshSplice(e1, e2);
        }

        /// <summary>
        /// Find some weights which describe how the intersection vertex is
        /// a linear combination of "org" and "dest".  Each of the two edges
        /// which generated "isect" is allocated 50% of the weight; each edge
        /// splits the weight between its org and dst according to the
        /// relative distance to "isect".
        /// </summary>
        private void VertexWeights(Vertex isect, Vertex org, Vertex dst, out double weight0, out double weight1)
        {
            double t1 = VertL1Dist(org, isect);
            double t2 = VertL1Dist(dst, isect);

            weight0 = 0.5 * t2 / (t1 + t2);
            weight1 = 0.5 * t1 / (t1 + t2);
        }

        /// <summary>
        /// We've computed a new intersection point, now we need a "data" pointer
        /// from the user so that we can refer to this new vertex in the
        /// rendering callbacks.
        /// </summary>
        private void GetIntersectData(Vertex isect, Vertex orgUp, Vertex dstUp, Vertex orgLo, Vertex dstLo)
        {
            double weight0, weight1, weight2, weight3;

            VertexWeights(isect, orgUp, dstUp, out weight0, out weight1);
            VertexWeights(isect, orgLo, dstLo, out weight2, out weight3);

            CallCombine(isect, new double[] { weight0, weight1, weight2, weight3 }, true);
        }

        /// <summary>
        /// Given parameters a, x, b, y returns the value (b*x + a*y)/(a+b),
        /// or (x + y)/2 if a == b == 0.  It requires that a, b >= 0, and enforces
        /// this in the rare case that one argument is slightly negative.
        /// The implementation is extremely stable numerically.
        /// In particular it guarantees that the result r satisfies
        /// MIN(x, y) leq r leq MAX(x, y), and the results are very accurate
        /// even when a and b differ greatly in magnitude.
        /// </summary>
        private double Interpolate(double a, double x, double b, double y)
        {
            a = (a < 0.0) ? 0.0 : a;
            b = (b < 0.0) ? 0.0 : b;
            if (a <= b) {
                return (b == 0.0) ? (x + y) / 2 : x + (y - x) * (a / (a + b));
            } else {
                return y + (x - y) * (b / (a + b));
            }
        }

        private void Swap(ref Vertex a, ref Vertex b)
        {
            Vertex temp = a;
            a = b;
            b = temp;
        }

        private void EdgeIntersect(Vertex o1, Vertex d1, Vertex o2, Vertex d2, out Vertex v)
        {
            //  Given edges (o1, d1) and (o2, d2), compute their point of intersection.
            //  The computed point is guaranteed to lie in the intersection of the
            //  bounding rectangles defined by each edge.

            v = new Vertex();

            //  This is certainly not the most efficient way to find the intersection
            //  of two line segments, but it is very numerically stable.

            //  Strategy: find the two middle vertices in the VertLeq ordering,
            //  and interpolate the intersection X-value from these.  Then repeat
            //  using the TransLeq ordering to find the intersection Y-value.

            if (!Vertex.LessOrEqual(o1, d1)) { Swap(ref o1, ref d1); }
            if (!Vertex.LessOrEqual(o2, d2)) { Swap(ref o2, ref d2); }
            if (!Vertex.LessOrEqual(o1, o2)) { Swap(ref o1, ref o2); Swap(ref d1, ref d2); }

            if (!Vertex.LessOrEqual(o2, d1)) {
                //  Technically, no intersection -- do our best.
                v.X = (o2.X + d1.X) / 2;
            } else if (Vertex.LessOrEqual(d1, d2)) {
                //  Interpolate between o2 and d1.
                double z1 = EdgeEval(o1, o2, d1);
                double z2 = EdgeEval(o2, d1, d2);
                if (z1 + z2 < 0.0) {
                    z1 = -z1;
                    z2 = -z2;
                }
                v.X = Interpolate(z1, o2.X, z2, d1.X);
            } else {
                //  Interpolate between o2 and d2.
                double z1 = EdgeSign(o1, o2, d1);
                double z2 = -EdgeSign(o1, d2, d1);
                if (z1 + z2 < 0) {
                    z1 = -z1;
                    z2 = -z2;
                }
                v.X = Interpolate(z1, o2.X, z2, d2.X);
            }

            //  Now repeat the process for Y.

            if (!Vertex.LessOrEqualTrans(o1, d1)) { Swap(ref o1, ref d1); }
            if (!Vertex.LessOrEqualTrans(o2, d2)) { Swap(ref o2, ref d2); }
            if (!Vertex.LessOrEqualTrans(o1, o2)) { Swap(ref o1, ref o2); Swap(ref d1, ref d2); }

            if (!Vertex.LessOrEqualTrans(o2, d1)) {
                //  Technically, no intersection -- do our best.
                v.Y = (o2.Y + d1.Y) / 2;
            } else if (Vertex.LessOrEqualTrans(d1, d2)) {
                //  Interpolate between o2 and d1.
                double z1 = EdgeEvalTrans(o1, o2, d1);
                double z2 = EdgeEvalTrans(o2, d1, d2);
                if (z1 + z2 < 0.0) {
                    z1 = -z1;
                    z2 = -z2;
                }
                v.Y = Interpolate(z1, o2.Y, z2, d1.Y);
            } else {
                //  Interpolate between o2 and d2.
                double z1 = EdgeSignTrans(o1, o2, d1);
                double z2 = -EdgeSignTrans(o1, d2, d1);
                if (z1 + z2 < 0.0) {
                    z1 = -z1;
                    z2 = -z2;
                }
                v.Y = Interpolate(z1, o2.Y, z2, d2.Y);
            }
        }

        #endregion

        #region Render methods.

        private void RenderMesh(out List<Triangle> triangleList, out List<Vertex> vertexList)
        {
            triangleList = new List<Triangle>();
            vertexList = new List<Vertex>();

            for (Face f = m_mesh.FaceHead.Next; f != m_mesh.FaceHead; f = f.Next) {
                if (f.Inside) {
                    HalfEdge e = f.AnEdge;
                    Triangle triangle = new Triangle();
                    triangle.VertexO = e.Orig;
                    triangle.VertexD = e.LeftNext.Orig;
                    triangle.VertexA = e.LeftPrev.Orig;
                    triangleList.Add(triangle);

                    if (triangle.VertexO.InternalId < 0) {
                        triangle.VertexO.InternalId = vertexList.Count;
                        vertexList.Add(triangle.VertexO);
                    }
                    if (triangle.VertexD.InternalId < 0) {
                        triangle.VertexD.InternalId = vertexList.Count;
                        vertexList.Add(triangle.VertexD);
                    }
                    if (triangle.VertexA.InternalId < 0) {
                        triangle.VertexA.InternalId = vertexList.Count;
                        vertexList.Add(triangle.VertexA);
                    }
                }
            }
        }

        private void RenderBoundary(out List<List<Vertex>> contourList)
        {
            contourList = new List<List<Vertex>>();

            for (Face f = m_mesh.FaceHead.Next; f != m_mesh.FaceHead; f = f.Next) {
                if (f.Inside) {
                    HalfEdge e = f.AnEdge;
                    List<Vertex> contour = new List<Vertex>();
                    do {
                        contour.Add(e.Orig);
                        e = e.LeftNext;
                    } while (e != f.AnEdge);
                    contourList.Add(contour);
                }
            }
        }

        #endregion

        #region Vertex class.

        public class Vertex
        {

            #region Public fields.

            public int VertexIndex;
            public double X;
            public double Y;

            public Vertex Next;
            public Vertex Prev;
            public HalfEdge AnEdge;
            public int InternalId;

            #endregion

            #region Public constructors.

            public Vertex()
            {
                VertexIndex = -1;
                InternalId = -1;
            }

            public Vertex(int vertexIndex, double x, double y)
            {
                VertexIndex = vertexIndex;
                X = x;
                Y = y;
                InternalId = -1;
            }

            #endregion

            #region Comparison methods.

            public static int Compare(Vertex left, Vertex right)
            {
                if (left.X < right.X)
                    return -1;
                if (left.X > right.X)
                    return +1;
                if (left.Y < right.Y)
                    return -1;
                if (left.Y > right.Y)
                    return +1;
                if (left.VertexIndex < right.VertexIndex)
                    return -1;
                if (left.VertexIndex > right.VertexIndex)
                    return +1;
                return 0;
            }

            public static bool Equal(Vertex left, Vertex right)
            {
                return (left.X == right.X && left.Y == right.Y);
            }

            public static bool LessOrEqual(Vertex left, Vertex right)
            {
                return (left.X < right.X || (left.X == right.X && left.Y <= right.Y));
            }

            public static bool LessOrEqualTrans(Vertex left, Vertex right)
            {
                return (left.Y < right.Y || (left.Y == right.Y && left.X <= right.X));
            }

            #endregion

            #region Virtual method overrides.

            public override string ToString()
            {
                return string.Format("Vertex({0}: {1}, {2})", VertexIndex, X, Y);
            }

            #endregion

        }

        #endregion

        #region Triangle class.

        public class Triangle
        {
            public Vertex VertexO;
            public Vertex VertexD;
            public Vertex VertexA;
        }

        #endregion

        #region Face class.

        public class Face
        {

            #region Public fields.

            //public int FaceIndex;
            public HalfEdge AnEdge;
            public bool Inside;

            public Face Next;
            public Face Prev;

            #endregion

            #region Public constructors.

            //public Face()
            //{
            //    FaceIndex = GlobalFaceCount++;
            //}

            #endregion

            #region Virtual method overrides.

            //public override string ToString()
            //{
            //    return string.Format("Face({0})", FaceIndex);
            //}

            #endregion

            #region Private fields.

            //private static int GlobalFaceCount = 0;

            #endregion

        }

        #endregion

        #region HalfEdge class.

        public class HalfEdge
        {

            #region Public fields and properties.

            //public int EdgeIndex;
            public Vertex Orig;
            public Vertex Dest { get { return Sym.Orig; } set { Sym.Orig = value; } }

            public HalfEdge Sym;
            public Region Region;
            public int Winding;

            public HalfEdge Next;

            public HalfEdge OrigPrev { get { return Sym.LeftNext; } set { Sym.LeftNext = value; } }
            public HalfEdge OrigNext;
            public HalfEdge DestPrev { get { return LeftNext.Sym; } set { LeftNext.Sym = value; } }
            public HalfEdge DestNext { get { return RightPrev.Sym; } set { RightPrev.Sym = value; } }

            public HalfEdge LeftPrev { get { return OrigNext.Sym; } set { OrigNext.Sym = value; } }
            public HalfEdge LeftNext;
            public HalfEdge RightPrev { get { return Sym.OrigNext; } set { Sym.OrigNext = value; } }
            public HalfEdge RightNext { get { return OrigPrev.Sym; } set { OrigPrev.Sym = value; } }

            public Face LeftFace;
            public Face RightFace { get { return Sym.LeftFace; } set { Sym.LeftFace = value; } }


            public bool EdgeGoesLeft { get { return Vertex.LessOrEqual(Dest, Orig); } }
            public bool EdgeGoesRight { get { return Vertex.LessOrEqual(Orig, Dest); } }

            #endregion

            #region Public constructors.

            //public HalfEdge()
            //{
            //    EdgeIndex = GlobalEdgeCount++;
            //}

            #endregion

            #region Virtual method overrides.

            public override string ToString()
            {
                return string.Format("HalfEdge({1}, {2})", Orig == null ? "<null>" : Orig.VertexIndex.ToString(), Dest == null ? "<null>" : Dest.VertexIndex.ToString());
            }

            #endregion

            #region Private fields.

            //private static int GlobalEdgeCount = 0;

            #endregion

        }

        #endregion

        #region Region class.

        public class Region
        {

            #region Public fields.

            public HalfEdge UpperEdge;  //  Upper edge, directed right to left.
            public LinkedListNode<Region> UpperNode;    //  Dictionary node corresponding to UpperEdge.
            public int WindingNumber;   //  Used to determine which regions are inside the polygon.
            public bool Inside; //  Is this region inside the polygon?
            public bool Sentinel;   //  Marks fake edges at Y = +/-infinity.
            public bool Dirty;  //  Marks regions where the upper or lower edge has changed, but we haven't checked whether they intersect yet.
            public bool FixUpperEdge;   //  Marks temporary edges introduced when we process a "right vertex" (one without any edges leaving to the right).

            #endregion

            #region Public properties.

            public Region RegionBelow { get { return UpperNode.Previous.Value; } }
            public Region RegionAbove { get { return UpperNode.Next.Value; } }

            #endregion

            #region Virtual method overrides.

            public override string ToString()
            {
                return string.Format("Region({0})", UpperEdge);
            }

            #endregion

        }

        #endregion

        #region Mesh class.

        private class Mesh
        {

            #region Public fields.

            public Vertex VertexHead;
            public Face FaceHead;
            public HalfEdge EdgeHead;
            public HalfEdge EdgeHeadSym;

            #endregion

            #region Public constructors.

            public Mesh()
            {
                VertexHead = new Vertex();
                FaceHead = new Face();
                EdgeHead = new HalfEdge();
                EdgeHeadSym = new HalfEdge();

                VertexHead.Next = VertexHead.Prev = VertexHead;
                VertexHead.AnEdge = null;

                FaceHead.Next = FaceHead.Prev = FaceHead;
                FaceHead.AnEdge = null;
                FaceHead.Inside = false;

                EdgeHead.Next = EdgeHead;
                EdgeHead.Sym = EdgeHeadSym;
                EdgeHead.OrigNext = null;
                EdgeHead.LeftNext = null;
                EdgeHead.Orig = null;
                EdgeHead.LeftFace = null;
                EdgeHead.Winding = 0;
                EdgeHead.Region = null;

                EdgeHeadSym.Next = EdgeHeadSym;
                EdgeHeadSym.Sym = EdgeHead;
                EdgeHeadSym.OrigNext = null;
                EdgeHeadSym.LeftNext = null;
                EdgeHeadSym.Orig = null;
                EdgeHeadSym.LeftFace = null;
                EdgeHeadSym.Winding = 0;
                EdgeHeadSym.Region = null;
            }

            #endregion

            #region Geometry creation / destruction methods.

            public void MakeVertex(Vertex newVertex, HalfEdge eOrig, Vertex vNext)
            {
                Debug.Assert(newVertex != null);

                //  Insert in circular doubly-linked list before vNext.
                Vertex vPrev = vNext.Prev;
                newVertex.Prev = vPrev;
                vPrev.Next = newVertex;
                newVertex.Next = vNext;
                vNext.Prev = newVertex;

                newVertex.AnEdge = eOrig;

                //  Leave X, Y, VertexIndex undefined.

                //  Fix other edges on this vertex loop.
                HalfEdge e = eOrig;
                do {
                    e.Orig = newVertex;
                    e = e.OrigNext;
                } while (e != eOrig);
            }

            public void KillVertex(Vertex vDel, Vertex newOrg)
            {
                //  Change the origin of all affected edges.
                HalfEdge eStart = vDel.AnEdge;
                HalfEdge e = eStart;
                do {
                    e.Orig = newOrg;
                    e = e.OrigNext;
                } while (e != eStart);

                //  Delete from circular doubly-linked list.
                Vertex vPrev = vDel.Prev;
                Vertex vNext = vDel.Next;
                vNext.Prev = vPrev;
                vPrev.Next = vNext;
            }

            public void MakeFace(Face newFace, HalfEdge eOrig, Face fNext)
            {
                Debug.Assert(newFace != null);

                //  Insert in circular doubly-linked list before fNext.
                Face fPrev = fNext.Prev;
                newFace.Prev = fPrev;
                fPrev.Next = newFace;
                newFace.Next = fNext;
                fNext.Prev = newFace;

                newFace.AnEdge = eOrig;

                //  The new face is marked "inside" if the old one was.  This is a
                //  convenience for the common case where a face has been split in two.
                newFace.Inside = fNext.Inside;

                //  Fix other edges on this face loop.
                HalfEdge e = eOrig;
                do {
                    e.LeftFace = newFace;
                    e = e.LeftNext;
                } while (e != eOrig);
            }

            public void KillFace(Face fDel, Face newLFace)
            {
                //  Change the left face of all affected edges.
                HalfEdge eStart = fDel.AnEdge;
                HalfEdge e = eStart;
                do {
                    e.LeftFace = newLFace;
                    e = e.LeftNext;
                } while (e != eStart);

                //  Delete from circular doubly-linked list.
                Face fPrev = fDel.Prev;
                Face fNext = fDel.Next;
                fNext.Prev = fPrev;
                fPrev.Next = fNext;
            }

            public HalfEdge MakeEdge(HalfEdge eNext)
            {
                HalfEdge e = new HalfEdge();
                HalfEdge eSym = new HalfEdge();

                //  Make sure eNext points to the first edge of the edge pair.
                //if (eNext.Sym.EdgeIndex < eNext.EdgeIndex) {
                //    eNext = eNext.Sym;
                //}

                //  Insert in circular doubly-linked list before eNext.
                //  Note that the prev pointer is stored in Sym.Next.
                HalfEdge ePrev = eNext.Sym.Next;
                eSym.Next = ePrev;
                ePrev.Sym.Next = e;
                e.Next = eNext;
                eNext.Sym.Next = eSym;

                e.Sym = eSym;
                e.OrigNext = e;
                e.LeftNext = eSym;
                e.Orig = null;
                e.LeftFace = null;
                e.Winding = 0;
                e.Region = null;

                eSym.Sym = e;
                eSym.OrigNext = eSym;
                eSym.LeftNext = e;
                eSym.Orig = null;
                eSym.LeftFace = null;
                eSym.Winding = 0;
                eSym.Region = null;

                return e;
            }

            public void KillEdge(HalfEdge eDel)
            {
                //  Half-edges are allocated in pairs, see EdgePair above.
                //if (eDel.Sym.EdgeIndex < eDel.EdgeIndex) {
                //    eDel = eDel.Sym;
                //}

                //  Delete from circular doubly-linked list.
                HalfEdge eNext = eDel.Next;
                HalfEdge ePrev = eDel.Sym.Next;
                eNext.Sym.Next = ePrev;
                ePrev.Sym.Next = eNext;
            }

            #endregion

            #region Geometry manipulation methods.

            public void Splice(HalfEdge a, HalfEdge b)
            {
                HalfEdge aONext = a.OrigNext;
                HalfEdge bONext = b.OrigNext;

                aONext.Sym.LeftNext = b;
                bONext.Sym.LeftNext = a;
                a.OrigNext = bONext;
                b.OrigNext = aONext;
            }

            public HalfEdge MeshMakeEdge()
            {
                Vertex newVertex1 = new Vertex();
                Vertex newVertex2 = new Vertex();
                Face newFace = new Face();

                HalfEdge e = MakeEdge(EdgeHead);

                MakeVertex(newVertex1, e, VertexHead);
                MakeVertex(newVertex2, e.Sym, VertexHead);
                MakeFace(newFace, e, FaceHead);
                return e;
            }

            public void MeshSplice(HalfEdge eOrg, HalfEdge eDst)
            {
                bool joiningLoops = false;
                bool joiningVertices = false;

                if (eOrg == eDst)
                    return;

                if (eDst.Orig != eOrg.Orig) {
                    //  We are merging two disjoint vertices -- destroy eDst->Org.
                    joiningVertices = true;
                    KillVertex(eDst.Orig, eOrg.Orig);
                }
                if (eDst.LeftFace != eOrg.LeftFace) {
                    //  We are connecting two disjoint loops -- destroy eDst->LFace.
                    joiningLoops = true;
                    KillFace(eDst.LeftFace, eOrg.LeftFace);
                }

                //  Change the edge structure.
                Splice(eDst, eOrg);

                if (!joiningVertices) {
                    Vertex newVertex = new Vertex();

                    //  We split one vertex into two -- the new vertex is eDst->Org.
                    //  Make sure the old vertex points to a valid half-edge.
                    MakeVertex(newVertex, eDst, eOrg.Orig);
                    eOrg.Orig.AnEdge = eOrg;
                }
                if (!joiningLoops) {
                    Face newFace = new Face();

                    //  We split one loop into two -- the new loop is eDst->LFace.
                    //  Make sure the old face points to a valid half-edge.
                    MakeFace(newFace, eDst, eOrg.LeftFace);
                    eOrg.LeftFace.AnEdge = eOrg;
                }
            }

            public void MeshDelete(HalfEdge eDel)
            {
                HalfEdge eDelSym = eDel.Sym;

                //  First step: disconnect the origin vertex eDel.Org.  We make all
                //  changes to get a consistent mesh in this "intermediate" state.
                bool joiningLoops = false;
                if (eDel.LeftFace != eDel.RightFace) {
                    //  We are joining two loops into one -- remove the left face.
                    joiningLoops = true;
                    KillFace(eDel.LeftFace, eDel.RightFace);
                }

                if (eDel.OrigNext == eDel) {
                    KillVertex(eDel.Orig, null);
                } else {
                    //  Make sure that eDel->Org and eDel->Rface point to valid half-edges.
                    eDel.RightFace.AnEdge = eDel.OrigPrev;
                    eDel.Orig.AnEdge = eDel.OrigNext;

                    Splice(eDel, eDel.OrigPrev);
                    if (!joiningLoops) {
                        Face newFace = new Face();

                        //  We are splitting one loop into two -- create a new loop for eDel.
                        MakeFace(newFace, eDel, eDel.LeftFace);
                    }
                }

                //  Claim: the mesh is now in a consistent state, except that eDel.Org
                //  may have been deleted.  Now we disconnect eDel.Dst.
                if (eDelSym.OrigNext == eDelSym) {
                    KillVertex(eDelSym.Orig, null);
                    KillFace(eDelSym.LeftFace, null);
                } else {
                    //  Make sure that eDel->Dst and eDel->Lface point to valid half-edges.
                    eDel.LeftFace.AnEdge = eDelSym.OrigPrev;
                    eDelSym.Orig.AnEdge = eDelSym.OrigNext;
                    Splice(eDelSym, eDelSym.OrigPrev);
                }

                //  Any isolated vertices or faces have already been freed.
                KillEdge(eDel);
            }

            public HalfEdge MeshAddEdgeVertex(HalfEdge eOrg)
            {
                HalfEdge eNew = MakeEdge(eOrg);
                HalfEdge eNewSym = eNew.Sym;

                //  Connect the new edge appropriately.
                Splice(eNew, eOrg.LeftNext);

                //  Set the vertex and face information.
                eNew.Orig = eOrg.Dest;
                {
                    Vertex newVertex = new Vertex();

                    MakeVertex(newVertex, eNewSym, eNew.Orig);
                }
                eNew.LeftFace = eNewSym.LeftFace = eOrg.LeftFace;

                return eNew;
            }

            public HalfEdge MeshSplitEdge(HalfEdge eOrg)
            {
                HalfEdge tempHalfEdge = MeshAddEdgeVertex(eOrg);
                HalfEdge eNew = tempHalfEdge.Sym;

                //  Disconnect eOrg from eOrg.Dst and connect it to eNew.Org.
                Splice(eOrg.Sym, eOrg.Sym.OrigPrev);
                Splice(eOrg.Sym, eNew);

                //  Set the vertex and face information.
                eOrg.Dest = eNew.Orig;
                eNew.Dest.AnEdge = eNew.Sym; //  May have pointed to eOrg.Sym.
                eNew.RightFace = eOrg.RightFace;
                eNew.Winding = eOrg.Winding;    //  Copy old winding information.
                eNew.Sym.Winding = eOrg.Sym.Winding;

                return eNew;
            }

            public HalfEdge MeshConnect(HalfEdge eOrg, HalfEdge eDst)
            {
                HalfEdge eNew = MakeEdge(eOrg);
                HalfEdge eNewSym = eNew.Sym;

                bool joiningLoops = false;
                if (eDst.LeftFace != eOrg.LeftFace) {
                    //  We are connecting two disjoint loops -- destroy eDst.LFace.
                    joiningLoops = true;
                    KillFace(eDst.LeftFace, eOrg.LeftFace);
                }

                //  Connect the new edge appropriately.
                Splice(eNew, eOrg.LeftNext);
                Splice(eNewSym, eDst);

                //  Set the vertex and face information.
                eNew.Orig = eOrg.Dest;
                eNewSym.Orig = eDst.Orig;
                eNew.LeftFace = eNewSym.LeftFace = eOrg.LeftFace;

                //  Make sure the old face points to a valid half-edge.
                eOrg.LeftFace.AnEdge = eNewSym;

                if (!joiningLoops) {
                    Face newFace = new Face();

                    //  We split one loop into two -- the new loop is eNew.LFace.
                    MakeFace(newFace, eNew, eOrg.LeftFace);
                }
                return eNew;
            }

            public void MeshZapFace(Face fZap)
            {
                //  Walk around face, deleting edges whose right face is also null.
                HalfEdge eStart = fZap.AnEdge;
                HalfEdge eNext = eStart.LeftNext;
                HalfEdge e;
                do {
                    e = eNext;
                    eNext = e.LeftNext;

                    e.LeftFace = null;
                    if (e.RightFace == null) {
                        //  Delete the edge -- see MeshDelete above.

                        if (e.OrigNext == e) {
                            KillVertex(e.Orig, null);
                        } else {
                            //  Make sure that e.Org points to a valid half-edge.
                            e.Orig.AnEdge = e.OrigNext;
                            Splice(e, e.OrigPrev);
                        }
                        HalfEdge eSym = e.Sym;
                        if (eSym.OrigNext == eSym) {
                            KillVertex(eSym.Orig, null);
                        } else {
                            //  Make sure that eSym.Org points to a valid half-edge.
                            eSym.Orig.AnEdge = eSym.OrigNext;
                            Splice(eSym, eSym.OrigPrev);
                        }
                        KillEdge(e);
                    }
                } while (e != eStart);

                //  Delete from circular doubly-linked list.
                Face fPrev = fZap.Prev;
                Face fNext = fZap.Next;
                fNext.Prev = fPrev;
                fPrev.Next = fNext;
            }

            public void MeshCheckMesh()
            {
                return;

                Face f, fPrev;
                Vertex v, vPrev;
                HalfEdge e, ePrev;

                fPrev = FaceHead;
                for (fPrev = FaceHead; (f = fPrev.Next) != FaceHead; fPrev = f) {
                    Debug.Assert(f.Prev == fPrev);
                    e = f.AnEdge;
                    do {
                        Debug.Assert(e.Sym != e);
                        Debug.Assert(e.Sym.Sym == e);
                        Debug.Assert(e.LeftNext.OrigNext.Sym == e);
                        Debug.Assert(e.OrigNext.Sym.LeftNext == e);
                        Debug.Assert(e.LeftFace == f);
                        e = e.LeftNext;
                    } while (e != f.AnEdge);
                }
                Debug.Assert(f.Prev == fPrev && f.AnEdge == null);

                vPrev = VertexHead;
                for (vPrev = VertexHead; (v = vPrev.Next) != VertexHead; vPrev = v) {
                    Debug.Assert(v.Prev == vPrev);
                    e = v.AnEdge;
                    do {
                        Debug.Assert(e.Sym != e);
                        Debug.Assert(e.Sym.Sym == e);
                        Debug.Assert(e.LeftNext.OrigNext.Sym == e);
                        Debug.Assert(e.OrigNext.Sym.LeftNext == e);
                        Debug.Assert(e.Orig == v);
                        e = e.OrigNext;
                    } while (e != v.AnEdge);
                }
                Debug.Assert(v.Prev == vPrev && v.AnEdge == null);

                ePrev = EdgeHead;
                for (ePrev = EdgeHead; (e = ePrev.Next) != EdgeHead; ePrev = e) {
                    Debug.Assert(e.Sym.Next == ePrev.Sym);
                    Debug.Assert(e.Sym != e);
                    Debug.Assert(e.Sym.Sym == e);
                    Debug.Assert(e.Orig != null);
                    Debug.Assert(e.Dest != null);
                    Debug.Assert(e.LeftNext.OrigNext.Sym == e);
                    Debug.Assert(e.OrigNext.Sym.LeftNext == e);
                }
                Debug.Assert(e.Sym.Next == ePrev.Sym
                   && e.Sym == EdgeHeadSym
                   && e.Sym.Sym == e
                   && e.Orig == null && e.Dest == null
                   && e.LeftFace == null && e.RightFace == null);
            }

            public static Mesh MeshUnion(Mesh mesh1, Mesh mesh2)
            {
                Face f1 = mesh1.FaceHead;
                Vertex v1 = mesh1.VertexHead;
                HalfEdge e1 = mesh1.EdgeHead;
                Face f2 = mesh2.FaceHead;
                Vertex v2 = mesh2.VertexHead;
                HalfEdge e2 = mesh2.EdgeHead;

                //  Add the faces, vertices, and edges of mesh2 to those of mesh1.
                if (f2.Next != f2) {
                    f1.Prev.Next = f2.Next;
                    f2.Next.Prev = f1.Prev;
                    f2.Prev.Next = f1;
                    f1.Prev = f2.Prev;
                }

                if (v2.Next != v2) {
                    v1.Prev.Next = v2.Next;
                    v2.Next.Prev = v1.Prev;
                    v2.Prev.Next = v1;
                    v1.Prev = v2.Prev;
                }

                if (e2.Next != e2) {
                    e1.Sym.Next.Sym.Next = e2.Next;
                    e2.Next.Sym.Next = e1.Sym.Next;
                    e2.Sym.Next.Sym.Next = e1;
                    e1.Sym.Next = e2.Sym.Next;
                }

                return mesh1;
            }

            #endregion

        }

        #endregion

        #region OrderedList class.

        public class OrderedList<Type> : LinkedList<Type>
            where Type : class
        {

            #region Public properties.

            public LinkedListNode<Type> ListMin { get { return First.Next; } }
            public LinkedListNode<Type> ListMax { get { return Last.Previous; } }

            public new int Count { get { return base.Count - 2; } }

            #endregion

            #region Public delegates.

            public delegate bool LeqDelegate(Type a, Type b);

            #endregion

            #region Public constructors.

            public OrderedList(LeqDelegate leqDelegate)
            {
                AddFirst((Type)null);
                AddLast((Type)null);

                m_leqDelegate = leqDelegate;
            }

            #endregion

            #region Public access methods.

            public LinkedListNode<Type> Search(Type value)
            {
                LinkedListNode<Type> node = First;

                do {
                    node = node.Next;
                } while (node.Value != null && !m_leqDelegate(value, node.Value));

                return node;
            }

            public LinkedListNode<Type> Insert(Type value)
            {
                return InsertBefore(Last, value);
            }

            public LinkedListNode<Type> InsertBefore(LinkedListNode<Type> node, Type value)
            {
                do {
                    node = node.Previous;
                } while (node.Value != null && !m_leqDelegate(node.Value, value));

                LinkedListNode<Type> newNode = new LinkedListNode<Type>(value);
                AddAfter(node, newNode);

                return newNode;
            }

            #endregion

            #region Private fields.

            private LeqDelegate m_leqDelegate;

            #endregion

        }

        #endregion

        #region Private fields.

        private Mesh m_mesh;    //  Stores the input contours, and eventually the tessellation itself.

        private WindingRule m_windingRule;  //  Rule for determining polygon interior.

        private OrderedList<Region> m_regionList;  //  Ordered set of active regions.
        private List<Vertex> m_vertexQueue;   //  Priority queue of vertex events.
        private int m_currentVertexNum;
        private Vertex m_currentEvent;  //  Current sweep event (vertex) being processed.

        #endregion

    }

}
