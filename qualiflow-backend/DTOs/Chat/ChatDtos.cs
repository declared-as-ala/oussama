using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DocApi.DTOs.Chat
{
    public class AskChatRequestDto
    {
        public int? ConversationId { get; set; }

        [Required]
        [MaxLength(1500)]
        public string Question { get; set; } = string.Empty;
    }

    public class AskChatResponseDto
    {
        public int ConversationId { get; set; }
        public string Answer { get; set; } = string.Empty;
        public ChatMessageDto? AssistantMessage { get; set; }
    }

    public class CreateConversationDto
    {
        [MaxLength(200)]
        public string? Title { get; set; }
    }

    public class ChatConversationDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class ChatConversationDetailsDto : ChatConversationDto
    {
        public List<ChatMessageDto> Messages { get; set; } = new();
    }

    public class ChatMessageDto
    {
        public int Id { get; set; }
        public int ConversationId { get; set; }
        public string Role { get; set; } = "ASSISTANT";
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
