using System.Runtime.InteropServices;

namespace ChickenWithLips.ArteryFont;

public class Font<T> where T : struct
{
    public MetadataFormat MetadataFormat { get; }
    public string Metadata { get; }
    public FontVariant<T>[] Variants { get; }
    public FontImage[] Images { get; }
    public FontAppendix[] Appendices { get; }

    public Font(MetadataFormat metadataFormat, string metadata, in FontVariant<T>[] variants, in FontImage[] images, in FontAppendix[] appendices)
    {
        MetadataFormat = metadataFormat;
        Metadata = metadata;
        Variants = variants;
        Images = images;
        Appendices = appendices;
    }
}

public class FontVariant<T>
    where T : struct
{
    public uint Flags { get; init; }
    public uint Weight { get; init; }
    public CodepointType CodepointType { get; init; }
    public ImageType ImageType { get; init; }
    public uint FallbackVariant { get; init; }
    public uint FallbackGlyph { get; init; }


    public FontVariantMetrics<T> Metrics { get; init; }
    public string Name { get; init; }
    public string Metadata { get; init; }
    public Glyph<T>[] Glyphs { get; init; }
    public KernPair<T>[] KernPairs { get; init; }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct FontVariantMetrics<T>
        where T : struct
    {
        // In pixels:
        public T FontSize { get; init; }
        public T DistanceRange { get; init; }

        // Proportional to font size:
        public T EmSize { get; init; }
        public T Ascender { get; init; }
        public T Descender { get; init; }
        public T LineHeight { get; init; }
        public T UnderlineY { get; init; }
        public T UnderlineThickness { get; init; }
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct Glyph<T>
    where T : struct
{
    public readonly uint Codepoint;
    public readonly uint Image;
    public readonly GlyphBounds<T> PlaneBounds;
    public readonly GlyphBounds<T> ImageBounds;
    public readonly GlyphAdvance<T> Advance;

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct GlyphBounds<T>
        where T : struct
    {
        public readonly T Left;
        public readonly T Bottom;
        public readonly T Right;
        public readonly T Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct GlyphAdvance<T>
        where T : struct
    {
        public readonly T Horizontal;
        public readonly T Vertical;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct KernPair<T>
    where T : struct
{
    public readonly uint Codepoint1;
    public readonly uint Codepoint2;

    public readonly KernPairAdvance<T> Advance;

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct KernPairAdvance<T>
        where T : struct
    {
        public readonly T Horizontal;
        public readonly T Vertical;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct FontImage
{
    public uint Flags { get; init; }
    public ImageEncoding Encoding { get; init; }
    public uint Width { get; init; }
    public uint Height { get; init; }
    public uint Channels { get; init; }
    public PixelFormat PixelFormat { get; init; }
    public ImageType ImageType { get; init; }

    public uint ChildImages { get; init; }
    public uint TextureFlags { get; init; }
    public string Metadata { get; init; }
    public byte[] Data { get; init; }
    public FontImageRawBinaryFormat RawBinaryFormat { get; init; }

    [StructLayout(LayoutKind.Sequential)]
    public struct FontImageRawBinaryFormat
    {
        public uint RowLength { get; init; }
        public ImageOrientation Orientation { get; init; }
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct FontAppendix
{
    public string Metadata { get; init; }
    public byte[] Data { get; init; }
}

public enum MetadataFormat
{
    None,
    PlainText,
    Json,
}

[Flags]
public enum FontFlags
{
    Build = 0x01,
    Light = 0x02,
    ExtraBol = 0x04,
    Condensed = 0x08,
    Italic = 0x10,
    SmallCaps = 0x20,
    IconoGraphic = 0x0100,
    SansSerif = 0x0200,
    Serif = 0x0400,
    Monospace = 0x1000,
    Cursive = 0x2000,
}

public enum CodepointType
{
    Unspecified = 0,
    Unicode = 1,
    Indexed = 2,
    IconoGraphic = 14,
}

public enum ImageType
{
    None = 0,
    SrgbImage = 1,
    LinearMask = 2,
    MaskedSrgbImage = 3,
    Sdf = 4,
    Psdf = 5,
    Msdf = 6,
    Mtsdf = 7,
    MixedContent = 255,
}

public enum PixelFormat
{
    Unknown = 0,
    Boolean1 = 1,
    Unsigned8 = 8,
    Float32 = 32,
}

public enum ImageEncoding
{
    UnknownEncoding = 0,
    RawBinary = 1,
    Bmp = 4,
    Tiff = 5,
    Png = 8,
    Tga = 9,
};

public enum ImageOrientation
{
    TopDown = 1,
    BottomUp = -1,
}
