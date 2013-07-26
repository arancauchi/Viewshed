using System;
using Myriax.Eonfusion.API;
using Myriax.Eonfusion.API.Binding;
using Myriax.Eonfusion.API.Data;

namespace Add_in1
{
    public partial interface FocalPointVectors :
        IVectorSetGroup<FocalPointVectorsFeature2D, FocalPointVectorsPrimitive2D, FocalPointVectorsFeature1D, FocalPointVectorsPrimitive1D, FocalPointVectorsFeature0D, FocalPointVectorsPrimitive0D, FocalPointVectorsVertex> { }

    public partial interface FocalPointVectorsVertex :
        IVertex0D<FocalPointVectorsFeature0D, FocalPointVectorsPrimitive0D, FocalPointVectorsVertex>,
        IVertex1D<FocalPointVectorsFeature1D, FocalPointVectorsPrimitive1D, FocalPointVectorsVertex>,
        IVertex2D<FocalPointVectorsFeature2D, FocalPointVectorsPrimitive2D, FocalPointVectorsVertex> { }

    public partial interface FocalPointVectorsFeature0D :
        IFeature0D<FocalPointVectorsFeature0D, FocalPointVectorsPrimitive0D, FocalPointVectorsVertex> { }

    public partial interface FocalPointVectorsPrimitive0D :
        IPrimitive0D<FocalPointVectorsFeature0D, FocalPointVectorsPrimitive0D, FocalPointVectorsVertex> { }

    public partial interface FocalPointVectorsFeature1D :
        IFeature1D<FocalPointVectorsFeature1D, FocalPointVectorsPrimitive1D, FocalPointVectorsVertex> { }

    public partial interface FocalPointVectorsPrimitive1D :
        IPrimitive1D<FocalPointVectorsFeature1D, FocalPointVectorsPrimitive1D, FocalPointVectorsVertex> { }

    public partial interface FocalPointVectorsFeature2D :
        IFeature2D<FocalPointVectorsFeature2D, FocalPointVectorsPrimitive2D, FocalPointVectorsVertex> { }

    public partial interface FocalPointVectorsPrimitive2D :
        IPrimitive2D<FocalPointVectorsFeature2D, FocalPointVectorsPrimitive2D, FocalPointVectorsVertex> { }

}
