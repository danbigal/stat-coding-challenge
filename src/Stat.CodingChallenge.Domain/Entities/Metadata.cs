namespace Stat.CodingChallenge.Domain.Entities
{
    public class Metadata
    {
        public List<string> ProcessedZips { get; set; }
        public List<FileMetadata> ProcessedFiles { get; set; }

        public Metadata()
        {
            this.ProcessedZips = new List<string>();
            this.ProcessedFiles = new List<FileMetadata>();
        }

        public HashSet<string> BuildPdfLookup()
        {
            var hashSet = this.ProcessedFiles
                .GroupBy(f => f.PdfFileName)
                .Select(g => g.Key)
                .ToHashSet();

            return hashSet;
        }
    }

    public class FileMetadata
    {
        public string PdfFileName { get; set; }
        public string ZipFileName { get; set; }
        public DateTime ProcessedTimestampUtc { get; set; }

        public FileMetadata()
        { 
        }

        public FileMetadata(string pdfFileName, string zipFileName, DateTime processedTimestampUtc)
        {
            this.PdfFileName = pdfFileName;
            this.ZipFileName = zipFileName;
            this.ProcessedTimestampUtc = processedTimestampUtc;
        }
    }
}
