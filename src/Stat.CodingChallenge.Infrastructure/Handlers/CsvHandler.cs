using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stat.CodingChallenge.Domain.Entities;
using Stat.CodingChallenge.Domain.Handlers;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Stat.CodingChallenge.Infrastructure.Handlers
{
    public class CsvHandler : ICsvHandler
    {
        private readonly ILogger<ICsvHandler> logger;
        private readonly string delimiter;

        public CsvHandler(
            IConfiguration configuration,
            ILogger<ICsvHandler> logger)
        {
            this.delimiter = configuration.GetSection("CsvDelimiter").Value;
            this.logger = logger;
        }

        public async Task<IReadOnlyList<Mapping>> ExtractMappingAsync(string csvPath)
        {
            using var reader = new StreamReader(csvPath);
            var configReader = new CsvConfiguration(CultureInfo.CurrentCulture) { Delimiter = this.delimiter };
            
            using var csvReader = new CsvReader(reader, configReader);

            await csvReader.ReadAsync();
            csvReader.ReadHeader();

            var mappings = new HashSet<Mapping>();

            while (await csvReader.ReadAsync())
            {
                var poNumber = csvReader.GetField("PO Number").Trim();
                if (string.IsNullOrEmpty(poNumber))
                {
                    this.logger.LogWarning($"PO Number not found. Line: {csvReader.CurrentIndex} File: {csvPath}");
                    continue;
                }

                var pdfs = ExtractPdfName(csvReader.GetField("Attachment List"));
                

                foreach (var pdf in pdfs )
                {
                    mappings.Add(new Mapping(pdf, poNumber));
                }
            }

            return mappings.ToList();
        }

        private IReadOnlyList<string> ExtractPdfName(string attachmentList)
        {
            string pattern = @"[^/]+\.pdf";

            var matches = Regex.Matches(attachmentList, pattern);
            var pdfs = matches.Select(m => m.Value).ToList();

            return pdfs;
        }
    }
}
