export interface SupportContactInfoResponse {
  assistantName: string;
  assistantEmail: string;
  assistantPhone: string;
}

export interface SubmitSupportTicketRequest {
  email: string;
  organizationName: string;
  problemType: string;
  description: string;
}

export interface SubmitSupportTicketResponse {
  success: boolean;
  message: string;
}
