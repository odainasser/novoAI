using ZXing;
using ZXing.Common;
using ZXing.Rendering;

namespace Web.Services.Barcode;

public static class Code128SvgGenerator
{
    public static string Generate(string value, int width = 760, int height = 170)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var writer = new BarcodeWriterSvg
        {
            Format = BarcodeFormat.CODE_128,
            Options = new EncodingOptions
            {
                Width = width,
                Height = height,
                Margin = 6,
                PureBarcode = true
            }
        };

        var svg = writer.Write(value.Trim());
        return svg.Content;
    }
}
