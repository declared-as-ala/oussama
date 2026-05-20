import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  AuthService,
  ForgotPasswordRequest,
  ResetPasswordRequest
} from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';

@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './reset-password.component.html',
  styleUrls: ['./reset-password.component.scss']
})
export class ResetPasswordComponent implements OnInit {
  resetForm!: FormGroup;
  step: 'code' | 'password' = 'code';
  isLoading = false;
  isResending = false;
  hidePassword = true;
  errorMessage: string | null = null;

  passwordRequirements = {
    length: false,
    upper: false,
    lower: false,
    digit: false,
    special: false
  };

  constructor(
    private readonly fb: FormBuilder,
    private readonly authService: AuthService,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly notificationService: NotificationService
  ) { }

  ngOnInit(): void {
    this.resetForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      code: ['', [Validators.required, Validators.pattern(/^\d{6}$/)]],
      newPassword: ['', [Validators.required, Validators.minLength(8), Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#$%^&*(),.?":{}|<>])[A-Za-z\d!@#$%^&*(),.?":{}|<>]{8,}$/)]],
      confirmPassword: ['', [Validators.required, Validators.minLength(8)]]
    }, { validators: this.passwordMatchValidator });

    // Watch password changes for requirements feedback
    this.resetForm.get('newPassword')?.valueChanges.subscribe(value => {
      this.checkPasswordStrength(value || '');
    });

    const emailFromQuery = this.route.snapshot.queryParamMap.get('email');
    if (emailFromQuery) {
      this.resetForm.patchValue({ email: emailFromQuery });
    } else {
      // If no email, they shouldn't be here direct, redirect back to step 1
      this.notificationService.showError("Veuillez d'abord saisir votre email.");
      this.router.navigate(['/forgot-password']);
    }
  }

  onVerifyCode(): void {
    const email = this.resetForm.get('email')?.value;
    const code = this.resetForm.get('code')?.value;

    if (!email || !code || this.resetForm.get('code')?.invalid) {
      return;
    }

    this.isLoading = true;
    this.errorMessage = null;

    this.authService.verifyResetCode({ email, code }).subscribe({
      next: (res: { success: boolean, message?: string }) => {
        if (res.success) {
          this.step = 'password';
          this.notificationService.showSuccess('Code validé. Définissez votre nouveau mot de passe.');
        } else {
          this.errorMessage = res.message || 'Code invalide ou expiré.';
        }
      },
      error: (error: HttpErrorResponse) => {
        this.errorMessage = error.error?.message || 'Code invalide ou expiré.';
        this.isLoading = false;
      },
      complete: () => {
        this.isLoading = false;
      }
    });
  }

  onSubmit(): void {
    if (this.resetForm.invalid) {
      this.resetForm.markAllAsTouched();
      return;
    }

    this.isLoading = true;
    this.errorMessage = null;

    const payload: ResetPasswordRequest = {
      email: String(this.resetForm.value.email ?? '').trim(),
      code: String(this.resetForm.value.code ?? '').trim(),
      newPassword: String(this.resetForm.value.newPassword ?? ''),
      confirmPassword: String(this.resetForm.value.confirmPassword ?? '')
    };

    this.authService.resetPassword(payload).subscribe({
      next: () => {
        this.notificationService.showSuccess('Mot de passe réinitialisé. Connectez-vous.');
        this.router.navigate(['/login'], { queryParams: { email: payload.email } });
      },
      error: (error: HttpErrorResponse) => {
        this.errorMessage = error.error?.message || 'Erreur de réinitialisation.';
        this.isLoading = false;
      },
      complete: () => {
        this.isLoading = false;
      }
    });
  }

  onResendCode(): void {
    const email = String(this.resetForm.get('email')?.value ?? '').trim();
    if (!email) {
      this.errorMessage = "Entrez d'abord votre email.";
      return;
    }

    this.isResending = true;
    this.errorMessage = null;

    const payload: ForgotPasswordRequest = { email };
    this.authService.forgotPassword(payload).subscribe({
      next: () => {
        this.notificationService.showSuccess('Nouveau code envoye par email.');
      },
      error: (error: HttpErrorResponse) => {
        this.errorMessage = error.error?.message || "Impossible d'envoyer le code.";
      },
      complete: () => {
        this.isResending = false;
      }
    });
  }

  get emailControl() {
    return this.resetForm.get('email');
  }

  checkPasswordStrength(password: string): void {
    this.passwordRequirements.length = password.length >= 8;
    this.passwordRequirements.upper = /[A-Z]/.test(password);
    this.passwordRequirements.lower = /[a-z]/.test(password);
    this.passwordRequirements.digit = /[0-9]/.test(password);
    this.passwordRequirements.special = /[!@#$%^&*(),.?":{}|<>]/.test(password);
  }

  private passwordMatchValidator(group: FormGroup) {
    const newPassword = group.get('newPassword')?.value;
    const confirmPassword = group.get('confirmPassword')?.value;
    return newPassword === confirmPassword ? null : { mismatch: true };
  }
}
