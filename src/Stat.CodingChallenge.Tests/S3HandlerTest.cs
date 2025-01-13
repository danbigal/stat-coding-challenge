using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Moq;
using Stat.CodingChallenge.Domain.Handlers;
using Stat.CodingChallenge.Infrastructure.Handlers;

namespace Stat.CodingChallenge.Tests
{
    public class S3HandlerTest
    {
        private readonly Mock<IAmazonS3> amazonS3Mock;
        private readonly Mock<ILogger<IS3Handler>> loggerMock;
        private readonly IS3Handler s3Handler;

        public S3HandlerTest()
        {
            amazonS3Mock = new Mock<IAmazonS3>();
            loggerMock = new Mock<ILogger<IS3Handler>>();

            s3Handler = new S3Handler(amazonS3Mock.Object, loggerMock.Object);
        }

        [Fact]
        public async Task ListAllZipFilesAsync_ReturnsOnlyZip()
        {
            var s3Response = new ListObjectsV2Response
            {
                S3Objects = new List<S3Object>()
                { 
                    new S3Object() { Key = "file1.zip" },
                    new S3Object() { Key = "file2.zip" },
                    new S3Object() { Key = "file3.txt" },
                    new S3Object() { Key = "file4.ZIP" },
                    new S3Object() { Key = "file5.json" },
                }
            };

            amazonS3Mock
                .Setup(s => s.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), CancellationToken.None))
                .ReturnsAsync(s3Response);

            var result = await s3Handler.ListAllZipFilesAsync("fake_bucket");

            var expectedResult = new List<string> { "file1.zip", "file2.zip", "file4.ZIP" };

            Assert.Equal(expectedResult, result);
        }
    }
}