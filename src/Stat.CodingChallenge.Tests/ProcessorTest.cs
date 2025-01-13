using Amazon.Runtime.Internal.Transform;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Stat.CodingChallenge.Application;
using Stat.CodingChallenge.Domain.Entities;
using Stat.CodingChallenge.Domain.Handlers;
using Stat.CodingChallenge.Domain.Wrappers;
using System.Text.Json;

namespace Stat.CodingChallenge.Tests
{
    public class ProcessorTest
    {
        private const string bucketName = "bucket-test";

        private readonly Mock<IS3Handler> s3HandlerMock;
        private readonly Mock<ICsvHandler> csvHandlerMock;
        private readonly Mock<IFileWrapper> fileWrapperMock;
        private readonly Mock<ILogger<Processor>> loggerMock;

        private readonly Processor processor;

        public ProcessorTest()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>() { { "S3BucketName", bucketName } })
                .Build();


            s3HandlerMock = new Mock<IS3Handler>();
            csvHandlerMock = new Mock<ICsvHandler>();
            fileWrapperMock = new Mock<IFileWrapper>();
            loggerMock = new Mock<ILogger<Processor>>();

            processor = new Processor(
                configuration,
                s3HandlerMock.Object,
                csvHandlerMock.Object,
                fileWrapperMock.Object,
                loggerMock.Object);
        }

        [Fact]
        public async Task Process_ProcessOnlyUnprocessed()
        {
            var initialMetadata = new Metadata()
            {
                ProcessedZips = new List<string> { "zip1.zip", "zip2.zip" },
                ProcessedFiles = new List<FileMetadata> 
                {
                    new FileMetadata("pdf1_z1.pdf", "zip1.zip", DateTime.UtcNow),
                    new FileMetadata("pdf1_z2.pdf", "zip2.zip", DateTime.UtcNow),
                    new FileMetadata("pdf2_z2.pdf", "zip2.zip", DateTime.UtcNow),
                    new FileMetadata("pdf1_z3.pdf", "zip3.zip", DateTime.UtcNow),
                }
            };

            var jsonMetadata = JsonSerializer.Serialize(initialMetadata);

            fileWrapperMock
                .Setup(s => s.ReadAllTextAsync(It.IsAny<string>(), CancellationToken.None))
                .ReturnsAsync(jsonMetadata);

            fileWrapperMock
                .Setup(s => s.ExtractZipToDirectory(It.IsAny<string>(), It.IsAny<string>()));

            var s3ZipFiles = new List<string>() { "zip1.zip", "zip2.zip", "zip3.zip" };
            s3HandlerMock
                .Setup(x => x.ListAllZipFilesAsync(It.IsAny<string>()))
                .ReturnsAsync(s3ZipFiles);

            var mappings = new List<Mapping>()
            {
                new Mapping("pdf1_z3.pdf", "1000"),
                new Mapping("pdf2_z3.pdf", "1000"),
                new Mapping("pdf3_z3.pdf", "3000"),
                new Mapping("pdf4_z3.pdf", "4000"),
            };

            csvHandlerMock
                .Setup(s => s.ExtractMappingAsync(It.IsAny<string>()))
                .ReturnsAsync(mappings);

            var metadataResult = await processor.ProcessAsync();

            s3HandlerMock.Verify(v => v.DownloadFileAsync(It.IsAny<string>(), "metadata.json", It.IsAny<string>()), Times.Once);
            s3HandlerMock.Verify(v => v.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), "metadata.json"), Times.Once);

            s3HandlerMock.Verify(v => v.DownloadFileAsync(It.IsAny<string>(), "zip1.zip", It.IsAny<string>()), Times.Never);
            s3HandlerMock.Verify(v => v.DownloadFileAsync(It.IsAny<string>(), "zip2.zip", It.IsAny<string>()), Times.Never);
            s3HandlerMock.Verify(v => v.DownloadFileAsync(It.IsAny<string>(), "zip3.zip", It.IsAny<string>()), Times.Once);

            fileWrapperMock.Verify(v => v.ExtractZipToDirectory(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            csvHandlerMock.Verify(v => v.ExtractMappingAsync(It.IsAny<string>()), Times.Once);

            s3HandlerMock.Verify(v => v.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), "by-po/1000/pdf1_z3.pdf"), Times.Never);
            s3HandlerMock.Verify(v => v.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), "by-po/1000/pdf2_z3.pdf"), Times.Once);
            s3HandlerMock.Verify(v => v.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), "by-po/3000/pdf3_z3.pdf"), Times.Once);
            s3HandlerMock.Verify(v => v.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), "by-po/4000/pdf4_z3.pdf"), Times.Once);

            Assert.Equal(s3ZipFiles, metadataResult.ProcessedZips);
            Assert.Equal(7, metadataResult.ProcessedFiles.Count);
            Assert.Contains(metadataResult.ProcessedFiles, f => f.PdfFileName == "pdf1_z1.pdf" && f.ZipFileName == "zip1.zip");
            Assert.Contains(metadataResult.ProcessedFiles, f => f.PdfFileName == "pdf1_z2.pdf" && f.ZipFileName == "zip2.zip");
            Assert.Contains(metadataResult.ProcessedFiles, f => f.PdfFileName == "pdf2_z2.pdf" && f.ZipFileName == "zip2.zip");
            Assert.Contains(metadataResult.ProcessedFiles, f => f.PdfFileName == "pdf1_z3.pdf" && f.ZipFileName == "zip3.zip");
            Assert.Contains(metadataResult.ProcessedFiles, f => f.PdfFileName == "pdf2_z3.pdf" && f.ZipFileName == "zip3.zip");
            Assert.Contains(metadataResult.ProcessedFiles, f => f.PdfFileName == "pdf3_z3.pdf" && f.ZipFileName == "zip3.zip");
            Assert.Contains(metadataResult.ProcessedFiles, f => f.PdfFileName == "pdf4_z3.pdf" && f.ZipFileName == "zip3.zip");
        }
    }
}