using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Stat.CodingChallenge.Domain.Entities;
using Stat.CodingChallenge.Domain.Handlers;
using Stat.CodingChallenge.Infrastructure.Handlers;

namespace Stat.CodingChallenge.Tests
{
    public class CsvHandlerTest
    {
        private readonly Mock<ILogger<ICsvHandler>> loggerMock;
        private readonly CsvHandler csvHandler;

        public CsvHandlerTest()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>() { { "CsvDelimiter", "~"} })
                .Build();

            loggerMock = new Mock<ILogger<ICsvHandler>>();

            csvHandler = new CsvHandler(configuration, loggerMock.Object);
        }

        [Fact]
        public async Task ExtractMappingAsync_ReturnsMapping()
        {
            var result = await csvHandler.ExtractMappingAsync($"{AppDomain.CurrentDomain.BaseDirectory}Files\\mapping_test.csv");

            var expectedResult = new List<Mapping>()
            {
                new Mapping("_1638309233665.pdf", "9513353068"),
                new Mapping("270012472_66603311_Walmart_claim.pdf", "9513353068"),
                new Mapping("_1638309233801.pdf", "9513353068"),
                new Mapping("_1638306619767.pdf", "6213833408"),
                new Mapping("21944496_13550154_Walmart_claim.pdf", "6213833408"),
            };

            Assert.Equal(expectedResult, result);
        }
    }
}