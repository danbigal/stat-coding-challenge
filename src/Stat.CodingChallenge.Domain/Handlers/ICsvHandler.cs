using Stat.CodingChallenge.Domain.Entities;

namespace Stat.CodingChallenge.Domain.Handlers
{
    public interface ICsvHandler
    {
        Task<IReadOnlyList<Mapping>> ExtractMappingAsync(string csvPath);
    }
}
