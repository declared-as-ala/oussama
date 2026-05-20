using System.Threading;
using System.Threading.Tasks;

namespace DocApi.Services.Interfaces
{
    public interface IOpenRouterService
    {
        Task<string> GenerateAnswerAsync(
            string systemPrompt,
            string question,
            string context,
            CancellationToken cancellationToken = default);
    }
}
