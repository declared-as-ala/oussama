import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { AuthService, RegisterRequest, RegisterResponse } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';

interface Country {
  code: string;
  label: string;
  flag: string;
  dial: string;
}

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './register.component.html',
  styleUrls: ['./register.component.scss']
})
export class RegisterComponent implements OnInit {
  registerForm!: FormGroup;
  isLoading = false;
  hidePassword = true;
  hideConfirmPassword = true;
  errorMessage: string | null = null;
  selectedDialCode = '+216';

  passwordRequirements = {
    length: false,
    upper: false,
    lower: false,
    digit: false,
    special: false
  };

  captchaNum1 = 0;
  captchaNum2 = 0;

  readonly nationalities: Country[] = [
    { code: 'TN', label: 'Tunisie', flag: '🇹🇳', dial: '+216' },
    { code: 'FR', label: 'France', flag: '🇫🇷', dial: '+33' },
    { code: 'DZ', label: 'Algérie', flag: '🇩🇿', dial: '+213' },
    { code: 'MA', label: 'Maroc', flag: '🇲🇦', dial: '+212' },
    { code: 'LY', label: 'Libye', flag: '🇱🇾', dial: '+218' },
    { code: 'MR', label: 'Mauritanie', flag: '🇲🇷', dial: '+222' },
    { code: 'EG', label: 'Égypte', flag: '🇪🇬', dial: '+20' },
    { code: 'SA', label: 'Arabie Saoudite', flag: '🇸🇦', dial: '+966' },
    { code: 'AE', label: 'Émirats Arabes Unis', flag: '🇦🇪', dial: '+971' },
    { code: 'QA', label: 'Qatar', flag: '🇶🇦', dial: '+974' },
    { code: 'KW', label: 'Koweït', flag: '🇰🇼', dial: '+965' },
    { code: 'JO', label: 'Jordanie', flag: '🇯🇴', dial: '+962' },
    { code: 'LB', label: 'Liban', flag: '🇱🇧', dial: '+961' },
    { code: 'IQ', label: 'Irak', flag: '🇮🇶', dial: '+964' },
    { code: 'SY', label: 'Syrie', flag: '🇸🇾', dial: '+963' },
    { code: 'SD', label: 'Soudan', flag: '🇸🇩', dial: '+249' },
    { code: 'BE', label: 'Belgique', flag: '🇧🇪', dial: '+32' },
    { code: 'CH', label: 'Suisse', flag: '🇨🇭', dial: '+41' },
    { code: 'CA', label: 'Canada', flag: '🇨🇦', dial: '+1' },
    { code: 'DE', label: 'Allemagne', flag: '🇩🇪', dial: '+49' },
    { code: 'GB', label: 'Royaume-Uni', flag: '🇬🇧', dial: '+44' },
    { code: 'ES', label: 'Espagne', flag: '🇪🇸', dial: '+34' },
    { code: 'IT', label: 'Italie', flag: '🇮🇹', dial: '+39' },
    { code: 'US', label: 'États-Unis', flag: '🇺🇸', dial: '+1' },
    { code: 'TR', label: 'Turquie', flag: '🇹🇷', dial: '+90' },
    { code: 'SN', label: 'Sénégal', flag: '🇸🇳', dial: '+221' },
    { code: 'CI', label: "Côte d'Ivoire", flag: '🇨🇮', dial: '+225' },
    { code: 'CM', label: 'Cameroun', flag: '🇨🇲', dial: '+237' },
    { code: 'ML', label: 'Mali', flag: '🇲🇱', dial: '+223' },
    { code: 'NE', label: 'Niger', flag: '🇳🇪', dial: '+227' },
    { code: 'TD', label: 'Tchad', flag: '🇹🇩', dial: '+235' },
    { code: 'BF', label: 'Burkina Faso', flag: '🇧🇫', dial: '+226' },
    { code: 'GA', label: 'Gabon', flag: '🇬🇦', dial: '+241' },
  ];

  get selectedCountry(): Country {
    const val = this.registerForm?.get('nationality')?.value;
    return this.nationalities.find(n => n.code === val) || this.nationalities[0];
  }

  constructor(
    private readonly fb: FormBuilder,
    private readonly authService: AuthService,
    private readonly router: Router,
    private readonly notificationService: NotificationService
  ) { }

  ngOnInit(): void {
    this.registerForm = this.fb.group({
      firstName: ['', [Validators.required, Validators.minLength(2)]],
      lastName: ['', [Validators.required, Validators.minLength(2)]],
      email: ['', [Validators.required, Validators.email]],
      phone: ['', [Validators.required, Validators.minLength(8)]],
      nationality: ['TN', [Validators.required]],
      organizationCode: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(50)]],
      birthDate: ['', [Validators.required]],
      password: ['', [Validators.required, Validators.minLength(8), Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#$%^&*(),.?":{}|<>])[A-Za-z\d!@#$%^&*(),.?":{}|<>]{8,}$/)]],
      confirmPassword: ['', [Validators.required, Validators.minLength(8)]],
      captcha: ['', [Validators.required]],
      acceptTerms: [false, [Validators.requiredTrue]]
    });

    this.generateCaptcha();

    // Watch password changes for requirements feedback
    this.registerForm.get('password')?.valueChanges.subscribe(value => {
      this.checkPasswordStrength(value || '');
    });

    // Update dial code when nationality changes
    this.registerForm.get('nationality')?.valueChanges.subscribe(code => {
      const country = this.nationalities.find(n => n.code === code);
      if (country) {
        this.selectedDialCode = country.dial;
      }
    });
  }

  onNationalityChange(): void {
    const country = this.selectedCountry;
    this.selectedDialCode = country.dial;
  }

  checkPasswordStrength(password: string): void {
    this.passwordRequirements.length = password.length >= 8;
    this.passwordRequirements.upper = /[A-Z]/.test(password);
    this.passwordRequirements.lower = /[a-z]/.test(password);
    this.passwordRequirements.digit = /[0-9]/.test(password);
    this.passwordRequirements.special = /[!@#$%^&*(),.?":{}|<>]/.test(password);
  }

  generateCaptcha(): void {
    this.captchaNum1 = Math.floor(Math.random() * 10) + 1;
    this.captchaNum2 = Math.floor(Math.random() * 10) + 1;
  }

  get captchaValid(): boolean {
    const answer = parseInt(this.registerForm.get('captcha')?.value || '0', 10);
    return answer === (this.captchaNum1 + this.captchaNum2);
  }

  onSubmit(): void {
    if (this.registerForm.invalid || this.passwordMismatch || !this.captchaValid) {
      if (!this.captchaValid && this.registerForm.get('captcha')?.value) {
        this.notificationService.showError('Réponse CAPTCHA incorrecte.');
        this.generateCaptcha();
        this.registerForm.patchValue({ captcha: '' });
      }
      this.registerForm.markAllAsTouched();
      return;
    }

    this.isLoading = true;
    this.errorMessage = null;

    const country = this.selectedCountry;
    const rawPhone = this.registerForm.value.phone;
    const fullPhone = rawPhone.startsWith('+') ? rawPhone : `${this.selectedDialCode}${rawPhone}`;

    const payload: RegisterRequest = {
      firstName: this.registerForm.value.firstName,
      lastName: this.registerForm.value.lastName,
      email: this.registerForm.value.email,
      phone: fullPhone,
      nationality: country.label,
      organizationCode: this.registerForm.value.organizationCode,
      birthDate: this.registerForm.value.birthDate,
      password: this.registerForm.value.password,
      confirmPassword: this.registerForm.value.confirmPassword,
      captchaNum1: this.captchaNum1,
      captchaNum2: this.captchaNum2,
      captchaAnswer: parseInt(this.registerForm.value.captcha || '0', 10)
    };

    this.authService.register(payload).subscribe({
      next: (response: RegisterResponse) => {
        if (response.requiresEmailVerification) {
          if (response.verificationEmailSent === false) {
            this.notificationService.showWarning(
              "Compte créé, mais l'envoi de l'email a échoué. Ouvrez l'écran de vérification puis cliquez sur 'Renvoyer le code'."
            );
          } else {
            this.notificationService.showSuccess('Compte créé. Vérifiez votre boîte mail pour activer votre compte.');
          }

          this.router.navigate(['/verify-email'], {
            queryParams: { email: payload.email }
          });
          return;
        }

        this.notificationService.showSuccess('Compte créé avec succès. Vous pouvez vous connecter.');
        this.router.navigate(['/login'], {
          queryParams: { email: payload.email }
        });
      },
      error: (error: HttpErrorResponse) => {
        const message = error.error?.message || 'Impossible de créer le compte.';
        this.errorMessage = message;
        this.notificationService.showError(message);
        this.isLoading = false;
        this.generateCaptcha();
        this.registerForm.patchValue({ captcha: '' });
      },
      complete: () => {
        this.isLoading = false;
      }
    });
  }

  get passwordMismatch(): boolean {
    const password = this.registerForm?.get('password')?.value;
    const confirmPassword = this.registerForm?.get('confirmPassword')?.value;
    return !!password && !!confirmPassword && password !== confirmPassword;
  }
}
