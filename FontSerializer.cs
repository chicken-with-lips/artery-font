using System.Buffers;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace ChickenWithLips.ArteryFont;

public class FontSerializer<T>
    where T : struct
{
    public static FontSerializer<T> Default = new();

    public Font<T> Deserialize(ReadOnlySpan<byte> source)
    {
        uint cursorPosition = 0;
        uint prevCursorPosition = 0;

        uint variantCount = 0;
        uint imageCount = 0;
        uint appendixCount = 0;
        uint variantsLength = 0;
        uint imagesLength = 0;
        uint appendicesLength = 0;

        var metadataFormat = MetadataFormat.None;
        string metadata;

        // Read header
        {
            var header = Read<FontHeader>(source, ref cursorPosition);

            AssertHeaderTag(header);
            AssertHeaderMagicNumber(header);
            AssertHeaderRealType<T>(header);

            metadataFormat = (MetadataFormat)header.MetadataFormat;
            metadata = ReadString(source, header.MetadataLength, ref cursorPosition);

            variantCount = header.VariantCount;
            imageCount = header.ImageCount;
            appendixCount = header.AppendixCount;
            variantsLength = header.VariantsLength;
            imagesLength = header.ImagesLength;
            appendicesLength = header.AppendicesLength;
        }

        var variants = new FontVariant<T>[variantCount];
        var images = new FontImage[imageCount];
        var appendices = new FontAppendix[appendixCount];

        prevCursorPosition = cursorPosition;

        // Read variants
        for (int i = 0; i < variantCount; ++i) {
            var header = Read<FontVariantHeader<T>>(source, ref cursorPosition);
            var glyphs = new Glyph<T>[header.GlyphCount];
            var kernPairs = new KernPair<T>[header.GlyphCount];

            for (int j = 0; j < header.GlyphCount; j++) {
                glyphs[j] = Read<Glyph<T>>(source, ref cursorPosition);
            }

            for (int j = 0; j < header.KernPairCount; j++) {
                kernPairs[j] = Read<KernPair<T>>(source, ref cursorPosition);
            }

            variants[i] = new FontVariant<T>() {
                Flags = header.Flags,
                Weight = header.Weight,
                CodepointType = (CodepointType)header.CodepointType,
                ImageType = (ImageType)header.ImageType,
                FallbackVariant = header.FallbackVariant,
                FallbackGlyph = header.FallbackGlyph,
                Name = ReadString(source, header.NameLength, ref cursorPosition),
                Metadata = ReadString(source, header.MetadataLength, ref cursorPosition),
                Glyphs = glyphs,
                KernPairs = kernPairs,
                Metrics = new FontVariant<T>.FontVariantMetrics<T> {
                    FontSize = header.Metrics0,
                    DistanceRange = header.Metrics1,

                    // Proportional to font size:
                    EmSize = header.Metrics2,
                    Ascender = header.Metrics3,
                    Descender = header.Metrics4,
                    LineHeight = header.Metrics5,
                    UnderlineY = header.Metrics6,
                    UnderlineThickness = header.Metrics7,
                }
            };
        }

        if (cursorPosition - prevCursorPosition != variantsLength) {
            throw new ArteryFontException("Unexpected variants length");
        }

        prevCursorPosition = cursorPosition;

        // Read images
        for (int i = 0; i < imageCount; ++i) {
            var header = Read<ImageHeader>(source, ref cursorPosition);

            images[i] = new FontImage() {
                Flags = header.Flags,
                Encoding = (ImageEncoding)header.Encoding,
                Width = header.Width,
                Height = header.Height,
                Channels = header.Channels,
                PixelFormat = (PixelFormat)header.PixelFormat,
                ImageType = (ImageType)header.ImageType,
                RawBinaryFormat = new FontImage.FontImageRawBinaryFormat() {
                    RowLength = header.RowLength,
                    Orientation = (ImageOrientation)header.Orientation,
                },
                ChildImages = header.ChildImages,
                TextureFlags = header.TextureFlags,
                Metadata = ReadString(source, header.MetadataLength, ref cursorPosition),
                Data = ReadByteArray(source, header.DataLength, ref cursorPosition),
            };

            Realign(ref cursorPosition);
        }

        if (cursorPosition - prevCursorPosition != imagesLength) {
            throw new ArteryFontException("Unexpected images length");
        }

        prevCursorPosition = cursorPosition;

        // Read appendices
        for (int i = 0; i < appendixCount; ++i) {
            var header = Read<AppendixHeader>(source, ref cursorPosition);

            appendices[i] = new FontAppendix() {
                Metadata = ReadString(source, header.MetadataLength, ref cursorPosition),
                Data = ReadByteArray(source, header.DataLength, ref cursorPosition),
            };

            Realign(ref cursorPosition);
        }

        if (cursorPosition - prevCursorPosition != appendicesLength) {
            throw new ArteryFontException("Unexpected appendices length");
        }

        // Read footer
        {
            var footer = Read<ArteryFontFooter>(source, ref cursorPosition);

            if (footer.MagicNo != 0x55ccb363u) {
                throw new ArteryFontException("Magic number mismatch");
            }

            if (cursorPosition != footer.TotalLength) {
                throw new ArteryFontException("Unexpected cursor position");
            }
        }

        return new Font<T>(
            metadataFormat,
            metadata,
            variants,
            images,
            appendices
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private T Read<T>(ReadOnlySpan<byte> source, ref uint cursorPosition) where T : struct
    {
        uint headerSize = (uint)Unsafe.SizeOf<T>();
        var header = MemoryMarshal.Read<T>(source.Slice((int)cursorPosition, (int)headerSize));
        cursorPosition += headerSize;

        return header;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string ReadString(ReadOnlySpan<byte> source, in uint length, ref uint cursorPosition)
    {
        if (length == 0) {
            return string.Empty;
        }

        var charSource = MemoryMarshal.Cast<byte, char>(source.Slice((int)cursorPosition, (int)length));
        cursorPosition += length + 1;

        return charSource.ToString();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte[] ReadByteArray(ReadOnlySpan<byte> source, in uint length, ref uint cursorPosition)
    {
        var bytes = source.Slice((int)cursorPosition, (int)length).ToArray();
        cursorPosition += length;

        return bytes;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Realign(ref uint cursorPosition)
    {
        if ((cursorPosition & 0x03u) != 0) {
            uint len = 0x04u - (cursorPosition & 0x03u);
            cursorPosition += len;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AssertHeaderTag(in FontHeader header)
    {
        if (header.Tag0 != (byte)'A'
            || header.Tag1 != (byte)'R'
            || header.Tag2 != (byte)'T'
            || header.Tag3 != (byte)'E'
            || header.Tag4 != (byte)'R'
            || header.Tag5 != (byte)'Y'
            || header.Tag6 != (byte)'/'
            || header.Tag7 != (byte)'F'
            || header.Tag8 != (byte)'O'
            || header.Tag9 != (byte)'N'
            || header.Tag10 != (byte)'T'
            || header.Tag11 != (byte)'\0'
            || header.Tag12 != (byte)'\0'
            || header.Tag13 != (byte)'\0'
            || header.Tag14 != (byte)'\0'
            || header.Tag15 != (byte)'\0') {
            throw new ArteryFontException("Unexpected header tag value");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AssertHeaderMagicNumber(in FontHeader header)
    {
        if (header.MagicNo != 0x4d276a5cu) {
            throw new ArteryFontException("Unexpected header magic number");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AssertHeaderRealType<T>(in FontHeader header)
        where T : struct
    {
        if (header.RealType != GetRealTypeCode<T>()) {
            throw new ArteryFontException("Unexpected header real type");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint GetRealTypeCode<T>()
        where T : struct
    {
        if (typeof(T) == typeof(float)) {
            return 0x14u;
        }

        if (typeof(T) == typeof(double)) {
            return 0x18u;
        }

        throw new InvalidDataException();
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct FontHeader
    {
        public readonly byte Tag0;
        public readonly byte Tag1;
        public readonly byte Tag2;
        public readonly byte Tag3;
        public readonly byte Tag4;
        public readonly byte Tag5;
        public readonly byte Tag6;
        public readonly byte Tag7;
        public readonly byte Tag8;
        public readonly byte Tag9;
        public readonly byte Tag10;
        public readonly byte Tag11;
        public readonly byte Tag12;
        public readonly byte Tag13;
        public readonly byte Tag14;
        public readonly byte Tag15;

        public readonly uint MagicNo;
        public readonly uint Version;
        public readonly uint Flags;
        public readonly uint RealType;

        public readonly uint Reserved0;
        public readonly uint Reserved1;
        public readonly uint Reserved2;
        public readonly uint Reserved3;

        public readonly uint MetadataFormat;
        public readonly uint MetadataLength;
        public readonly uint VariantCount;
        public readonly uint VariantsLength;
        public readonly uint ImageCount;
        public readonly uint ImagesLength;
        public readonly uint AppendixCount;
        public readonly uint AppendicesLength;

        public readonly uint Reserved20;
        public readonly uint Reserved21;
        public readonly uint Reserved22;
        public readonly uint Reserved23;
        public readonly uint Reserved24;
        public readonly uint Reserved25;
        public readonly uint Reserved26;
        public readonly uint Reserved27;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct FontVariantHeader<T>
        where T : struct
    {
        public readonly uint Flags;
        public readonly uint Weight;
        public readonly uint CodepointType;
        public readonly uint ImageType;
        public readonly uint FallbackVariant;
        public readonly uint FallbackGlyph;
        public readonly uint Reserved0;
        public readonly uint Reserved1;
        public readonly uint Reserved2;
        public readonly uint Reserved3;
        public readonly uint Reserved4;
        public readonly uint Reserved5;
        public readonly T Metrics0;
        public readonly T Metrics1;
        public readonly T Metrics2;
        public readonly T Metrics3;
        public readonly T Metrics4;
        public readonly T Metrics5;
        public readonly T Metrics6;
        public readonly T Metrics7;
        public readonly T Metrics8;
        public readonly T Metrics9;
        public readonly T Metrics10;
        public readonly T Metrics11;
        public readonly T Metrics12;
        public readonly T Metrics13;
        public readonly T Metrics14;
        public readonly T Metrics15;
        public readonly T Metrics16;
        public readonly T Metrics17;
        public readonly T Metrics18;
        public readonly T Metrics19;
        public readonly T Metrics20;
        public readonly T Metrics21;
        public readonly T Metrics22;
        public readonly T Metrics23;
        public readonly T Metrics24;
        public readonly T Metrics25;
        public readonly T Metrics26;
        public readonly T Metrics27;
        public readonly T Metrics28;
        public readonly T Metrics29;
        public readonly T Metrics30;
        public readonly T Metrics31;
        public readonly uint NameLength;
        public readonly uint MetadataLength;
        public readonly uint GlyphCount;
        public readonly uint KernPairCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct ImageHeader
    {
        public readonly uint Flags;
        public readonly uint Encoding;
        public readonly uint Width;
        public readonly uint Height;
        public readonly uint Channels;
        public readonly uint PixelFormat;
        public readonly uint ImageType;
        public readonly uint RowLength;
        public readonly int Orientation;
        public readonly uint ChildImages;
        public readonly uint TextureFlags;
        public readonly uint Reserved0;
        public readonly uint Reserved1;
        public readonly uint Reserved2;
        public readonly uint MetadataLength;
        public readonly uint DataLength;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct AppendixHeader
    {
        public readonly uint MetadataLength;
        public readonly uint DataLength;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct ArteryFontFooter
    {
        public readonly uint Salt;
        public readonly uint MagicNo;
        public readonly uint Reserved0;
        public readonly uint Reserved1;
        public readonly uint Reserved2;
        public readonly uint Reserved3;
        public readonly uint TotalLength;
        public readonly uint Checksum;
    }
}
