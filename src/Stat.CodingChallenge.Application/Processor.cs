using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stat.CodingChallenge.Domain.Entities;
using Stat.CodingChallenge.Domain.Handlers;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;

namespace Stat.CodingChallenge.Application
{
    public class Processor
    {
        private readonly IConfiguration configuration;
        private readonly IS3Handler s3Handler;
        private readonly ICsvHandler csvHandler;
        private readonly ILogger<Processor> logger;

        public Processor(
            IConfiguration configuration,
            IS3Handler s3Handler,
            ICsvHandler csvHandler,
            ILogger<Processor> logger)
        {
            this.configuration = configuration;
            this.s3Handler = s3Handler;
            this.csvHandler = csvHandler;
            this.logger = logger;
        }

        public async Task ProcessAsync()
        {
            logger.LogInformation("Starting process...");

            var bucketName = configuration.GetSection("S3BucketName").Value;
            logger.LogInformation($"Bucket Name: {bucketName}");

            this.LocalCleanup(bucketName);
            var metadata = await this.LoadMetadataAsync(bucketName);

            var zipFileNames = await s3Handler.ListAllZipFilesAsync(bucketName);

            var processedZips = new ConcurrentBag<string>();
            var processedFiles = new ConcurrentBag<FileMetadata>();
            
            var pdfLookup = metadata.BuildPdfLookup();

            try
            {
                await Parallel.ForEachAsync(zipFileNames, async (zipFileName, cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var isZipProcessed = metadata.ProcessedZips.Contains(zipFileName);

                    if (!isZipProcessed)
                    {
                        try
                        {
                            logger.LogInformation($"Processing Zip: {zipFileName}...");

                            var localZipPath = $"{AppDomain.CurrentDomain.BaseDirectory}{bucketName}\\{zipFileName}";
                            await s3Handler.DownloadFileAsync(bucketName, zipFileName, localZipPath);

                            var localUnzipedPath = localZipPath.Replace(".zip", "");
                            ZipFile.ExtractToDirectory(localZipPath, localZipPath.Replace(".zip", ""));

                            var csvPath = $"{localUnzipedPath}\\Komar_Deduction_{zipFileName.Replace(".zip", ".csv")}";
                            var mappings = await csvHandler.ExtractMappingAsync(csvPath);

                            await Parallel.ForEachAsync(mappings, async (mapping, cancellationToken) =>
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                var isPdfProcessed = pdfLookup.Contains(mapping.FileName);

                                if (!isPdfProcessed)
                                {
                                    logger.LogInformation($"Processing File: {mapping.FileName}...");

                                    var localPdfPath = $"{localUnzipedPath}\\{mapping.FileName}";
                                    var s3Key = $"by-po/{mapping.PONumber}\\{mapping.FileName}";

                                    await s3Handler.UploadFileAsync(localPdfPath, bucketName, s3Key);

                                    processedFiles.Add(new FileMetadata(mapping.FileName, zipFileName, DateTime.UtcNow));

                                    logger.LogInformation($"File: {mapping.FileName} uploaded...");
                                }
                                else
                                {
                                    logger.LogInformation($"File: {mapping.FileName} already processed...");
                                }
                            });

                            processedZips.Add(zipFileName);
                        }
                        catch (Exception ex)
                        {
                            // Log the error and keep trying the others zips.
                            logger.LogError(ex, $"Erro ao processar zip. {zipFileName}");
                        }
                    }
                    else
                    {
                        logger.LogInformation($"Zip file: {zipFileName} already processed...");
                    }
                });
            }
            finally
            {
                metadata.ProcessedZips.AddRange(processedZips);
                metadata.ProcessedFiles.AddRange(processedFiles);

                await this.UploadMetadataAsync(metadata, bucketName);
                logger.LogInformation($"Metadata updated...");

                this.LocalCleanup(bucketName);
            }
        }

        private void LocalCleanup(string bucketName)
        {

            var localPath = $"{AppDomain.CurrentDomain.BaseDirectory}\\{bucketName}";

            if (Directory.Exists(localPath))
            {
                Directory.Delete(localPath, true);
            }
        }

        private async Task<Metadata> LoadMetadataAsync(string bucketName)
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

        private async Task UploadMetadataAsync(Metadata metadata, string bucketName)
        {
            var metadataFileName = "metadata.json";
            var localPath = $"{AppDomain.CurrentDomain.BaseDirectory}\\{bucketName}\\{metadataFileName}";

            using (var stream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, metadata);
            }

            await s3Handler.UploadFileAsync(localPath, bucketName, metadataFileName);
        }
    }
}
