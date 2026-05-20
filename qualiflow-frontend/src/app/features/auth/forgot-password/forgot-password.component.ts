import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService, ForgotPasswordRequest } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './forgot-password.component.html',
  styleUrls: ['./forgot-password.component.scss']
})
export class ForgotPasswordComponent implements OnInit {
  forgotForm!: FormGroup;
  isLoading = false;
  isSubmitted = false;
  errorMessage: string | null = null;

  constructor(
    private readonly fb: FormBuilder,
    private readonly authService: AuthService,
    private readonly router: Router,
    private readonly notificationService: NotificationService
  ) { }

  ngOnInit(): void {
    this.forgotForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]]
    });
  }

  onSubmit(): void {
    if (this.forgotForm.invalid) {
      this.forgotForm.markAllAsTouched();
      return;
    }

    this.isLoading = true;
    this.errorMessage = null;

    const payload: ForgotPasswordRequest = {
      email: this.forgotForm.value.email
    };

    this.authService.forgotPassword(payload).subscribe({
      next: () => {
        this.isSubmitted = true;
        this.notificationService.showSuccess('Code de reinitialisation envoye. Verifiez votre email.');
        // Redirect to step 2 after a short delay
        setTimeout(() => {
          this.router.navigate(['/reset-password'], { queryParams: { email: payload.email } });
        }, 1500);
      },
      error: (error: HttpErrorResponse) => {
        const message = error.error?.message || "Impossible d'envoyer la demande.";
        this.errorMessage = message;
        this.notificationService.showError(message);
        this.isLoading = false;
      },
      complete: () => {
        this.isLoading = false;
      }
    });
  }
}
