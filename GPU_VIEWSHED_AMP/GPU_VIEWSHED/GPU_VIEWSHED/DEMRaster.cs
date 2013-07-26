using System;
using Myriax.Eonfusion.API;
using Myriax.Eonfusion.API.Binding;
using Myriax.Eonfusion.API.Data;

namespace GPU_VIEWSHED
{

    partial interface DEMRasterVertex
    {

        [BindInstruction(BindType = BindType.UI, DefaultValue = "X", PropertyDescription = "Attribute to use for DEM X values.", PropertyName = "DEM X attribute")]
        double X { get; set; }

        [BindInstruction(BindType = BindType.UI, DefaultValue = "Y", PropertyDescription = "Attribute to use for DEM Y values.", PropertyName = "DEM Y attribute")]
        double Y { get; set; }

        [BindInstruction(BindType = BindType.UI, DefaultValue = "Z", PropertyDescription = "Attribute to use for DEM Z values.", PropertyName = "DEM Z attribute")]
        double Z { get; set; }

        [BindInstruction(BindType = BindType.CreateIfMissing, DefaultValue = "Visible")]
        bool Visible { get; set; }

        [BindInstruction(BindType = BindType.CreateIfMissing, DefaultValue = "VisibleInt")]
        int VisibleInt { get; set; }

    }

}