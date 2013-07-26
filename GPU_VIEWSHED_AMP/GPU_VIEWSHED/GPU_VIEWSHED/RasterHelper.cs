using System;
using Myriax.Eonfusion.API.Data;

namespace Myriax.Eonfusion.API.Helpers
{

    public abstract class RasterHelper
    {

        #region Public properties.

        public int[] TileSize { get { return m_tileSize; } }
        public int TileIndexCount { get { return m_tileIndexCount; } }

        #endregion

        #region Constructors.

        protected RasterHelper(IRaster raster)
        {
            if (raster == null) {
                throw new ArgumentNullException("raster");
            }

            m_raster = raster;
        }

        #endregion

        #region Public access methods.

        public delegate void ProcessRasterWindowSpanDelegate(int rasterIndex, int[] rasterTileOfs, int windowIndexOfs, int[] windowOfs, int spanSize);

        public void ProcessRasterWindow(bool useVertices, int[] windowPos, int[] windowSize, ProcessRasterWindowSpanDelegate processWindowSpanDelegate)
        {
            if (windowPos == null)
                throw new ArgumentNullException("windowPos");
            if (windowSize == null)
                throw new ArgumentNullException("windowSize");
            if (processWindowSpanDelegate == null)
                throw new ArgumentNullException("processWindowSpanDelegate");
            if (windowPos.Length != m_raster.Dimension)
                throw new ArgumentException("The number of entries in windowPos should match the raster axis count.");
            if (windowSize.Length != m_raster.Dimension)
                throw new ArgumentException("The number of entries in windowSize should match the raster axis count.");

            int dimension = m_raster.Dimension;

            int[] rasterTileOfs = new int[dimension];
            int[] windowOfs = new int[dimension];
            int[] blockSize = new int[dimension];

            ProcessRasterWindowDimensionDelegate processRasterWindowDelegate = CreateProcessRasterWindowDimensionDelegate(dimension - 1, useVertices, windowPos, windowSize, processWindowSpanDelegate, rasterTileOfs, windowOfs, blockSize);

            processRasterWindowDelegate(0);
        }

        public void ProcessVertexWindow(int[] windowPos, int[] windowSize, ProcessRasterWindowSpanDelegate processWindowSpanDelegate)
        {
            ProcessRasterWindow(true, windowPos, windowSize, processWindowSpanDelegate);
        }

        public void ProcessCellWindow(int[] windowPos, int[] windowSize, ProcessRasterWindowSpanDelegate processWindowSpanDelegate)
        {
            ProcessRasterWindow(false, windowPos, windowSize, processWindowSpanDelegate);
        }

        public void ProcessVertexWindow2D(int windowStartX, int windowStartY, int windowWidth, int windowHeight, ProcessRasterWindowSpanDelegate processWindowSpanDelegate)
        {
            ProcessVertexWindow(new int[] { windowStartX, windowStartY }, new int[] { windowWidth, windowHeight }, processWindowSpanDelegate);
        }

        public void ProcessCellWindow2D(int windowStartX, int windowStartY, int windowWidth, int windowHeight, ProcessRasterWindowSpanDelegate processWindowSpanDelegate)
        {
            ProcessCellWindow(new int[] { windowStartX, windowStartY }, new int[] { windowWidth, windowHeight }, processWindowSpanDelegate);
        }

        #endregion

        #region Protected access methods.

        protected delegate int GetIndexDelegate(int[] position);

        protected void SetGetIndexDelegate(GetIndexDelegate getIndexDelegate)
        {
            m_getIndexDelegate = getIndexDelegate;

            DetermineTileGeometry();
        }

        #endregion

        #region Private methods.

        private void DetermineTileGeometry()
        {
            int dimension = m_raster.Dimension;

            m_tileSize = new int[dimension];
            int[] position = new int[dimension];

            m_tileIndexCount = 1;

            for (int d = 0; d < dimension; ++d) {
                int axisSize = m_raster.GetVertexIndexCount(d);

                int tileSize;
                position[d] = axisSize - 1;
                int testIndex = m_getIndexDelegate(position);

                if (testIndex == position[d] * m_tileIndexCount) {
                    //  The whole slice is in a single tile.
                    //  Check to see if the tile size is larger than the raster
                    //  size.

                    //  First find the next highest non-trivial dimension.
                    int nextD = d + 1;
                    while (nextD < dimension && m_raster.GetVertexIndexCount(nextD) <= 1) {
                        ++nextD;
                    }

                    if (nextD < dimension) {
                        //  We can query the first item in the next slice
                        //  to see how big the tile actually is in this
                        //  axis.
                        position[d] = 0;
                        position[d + 1] = 1;
                        testIndex = m_getIndexDelegate(position);
                        tileSize = testIndex / m_tileIndexCount;
                        position[d + 1] = 0;
                    } else {
                        //  Since there are no more non-trivial dimensions, we
                        //  assume that there is no further tiling in these
                        //  dimensions.
                        tileSize = position[d] / m_tileIndexCount + 1;
                    }

                } else {
                    //  There is a tile boundary somewhere.
                    //  Use binary search to find it.
                    int upperBound = position[d];
                    int lowerBound = 0;

                    while (upperBound > lowerBound + 1) {
                        position[d] = (lowerBound + upperBound) / 2;
                        testIndex = m_getIndexDelegate(position);
                        if (testIndex == position[d] * m_tileIndexCount) {
                            //  Position is before tile boundary.
                            lowerBound = position[d];
                        } else {
                            //  Position is after tile boundary.
                            upperBound = position[d];
                        }
                    }

                    tileSize = upperBound;
                }

                position[d] = 0;
                m_tileSize[d] = tileSize;
                m_tileIndexCount *= tileSize;
            }

            //  We have determined the tile size in each dimension and the
            //  minimum tile index count, but we now check that actual index
            //  separation between tiles in order to account for any tile
            //  padding.

            for (int d = 0; d < dimension; ++d) {
                if (m_tileSize[d] < m_raster.GetVertexIndexCount(d)) {
                    //  This is the first dimension in which the tile size is
                    //  less than the raster size.  Therefore the second tile
                    //  in the raster will start at position m_tileSize[d]
                    //  along this axis.
                    position[d] = m_tileSize[d];
                    int testIndex = m_getIndexDelegate(position);
                    m_tileIndexCount = testIndex;
                    position[d] = 0;
                    break;
                }
            }

            //  If we did not find an axis in which the tile size was smaller
            //  than the raster size then the entire raster is contained in a
            //  single tile.  It therefore does not matter to us how big the
            //  tile actually is, since we will never need to increment beyond
            //  the first tile.
        }

        private delegate void ProcessRasterWindowDimensionDelegate(int rasterTileNum);
        private delegate void ProcessRasterBlockDimensionDelegate(int rasterTileBaseIndex, int rasterTileIndexOfs, int windowIndexOfs);

        private ProcessRasterWindowDimensionDelegate CreateProcessRasterWindowDimensionDelegate(int dimension, bool useVertices, int[] windowPos, int[] windowSize,
            ProcessRasterWindowSpanDelegate processRasterWindowSpanDelegate, int[] rasterTileOfs, int[] windowOfs, int[] blockSize)
        {
            int rasterSizeD = useVertices ? m_raster.GetVertexIndexCount(dimension) : m_raster.GetVertexIndexCount(dimension) - 1;
            int tileSizeD = m_tileSize[dimension];
            int tileCountD = (rasterSizeD + tileSizeD - 1) / tileSizeD;
            int windowSizeD = windowSize[dimension];
            int windowPosD = windowPos[dimension];

            ProcessRasterWindowDimensionDelegate innerDelegate;
            if (dimension > 0) {
                innerDelegate = CreateProcessRasterWindowDimensionDelegate(dimension - 1, useVertices, windowPos, windowSize, processRasterWindowSpanDelegate, rasterTileOfs, windowOfs, blockSize);

                return delegate(int rasterTileNum)
                {
                    int windowOfsD = 0;
                    while (windowOfsD < windowSizeD) {
                        int rasterPosD = windowPosD + windowOfsD;
                        if (rasterPosD < 0) {
                            windowOfsD += -rasterPosD;
                            continue;
                        }
                        if (rasterPosD >= rasterSizeD)
                            break;

                        int rasterTileNumD = rasterPosD / tileSizeD;
                        int rasterTileOfsD = rasterPosD % tileSizeD;

                        int innerRasterTileNum = rasterTileNum * tileCountD + rasterTileNumD;

                        int nextRasterPosD = (rasterTileNumD + 1) * tileSizeD;
                        if (nextRasterPosD > rasterSizeD)
                            nextRasterPosD = rasterSizeD;
                        if (nextRasterPosD > windowPosD + windowSizeD)
                            nextRasterPosD = windowPosD + windowSizeD;

                        rasterTileOfs[dimension] = rasterTileOfsD;
                        windowOfs[dimension] = windowOfsD;
                        blockSize[dimension] = nextRasterPosD - rasterPosD;

                        innerDelegate(innerRasterTileNum);

                        windowOfsD += nextRasterPosD - rasterPosD;
                    }
                };
            } else {
                int rasterTileIndexCount = m_tileIndexCount;

                int rasterDimension = m_raster.Dimension;

                int[] blockRasterTileOfs = new int[rasterDimension];
                int[] blockWindowOfs = new int[rasterDimension];

                ProcessRasterBlockDimensionDelegate processRasterBlockDelegate = CreateProcessBlockDimensionDelegate(rasterDimension - 1, windowPos, windowSize, processRasterWindowSpanDelegate, rasterTileOfs, blockRasterTileOfs, windowOfs, blockWindowOfs, blockSize);

                return delegate(int rasterTileNum)
                {
                    int windowOfsD = 0;
                    while (windowOfsD < windowSizeD) {
                        int rasterPosD = windowPosD + windowOfsD;
                        if (rasterPosD < 0) {
                            windowOfsD += -rasterPosD;
                            continue;
                        }
                        if (rasterPosD >= rasterSizeD)
                            break;

                        int rasterTileNumD = rasterPosD / tileSizeD;
                        int rasterTileOfsD = rasterPosD % tileSizeD;

                        int innerRasterTileNum = rasterTileNum * tileCountD + rasterTileNumD;

                        int nextRasterPosD = (rasterTileNumD + 1) * tileSizeD;
                        if (nextRasterPosD > rasterSizeD)
                            nextRasterPosD = rasterSizeD;
                        if (nextRasterPosD > windowPosD + windowSizeD)
                            nextRasterPosD = windowPosD + windowSizeD;

                        rasterTileOfs[dimension] = rasterTileOfsD;
                        windowOfs[dimension] = windowOfsD;
                        blockSize[dimension] = nextRasterPosD - rasterPosD;

                        int rasterTileBaseIndex = innerRasterTileNum * rasterTileIndexCount;

                        processRasterBlockDelegate(rasterTileBaseIndex, 0, 0);

                        windowOfsD += nextRasterPosD - rasterPosD;
                    }
                };
            }
        }

        private ProcessRasterBlockDimensionDelegate CreateProcessBlockDimensionDelegate(int dimension, int[] windowPos, int[] windowSize,
            ProcessRasterWindowSpanDelegate processWindowSpanDelegate, int[] rasterTileOfs, int[] blockRasterTileOfs, int[] windowOfs, int[] blockWindowOfs, int[] blockSize)
        {
            int rasterTileSizeD = m_tileSize[dimension];
            int windowSizeD = windowSize[dimension];

            if (dimension > 0) {
                ProcessRasterBlockDimensionDelegate innerDelegate = CreateProcessBlockDimensionDelegate(dimension - 1, windowPos, windowSize, processWindowSpanDelegate, rasterTileOfs, blockRasterTileOfs, windowOfs, blockWindowOfs, blockSize);

                return delegate(int rasterTileBaseIndex, int rasterTileIndexOfs, int windowIndexOfs)
                {
                    int rasterTileOfsD = rasterTileOfs[dimension];
                    int windowOfsD = windowOfs[dimension];
                    int blockSizeD = blockSize[dimension];

                    for (int blockOfsD = 0; blockOfsD < blockSizeD; ++rasterTileOfsD, ++windowOfsD, ++blockOfsD) {
                        int innerRasterTileIndexOfs = rasterTileIndexOfs * rasterTileSizeD + rasterTileOfsD;
                        int innerWindowIndexOfs = windowIndexOfs * windowSizeD + windowOfsD;

                        blockRasterTileOfs[dimension] = rasterTileOfsD;
                        blockWindowOfs[dimension] = windowOfsD;

                        innerDelegate(rasterTileBaseIndex, innerRasterTileIndexOfs, innerWindowIndexOfs);
                    }
                };
            } else {
                return delegate(int rasterTileBaseIndex, int rasterTileIndexOfs, int windowIndexOfs)
                {
                    int rasterTileOfsD = rasterTileOfs[dimension];
                    int windowOfsD = windowOfs[dimension];
                    int blockSizeD = blockSize[dimension];

                    int innerRasterTileIndexOfs = rasterTileIndexOfs * rasterTileSizeD + rasterTileOfsD;
                    int innerWindowIndexOfs = windowIndexOfs * windowSizeD + windowOfsD;

                    blockRasterTileOfs[dimension] = rasterTileOfsD;
                    blockWindowOfs[dimension] = windowOfsD;

                    int rasterIndex = rasterTileBaseIndex + innerRasterTileIndexOfs;

                    processWindowSpanDelegate(rasterIndex, blockRasterTileOfs, innerWindowIndexOfs, blockWindowOfs, blockSizeD);
                };
            }
        }

        #endregion

        #region Private fields.

        private IRaster m_raster;

        private GetIndexDelegate m_getIndexDelegate;
        private int[] m_tileSize;
        private int m_tileIndexCount;

        #endregion

    }



    public class CellRasterHelper<CellType, VertexType> : RasterHelper
        where CellType : ICellRasterCell<CellType, VertexType>
        where VertexType : ICellRasterVertex<CellType, VertexType>
    {

        #region Public properties.

        public ICellRaster<CellType, VertexType> Raster { get; private set; }

        #endregion

        #region Constructors.

        public CellRasterHelper(ICellRaster<CellType, VertexType> raster)
            : base(raster)
        {
            if (raster == null) {
                throw new ArgumentNullException("raster");
            }

            Raster = raster;

            IRasterVertexHandle<VertexType> vertexHandle = Raster.CreateVertexHandle();

            GetIndexDelegate getVertexIndexDelegate = delegate(int[] position) { return vertexHandle.MoveTo(position).Vertex.RowIndex; };

            SetGetIndexDelegate(getVertexIndexDelegate);
        }

        #endregion

    }



    public class ContinuousRasterHelper<VertexType> : RasterHelper
        where VertexType : IContinuousRasterVertex<VertexType>
    {

        #region Public properties.

        public IContinuousRaster<VertexType> Raster { get; private set; }

        #endregion

        #region Constructors.

        public ContinuousRasterHelper(IContinuousRaster<VertexType> raster)
            : base(raster)
        {
            if (raster == null) {
                throw new ArgumentNullException("raster");
            }

            Raster = raster;

            IRasterVertexHandle<VertexType> vertexHandle = Raster.CreateVertexHandle();

            GetIndexDelegate getVertexIndexDelegate = delegate(int[] position) { return vertexHandle.MoveTo(position).Vertex.RowIndex; };

            SetGetIndexDelegate(getVertexIndexDelegate);
        }

        #endregion

    }

}
