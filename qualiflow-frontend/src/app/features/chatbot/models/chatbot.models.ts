export type ChatMessageRole = 'USER' | 'ASSISTANT' | 'SYSTEM';

export interface ChatMessageDto {
  id: number;
  conversationId: number;
  role: ChatMessageRole | string;
  content: string;
  createdAt: string;
}

export interface ChatConversationDto {
  id: number;
  title: string;
  createdAt: string;
  updatedAt: string;
}

export interface ChatConversationDetailsDto extends ChatConversationDto {
  messages: ChatMessageDto[];
}

export interface CreateConversationDto {
  title?: string;
}

export interface AskChatRequestDto {
  conversationId?: number | null;
  question: string;
}

export interface AskChatResponseDto {
  conversationId?: number;
  answer?: string;
  response?: string;
  content?: string;
  assistantMessage?: ChatMessageDto;
}
