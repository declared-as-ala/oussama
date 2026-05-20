import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, finalize, of, switchMap, tap } from 'rxjs';
import { AuthService, LoginRequest, LoginResponse, MeResponse } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';

interface DemoAccount {
  label: string;
  role: string;
  email: string;
  password: string;
}

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss']
})
export class LoginComponent implements OnInit {
  loginForm!: FormGroup;
  hidePassword = true;
  isLoading = false;

  antiBotQuestion = '';
  antiBotError: string | null = null;
  private antiBotExpectedAnswer = 0;

  readonly demoAccounts: DemoAccount[] = [
    {
      label: 'Super Admin',
      role: 'SUPER_ADMIN',
      email: 'superadmin@demo.local',
      password: 'SuperAdmin@123'
    },
    {
      label: 'Admin Organisation',
      role: 'ADMIN_ORG',
      email: 'admin@demo.local',
      password: 'Admin@123'
    },
    {
      label: 'Responsable Qualite',
      role: 'RESPONSABLE_QUALITE',
      email: 'qualite@demo.local',
      password: 'Qualite@123'
    },
    {
      label: 'Chef Service',
      role: 'CHEF_SERVICE',
      email: 'chef@demo.local',
      password: 'Chef@123'
    },
    {
      label: 'Utilisateur',
      role: 'UTILISATEUR',
      email: 'user@demo.local',
      password: 'User@123'
    }
  ];

  constructor(
    private readonly fb: FormBuilder,
    private readonly authService: AuthService,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly notificationService: NotificationService
  ) { }

  ngOnInit(): void {
    this.initForm();
  }

  private initForm(): void {
    this.loginForm = this.fb.group({
      email: [this.demoAccounts[0].email, [Validators.required, Validators.email]],
      password: [this.demoAccounts[0].password, [Validators.required]],
      antiBotAnswer: ['', [Validators.required, Validators.pattern(/^\d+$/)]]
    });

    this.regenerateAntiBotChallenge();

    const emailFromQuery = this.route.snapshot.queryParamMap.get('email');
    if (emailFromQuery) {
      this.loginForm.patchValue({
        email: emailFromQuery,
        password: '',
        antiBotAnswer: ''
      });
    }
  }

  useDemoAccount(account: DemoAccount): void {
    this.loginForm.patchValue({
      email: account.email,
      password: account.password,
      antiBotAnswer: ''
    });

    this.antiBotError = null;
    this.hidePassword = true;
    this.loginForm.markAsDirty();
    this.loginForm.markAsTouched();
  }

  regenerateAntiBotChallenge(): void {
    const first = this.generateOperand();
    const second = this.generateOperand();
    this.antiBotQuestion = `${first} + ${second}`;
    this.antiBotExpectedAnswer = first + second;
    this.antiBotError = null;
    this.loginForm?.patchValue({ antiBotAnswer: '' });
  }

  onSubmit(): void {
    if (this.loginForm.invalid) {
      return;
    }

    if (!this.isAntiBotAnswerCorrect()) {
      this.regenerateAntiBotChallenge();
      this.antiBotError = 'Resultat incorrect. Veuillez reessayer.';
      return;
    }

    this.isLoading = true;
    this.antiBotError = null;

    const payload: LoginRequest = {
      email: String(this.loginForm.value.email ?? '').trim(),
      password: String(this.loginForm.value.password ?? '')
    };

    this.authService.login(payload).pipe(
      switchMap((response: LoginResponse) => {
        if (!response.accessToken || !response.refreshToken) {
          return of(null);
        }

        return (
        this.authService.getProfile().pipe(
          tap((profile: MeResponse) => {
            this.notificationService.showSuccess('Connexion reussie !');
            this.navigateAfterLogin(profile.role);
          }),
          catchError((_profileError: HttpErrorResponse) => {
            if (response.role) {
              this.navigateAfterLogin(response.role);
            }
            return of(null);
          })
        )
        );
      }),
      catchError((error: HttpErrorResponse) => {
        const requiresEmailVerification = Boolean(error?.error?.requiresEmailVerification);
        const message = String(error?.error?.message ?? '').toLowerCase();
        if (requiresEmailVerification || message.includes('vérifier votre email') || message.includes('verifier votre email')) {
          return this.authService.resendVerificationCode({ email: payload.email }).pipe(
            tap(() => {
              this.router.navigate(['/verify-email'], {
                queryParams: { email: payload.email }
              });
            }),
            catchError(() => {
              this.router.navigate(['/verify-email'], {
                queryParams: { email: payload.email }
              });
              return of(null);
            })
          );
        }

        this.regenerateAntiBotChallenge();
        return of(null);
      }),
      finalize(() => {
        this.isLoading = false;
      })
    ).subscribe();
  }

  private navigateAfterLogin(role: string): void {
    if (role === 'SUPER_ADMIN') {
      this.router.navigate(['/super-admin/dashboard']);
      return;
    }

    this.router.navigate(['/dashboard']);
  }

  private generateOperand(): number {
    return Math.floor(Math.random() * 9) + 1;
  }

  private isAntiBotAnswerCorrect(): boolean {
    const rawValue = this.loginForm.get('antiBotAnswer')?.value;
    const parsedValue = Number.parseInt(String(rawValue ?? ''), 10);
    return Number.isFinite(parsedValue) && parsedValue === this.antiBotExpectedAnswer;
  }

  get antiBotAnswerControl() {
    return this.loginForm.get('antiBotAnswer');
  }
}
