using System.IO.Compression;
using FFMpeg.StaticFetcher.Exceptions;
using SharpCompress.Archives;
using SharpCompress.Archives.Tar;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Xz;

namespace FFMpeg.StaticFetcher.Archives;

internal static class ArchiveExtractor
{
    private static readonly byte[] GzipMagic  = [0x1F, 0x8B];
    private static readonly byte[] ZipMagic   = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] Zip7Magic  = [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C];
    // Shared 6-byte prefix for RAR 4.x (7-byte full magic, trailing 0x00) and RAR 5.x (8-byte full magic, trailing 0x01 0x00).
    private static readonly byte[] RarMagic   = [0x52, 0x61, 0x72, 0x21, 0x1A, 0x07];
    private static readonly byte[] XzMagic    = [0xFD, 0x37, 0x7A, 0x58, 0x5A, 0x00];
    private static readonly byte[] Bzip2Magic = [0x42, 0x5A, 0x68];

    /// <summary>
    /// Inspects <paramref name="archivePath"/>, auto-detects its format, and writes the binary
    /// matching <paramref name="expectedFile"/> to <paramref name="destinationPath"/>.
    /// Supports gzip (single + tar.gz), xz (single + tar.xz), bzip2 (single + tar.bz2), zip, 7z, rar, plain tar, and raw files.
    /// </summary>
    public static void ExtractBinary(string archivePath, string expectedFile, string destinationPath,
        long maxBytes = long.MaxValue, CancellationToken cancellationToken = default)
    {
        // Write the extracted binary to a sibling ".downloading" file first so a crash or I/O error
        // never leaves a truncated binary at destinationPath that File.Exists would later trust.
        var stagingPath = destinationPath + ".downloading";

        try
        {
            if (File.Exists(stagingPath)) File.Delete(stagingPath);

            ExtractBinaryCore(archivePath, expectedFile, stagingPath, maxBytes, cancellationToken);

            // A structurally-valid archive can still yield a 0-byte binary (empty gzip content, a 0-length
            // entry, the symlink-selection case). Reject it before it can be promoted and trusted.
            if (new FileInfo(stagingPath).Length == 0)
                throw new FFMpegStaticFetcherException($"Extracted binary '{expectedFile}' was empty (0 bytes)");

            File.Move(stagingPath, destinationPath, overwrite: true);
        }
        catch
        {
            // Best-effort cleanup: a locked staging file must not replace the original extraction exception.
            try { if (File.Exists(stagingPath)) File.Delete(stagingPath); } catch { /* ignored */ }

            throw;
        }
    }

    private static void ExtractBinaryCore(string archivePath, string expectedFile, string destinationPath, long maxBytes, CancellationToken cancellationToken)
    {
        var magic = ReadBytes(archivePath, 8);

        if (StartsWith(magic, ZipMagic) || StartsWith(magic, Zip7Magic) || StartsWith(magic, RarMagic))
        {
            ExtractFromMultiEntryArchive(archivePath, expectedFile, destinationPath, maxBytes, cancellationToken);

            return;
        }

        if (StartsWith(magic, GzipMagic) || StartsWith(magic, XzMagic) || StartsWith(magic, Bzip2Magic))
        {
            var innerPath = archivePath + ".inner";

            try
            {
                DecompressStreamToFile(archivePath, innerPath, magic, maxBytes, cancellationToken);

                if (IsTarFile(innerPath)) ExtractFromTar(innerPath, expectedFile, destinationPath, maxBytes, cancellationToken);
                else
                {
                    if (File.Exists(destinationPath)) File.Delete(destinationPath);
                    File.Move(innerPath, destinationPath);
                }
            }
            finally
            {
                try { if (File.Exists(innerPath)) File.Delete(innerPath); } catch { /* ignored */ }
            }
            return;
        }

        if (IsTarFile(archivePath))
        {
            ExtractFromTar(archivePath, expectedFile, destinationPath, maxBytes, cancellationToken);
            return;
        }

        if (new FileInfo(archivePath).Length > maxBytes)
            throw new FFMpegStaticFetcherException($"Download exceeded the maximum of {maxBytes} bytes");

        if (File.Exists(destinationPath)) File.Delete(destinationPath);

        File.Copy(archivePath, destinationPath);
    }

    private static void ExtractFromMultiEntryArchive(string archivePath, string expectedFile, string destinationPath, long maxBytes, CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(archivePath);
        using var archive = ArchiveFactory.OpenArchive(stream);

        ExtractMatchingEntry(archive.Entries, "archive", expectedFile, destinationPath, maxBytes, cancellationToken);
    }

    private static void ExtractFromTar(string tarPath, string expectedFile, string destinationPath, long maxBytes, CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(tarPath);
        using var archive = TarArchive.OpenArchive(stream);

        ExtractMatchingEntry(archive.Entries, "tar archive", expectedFile, destinationPath, maxBytes, cancellationToken);
    }

    // Selects the single regular-file entry whose leaf name matches. Directories and symlink/hardlink entries
    // (non-empty LinkTarget) are excluded — they can share the binary's leaf name and would otherwise be picked first.
    private static void ExtractMatchingEntry(IEnumerable<IArchiveEntry> entries, string kindLabel, string expectedFile,
        string destinationPath, long maxBytes, CancellationToken cancellationToken)
    {
        var files = entries.Where(e => !e.IsDirectory).ToList();

        var entry = files
            .Where(e => string.IsNullOrEmpty(e.LinkTarget))
            .FirstOrDefault(e => FileNameMatches(e.Key, expectedFile));

        if (entry is null)
            throw new FFMpegStaticFetcherException(
                $"Binary '{expectedFile}' not found in {kindLabel}",
                $"Available entries: {string.Join(", ", files.Select(e => e.Key))}");

        using var entryStream = entry.OpenEntryStream();
        using var output = File.Create(destinationPath);

        CopyBounded(entryStream, output, maxBytes, cancellationToken);
    }

    private static void DecompressStreamToFile(string archivePath, string outputPath, byte[] magic, long maxBytes, CancellationToken cancellationToken)
    {
        using var input = File.OpenRead(archivePath);
        using var output = File.Create(outputPath);

        if (StartsWith(magic, GzipMagic))
        {
            using var gz = new GZipStream(input, CompressionMode.Decompress);

            CopyBounded(gz, output, maxBytes, cancellationToken);
        }
        else if (StartsWith(magic, XzMagic))
        {
            using var xz = new XZStream(input);
            CopyBounded(xz, output, maxBytes, cancellationToken);
        }
        else if (StartsWith(magic, Bzip2Magic))
        {
            using var bz = BZip2Stream.Create(input, SharpCompress.Compressors.CompressionMode.Decompress, decompressConcatenated: false);

            CopyBounded(bz, output, maxBytes, cancellationToken);
        }
        else throw new InvalidOperationException($"Unsupported compression format; magic bytes: {BitConverter.ToString(magic)}");
    }

    // Copies with a hard ceiling so a small archive can't decompress/expand to fill the disk (decompression bomb).
    private static void CopyBounded(Stream source, Stream destination, long maxBytes, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;

        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            total += read;

            if (total > maxBytes)
                throw new FFMpegStaticFetcherException($"Extraction exceeded the maximum of {maxBytes} bytes (possible decompression bomb)");

            destination.Write(buffer, 0, read);
        }
    }

    private static bool IsTarFile(string path)
    {
        using var stream = File.OpenRead(path);

        if (stream.Length < 512) return false;

        stream.Position = 257;

        var buf = (Span<byte>)stackalloc byte[5];
        var read = stream.Read(buf);

        if (read < 5) return false;

        return buf[0] == (byte)'u' && buf[1] == (byte)'s' && buf[2] == (byte)'t' && buf[3] == (byte)'a' && buf[4] == (byte)'r';
    }

    private static bool FileNameMatches(string? entryKey, string expectedFile)
    {
        if (string.IsNullOrEmpty(entryKey)) return false;

        var name = Path.GetFileName(entryKey.Replace('\\', '/'));

        return string.Equals(name, expectedFile, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] ReadBytes(string path, int count)
    {
        using var stream = File.OpenRead(path);

        var buf = new byte[count];
        var read = stream.Read(buf, 0, count);

        if (read < count) Array.Resize(ref buf, read);

        return buf;
    }

    private static bool StartsWith(byte[] source, byte[] prefix)
    {
        if (source.Length < prefix.Length) return false;

        for (var i = 0; i < prefix.Length; i++) if (source[i] != prefix[i]) return false;

        return true;
    }
}
