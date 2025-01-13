using Stat.CodingChallenge.Domain.Wrappers;
using System.IO.Compression;

namespace Stat.CodingChallenge.Infrastructure.Wrappers
{
    public class FileWrapper : IFileWrapper
    {
        public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
        {
            return File.ReadAllTextAsync(path, cancellationToken);
        }
        
        public void ExtractZipToDirectory(string sourceArchiveFileName, string destinationDirectoryName)
        {
            ZipFile.ExtractToDirectory(sourceArchiveFileName, destinationDirectoryName);
        }
    }
}
