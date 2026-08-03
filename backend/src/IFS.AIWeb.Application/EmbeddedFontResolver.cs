using System.Reflection;
using PdfSharp.Fonts;

namespace IFS.AIWeb.Application;

public sealed class EmbeddedFontResolver : IFontResolver
{
    public static readonly EmbeddedFontResolver Instance = new();
    private static readonly Lazy<byte[]> RegularFont = new(() => LoadResource("IFS.AIWeb.Application.Assets.Fonts.NotoSans-Regular.ttf"));
    private static readonly Lazy<byte[]> BoldFont = new(() => LoadResource("IFS.AIWeb.Application.Assets.Fonts.NotoSans-Bold.ttf"));

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var name = isBold ? "NotoSans-Bold" : "NotoSans-Regular";
        return new FontResolverInfo(name);
    }

    public byte[]? GetFont(string faceName)
    {
        if (faceName.Equals("NotoSans-Bold", StringComparison.OrdinalIgnoreCase))
            return BoldFont.Value;
        return RegularFont.Value;
    }

    private static byte[] LoadResource(string name)
    {
        var assembly = typeof(EmbeddedFontResolver).Assembly;
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded font resource '{name}' not found.");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
