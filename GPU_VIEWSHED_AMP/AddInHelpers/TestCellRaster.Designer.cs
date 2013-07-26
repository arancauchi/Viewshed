using System;
using Myriax.Eonfusion.API;
using Myriax.Eonfusion.API.Binding;
using Myriax.Eonfusion.API.Data;

namespace AddInHelpers
{

    public interface TestCellRaster :
        ICellRaster<TestCellRasterCell, TestCellRasterVertex> { }

    public partial interface TestCellRasterCell :
        ICellRasterCell<TestCellRasterCell, TestCellRasterVertex> { }

    public partial interface TestCellRasterVertex :
        ICellRasterVertex<TestCellRasterCell, TestCellRasterVertex> { }

}