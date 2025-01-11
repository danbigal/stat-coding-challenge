﻿using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Stat.CodingChallenge.Domain.Handlers;

namespace Stat.CodingChallenge.Infrastructure.Handlers
{
    public class S3Handler : IS3Handler
    {
        private readonly IAmazonS3 s3Client;

        public S3Handler(IAmazonS3 s3Client)
        {
            this.s3Client = s3Client;
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
            var transferUtility = new TransferUtility(s3Client);
            await transferUtility.UploadAsync(localSourcePath, s3BucketName, s3Key);
        }
    }
}
