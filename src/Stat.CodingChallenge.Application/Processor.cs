using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stat.CodingChallenge.Domain.Entities;
using Stat.CodingChallenge.Domain.Handlers;
using Stat.CodingChallenge.Domain.Wrappers;
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
        private readonly IFileWrapper fileWrapper;
        private readonly ILogger<Processor> logger;

        public Processor(
            IConfiguration configuration,
            IS3Handler s3Handler,
            ICsvHandler csvHandler,
            IFileWrapper fileWrapper,
            ILogger<Processor> logger)
        {
            this.configuration = configuration;
            this.s3Handler = s3Handler;
            this.csvHandler = csvHandler;
            this.fileWrapper = fileWrapper;
            this.logger = logger;
        }

        public async Task<Metadata> ProcessAsync()
        {
            this.logger.LogInformation("Starting process...");

            var bucketName = this.configuration.GetSection("S3BucketName").Value;
            this.logger.LogInformation($"Bucket Name: {bucketName}");

            this.LocalCleanup(bucketName);
            var metadata = await this.LoadMetadataAsync(bucketName);

            var zipFileNames = await this.s3Handler.ListAllZipFilesAsync(bucketName);

            var processedZips = new ConcurrentBag<string>();
            var processedFiles = new ConcurrentBag<FileMetadata>();
            
            // Building a HashSet lookup, because HashSet is O(1) to access its data, so it'll be faster.
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
                            this.logger.LogInformation($"Processing Zip: {zipFileName}...");

                            var localZipPath = $"{AppDomain.CurrentDomain.BaseDirectory}{bucketName}\\{zipFileName}";
                            await this.s3Handler.DownloadFileAsync(bucketName, zipFileName, localZipPath);

                            var localUnzipedPath = localZipPath.Replace(".zip", "");
                            this.fileWrapper.ExtractZipToDirectory(localZipPath, localZipPath.Replace(".zip", ""));

                            var csvPath = $"{localUnzipedPath}\\Komar_Deduction_{zipFileName.Replace(".zip", ".csv")}";
                            var mappings = await this.csvHandler.ExtractMappingAsync(csvPath);

                            await Parallel.ForEachAsync(mappings, async (mapping, cancellationToken) =>
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                var isPdfProcessed = pdfLookup.Contains(mapping.FileName);

                                if (!isPdfProcessed)
                                {
                                    this.logger.LogInformation($"Processing File: {mapping.FileName}...");

                                    var localPdfPath = $"{localUnzipedPath}\\{mapping.FileName}";
                                    var s3Key = $"by-po/{mapping.PONumber}/{mapping.FileName}";

                                    await this.s3Handler.UploadFileAsync(localPdfPath, bucketName, s3Key);

                                    processedFiles.Add(new FileMetadata(mapping.FileName, zipFileName, DateTime.UtcNow));

                                    this.logger.LogInformation($"File: {mapping.FileName} uploaded...");
                                }
                                else
                                {
                                    this.logger.LogInformation($"File: {mapping.FileName} already processed...");
                                }
                            });

                            processedZips.Add(zipFileName);
                        }
                        catch (Exception ex)
                        {
                            // Log the error and keep trying the others zips.
                            this.logger.LogError(ex, $"Erro ao processar zip. {zipFileName}");
                        }
                    }
                    else
                    {
                        this.logger.LogInformation($"Zip file: {zipFileName} already processed...");
                    }
                });
            }
            finally
            {
                metadata.ProcessedZips.AddRange(processedZips);
                metadata.ProcessedFiles.AddRange(processedFiles);

                await this.UploadMetadataAsync(metadata, bucketName);
                this.logger.LogInformation($"Metadata updated...");

                this.LocalCleanup(bucketName);
            }

            return metadata;
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
                await this.s3Handler.DownloadFileAsync(bucketName, metadataFileName, localPath);
            }
            catch (Exception)
            {
                return new Metadata();
            }

            var metadataJsonString = await this.fileWrapper.ReadAllTextAsync(localPath);
            var metadata = JsonSerializer.Deserialize<Metadata>(metadataJsonString);

            return metadata;
        }

        private async Task UploadMetadataAsync(Metadata metadata, string bucketName)
        {
            using (var stream = new MemoryStream())
            {
                await JsonSerializer.SerializeAsync(stream, metadata);
                await this.s3Handler.UploadFileAsync(stream, bucketName, "metadata.json");
            }
        }
    }
}
