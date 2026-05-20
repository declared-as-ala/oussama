using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DocApi.Common;
using DocApi.DTOs.Chat;

namespace DocApi.Services.Interfaces
{
    public interface IChatbotService
    {
        Task<AskChatResponseDto> AskAsync(
            AskChatRequestDto request,
            UserContext userContext,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ChatConversationDto>> GetConversationsAsync(
            UserContext userContext,
            CancellationToken cancellationToken = default);

        Task<ChatConversationDetailsDto> GetConversationByIdAsync(
            int conversationId,
            UserContext userContext,
            CancellationToken cancellationToken = default);

        Task<ChatConversationDto> CreateConversationAsync(
            CreateConversationDto request,
            UserContext userContext,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteConversationAsync(
            int conversationId,
            UserContext userContext,
            CancellationToken cancellationToken = default);
    }
}
