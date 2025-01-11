using Stat.CodingChallenge.Application.Models;
using Stat.CodingChallenge.Domain.Handlers;
using System;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;

namespace Stat.CodingChallenge.Application
{
    public class Processor
    {
        private readonly string bucketName;
        private readonly IS3Handler s3Handler;
        private readonly ICsvHandler csvHandler;

        public Processor(
            string bucketName,
            IS3Handler s3Handler,
            ICsvHandler csvHandler)
        {
            this.bucketName = bucketName;
            this.s3Handler = s3Handler;
            this.csvHandler = csvHandler;
        }

        public async Task ProcessAsync()
        {
            this.Cleanup();
            var metadata = await this.LoadMetadataAsync();

            var zipFileNames = await s3Handler.ListAllZipFilesAsync(bucketName);

            var processedZips = new ConcurrentBag<string>();
            var processedFiles = new ConcurrentBag<FileMetadata>();
            
            var pdfLookup = metadata.BuildPdfLookup();

            await Parallel.ForEachAsync(zipFileNames, new ParallelOptions { MaxDegreeOfParallelism = 1 },  async (zipFileName, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var isZipProcessed = metadata.ProcessedZips.Contains(zipFileName);

                if (!isZipProcessed)
                {
                    var localZipPath = $"{AppDomain.CurrentDomain.BaseDirectory}{bucketName}\\{zipFileName}";
                    await s3Handler.DownloadFileAsync(bucketName, zipFileName, localZipPath);

                    var localUnzipedPath = localZipPath.Replace(".zip", "");
                    ZipFile.ExtractToDirectory(localZipPath, localZipPath.Replace(".zip", ""));

                    var csvPath = $"{localUnzipedPath}\\Komar_Deduction_{zipFileName.Replace(".zip", ".csv")}";
                    var mappings = await csvHandler.ExtractMapping(csvPath);

                    await Parallel.ForEachAsync(mappings, new ParallelOptions { MaxDegreeOfParallelism = 1 }, async (mapping, cancellationToken) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var isPdfProcessed = pdfLookup.Contains(mapping.Key);

                        if (!isPdfProcessed)
                        {
                            var pdfFileName = mapping.Key;
                            var poNumber = mapping.Value;

                            var localPdfPath = $"{localUnzipedPath}\\{pdfFileName}";
                            var s3Key = $"by-po/{poNumber}\\{pdfFileName}";
                            await s3Handler.UploadFileAsync(localPdfPath, bucketName, s3Key);

                            processedFiles.Add(new FileMetadata(pdfFileName, zipFileName, DateTime.UtcNow));
                        }
                    });

                    processedZips.Add(zipFileName);
                }
            });

            metadata.ProcessedZips.AddRange(processedZips);
            metadata.ProcessedFiles.AddRange(processedFiles);

            await this.UploadMetadataAsync(metadata);
        }

        private void Cleanup()
        {
            var localPath = $"{AppDomain.CurrentDomain.BaseDirectory}\\{bucketName}";
            Directory.Delete(localPath, true);
        }

        private async Task<Metadata> LoadMetadataAsync()
        {
            var metadataFileName = "metadata.json";
            var localPath = $"{AppDomain.CurrentDomain.BaseDirectory}{bucketName}\\{metadataFileName}";

            try
            {
                await s3Handler.DownloadFileAsync(bucketName, metadataFileName, localPath);
            }
            catch (Exception)
            {
                return new Metadata();
            }

            var metadataJsonString = await File.ReadAllTextAsync(localPath);
            var metadata = JsonSerializer.Deserialize<Metadata>(metadataJsonString);

            return metadata;
        }

        private async Task UploadMetadataAsync(Metadata metadata)
        {
            var metadataFileName = "metadata.json";
            var localPath = $"{AppDomain.CurrentDomain.BaseDirectory}\\{bucketName}\\{metadataFileName}";

            using var stream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, metadata);

            await s3Handler.UploadFileAsync(localPath, bucketName, metadataFileName);
        }
    }
}
