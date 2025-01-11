namespace Stat.CodingChallenge.Domain.Handlers
{
    public interface ICsvHandler
    {
        Task<Dictionary<string, string>> ExtractMapping(string csvPath);
    }
}
