using System;
using Myriax.Eonfusion.API;
using Myriax.Eonfusion.API.Binding;
using Myriax.Eonfusion.API.Data;

namespace AddInHelpers
{

    // Vertex declaration

    partial interface TestVectorsVertex
    {

        [BindInstruction(BindType = BindType.CreateIfMissing, DefaultValue = "X")]
        double X { get; set; }

        [BindInstruction(BindType = BindType.CreateIfMissing, DefaultValue = "Y")]
        double Y { get; set; }

        [BindInstruction(BindType = BindType.CreateIfMissing, DefaultValue = "Z")]
        double Z { get; set; }

    }


    // 2D VectorSet declaration

    partial interface TestVectorsFeature2D
    {

        [BindInstruction(BindType = BindType.CreateIfMissing, DefaultValue = "ID")]
        double ID { get; set; }

    }

    partial interface TestVectorsPrimitive2D
    {
    }

}