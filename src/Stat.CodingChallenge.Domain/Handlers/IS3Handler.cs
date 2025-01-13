namespace Stat.CodingChallenge.Domain.Handlers
{
    public interface IS3Handler
    {
        Task<IReadOnlyList<string>> ListAllZipFilesAsync(string s3BucketName);

        Task DownloadFileAsync(string s3BucketName, string s3Key, string localDestinationPath);

        Task UploadFileAsync(string localSourcePath, string s3BucketName, string s3Key);

        Task UploadFileAsync(Stream stream, string s3BucketName, string s3Key);
    }
}
