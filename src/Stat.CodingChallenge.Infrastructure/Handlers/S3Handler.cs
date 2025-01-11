using Amazon.Runtime.Internal.Util;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Logging;
using Stat.CodingChallenge.Domain.Handlers;

namespace Stat.CodingChallenge.Infrastructure.Handlers
{
    public class S3Handler : IS3Handler
    {
        private readonly IAmazonS3 s3Client;
        private readonly ILogger<IS3Handler> logger;

        public S3Handler(
            IAmazonS3 s3Client,
            ILogger<IS3Handler> logger)
        {
            this.s3Client = s3Client;
            this.logger = logger;
        }

        public async Task<IReadOnlyList<string>> ListAllZipFilesAsync(string s3BucketName)
        {
            var zipFiles = new List<string>();

            var request = new ListObjectsV2Request
            {
                BucketName = s3BucketName,
            };

            var response = await s3Client.ListObjectsV2Async(request);

            foreach (var obj in response.S3Objects)
            {
                if (obj.Key.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    zipFiles.Add(obj.Key);
                }
            }

            return zipFiles;
        }
        public async Task DownloadFileAsync(string s3BucketName, string s3Key, string localDestinationPath)
        {
            var transferUtility = new TransferUtility(s3Client);
            await transferUtility.DownloadAsync(localDestinationPath, s3BucketName, s3Key);
        }

        public async Task UploadFileAsync(string localSourcePath, string s3BucketName, string s3Key)
        {
            if (File.Exists(localSourcePath))
            {
                var transferUtility = new TransferUtility(s3Client);
                await transferUtility.UploadAsync(localSourcePath, s3BucketName, s3Key);
            }
            else
            {
                logger.LogWarning($"Unable to upload {s3Key}. File not found.");
            }
        }
    }
}
