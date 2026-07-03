using System.Formats.Tar;
using System.IO.Compression;
using FFMpeg.StaticFetcher.Archives;
using FFMpeg.StaticFetcher.Exceptions;
using SharpCompress.Compressors.BZip2;

namespace FFMpeg.StaticFetcher.Tests;

public class ArchiveExtractorTests : TempFolderTestBase
{
    private static readonly byte[] BinaryPayload = { 0x7F, 0x45, 0x4C, 0x46, 0x02, 0x01, 0x01, 0x00, 0xAA, 0xBB, 0xCC };

    [Fact]
    public void ExtractBinary_BareGzip_DecompressesToDestination()
    {
        var archive = Path.Combine(_outputFolder, "src.gz");
        var dest = Path.Combine(_outputFolder, "ffmpeg");
        Directory.CreateDirectory(_outputFolder);

        using (var fs = File.Create(archive))
        using (var gz = new GZipStream(fs, CompressionLevel.Fastest))
            gz.Write(BinaryPayload);

        ArchiveExtractor.ExtractBinary(archive, "ffmpeg", dest);

        Assert.True(File.Exists(dest));
        Assert.Equal(BinaryPayload, File.ReadAllBytes(dest));
    }

    [Fact]
    public void ExtractBinary_Zip_PicksMatchingEntry()
    {
        var archive = Path.Combine(_outputFolder, "src.zip");
        var dest = Path.Combine(_outputFolder, "ffmpeg.exe");
        Directory.CreateDirectory(_outputFolder);

        using (var fs = File.Create(archive))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            WriteZipEntry(zip, "bin/README.txt", "readme"u8.ToArray());
            WriteZipEntry(zip, "bin/ffmpeg.exe", BinaryPayload);
            WriteZipEntry(zip, "bin/ffprobe.exe", "not-this-one"u8.ToArray());
        }

        ArchiveExtractor.ExtractBinary(archive, "ffmpeg.exe", dest);

        Assert.True(File.Exists(dest));
        Assert.Equal(BinaryPayload, File.ReadAllBytes(dest));
    }

    [Fact]
    public void ExtractBinary_Zip_MissingBinary_Throws()
    {
        var archive = Path.Combine(_outputFolder, "src.zip");
        var dest = Path.Combine(_outputFolder, "ffmpeg.exe");
        Directory.CreateDirectory(_outputFolder);

        using (var fs = File.Create(archive))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            WriteZipEntry(zip, "bin/ffprobe.exe", BinaryPayload);

        var ex = Assert.Throws<FFMpegStaticFetcherException>(() =>
            ArchiveExtractor.ExtractBinary(archive, "ffmpeg.exe", dest));

        Assert.Contains("ffmpeg.exe", ex.Message);
        Assert.Contains("not found", ex.Message);
        Assert.Contains("ffprobe.exe", ex.Detail);
    }

    [Fact]
    public void ExtractBinary_TarGz_ExtractsMatchingEntry()
    {
        var tarPath = Path.Combine(_outputFolder, "src.tar");
        var archive = Path.Combine(_outputFolder, "src.tar.gz");
        var dest = Path.Combine(_outputFolder, "ffmpeg");
        Directory.CreateDirectory(_outputFolder);

        // Build a tar with multiple entries, then gzip it
        using (var fs = File.Create(tarPath))
        using (var writer = new TarWriter(fs, TarEntryFormat.Ustar))
        {
            WriteTarEntry(writer, "ffmpeg-build/README", "readme"u8.ToArray());
            WriteTarEntry(writer, "ffmpeg-build/bin/ffmpeg", BinaryPayload);
        }

        using (var src = File.OpenRead(tarPath))
        using (var dst = File.Create(archive))
        using (var gz = new GZipStream(dst, CompressionLevel.Fastest))
            src.CopyTo(gz);

        ArchiveExtractor.ExtractBinary(archive, "ffmpeg", dest);

        Assert.True(File.Exists(dest));
        Assert.Equal(BinaryPayload, File.ReadAllBytes(dest));
    }

    [Fact]
    public void ExtractBinary_Tar_ExtractsMatchingEntry()
    {
        var archive = Path.Combine(_outputFolder, "src.tar");
        var dest = Path.Combine(_outputFolder, "ffmpeg");
        Directory.CreateDirectory(_outputFolder);

        using (var fs = File.Create(archive))
        using (var writer = new TarWriter(fs, TarEntryFormat.Ustar))
        {
            WriteTarEntry(writer, "build/ffprobe", "nope"u8.ToArray());
            WriteTarEntry(writer, "build/ffmpeg", BinaryPayload);
        }

        ArchiveExtractor.ExtractBinary(archive, "ffmpeg", dest);

        Assert.True(File.Exists(dest));
        Assert.Equal(BinaryPayload, File.ReadAllBytes(dest));
    }

    [Fact]
    public void ExtractBinary_BareBzip2_DecompressesToDestination()
    {
        var archive = Path.Combine(_outputFolder, "src.bz2");
        var dest = Path.Combine(_outputFolder, "ffmpeg");
        Directory.CreateDirectory(_outputFolder);

        using (var fs = File.Create(archive))
        using (var bz = BZip2Stream.Create(fs, SharpCompress.Compressors.CompressionMode.Compress, decompressConcatenated: false))
            bz.Write(BinaryPayload);

        ArchiveExtractor.ExtractBinary(archive, "ffmpeg", dest);

        Assert.True(File.Exists(dest));
        Assert.Equal(BinaryPayload, File.ReadAllBytes(dest));
    }

    [Fact]
    public void ExtractBinary_TarBz2_ExtractsMatchingEntry()
    {
        var tarPath = Path.Combine(_outputFolder, "src.tar");
        var archive = Path.Combine(_outputFolder, "src.tar.bz2");
        var dest = Path.Combine(_outputFolder, "ffmpeg");
        Directory.CreateDirectory(_outputFolder);

        using (var fs = File.Create(tarPath))
        using (var writer = new TarWriter(fs, TarEntryFormat.Ustar))
        {
            WriteTarEntry(writer, "prefix/ffprobe", "nope"u8.ToArray());
            WriteTarEntry(writer, "prefix/ffmpeg", BinaryPayload);
        }

        using (var src = File.OpenRead(tarPath))
        using (var dst = File.Create(archive))
        using (var bz = BZip2Stream.Create(dst, SharpCompress.Compressors.CompressionMode.Compress, decompressConcatenated: false))
            src.CopyTo(bz);

        ArchiveExtractor.ExtractBinary(archive, "ffmpeg", dest);

        Assert.True(File.Exists(dest));
        Assert.Equal(BinaryPayload, File.ReadAllBytes(dest));
    }

    [Fact]
    public void ExtractBinary_RarMagic_RoutedToMultiEntryArchive()
    {
        // RAR 5.0 magic prefix. A bare header is an invalid archive, so SharpCompress will throw
        // from ArchiveFactory.OpenArchive. That throw is what proves the RAR bytes took the
        // multi-entry path — without RAR detection, the bytes would fall through to raw-copy and
        // succeed silently.
        var archive = Path.Combine(_outputFolder, "src.rar");
        var dest = Path.Combine(_outputFolder, "ffmpeg");
        Directory.CreateDirectory(_outputFolder);

        File.WriteAllBytes(archive, [0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x01, 0x00]);

        Assert.ThrowsAny<Exception>(() => ArchiveExtractor.ExtractBinary(archive, "ffmpeg", dest));
        Assert.False(File.Exists(dest), "RAR routing failure must not leave a partial destination file");
        Assert.False(File.Exists(dest + ".downloading"), "RAR routing failure must not leave a staging file");
    }

    [Fact]
    public void ExtractBinary_Raw_CopiesAsIs()
    {
        var archive = Path.Combine(_outputFolder, "src.bin");
        var dest = Path.Combine(_outputFolder, "ffmpeg");
        Directory.CreateDirectory(_outputFolder);

        File.WriteAllBytes(archive, BinaryPayload);

        ArchiveExtractor.ExtractBinary(archive, "ffmpeg", dest);

        Assert.True(File.Exists(dest));
        Assert.Equal(BinaryPayload, File.ReadAllBytes(dest));
    }

    [Fact]
    public void ExtractBinary_Tar_MissingEntry_Throws()
    {
        var archive = Path.Combine(_outputFolder, "src.tar");
        var dest = Path.Combine(_outputFolder, "ffmpeg");
        Directory.CreateDirectory(_outputFolder);

        using (var fs = File.Create(archive))
        using (var writer = new TarWriter(fs, TarEntryFormat.Ustar))
        {
            WriteTarEntry(writer, "build/ffprobe", BinaryPayload);
        }

        var ex = Assert.Throws<FFMpegStaticFetcherException>(() =>
            ArchiveExtractor.ExtractBinary(archive, "ffmpeg", dest));

        Assert.Contains("ffmpeg", ex.Message);
        Assert.Contains("not found in tar", ex.Message);
        Assert.Contains("ffprobe", ex.Detail);
        Assert.False(File.Exists(dest + ".downloading"), "Missing-entry failure must clean up the staging file");
    }

    [Fact]
    public void ExtractBinary_LargeRawFile_NotTar_TreatedAsRawBinary()
    {
        // 1 KB of zeroes exceeds the IsTarFile size guard (>=512) but lacks the "ustar"
        // identifier at offset 257, so it must fall through to raw copy rather than being
        // parsed as a tar archive.
        var archive = Path.Combine(_outputFolder, "src.bin");
        var dest = Path.Combine(_outputFolder, "ffmpeg");
        Directory.CreateDirectory(_outputFolder);

        var bytes = new byte[1024];

        // First bytes are non-zero so we can prove the contents round-trip verbatim without
        // colliding with any archive magic.
        bytes[0] = 0xAA;
        bytes[1] = 0xBB;
        File.WriteAllBytes(archive, bytes);

        ArchiveExtractor.ExtractBinary(archive, "ffmpeg", dest);

        Assert.Equal(bytes, File.ReadAllBytes(dest));
    }

    [Fact]
    public void ExtractBinary_PreExistingStagingFile_IsCleanedUp()
    {
        // Simulate a crashed prior extraction that left a stale "<dest>.downloading" file.
        // The extractor must delete it before writing fresh contents, then move it into place.
        var archive = Path.Combine(_outputFolder, "src.gz");
        var dest = Path.Combine(_outputFolder, "ffmpeg");
        var staging = dest + ".downloading";
        Directory.CreateDirectory(_outputFolder);

        File.WriteAllBytes(staging, [0xDE, 0xAD, 0xBE, 0xEF]);

        using (var fs = File.Create(archive))
        using (var gz = new GZipStream(fs, CompressionLevel.Fastest))
            gz.Write(BinaryPayload);

        ArchiveExtractor.ExtractBinary(archive, "ffmpeg", dest);

        Assert.Equal(BinaryPayload, File.ReadAllBytes(dest));
        Assert.False(File.Exists(staging), "Staging file must be gone after successful extraction");
    }

    [Fact]
    public void ExtractBinary_OverwritesExistingDestination()
    {
        var archive = Path.Combine(_outputFolder, "src.gz");
        var dest = Path.Combine(_outputFolder, "ffmpeg");
        Directory.CreateDirectory(_outputFolder);

        File.WriteAllBytes(dest, [0xFF, 0xEE]);

        using (var fs = File.Create(archive))
        using (var gz = new GZipStream(fs, CompressionLevel.Fastest))
            gz.Write(BinaryPayload);

        ArchiveExtractor.ExtractBinary(archive, "ffmpeg", dest);

        Assert.Equal(BinaryPayload, File.ReadAllBytes(dest));
    }

    [Fact]
    public void ExtractBinary_Tar_SymlinkWithBinaryLeafName_PrefersRealFile()
    {
        var tarPath = Path.Combine(_outputFolder, "src.tar");
        var dest = Path.Combine(_outputFolder, "ffmpeg");
        Directory.CreateDirectory(_outputFolder);

        using (var fs = File.Create(tarPath))
        using (var writer = new TarWriter(fs, TarEntryFormat.Ustar))
        {
            // A symlink whose leaf name is 'ffmpeg' appears BEFORE the genuine binary at a deeper path.
            writer.WriteEntry(new UstarTarEntry(TarEntryType.SymbolicLink, "ffmpeg") { LinkName = "bin/ffmpeg" });
            WriteTarEntry(writer, "bin/ffmpeg", BinaryPayload);
        }

        ArchiveExtractor.ExtractBinary(tarPath, "ffmpeg", dest);

        // The real regular file must win, not the same-named symlink (which would extract as 0 bytes).
        Assert.Equal(BinaryPayload, File.ReadAllBytes(dest));
    }

    [Fact]
    public void ExtractBinary_ZeroByteExtractedEntry_Throws()
    {
        var tarPath = Path.Combine(_outputFolder, "src.tar");
        var dest = Path.Combine(_outputFolder, "ffmpeg");
        Directory.CreateDirectory(_outputFolder);

        using (var fs = File.Create(tarPath))
        using (var writer = new TarWriter(fs, TarEntryFormat.Ustar))
            WriteTarEntry(writer, "ffmpeg", Array.Empty<byte>());

        var ex = Assert.Throws<FFMpegStaticFetcherException>(() =>
            ArchiveExtractor.ExtractBinary(tarPath, "ffmpeg", dest));

        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(dest), "A 0-byte extracted binary must not be promoted to the destination");
    }

    [Fact]
    public void ExtractBinary_DecompressionExceedingMaxBytes_Throws()
    {
        var archive = Path.Combine(_outputFolder, "bomb.gz");
        var dest = Path.Combine(_outputFolder, "ffmpeg");
        Directory.CreateDirectory(_outputFolder);

        // ~64 KB of highly-compressible zeros in a tiny archive; cap the extraction well below that.
        using (var fs = File.Create(archive))
        using (var gz = new GZipStream(fs, CompressionLevel.Fastest))
            gz.Write(new byte[64 * 1024]);

        var ex = Assert.Throws<FFMpegStaticFetcherException>(() =>
            ArchiveExtractor.ExtractBinary(archive, "ffmpeg", dest, maxBytes: 1024));

        Assert.Contains("exceeded", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteZipEntry(ZipArchive zip, string entryName, byte[] content)
    {
        var entry = zip.CreateEntry(entryName);
        using var s = entry.Open();
        s.Write(content);
    }

    private static void WriteTarEntry(TarWriter writer, string entryName, byte[] content)
    {
        var entry = new UstarTarEntry(TarEntryType.RegularFile, entryName)
        {
            DataStream = new MemoryStream(content)
        };
        writer.WriteEntry(entry);
    }
}
