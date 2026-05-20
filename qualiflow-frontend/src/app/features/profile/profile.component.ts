import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { Subscription } from 'rxjs';
import { ActivatedRoute } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import {
  AuthService,
  ChangePasswordRequest,
  ConfirmEmailChangeRequest,
  RequestEmailChangeCodeRequest,
  UpdateProfileRequest
} from '../../core/services/auth.service';
import { NotificationService } from '../../core/services/notification.service';
import {
  OrganizationResponse,
  OrganizationService,
  UpdateOrganizationRequest
} from '../../core/services/organization.service';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTabsModule,
    MatSlideToggleModule
  ],
  templateUrl: './profile.component.html',
  styleUrls: ['./profile.component.scss']
})
export class ProfileComponent implements OnInit, OnDestroy {
  private readonly subscriptions = new Subscription(); // trigger
  readonly typeOptions = ['UNIVERSITE', 'INSTITUT', 'CENTRE', 'ENTREPRISE'];
  readonly languageOptions: Array<{ value: 'fr' | 'en' | 'ar'; label: string }> = [
    { value: 'fr', label: 'Francais' },
    { value: 'en', label: 'English' },
    { value: 'ar', label: 'Arabe' }
  ];

  readonly personalForm = this.fb.group({
    firstName: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(2)]),
    lastName: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(2)]),
    birthDate: this.fb.control<string>(''),
    phone: this.fb.control<string>(''),
    city: this.fb.control<string>(''),
    preferredLanguage: this.fb.nonNullable.control<'fr' | 'en' | 'ar'>('fr', [Validators.required])
  });

  readonly passwordForm = this.fb.group({
    currentPassword: this.fb.nonNullable.control('', [Validators.required]),
    newPassword: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(8)]),
    confirmPassword: this.fb.nonNullable.control('', [Validators.required])
  });

  readonly emailChangeForm = this.fb.group({
    newEmail: this.fb.nonNullable.control('', [Validators.required, Validators.email]),
    code: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(6), Validators.maxLength(6)])
  });
  readonly resetWithCodeForm = this.fb.group({
    code: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(6), Validators.maxLength(6)]),
    newPassword: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(8)]),
    confirmPassword: this.fb.nonNullable.control('', [Validators.required])
  });

  readonly organizationForm = this.fb.group({
    name: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(2)]),
    type: this.fb.nonNullable.control('INSTITUT', [Validators.required]),
    address: this.fb.control<string>(''),
    email: this.fb.control<string>('', Validators.email),
    phone: this.fb.control<string>(''),
    fax: this.fb.control<string>(''),
    website: this.fb.control<string>(''),
    description: this.fb.control<string>(''),
    status: this.fb.nonNullable.control('ACTIF'),
    subscriptionDaysRemaining: this.fb.nonNullable.control(30, [Validators.required, Validators.min(0)]),
    subscriptionMonitorEnabled: this.fb.nonNullable.control(true)
  });

  loading = false;
  savingProfile = false;
  changingPassword = false;
  uploadingProfilePhoto = false;
  requestingEmailCode = false;
  confirmingEmailChange = false;
  savingOrganization = false;
  uploadingLogo = false;
  sendingResetEmail = false;
  verifyingResetCode = false;
  resettingPasswordWithCode = false;
  resetCodeVerified = false;
  resetEmailTarget = '';
  forgotPasswordStep: 0 | 1 | 2 = 0;
  locatingAddress = false;

  organization: OrganizationResponse | null = null;
  logoObjectUrl: string | null = null;
  selectedLogo: File | null = null;

  profilePhotoObjectUrl: string | null = null;
  selectedProfilePhoto: File | null = null;
  organizationOnlyView = false;

  constructor(
    private readonly fb: FormBuilder,
    private readonly route: ActivatedRoute,
    private readonly http: HttpClient,
    private readonly authService: AuthService,
    private readonly organizationService: OrganizationService,
    private readonly notificationService: NotificationService
  ) { }

  ngOnInit(): void {
    this.subscriptions.add(
      this.route.queryParamMap.subscribe(params => {
        const requestedView = params.get('view');
        this.organizationOnlyView = requestedView === 'organization' && this.canManageOrganization;
      })
    );

    this.loadProfile();
    this.loadProfilePhoto();

    if (this.canManageOrganization) {
      this.loadOrganization();
    }
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
    this.revokeLogoObjectUrl();
    this.revokeProfilePhotoObjectUrl();
  }

  get currentUser() {
    return this.authService.getCurrentUser();
  }

  get canManageOrganization(): boolean {
    return this.authService.hasRole('ADMIN_ORG');
  }

  get displayName(): string {
    const user = this.currentUser;
    if (!user) {
      return 'Utilisateur';
    }

    const fullName = `${user.firstName} ${user.lastName}`.trim();
    return fullName || user.email || 'Utilisateur';
  }

  get profileInitials(): string {
    const user = this.currentUser;
    if (!user) {
      return 'U';
    }

    return `${user.firstName?.[0] || ''}${user.lastName?.[0] || ''}`.toUpperCase() || 'U';
  }

  savePersonalProfile(): void {
    if (this.personalForm.invalid) {
      this.personalForm.markAllAsTouched();
      return;
    }

    const raw = this.personalForm.getRawValue();
    const payload: UpdateProfileRequest = {
      firstName: raw.firstName.trim(),
      lastName: raw.lastName.trim(),
      birthDate: raw.birthDate ? raw.birthDate : null,
      phone: raw.phone?.trim() || null,
      city: raw.city?.trim() || null,
      preferredLanguage: raw.preferredLanguage
    };

    this.savingProfile = true;
    this.authService.updateProfile(payload).subscribe({
      next: () => {
        this.savingProfile = false;
        this.notificationService.showSuccess('Profil mis a jour avec succes.');
      },
      error: () => {
        this.savingProfile = false;
        this.notificationService.showError('Mise a jour du profil impossible.');
      }
    });
  }

  changePassword(): void {
    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }

    const raw = this.passwordForm.getRawValue();
    const payload: ChangePasswordRequest = {
      currentPassword: raw.currentPassword,
      newPassword: raw.newPassword,
      confirmPassword: raw.confirmPassword
    };

    this.changingPassword = true;
    this.authService.changePassword(payload).subscribe({
      next: () => {
        this.changingPassword = false;
        this.passwordForm.reset({
          currentPassword: '',
          newPassword: '',
          confirmPassword: ''
        });
        this.notificationService.showSuccess('Mot de passe modifie avec succes.');
      },
      error: () => {
        this.changingPassword = false;
        this.notificationService.showError('Modification du mot de passe impossible.');
      }
    });
  }

  initiatePasswordReset(): void {
    this.resetCodeVerified = false;
    this.resetWithCodeForm.patchValue({ code: '' });
    this.forgotPasswordStep = 0;

    this.sendingResetEmail = true;
    this.authService.getProfile().subscribe({
      next: (profile) => {
        const email = String(profile?.email ?? '').trim().toLowerCase();
        const isValidEmail = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);

        if (!isValidEmail) {
          this.sendingResetEmail = false;
          this.notificationService.showError('Adresse email invalide sur votre profil. Veuillez la corriger puis réessayer.');
          return;
        }

        this.authService.forgotPassword({ email }).subscribe({
          next: () => {
            this.sendingResetEmail = false;
            this.resetEmailTarget = email;
            this.forgotPasswordStep = 1;
            this.notificationService.showSuccess(`Un code de réinitialisation a été envoyé à ${email}`);
          },
          error: () => {
            this.sendingResetEmail = false;
            this.notificationService.showError('Impossible d\'envoyer le code de réinitialisation.');
          }
        });
      },
      error: () => {
        this.sendingResetEmail = false;
        this.notificationService.showError('Impossible de charger votre profil pour vérifier l\'email.');
      }
    });
  }

  verifyResetCodeFromProfile(): void {
    const code = String(this.resetWithCodeForm.value.code ?? '').trim();
    if (!code) {
      this.resetWithCodeForm.controls.code.markAsTouched();
      return;
    }

    const email = this.resetEmailTarget || String(this.currentUser?.email ?? '').trim().toLowerCase();
    if (!email) {
      this.notificationService.showError('Email introuvable. Cliquez d abord sur "J ai oublié mon mot de passe actuel."');
      return;
    }

    this.verifyingResetCode = true;
    this.authService.verifyResetCode({ email, code }).subscribe({
      next: () => {
        this.verifyingResetCode = false;
        this.resetCodeVerified = true;
        this.forgotPasswordStep = 2;
        this.notificationService.showSuccess('Code valide. Vous pouvez maintenant définir un nouveau mot de passe.');
      },
      error: () => {
        this.verifyingResetCode = false;
        this.resetCodeVerified = false;
        this.notificationService.showError('Code invalide ou expiré.');
      }
    });
  }

  resetPasswordWithCode(): void {
    if (this.resetWithCodeForm.invalid) {
      this.resetWithCodeForm.markAllAsTouched();
      return;
    }

    const raw = this.resetWithCodeForm.getRawValue();
    if (raw.newPassword !== raw.confirmPassword) {
      this.notificationService.showError('La confirmation du mot de passe ne correspond pas.');
      return;
    }

    const email = this.resetEmailTarget || String(this.currentUser?.email ?? '').trim().toLowerCase();
    if (!email) {
      this.notificationService.showError('Email introuvable. Cliquez d abord sur "J ai oublié mon mot de passe actuel."');
      return;
    }

    this.resettingPasswordWithCode = true;
    this.authService.resetPassword({
      email,
      code: raw.code.trim(),
      newPassword: raw.newPassword,
      confirmPassword: raw.confirmPassword
    }).subscribe({
      next: () => {
        this.resettingPasswordWithCode = false;
        this.resetCodeVerified = false;
        this.forgotPasswordStep = 0;
        this.resetWithCodeForm.reset({
          code: '',
          newPassword: '',
          confirmPassword: ''
        });
        this.notificationService.showSuccess('Mot de passe réinitialisé avec succès. Déconnexion en cours...');
        this.authService.forceLogout();
      },
      error: () => {
        this.resettingPasswordWithCode = false;
        this.notificationService.showError('Impossible de réinitialiser le mot de passe.');
      }
    });
  }

  onProfilePhotoSelected(event: Event): void {
    const target = event.target as HTMLInputElement;
    this.selectedProfilePhoto = target.files?.[0] ?? null;
  }

  removeSelectedProfilePhoto(): void {
    this.selectedProfilePhoto = null;
  }

  requestEmailChangeCode(): void {
    const newEmailControl = this.emailChangeForm.controls.newEmail;
    if (newEmailControl.invalid) {
      newEmailControl.markAsTouched();
      return;
    }

    const payload: RequestEmailChangeCodeRequest = {
      newEmail: newEmailControl.value.trim().toLowerCase()
    };

    this.requestingEmailCode = true;
    this.authService.requestEmailChangeCode(payload).subscribe({
      next: () => {
        this.requestingEmailCode = false;
        this.notificationService.showSuccess('Code de verification envoye au nouvel email.');
      },
      error: () => {
        this.requestingEmailCode = false;
        this.notificationService.showError('Impossible d\'envoyer le code de verification.');
      }
    });
  }

  confirmEmailChange(): void {
    if (this.emailChangeForm.invalid) {
      this.emailChangeForm.markAllAsTouched();
      return;
    }

    const raw = this.emailChangeForm.getRawValue();
    const payload: ConfirmEmailChangeRequest = {
      newEmail: raw.newEmail.trim().toLowerCase(),
      code: raw.code.trim()
    };

    this.confirmingEmailChange = true;
    this.authService.confirmEmailChange(payload).subscribe({
      next: () => {
        this.confirmingEmailChange = false;
        this.emailChangeForm.patchValue({ code: '' });
        this.notificationService.showSuccess('Email modifie avec succes.');
        this.loadProfile();
      },
      error: () => {
        this.confirmingEmailChange = false;
        this.notificationService.showError('Code invalide ou changement d\'email impossible.');
      }
    });
  }

  uploadProfilePhoto(): void {
    if (!this.selectedProfilePhoto) {
      return;
    }

    this.uploadingProfilePhoto = true;
    this.authService.uploadProfilePhoto(this.selectedProfilePhoto).subscribe({
      next: () => {
        this.uploadingProfilePhoto = false;
        this.selectedProfilePhoto = null;
        this.notificationService.showSuccess('Photo de profil mise a jour.');
        this.loadProfile();
        this.loadProfilePhoto();
      },
      error: () => {
        this.uploadingProfilePhoto = false;
        this.notificationService.showError('Upload photo impossible.');
      }
    });
  }

  onLogoSelected(event: Event): void {
    const target = event.target as HTMLInputElement;
    const file = target.files?.[0] ?? null;
    this.selectedLogo = file;
  }

  removeSelectedLogo(): void {
    this.selectedLogo = null;
  }

  saveOrganization(): void {
    if (!this.canManageOrganization) {
      return;
    }

    if (this.organizationForm.invalid) {
      this.organizationForm.markAllAsTouched();
      return;
    }

    const raw = this.organizationForm.getRawValue();
    const payload: UpdateOrganizationRequest = {
      name: raw.name.trim(),
      type: raw.type.trim(),
      address: raw.address?.trim() || null,
      email: raw.email?.trim() || null,
      phone: raw.phone?.trim() || null,
      fax: raw.fax?.trim() || null,
      website: raw.website?.trim() || null,
      description: raw.description?.trim() || null,
      status: raw.status,
      subscriptionDaysRemaining: Number.isFinite(raw.subscriptionDaysRemaining) ? Math.max(raw.subscriptionDaysRemaining, 0) : null,
      subscriptionMonitorEnabled: raw.subscriptionMonitorEnabled
    };

    this.savingOrganization = true;
    this.organizationService.updateMyOrganization(payload).subscribe({
      next: () => {
        this.savingOrganization = false;
        this.notificationService.showSuccess('Informations organisation mises a jour.');
        this.loadOrganization();
      },
      error: () => {
        this.savingOrganization = false;
        this.notificationService.showError('Mise a jour organisation impossible.');
      }
    });
  }

  fillAddressFromGps(): void {
    if (!this.canManageOrganization || this.locatingAddress) {
      return;
    }

    if (!navigator.geolocation) {
      this.notificationService.showError('La geolocalisation n est pas supportee sur ce navigateur.');
      return;
    }

    this.locatingAddress = true;

    navigator.geolocation.getCurrentPosition(
      (position) => {
        const lat = position.coords.latitude;
        const lon = position.coords.longitude;

        this.http.get<{
          address?: {
            road?: string;
            pedestrian?: string;
            city?: string;
            town?: string;
            village?: string;
            municipality?: string;
            county?: string;
            state?: string;
            postcode?: string;
            country?: string;
          };
          display_name?: string;
        }>('https://nominatim.openstreetmap.org/reverse', {
          params: {
            format: 'jsonv2',
            lat: String(lat),
            lon: String(lon),
            addressdetails: '1'
          }
        }).subscribe({
          next: (result) => {
            const addr = result?.address;
            const street = addr?.road || addr?.pedestrian || '';
            const city = addr?.city || addr?.town || addr?.village || addr?.municipality || addr?.county || addr?.state || '';
            const postalCode = addr?.postcode || '';
            const country = addr?.country || '';

            const completeAddress = [street, city, postalCode, country]
              .map(part => part?.trim())
              .filter(part => !!part)
              .join(', ');

            this.organizationForm.patchValue({
              address: completeAddress || result?.display_name || ''
            });

            this.locatingAddress = false;
            this.notificationService.showSuccess('Adresse remplie automatiquement depuis votre position GPS.');
          },
          error: () => {
            this.locatingAddress = false;
            this.notificationService.showError('Impossible de recuperer l adresse depuis votre position GPS.');
          }
        });
      },
      () => {
        this.locatingAddress = false;
        this.notificationService.showError('Acces a la position refuse ou indisponible.');
      },
      {
        enableHighAccuracy: true,
        timeout: 10000,
        maximumAge: 0
      }
    );
  }

  uploadLogo(): void {
    if (!this.canManageOrganization || !this.selectedLogo) {
      return;
    }

    this.uploadingLogo = true;
    this.organizationService.uploadMyOrganizationLogo(this.selectedLogo).subscribe({
      next: () => {
        this.uploadingLogo = false;
        this.selectedLogo = null;
        this.notificationService.showSuccess('Logo organisation mis a jour.');
        this.organizationService.notifyLogoUpdated();
        this.loadLogo();
      },
      error: () => {
        this.uploadingLogo = false;
        this.notificationService.showError('Upload logo impossible.');
      }
    });
  }

  private loadProfile(): void {
    this.authService.getProfile().subscribe({
      next: (profile) => {
        this.personalForm.patchValue({
          firstName: profile.firstName,
          lastName: profile.lastName,
          birthDate: profile.birthDate ? profile.birthDate.toString().substring(0, 10) : '',
          phone: profile.phone ?? '',
          city: profile.city ?? '',
          preferredLanguage: profile.preferredLanguage || 'fr'
        });
      },
      error: () => {
        this.notificationService.showError('Impossible de charger le profil utilisateur.');
      }
    });
  }

  private loadProfilePhoto(): void {
    this.revokeProfilePhotoObjectUrl();
    this.authService.downloadProfilePhoto().subscribe({
      next: (blob) => {
        if (!blob || blob.size === 0) {
          this.profilePhotoObjectUrl = null;
          return;
        }

        this.profilePhotoObjectUrl = URL.createObjectURL(blob);
      },
      error: () => {
        this.profilePhotoObjectUrl = null;
      }
    });
  }

  private loadOrganization(): void {
    this.loading = true;
    this.organizationService.getMyOrganization().subscribe({
      next: (organization) => {
        this.organization = organization;
        this.organizationForm.patchValue({
          name: organization.name,
          type: organization.type ?? 'INSTITUT',
          address: organization.address ?? '',
          email: organization.email ?? '',
          phone: organization.phone ?? '',
          fax: organization.fax ?? '',
          website: organization.website ?? '',
          description: organization.description ?? '',
          status: organization.status || 'ACTIF',
          subscriptionDaysRemaining: organization.subscriptionDaysRemaining ?? 30,
          subscriptionMonitorEnabled: organization.subscriptionMonitorEnabled ?? true
        });
        this.loading = false;
        this.loadLogo();
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Impossible de charger votre organisation.');
      }
    });
  }

  private loadLogo(): void {
    this.revokeLogoObjectUrl();
    this.organizationService.downloadMyOrganizationLogo().subscribe({
      next: (blob) => {
        this.logoObjectUrl = URL.createObjectURL(blob);
      },
      error: () => {
        this.logoObjectUrl = null;
      }
    });
  }

  private revokeLogoObjectUrl(): void {
    if (this.logoObjectUrl) {
      URL.revokeObjectURL(this.logoObjectUrl);
      this.logoObjectUrl = null;
    }
  }

  private revokeProfilePhotoObjectUrl(): void {
    if (this.profilePhotoObjectUrl) {
      URL.revokeObjectURL(this.profilePhotoObjectUrl);
      this.profilePhotoObjectUrl = null;
    }
  }
}
