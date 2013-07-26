using System;
using Myriax.Eonfusion.API;
using Myriax.Eonfusion.API.Binding;
using Myriax.Eonfusion.API.Data;

namespace AddInHelpers
{
    public partial interface TestVectors :
        IVectorSetGroup<TestVectorsFeature2D, TestVectorsPrimitive2D, TestVectorsFeature1D, TestVectorsPrimitive1D, TestVectorsFeature0D, TestVectorsPrimitive0D, TestVectorsVertex> { }

    public partial interface TestVectorsVertex :
        IVertex0D<TestVectorsFeature0D, TestVectorsPrimitive0D, TestVectorsVertex>,
        IVertex1D<TestVectorsFeature1D, TestVectorsPrimitive1D, TestVectorsVertex>,
        IVertex2D<TestVectorsFeature2D, TestVectorsPrimitive2D, TestVectorsVertex> { }

    public partial interface TestVectorsFeature0D :
        IFeature0D<TestVectorsFeature0D, TestVectorsPrimitive0D, TestVectorsVertex> { }

    public partial interface TestVectorsPrimitive0D :
        IPrimitive0D<TestVectorsFeature0D, TestVectorsPrimitive0D, TestVectorsVertex> { }

    public partial interface TestVectorsFeature1D :
        IFeature1D<TestVectorsFeature1D, TestVectorsPrimitive1D, TestVectorsVertex> { }

    public partial interface TestVectorsPrimitive1D :
        IPrimitive1D<TestVectorsFeature1D, TestVectorsPrimitive1D, TestVectorsVertex> { }

    public partial interface TestVectorsFeature2D :
        IFeature2D<TestVectorsFeature2D, TestVectorsPrimitive2D, TestVectorsVertex> { }

    public partial interface TestVectorsPrimitive2D :
        IPrimitive2D<TestVectorsFeature2D, TestVectorsPrimitive2D, TestVectorsVertex> { }

}
