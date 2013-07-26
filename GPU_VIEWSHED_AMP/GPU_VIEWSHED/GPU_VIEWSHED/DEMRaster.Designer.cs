using System;
using Myriax.Eonfusion.API;
using Myriax.Eonfusion.API.Binding;
using Myriax.Eonfusion.API.Data;

namespace GPU_VIEWSHED
{

    public interface DEMRaster :
        IContinuousRaster<DEMRasterVertex> { }

    public partial interface DEMRasterVertex :
        IContinuousRasterVertex<DEMRasterVertex> { }

}