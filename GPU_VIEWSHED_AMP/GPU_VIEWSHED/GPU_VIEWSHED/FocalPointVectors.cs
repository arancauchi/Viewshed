using System;
using Myriax.Eonfusion.API;
using Myriax.Eonfusion.API.Binding;
using Myriax.Eonfusion.API.Data;

namespace Add_in1
{

    // Vertex declaration

    partial interface FocalPointVectorsVertex
    {

        [BindInstruction(BindType = BindType.UI, DefaultValue = "X", PropertyDescription = "Attribute to use for focal point X values.", PropertyName = "Focal point X attribute")]
        double X { get; set; }

        [BindInstruction(BindType = BindType.UI, DefaultValue = "Y", PropertyDescription = "Attribute to use for focal point Y values.", PropertyName = "Focal point Y attribute")]
        double Y { get; set; }

        [BindInstruction(BindType = BindType.UI, DefaultValue = "Z", PropertyDescription = "Attribute to use for focal point Z values.", PropertyName = "Focal point Z attribute")]
        double Z { get; set; }

    }


    // 0D VectorSet declaration

    partial interface FocalPointVectorsFeature0D
    {
    }

}