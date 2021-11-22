using MozJpegSharp;
using System.Buffers;
using System.IO.Compression;

if (args.Length == 0)
{
    Console.WriteLine("Usage:");
    Console.WriteLine("    >Img2Epub EpubFile");
    return 1;
}

foreach (var inEpub in args)
{
    var inSize = new FileInfo(inEpub).Length;

    var inDir = Path.GetDirectoryName(inEpub) ?? "";
    var outDir = Path.Combine(inDir, "reduced");
    var outEpub = Path.Combine(outDir, Path.GetFileName(inEpub));

    if (Directory.Exists(outDir) == false)
        Directory.CreateDirectory(outDir);

    File.Delete(outEpub);
    File.Copy(inEpub, outEpub);

    using (var archive = ZipFile.Open(outEpub, ZipArchiveMode.Update))
    {
        await Parallel.ForEachAsync(
            archive.Entries,
            async (entry, token) =>
            {
                var ext = Path.GetExtension(entry.FullName).ToLower();
                if (ext is not ".jpg" and not ".jpeg")
                    return;

                var length = (int)entry.Length;

                var temp = ArrayPool<byte>.Shared.Rent(length);
                {
                    try
                    {
                        using var es = entry.Open();

                        await es.ReadAsync(temp, token).ConfigureAwait(false);
                        es.SetLength(0);

                        var recompressed = MozJpeg.Recompress(temp.AsSpan(0, length), quality: 60);
                        await es.WriteAsync(recompressed, token).ConfigureAwait(false);
                    }
                    catch
                    {
                    }
                }
                ArrayPool<byte>.Shared.Return(temp);
            }
        ).ConfigureAwait(false);
    }

    var outSize = new FileInfo(outEpub).Length;
    if (outSize > inSize)
        File.Copy(inEpub, outEpub, true);
}

return 0;