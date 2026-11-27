using System.Text;
using SkiaSharp;
using Sugarista.Application.Affiliates.Services;

namespace Sugarista.Infrastructure.Services;

/// <summary>
/// Self-contained ISO/IEC 18004 QR Code generator (Byte mode, EC level M,
/// versions 1..10) producing crisp vector SVG and high-DPI PNG via SkiaSharp.
/// </summary>
public sealed class AffiliateQrCodeService : IAffiliateQrCodeService
{
    private const int QuietZone = 4;

    // Version specs for EC Level M: (totalCodewords, ecPerBlock, numBlocks, alignmentCenters)
    private sealed record VersionSpec(int Version, int TotalCodewords, int EcPerBlock, int BlocksGroup1, int DataCwGroup1, int BlocksGroup2, int DataCwGroup2, int[] Alignment);

    private static readonly VersionSpec[] Specs =
    [
        new(1, 26, 10, 1, 16, 0, 0, []),
        new(2, 44, 16, 1, 28, 0, 0, [6, 18]),
        new(3, 70, 26, 1, 44, 0, 0, [6, 22]),
        new(4, 100, 18, 2, 32, 0, 0, [6, 26]),
        new(5, 134, 24, 2, 43, 0, 0, [6, 30]),
        new(6, 172, 16, 4, 27, 0, 0, [6, 34]),
        new(7, 196, 18, 4, 31, 0, 0, [6, 22, 38]),
        new(8, 242, 22, 2, 38, 2, 39, [6, 24, 42]),
        new(9, 292, 22, 3, 36, 2, 37, [6, 26, 46]),
        new(10, 346, 26, 4, 43, 1, 44, [6, 28, 50]),
    ];

    public string GenerateSvg(string content, int sizePx = 512)
    {
        var matrix = EncodeMatrix(content);
        var moduleCount = matrix.GetLength(0);
        var totalModules = moduleCount + (QuietZone * 2);
        var clampedSize = Math.Clamp(sizePx, 128, 2048);

        var sb = new StringBuilder(moduleCount * moduleCount * 16);
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {totalModules} {totalModules}\" width=\"{clampedSize}\" height=\"{clampedSize}\" shape-rendering=\"crispEdges\">");
        sb.Append($"<rect width=\"{totalModules}\" height=\"{totalModules}\" fill=\"#FFFFFF\"/>");
        sb.Append("<path fill=\"#0F172A\" d=\"");

        for (var r = 0; r < moduleCount; r++)
        {
            for (var c = 0; c < moduleCount; c++)
            {
                if (matrix[r, c])
                {
                    sb.Append($"M{c + QuietZone},{r + QuietZone}h1v1h-1z");
                }
            }
        }

        sb.Append("\"/></svg>");
        return sb.ToString();
    }

    public byte[] GeneratePng(string content, int sizePx = 1024)
    {
        var matrix = EncodeMatrix(content);
        var moduleCount = matrix.GetLength(0);
        var totalModules = moduleCount + (QuietZone * 2);
        var clampedSize = Math.Clamp(sizePx, 128, 2048);

        var scale = Math.Max(1, clampedSize / totalModules);
        var imgSize = totalModules * scale;

        using var bitmap = new SKBitmap(imgSize, imgSize, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        using var paint = new SKPaint
        {
            Color = new SKColor(15, 23, 42),
            Style = SKPaintStyle.Fill,
            IsAntialias = false,
        };

        for (var r = 0; r < moduleCount; r++)
        {
            for (var c = 0; c < moduleCount; c++)
            {
                if (matrix[r, c])
                {
                    canvas.DrawRect(
                        (c + QuietZone) * scale,
                        (r + QuietZone) * scale,
                        scale,
                        scale,
                        paint);
                }
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    internal static bool[,] EncodeMatrix(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("QR content cannot be empty.", nameof(content));
        }

        var payload = Encoding.UTF8.GetBytes(content.Trim());
        var spec = SelectVersion(payload.Length);
        var dataCodewords = BuildDataCodewords(payload, spec);
        var fullCodewords = InterleaveWithEcc(dataCodewords, spec);

        var size = (spec.Version * 4) + 17;
        var modules = new bool[size, size];
        var isFunction = new bool[size, size];

        DrawFunctionPatterns(modules, isFunction, spec);
        PlaceCodewords(modules, isFunction, fullCodewords);

        // Apply Mask 0 ((r + c) % 2 == 0) + format info for EC Level M (00), Mask 0 (000) -> 0x5412
        for (var r = 0; r < size; r++)
        {
            for (var c = 0; c < size; c++)
            {
                if (!isFunction[r, c] && ((r + c) % 2 == 0))
                {
                    modules[r, c] = !modules[r, c];
                }
            }
        }

        DrawFormatBits(modules, 0x5412);
        if (spec.Version >= 7)
        {
            DrawVersionBits(modules, spec.Version);
        }

        return modules;
    }

    private static VersionSpec SelectVersion(int byteLength)
    {
        foreach (var spec in Specs)
        {
            var dataCapacityBytes = (spec.BlocksGroup1 * spec.DataCwGroup1) + (spec.BlocksGroup2 * spec.DataCwGroup2);
            var headerBits = 4 + (spec.Version <= 9 ? 8 : 16);
            var maxPayloadBytes = ((dataCapacityBytes * 8) - headerBits) / 8;
            if (byteLength <= maxPayloadBytes)
            {
                return spec;
            }
        }

        throw new ArgumentException("QR content exceeds maximum supported length (213 UTF-8 bytes).");
    }

    private static byte[] BuildDataCodewords(byte[] payload, VersionSpec spec)
    {
        var dataCapacityBytes = (spec.BlocksGroup1 * spec.DataCwGroup1) + (spec.BlocksGroup2 * spec.DataCwGroup2);
        var bits = new List<bool>(dataCapacityBytes * 8);

        void AppendBits(int value, int count)
        {
            for (var i = count - 1; i >= 0; i--)
            {
                bits.Add(((value >> i) & 1) != 0);
            }
        }

        // Mode indicator: Byte mode = 0100
        AppendBits(0b0100, 4);
        // Character count indicator
        AppendBits(payload.Length, spec.Version <= 9 ? 8 : 16);

        foreach (var b in payload)
        {
            AppendBits(b, 8);
        }

        var capacityBits = dataCapacityBytes * 8;
        var terminator = Math.Min(4, capacityBits - bits.Count);
        for (var i = 0; i < terminator; i++) bits.Add(false);
        while (bits.Count % 8 != 0) bits.Add(false);

        var bytes = new List<byte>(dataCapacityBytes);
        for (var i = 0; i < bits.Count; i += 8)
        {
            byte val = 0;
            for (var j = 0; j < 8; j++)
            {
                val = (byte)((val << 1) | (bits[i + j] ? 1 : 0));
            }
            bytes.Add(val);
        }

        byte[] padBytes = [0xEC, 0x11];
        var padIndex = 0;
        while (bytes.Count < dataCapacityBytes)
        {
            bytes.Add(padBytes[padIndex++ % 2]);
        }

        return bytes.ToArray();
    }

    private static byte[] InterleaveWithEcc(byte[] data, VersionSpec spec)
    {
        var generator = ComputeReedSolomonGenerator(spec.EcPerBlock);
        var dataBlocks = new List<byte[]>();
        var eccBlocks = new List<byte[]>();

        var offset = 0;
        for (var i = 0; i < spec.BlocksGroup1; i++)
        {
            var block = data.AsSpan(offset, spec.DataCwGroup1).ToArray();
            offset += spec.DataCwGroup1;
            dataBlocks.Add(block);
            eccBlocks.Add(ComputeReedSolomonRemainder(block, generator));
        }

        for (var i = 0; i < spec.BlocksGroup2; i++)
        {
            var block = data.AsSpan(offset, spec.DataCwGroup2).ToArray();
            offset += spec.DataCwGroup2;
            dataBlocks.Add(block);
            eccBlocks.Add(ComputeReedSolomonRemainder(block, generator));
        }

        var result = new List<byte>(spec.TotalCodewords);
        var maxDataLen = Math.Max(spec.DataCwGroup1, spec.DataCwGroup2);
        for (var i = 0; i < maxDataLen; i++)
        {
            foreach (var block in dataBlocks)
            {
                if (i < block.Length) result.Add(block[i]);
            }
        }

        for (var i = 0; i < spec.EcPerBlock; i++)
        {
            foreach (var ecc in eccBlocks)
            {
                result.Add(ecc[i]);
            }
        }

        return result.ToArray();
    }

    private static byte[] ComputeReedSolomonGenerator(int degree)
    {
        var poly = new byte[degree];
        poly[degree - 1] = 1;
        byte root = 1;
        for (var i = 0; i < degree; i++)
        {
            for (var j = 0; j < degree; j++)
            {
                poly[j] = GfMultiply(poly[j], root);
                if (j + 1 < degree)
                {
                    poly[j] ^= poly[j + 1];
                }
            }
            root = GfMultiply(root, 0x02);
        }
        return poly;
    }

    private static byte[] ComputeReedSolomonRemainder(byte[] data, byte[] generator)
    {
        var rem = new byte[generator.Length];
        foreach (var b in data)
        {
            var factor = (byte)(b ^ rem[0]);
            Array.Copy(rem, 1, rem, 0, rem.Length - 1);
            rem[^1] = 0;
            for (var i = 0; i < generator.Length; i++)
            {
                rem[i] ^= GfMultiply(generator[i], factor);
            }
        }
        return rem;
    }

    private static byte GfMultiply(byte x, byte y)
    {
        var z = 0;
        for (var i = 7; i >= 0; i--)
        {
            z = (z << 1) ^ ((z >> 7) * 0x11D);
            z ^= ((y >> i) & 1) * x;
        }
        return (byte)z;
    }

    private static void DrawFunctionPatterns(bool[,] m, bool[,] f, VersionSpec spec)
    {
        var size = m.GetLength(0);

        void SetFn(int r, int c, bool dark)
        {
            if (r < 0 || c < 0 || r >= size || c >= size) return;
            m[r, c] = dark;
            f[r, c] = true;
        }

        void DrawFinder(int top, int left)
        {
            for (var dr = -1; dr <= 7; dr++)
            {
                for (var dc = -1; dc <= 7; dc++)
                {
                    var dist = Math.Max(Math.Abs(dr - 3), Math.Abs(dc - 3));
                    SetFn(top + dr, left + dc, dist != 2 && dist != 4);
                }
            }
        }

        DrawFinder(0, 0);
        DrawFinder(0, size - 7);
        DrawFinder(size - 7, 0);

        // Timing patterns
        for (var i = 8; i < size - 8; i++)
        {
            SetFn(6, i, i % 2 == 0);
            SetFn(i, 6, i % 2 == 0);
        }

        // Alignment patterns
        foreach (var r in spec.Alignment)
        {
            foreach (var c in spec.Alignment)
            {
                if (f[r, c]) continue;
                for (var dr = -2; dr <= 2; dr++)
                {
                    for (var dc = -2; dc <= 2; dc++)
                    {
                        SetFn(r + dr, c + dc, Math.Max(Math.Abs(dr), Math.Abs(dc)) != 1);
                    }
                }
            }
        }

        // Reserve format info areas & dark module
        for (var i = 0; i <= 8; i++)
        {
            if (i != 6)
            {
                f[8, i] = true;
                f[i, 8] = true;
            }
        }
        for (var i = size - 8; i < size; i++)
        {
            f[8, i] = true;
            f[i, 8] = true;
        }
        SetFn(size - 8, 8, true);

        // Reserve version info areas (v7+)
        if (spec.Version >= 7)
        {
            for (var i = 0; i < 6; i++)
            {
                for (var j = 0; j < 3; j++)
                {
                    f[i, size - 11 + j] = true;
                    f[size - 11 + j, i] = true;
                }
            }
        }
    }

    private static void PlaceCodewords(bool[,] m, bool[,] f, byte[] codewords)
    {
        var size = m.GetLength(0);
        var bitIdx = 0;
        var totalBits = codewords.Length * 8;

        for (var right = size - 1; right >= 1; right -= 2)
        {
            if (right == 6) right = 5;
            for (var vert = 0; vert < size; vert++)
            {
                var upward = ((right + 1) & 2) == 0;
                var r = upward ? size - 1 - vert : vert;
                for (var j = 0; j < 2; j++)
                {
                    var c = right - j;
                    if (f[r, c]) continue;
                    var dark = false;
                    if (bitIdx < totalBits)
                    {
                        dark = ((codewords[bitIdx >> 3] >> (7 - (bitIdx & 7))) & 1) != 0;
                        bitIdx++;
                    }
                    m[r, c] = dark;
                }
            }
        }
    }

    private static void DrawFormatBits(bool[,] m, int bits)
    {
        var size = m.GetLength(0);
        for (var i = 0; i <= 5; i++) m[8, i] = ((bits >> i) & 1) != 0;
        m[8, 7] = ((bits >> 6) & 1) != 0;
        m[8, 8] = ((bits >> 7) & 1) != 0;
        m[7, 8] = ((bits >> 8) & 1) != 0;
        for (var i = 9; i < 15; i++) m[14 - i, 8] = ((bits >> i) & 1) != 0;

        for (var i = 0; i < 8; i++) m[size - 1 - i, 8] = ((bits >> i) & 1) != 0;
        for (var i = 8; i < 15; i++) m[8, size - 15 + i] = ((bits >> i) & 1) != 0;
    }

    private static void DrawVersionBits(bool[,] m, int version)
    {
        var rem = version;
        for (var i = 0; i < 12; i++)
        {
            rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
        }
        var bits = (version << 12) | rem;
        var size = m.GetLength(0);
        for (var i = 0; i < 18; i++)
        {
            var dark = ((bits >> i) & 1) != 0;
            var a = size - 11 + (i % 3);
            var b = i / 3;
            m[a, b] = dark;
            m[b, a] = dark;
        }
    }
}
