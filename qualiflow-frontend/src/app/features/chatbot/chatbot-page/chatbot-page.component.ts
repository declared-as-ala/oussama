import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { NotificationService } from '../../../core/services/notification.service';
import {
  AskChatResponseDto,
  ChatConversationDetailsDto,
  ChatConversationDto,
  ChatMessageDto,
  ChatMessageRole
} from '../models/chatbot.models';
import { ChatbotService } from '../services/chatbot.service';
import { MatIconModule } from '@angular/material/icon';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

@Component({
  selector: 'app-chatbot-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatIconModule],
  templateUrl: './chatbot-page.component.html',
  styleUrls: ['./chatbot-page.component.scss']
})
export class ChatbotPageComponent implements OnInit {
  @ViewChild('messagesContainer') messagesContainer?: ElementRef<HTMLDivElement>;

  readonly questionControl = new FormControl<string>('', {
    nonNullable: true
  });

  conversations: ChatConversationDto[] = [];
  messages: ChatMessageDto[] = [];
  selectedConversationId: number | null = null;

  loadingConversations = false;
  loadingMessages = false;
  sending = false;
  errorMessage = '';
  sidebarOpen = false;

  constructor(
    private readonly chatbotService: ChatbotService,
    private readonly notificationService: NotificationService,
    private readonly sanitizer: DomSanitizer
  ) { }

  ngOnInit(): void {
    void this.loadConversations(true);
  }

  toggleSidebar(): void {
    this.sidebarOpen = !this.sidebarOpen;
  }

  async startNewConversation(): Promise<void> {
    this.selectedConversationId = null;
    this.messages = [];
    this.errorMessage = '';
    this.questionControl.setValue('');
    this.sidebarOpen = false;
    this.scrollToBottom();
  }

  async sendQuestion(): Promise<void> {
    const question = this.questionControl.value.trim();
    if (!question || this.sending) {
      return;
    }

    this.sending = true;
    this.errorMessage = '';
    this.scrollToBottom();

    try {
      let conversationId = this.selectedConversationId;

      if (!conversationId) {
        const createdConversation = await firstValueFrom(
          this.chatbotService.createConversation({
            title: this.buildConversationTitle(question)
          })
        );
        conversationId = createdConversation.id;
        this.selectedConversationId = conversationId;
        this.upsertConversation(createdConversation);
      }

      this.appendLocalMessage({
        id: this.buildLocalMessageId(),
        conversationId,
        role: 'USER',
        content: question,
        createdAt: new Date().toISOString()
      });

      this.questionControl.setValue('');

      const response = await firstValueFrom(
        this.chatbotService.ask({
          conversationId,
          question
        })
      );

      await this.hydrateMessagesAfterAsk(conversationId, response);
      this.bumpConversation(conversationId);
    } catch (error) {
      this.errorMessage = this.extractErrorMessage(error);
      this.notificationService.showError(this.errorMessage);
    } finally {
      this.sending = false;
      this.scrollToBottom();
    }
  }

  async openConversation(conversationId: number): Promise<void> {
    if (this.loadingMessages) {
      return;
    }

    this.loadingMessages = true;
    this.errorMessage = '';
    this.sidebarOpen = false;

    try {
      const details = await firstValueFrom(this.chatbotService.getConversationById(conversationId));
      this.selectedConversationId = details.id;
      this.messages = this.normalizeMessages(details.messages, details.id);
      this.upsertConversation({
        id: details.id,
        title: details.title,
        createdAt: details.createdAt,
        updatedAt: details.updatedAt
      });
    } catch {
      this.notificationService.showError('Impossible de charger cette conversation.');
    } finally {
      this.loadingMessages = false;
      this.scrollToBottom();
    }
  }

  async deleteConversation(conversationId: number, event: MouseEvent): Promise<void> {
    event.stopPropagation();

    const shouldDelete = window.confirm('Supprimer cette conversation ?');
    if (!shouldDelete) {
      return;
    }

    try {
      await firstValueFrom(this.chatbotService.deleteConversation(conversationId));
      this.conversations = this.conversations.filter(conversation => conversation.id !== conversationId);

      if (this.selectedConversationId === conversationId) {
        this.selectedConversationId = null;
        this.messages = [];
      }
    } catch {
      this.notificationService.showError('Suppression impossible.');
    }
  }

  trackByConversation(_: number, conversation: ChatConversationDto): number {
    return conversation.id;
  }

  trackByMessage(_: number, message: ChatMessageDto): number {
    return message.id;
  }

  isUserMessage(role: ChatMessageRole | string): boolean {
    return role.toUpperCase() === 'USER';
  }

  formatTime(value: string): string {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return '';
    }

    return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }

  onComposerKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      void this.sendQuestion();
    }
  }

  copyToClipboard(text: string): void {
    void navigator.clipboard.writeText(text);
    this.notificationService.showSuccess('Copié dans le presse-papier');
  }

  renderMarkdown(text: string): SafeHtml {
    if (!text) return '';
    
    let html = text
      .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
      .replace(/__(.*?)__/g, '<strong>$1</strong>');

    // Simple table detection and conversion
    if (html.includes('|')) {
      const lines = html.split('\n');
      let inTable = false;

      const processedLines = lines.map(line => {
        const trimmed = line.trim();
        if (trimmed.startsWith('|') && trimmed.endsWith('|')) {
          const cells = trimmed.split('|').filter((_c, i, arr) => i > 0 && i < arr.length - 1);
          if (trimmed.includes('---')) return ''; // Skip separator row
          
          if (!inTable) {
            inTable = true;
            return '<table><thead><tr>' + cells.map(c => `<th>${c.trim()}</th>`).join('') + '</tr></thead><tbody>';
          }
          return '<tr>' + cells.map(c => `<td>${c.trim()}</td>`).join('') + '</tr>';
        } else {
          if (inTable) {
            inTable = false;
            return '</tbody></table>' + line;
          }
          return line;
        }
      });
      
      html = processedLines.join('\n');
      if (inTable) html += '</tbody></table>';
    }

    return this.sanitizer.bypassSecurityTrustHtml(html);
  }

  private async loadConversations(selectFirst: boolean): Promise<void> {
    this.loadingConversations = true;
    this.errorMessage = '';

    try {
      const conversations = await firstValueFrom(this.chatbotService.getConversations());
      this.conversations = [...conversations].sort((a, b) => {
        return new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime();
      });

      if (selectFirst && this.conversations.length > 0) {
        await this.openConversation(this.conversations[0].id);
      }
    } catch {
      this.errorMessage = 'Impossible de charger les conversations.';
      this.notificationService.showError(this.errorMessage);
    } finally {
      this.loadingConversations = false;
    }
  }

  private async hydrateMessagesAfterAsk(conversationId: number, response: AskChatResponseDto): Promise<void> {
    const targetConversationId = response.conversationId ?? conversationId;

    try {
      const details = await firstValueFrom(this.chatbotService.getConversationById(targetConversationId));
      this.selectedConversationId = targetConversationId;
      this.messages = this.normalizeMessages(details.messages, targetConversationId);
      this.upsertConversation({
        id: details.id,
        title: details.title,
        createdAt: details.createdAt,
        updatedAt: details.updatedAt
      });
    } catch {
      const assistantText = this.extractAssistantText(response);
      if (assistantText) {
        this.appendLocalMessage({
          id: this.buildLocalMessageId(),
          conversationId: targetConversationId,
          role: 'ASSISTANT',
          content: assistantText,
          createdAt: new Date().toISOString()
        });
      } else {
        throw new Error('Unable to hydrate conversation');
      }
    }
  }

  private extractAssistantText(response: AskChatResponseDto): string {
    return (
      response.assistantMessage?.content ??
      response.answer ??
      response.response ??
      response.content ??
      ''
    ).trim();
  }

  private normalizeMessages(messages: ChatMessageDto[], conversationId: number): ChatMessageDto[] {
    return messages.map(message => ({
      ...message,
      conversationId: message.conversationId ?? conversationId,
      role: (message.role ?? 'ASSISTANT').toUpperCase()
    }));
  }

  private upsertConversation(conversation: ChatConversationDto): void {
    const existingIndex = this.conversations.findIndex(item => item.id === conversation.id);
    if (existingIndex >= 0) {
      this.conversations[existingIndex] = conversation;
    } else {
      this.conversations.unshift(conversation);
    }

    this.sortConversations();
  }

  private bumpConversation(conversationId: number): void {
    const index = this.conversations.findIndex(conversation => conversation.id === conversationId);
    if (index < 0) {
      return;
    }

    const current = this.conversations[index];
    this.conversations[index] = {
      ...current,
      updatedAt: new Date().toISOString()
    };
    this.sortConversations();
  }

  private sortConversations(): void {
    this.conversations = [...this.conversations].sort((a, b) => {
      return new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime();
    });
  }

  private appendLocalMessage(message: ChatMessageDto): void {
    this.messages = [...this.messages, message];
    this.scrollToBottom();
  }

  private buildConversationTitle(question: string): string {
    const maxLength = 70;
    if (question.length <= maxLength) {
      return question;
    }

    return `${question.slice(0, maxLength - 3)}...`;
  }

  private buildLocalMessageId(): number {
    return -Date.now();
  }

  private scrollToBottom(): void {
    setTimeout(() => {
      if (!this.messagesContainer?.nativeElement) {
        return;
      }

      const container = this.messagesContainer.nativeElement;
      container.scrollTop = container.scrollHeight;
    }, 0);
  }

  private extractErrorMessage(error: unknown): string {
    const defaultMessage = 'Impossible d\'obtenir une reponse pour le moment.';

    if (error instanceof HttpErrorResponse) {
      const apiMessage = error.error?.message;
      if (typeof apiMessage === 'string' && apiMessage.trim().length > 0) {
        return apiMessage.trim();
      }
    }

    return defaultMessage;
  }
}
