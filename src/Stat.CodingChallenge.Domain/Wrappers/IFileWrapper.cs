namespace Stat.CodingChallenge.Domain.Wrappers
{
    public interface IFileWrapper
    {
        Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);

        void ExtractZipToDirectory(string sourceArchiveFileName, string destinationDirectoryName);
    }
}
