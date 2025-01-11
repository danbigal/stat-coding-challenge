using CsvHelper;
using CsvHelper.Configuration;
using Stat.CodingChallenge.Domain.Handlers;
using System.Globalization;

namespace Stat.CodingChallenge.Infrastructure.Handlers
{
    public class CsvHandler : ICsvHandler
    {
        public async Task<Dictionary<string, string>> ExtractMapping(string csvPath)
        {
            using var reader = new StreamReader(csvPath);
            var configReader = new CsvConfiguration(CultureInfo.CurrentCulture) { Delimiter = "~" };
            
            using var csvReader = new CsvReader(reader, configReader);

            await csvReader.ReadAsync();
            csvReader.ReadHeader();

            var extractedDict = new Dictionary<string, string>();

            while (await csvReader.ReadAsync())
            {
                string key = ExtractPdfName(csvReader.GetField("Attachment List"));
                string value = csvReader.GetField("PO Number");

                extractedDict.Add(key, value);
            }

            return extractedDict;
        }

        private string ExtractPdfName(string attachmentList)
        {
            return "";
        }
    }
}
