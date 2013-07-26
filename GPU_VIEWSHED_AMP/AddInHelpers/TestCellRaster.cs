using Myriax.Eonfusion.API.Binding;

namespace AddInHelpers
{

    partial interface TestCellRasterCell
    {

        [BindInstruction(BindType = BindType.UI, DefaultValue = "Luminance", PropertyDescription = "Luminance attribute.", PropertyName = "Luminance attribute")]
        byte Luminance { get; set; }

    }

    partial interface TestCellRasterVertex
    {
    }

}