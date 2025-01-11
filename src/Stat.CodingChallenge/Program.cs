using Amazon.S3;
using Stat.CodingChallenge.Application;
using Stat.CodingChallenge.Domain.Handlers;
using Stat.CodingChallenge.Infrastructure.Handlers;

namespace Stat.CodingChallenge
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // Such as a dependency injection
            string bucketName = "stat-coding-rwebmqasbt";
            IS3Handler s3Handler = new S3Handler(new AmazonS3Client());
            ICsvHandler csvHandler = new CsvHandler();

            var processor = new Processor(bucketName, s3Handler, csvHandler);
            await processor.ProcessAsync();
        }
    }
}
