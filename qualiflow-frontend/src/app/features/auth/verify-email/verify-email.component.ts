import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  AuthService,
  ResendVerificationCodeRequest,
  VerifyEmailCodeRequest
} from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';

@Component({
  selector: 'app-verify-email',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './verify-email.component.html',
  styleUrls: ['./verify-email.component.scss']
})
export class VerifyEmailComponent implements OnInit {
  verificationForm!: FormGroup;
  status: 'input' | 'loading' | 'success' | 'error' = 'input';
  registeredEmail: string | null = null;
  errorMessage: string | null = null;
  isSubmitting = false;
  isResending = false;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly fb: FormBuilder,
    private readonly authService: AuthService,
    private readonly notificationService: NotificationService
  ) { }

  ngOnInit(): void {
    this.verificationForm = this.fb.group({
      code: ['', [Validators.required, Validators.pattern(/^\d{6}$/)]]
    });

    const token = this.route.snapshot.queryParamMap.get('token');
    this.registeredEmail = this.route.snapshot.queryParamMap.get('email');

    if (token) {
      this.status = 'loading';
      this.authService.verifyEmail(token).subscribe({
        next: () => {
          this.status = 'success';
          this.notificationService.showSuccess('Email verifie avec succes.');
        },
        error: () => {
          this.status = 'error';
          this.errorMessage = 'Lien de verification invalide ou expire.';
          this.notificationService.showError(this.errorMessage);
        }
      });
      return;
    }

    if (!this.registeredEmail) {
      this.status = 'error';
      this.errorMessage = 'Email manquant. Retournez a la page de creation de compte.';
    }
  }

  onVerifyCode(): void {
    if (!this.registeredEmail) {
      this.status = 'error';
      this.errorMessage = 'Email manquant. Retournez a la page de creation de compte.';
      return;
    }

    if (this.verificationForm.invalid) {
      this.verificationForm.markAllAsTouched();
      return;
    }

    this.isSubmitting = true;
    this.errorMessage = null;

    const payload: VerifyEmailCodeRequest = {
      email: this.registeredEmail,
      code: String(this.verificationForm.value.code ?? '').trim()
    };

    this.authService.verifyEmailCode(payload).subscribe({
      next: () => {
        this.status = 'success';
        this.notificationService.showSuccess('Compte verifie avec succes.');
      },
      error: (error: HttpErrorResponse) => {
        this.status = 'input';
        const message = error.error?.message || 'Code invalide ou expire.';
        this.errorMessage = message;
        this.notificationService.showError(message);
        this.isSubmitting = false;
      },
      complete: () => {
        this.isSubmitting = false;
      }
    });
  }

  onResendCode(): void {
    if (!this.registeredEmail) {
      this.status = 'error';
      this.errorMessage = 'Email manquant. Retournez a la page de creation de compte.';
      return;
    }

    this.isResending = true;

    const payload: ResendVerificationCodeRequest = {
      email: this.registeredEmail
    };

    this.authService.resendVerificationCode(payload).subscribe({
      next: () => {
        this.notificationService.showSuccess('Un nouveau code a ete envoye par email.');
      },
      error: (error: HttpErrorResponse) => {
        const message = error.error?.message || "Impossible d'envoyer un nouveau code.";
        this.notificationService.showError(message);
        this.isResending = false;
      },
      complete: () => {
        this.isResending = false;
      }
    });
  }
}
