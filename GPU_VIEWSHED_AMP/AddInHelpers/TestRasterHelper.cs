using System;
using Myriax.Eonfusion.API;
using Myriax.Eonfusion.API.Data;
using Myriax.Eonfusion.API.Helpers;
using Myriax.Eonfusion.API.Properties;
using Myriax.Eonfusion.API.UI.Descriptors;

namespace AddInHelpers
{

    public class TestRasterHelper : IAddIn
    {

        #region Execute method.

        public IDataset Execute(IApplication application)
        {
            TestCellRaster cellRaster = application.InputDatasets.BindData(RasterBindProperty);

            CellRasterHelper<TestCellRasterCell, TestCellRasterVertex> cellRasterHelper = new CellRasterHelper<TestCellRasterCell, TestCellRasterVertex>(cellRaster);

            string tileSizeStr = "";
            for (int d = 0; d < cellRaster.Dimension; ++d) {
                if (d > 0)
                    tileSizeStr += " x ";
                tileSizeStr += cellRasterHelper.TileSize[d].ToString();
            }

            application.EventGroup.InsertInfoEvent(string.Format("Tile size: {0}.", tileSizeStr));
            application.EventGroup.InsertInfoEvent(string.Format("Tile index count: {0}.", cellRasterHelper.TileIndexCount));

            ITable<TestCellRasterCell> cellTable = cellRaster.CellTable;

            cellRasterHelper.ProcessCellWindow2D(50, 50, 900, 900, delegate(int rasterIndex, int[] rasterTileOfs, int windowIndexOfs, int[] windowOfs, int spanSize)
            {
                double y = windowOfs[1] / 50.0;
                double cosY = Math.Cos(y);

                for (int i = 0; i < spanSize; ++i) {
                    double x = (windowOfs[0] + i) / 50.0;
                    cellTable[rasterIndex + i].Luminance = (byte)(Math.Cos(x) * cosY * 127.5 + 127.5);
                }
            });

            return application.InputDatasets[0];
        }

        #endregion

        #region AddIn properties.

        public BindingProperty<TestCellRaster> RasterBindProperty = new BindingProperty<TestCellRaster> { PropertyName = "Raster", PropertyDescription = "Cell raster to use for test.", DefaultValue = "", InputSocketIndex = 0 };

        #endregion

        #region AddIn description.

        public int InputSocketCount
        {
            get { return 1; }
        }

        public string Name
        {
            get { return "Test AddIn RasterHelper"; }
        }

        public string Category
        {
            get { return "add-in helper"; }
        }

        public string Description
        {
            get { return "Test operator for Myriax.Eonfusion.API.Helpers.RasterHelper."; }
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

    }

}
