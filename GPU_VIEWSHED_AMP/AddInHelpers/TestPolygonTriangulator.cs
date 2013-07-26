using System;
using Myriax.Eonfusion.API;
using Myriax.Eonfusion.API.Binding;
using Myriax.Eonfusion.API.Data;
using Myriax.Eonfusion.API.Properties;
using Myriax.Eonfusion.API.UI.Descriptors;
using Myriax.Eonfusion.API.Helpers;
using System.Collections.Generic;

namespace AddInHelpers
{

    public class TestPolygonTriangulator : IAddIn
    {

        #region Execute method.

        public IDataset Execute(IApplication application)
        {
            IDataset dataSet = application.CreateDataset("Polygon test");

            TestVectors testVectors = dataSet.CreateVectorSetGroup<TestVectors>("Polygons");

            PolygonTriangulator polygonTriangulator = new PolygonTriangulator();

            Random random = new Random(0);
            
            List<List<PolygonTriangulator.Vertex>> intersectionContourList = new List<List<PolygonTriangulator.Vertex>>();

            ITable<TestVectorsFeature2D> featureTable = testVectors.VectorSet2D.FeatureTable;

            TestVectorsFeature2D intersectionPolygonFeature = featureTable.AllocateOne();
            intersectionPolygonFeature.ID = intersectionPolygonFeature.RowIndex;

            foreach (TestVectorsFeature2D polygonFeature in featureTable.AllocateMany((int)PolygonCount.Value)) {
                polygonFeature.ID = polygonFeature.RowIndex;
                List<PolygonTriangulator.Vertex> contour = new List<PolygonTriangulator.Vertex>();
                List<PolygonTriangulator.Vertex> intersectionContour = new List<PolygonTriangulator.Vertex>();

                double centreX = random.NextDouble() * 100.0 - 50.0;
                double centreY = random.NextDouble() * 80.0 - 40.0;
                double radiusX = random.NextDouble() * 28.0 + 4.0;
                double radiusY = random.NextDouble() * 28.0 + 4.0;
                double z = (double)(polygonFeature.RowIndex - 1) / (double)PolygonCount.Value * 5.0 + 1.0;
                
                int vertexCount = 160;

                for (int vertexNum = 0; vertexNum < vertexCount; ++vertexNum) {
                    double angle = (double)vertexNum / (double)vertexCount * Math.PI * 2.0;
                    double radiusScale = random.NextDouble() * 0.0 + 1.0;

                    double x = centreX + radiusX * radiusScale * Math.Cos(angle);
                    double y = centreY + radiusY * radiusScale * Math.Sin(angle);

                    contour.Add(new PolygonTriangulator.Vertex(-1, x, y));
                    intersectionContour.Add(new PolygonTriangulator.Vertex(-1, x, y));
                }

                List<List<PolygonTriangulator.Vertex>> contourList = new List<List<PolygonTriangulator.Vertex>>();
                contourList.Add(contour);

                intersectionContourList.Add(intersectionContour);

                List<PolygonTriangulator.Triangle> triangleList;
                List<PolygonTriangulator.Vertex> vertexList;
                polygonTriangulator.Tesselate(contourList, PolygonTriangulator.WindingRule.Odd, out triangleList, out vertexList);

                BuildPolygonPrimitives(testVectors, polygonFeature, z, triangleList, vertexList);
            }


            List<List<PolygonTriangulator.Vertex>> simplifiedContourList;
            polygonTriangulator.Simplify(intersectionContourList, PolygonTriangulator.WindingRule.Odd, out simplifiedContourList);

            List<PolygonTriangulator.Triangle> intersectionTriangleList;
            List<PolygonTriangulator.Vertex> intersectionVertexList;
            polygonTriangulator.Tesselate(simplifiedContourList, PolygonTriangulator.WindingRule.Positive, out intersectionTriangleList, out intersectionVertexList);

            BuildPolygonPrimitives(testVectors, intersectionPolygonFeature, 0.0, intersectionTriangleList, intersectionVertexList);

            return dataSet;
        }

        #endregion

        #region AddIn properties.

        public IntProperty PolygonCount = new IntProperty("Polygon count", 10, "Number of random polygons to create.");

        #endregion

        #region AddIn description.

        public int InputSocketCount
        {
            get { return 0; }
        }

        public string Name
        {
            get { return "Test AddIn PolygonTriangulator"; }
        }

        public string Category
        {
            get { return "add-in helper"; }
        }

        public string Description
        {
            get { return "Test operator for Myriax.Eonfusion.API.Helpers.PolygonTriangulator"; }
        }

        public string Author
        {
            get { return "John Corbett, Myriax"; }
        }

        public System.Collections.Generic.IEnumerable<Descriptor> Descriptors
        {
            get
            {
                // To add additional, optional authoring information, remove the "return null" statement and uncomment the
                // required fields.  The descriptors can be reordered and/or repeated as desired, and formatted using RTF markup.
                return null;

                //yield return new DateCreatedDescriptor("(insert date created here)");
                //yield return new DateModifiedDescriptor("(insert date modified here)");
                //yield return new LineBreakDescriptor();

                //yield return new OrganizationDescriptor("(insert organization here)");
                //yield return new URLDescriptor("(insert URL here)");
                //yield return new EmailDescriptor("(insert email address here)");
                //yield return new LineBreakDescriptor();

                //yield return new LicenseDescriptor("(insert license details here)");
                //yield return new CopyrightDescriptor("(insert copyright notice here)");
                //yield return new ReferencesDescriptor("(insert reference here)");
                //yield return new LineBreakDescriptor();

                //yield return new UserDescriptor("(insert additional heading here)", "(insert additional descriptor text here)");
            }
        }

        #endregion

        #region Metadata operator.

        public class Metadata : MetadataOperator
        {
        }

        #endregion

        #region Private methods.

        private void BuildPolygonPrimitives(TestVectors testVectors, TestVectorsFeature2D feature, double z, List<PolygonTriangulator.Triangle> triangleList, List<PolygonTriangulator.Vertex> vertexList)
        {
            //  Allocate data vertices for each triangulation vertex.

            ITable<TestVectorsVertex> vertexTable = testVectors.VertexTable;

            IRowCollectionReadOnly<TestVectorsVertex> dataVertices = vertexTable.AllocateMany(vertexList.Count);

            for (int vertexNum = 0; vertexNum < vertexList.Count; ++vertexNum) {
                PolygonTriangulator.Vertex vertex = vertexList[vertexNum];
                TestVectorsVertex dataVertex = dataVertices[vertexNum];
                dataVertex.X = vertex.X;
                dataVertex.Y = vertex.Y;
                dataVertex.Z = z;
            }



            //  Now allocate primitives and hook them up to the vertices.

            IPrimitiveBuilder2DList<TestVectorsFeature2D, TestVectorsPrimitive2D, TestVectorsVertex> primitiveBuilder = testVectors.VectorSet2D.CreatePrimitiveBuilderList(feature);

            foreach (PolygonTriangulator.Triangle triangle in triangleList) {
                primitiveBuilder.CreatePrimitive(dataVertices[triangle.VertexO.InternalId], dataVertices[triangle.VertexD.InternalId], dataVertices[triangle.VertexA.InternalId]);
            }
        }

        #endregion

    }

}
