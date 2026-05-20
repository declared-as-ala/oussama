import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatInputModule } from '@angular/material/input';
import { ThemeService } from '../../core/services/theme.service';
import { AuthService } from '../../core/services/auth.service';
import { NotificationService } from '../../core/services/notification.service';
import { finalize, map } from 'rxjs/operators';
import { TranslatePipe } from '../../shared/pipes/translate.pipe';
import { SupportService } from './services/support.service';
import { SupportContactInfoResponse } from './models/support.models';
import { NotificationSettingsService } from './services/notification-settings.service';
import { NotificationPreferenceResponse } from './models/notification-preference.models';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatSlideToggleModule,
    MatSelectModule,
    MatFormFieldModule,
    MatIconModule,
    MatButtonModule,
    MatInputModule,
    TranslatePipe
  ],
  templateUrl: './settings.component.html',
  styleUrls: ['./settings.component.scss']
})
export class SettingsComponent {
  readonly notificationPreferenceOptions: Array<{ type: string; label: string; description: string }> = [
    {
      type: 'DOCUMENT_APPROVAL_REQUIRED',
      label: 'Validation des documents',
      description: 'Recevoir les demandes de validation et de revue documentaire.'
    },
    {
      type: 'DOCUMENT_EXPIRED',
      label: 'Documents expirés',
      description: 'Être averti lorsqu’un document arrive à expiration ou devient périmé.'
    },
    {
      type: 'DOCUMENT_NEW_VERSION',
      label: 'Nouvelles versions',
      description: 'Suivre les nouvelles versions publiées des documents.'
    },
    {
      type: 'NONCONFORMITY_CREATED',
      label: 'Non-conformités',
      description: 'Recevoir les alertes de création de non-conformités.'
    },
    {
      type: 'CORRECTIVE_ACTION_ASSIGNED',
      label: 'Actions correctives',
      description: 'Être notifié lors de l’assignation d’une action corrective.'
    },
    {
      type: 'INDICATOR_ALERT',
      label: 'Alertes KPI',
      description: 'Recevoir les alertes liées aux indicateurs en dépassement.'
    },
    {
      type: 'SYSTEM_ALERT',
      label: 'Alertes système',
      description: 'Être informé des incidents ou alertes importantes de la plateforme.'
    }
  ];

  languages: Array<{ value: 'fr' | 'en' | 'ar'; label: string; flag: string }> = [
    { value: 'fr', label: 'Français', flag: '🇫🇷' },
    { value: 'en', label: 'English', flag: '🇬🇧' },
    { value: 'ar', label: 'العربية', flag: '🇦🇪' }
  ];

  readonly supportProblemTypes: string[] = [
    'Bug technique',
    'Problème connexion',
    'Erreur validation',
    'Document introuvable',
    'Autre'
  ];

  selectedLanguage: 'fr' | 'en' | 'ar' = 'fr';
  isSavingLanguage = false;
  isSupportPanelOpen = false;
  isSendingSupport = false;
  supportContact: SupportContactInfoResponse | null = null;
  notificationPreferencesLoading = false;
  notificationPreferencesSaving = false;

  readonly supportForm = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(200)]],
    organizationName: ['', [Validators.required, Validators.maxLength(200)]],
    problemType: ['', [Validators.required, Validators.maxLength(120)]],
    description: ['', [Validators.required, Validators.maxLength(4000), Validators.minLength(10)]]
  });

  readonly notificationPreferencesForm = this.fb.group(
    this.notificationPreferenceOptions.reduce((controls, option) => {
      controls[`${option.type}_inApp`] = this.fb.nonNullable.control(true);
      controls[`${option.type}_email`] = this.fb.nonNullable.control(false);
      return controls;
    }, {} as Record<string, ReturnType<FormBuilder['nonNullable']['control']>>)
  );

  isDarkMode$ = this.themeService.theme$.pipe(
    map(theme => theme === 'dark')
  );

  constructor(
    private readonly fb: FormBuilder,
    public themeService: ThemeService,
    private readonly authService: AuthService,
    private readonly notificationService: NotificationService,
    private readonly supportService: SupportService,
    private readonly notificationSettingsService: NotificationSettingsService
  ) {
    const userLang = this.authService.getCurrentUser()?.preferredLanguage;
    const savedLang = localStorage.getItem('language');
    this.selectedLanguage = this.normalizeLanguage(userLang ?? savedLang);

    const user = this.authService.getCurrentUser();
    this.supportForm.patchValue({
      email: user?.email ?? '',
      organizationName: user?.organizationName ?? ''
    });

    this.loadSupportContactInfo();
    this.loadNotificationPreferences();
  }

  toggleTheme(): void {
    this.themeService.toggleTheme();
  }

  onLanguageChange(lang: 'fr' | 'en' | 'ar'): void {
    if (this.isSavingLanguage) {
      return;
    }

    const nextLanguage = this.normalizeLanguage(lang);
    if (nextLanguage === this.selectedLanguage) {
      return;
    }

    const previousLanguage = this.selectedLanguage;
    this.selectedLanguage = nextLanguage;
    this.applyLanguagePreview(nextLanguage);
    this.isSavingLanguage = true;

    this.authService.updatePreferredLanguage(nextLanguage).pipe(
      finalize(() => {
        this.isSavingLanguage = false;
      })
    ).subscribe({
      next: () => {
        this.notificationService.showSuccess('Langue mise à jour avec succès.');
      },
      error: () => {
        this.selectedLanguage = previousLanguage;
        this.applyLanguagePreview(previousLanguage);
        this.notificationService.showError('Impossible de modifier la langue pour le moment.');
      }
    });
  }

  toggleSupportPanel(): void {
    this.isSupportPanelOpen = !this.isSupportPanelOpen;
  }

  submitSupportTicket(): void {
    if (this.supportForm.invalid || this.isSendingSupport) {
      this.supportForm.markAllAsTouched();
      return;
    }

    this.isSendingSupport = true;

    this.supportService.submitTicket(this.supportForm.getRawValue()).pipe(
      finalize(() => {
        this.isSendingSupport = false;
      })
    ).subscribe({
      next: (response) => {
        this.notificationService.showSuccess(response.message || 'Ticket support envoye.');
        this.supportForm.patchValue({ description: '', problemType: '' });
      },
      error: () => {
        this.notificationService.showError('Impossible d envoyer le ticket support pour le moment.');
      }
    });
  }

  saveNotificationPreferences(): void {
    if (this.notificationPreferencesSaving) {
      return;
    }

    this.notificationPreferencesSaving = true;

    const payload = {
      items: this.notificationPreferenceOptions.map(option => ({
        notificationType: option.type,
        inAppEnabled: Boolean(this.notificationPreferencesForm.get(`${option.type}_inApp`)?.value ?? true),
        emailEnabled: Boolean(this.notificationPreferencesForm.get(`${option.type}_email`)?.value ?? false)
      }))
    };

    this.notificationSettingsService.updatePreferences(payload).pipe(
      finalize(() => {
        this.notificationPreferencesSaving = false;
      })
    ).subscribe({
      next: (preferences) => {
        this.applyNotificationPreferences(preferences);
        this.notificationService.showSuccess('Paramètres de notification mis à jour.');
      },
      error: () => {
        this.notificationService.showError('Impossible de mettre à jour les paramètres de notification.');
      }
    });
  }

  private loadSupportContactInfo(): void {
    this.supportService.getContactInfo().subscribe({
      next: (response) => {
        this.supportContact = response;
      },
      error: () => {
        this.supportContact = {
          assistantName: 'Assistant Technique QualiFlow',
          assistantEmail: 'support.qualiflow@gmail.com',
          assistantPhone: '+216 70 000 000'
        };
      }
    });
  }

  private loadNotificationPreferences(): void {
    this.notificationPreferencesLoading = true;

    this.notificationSettingsService.getPreferences().pipe(
      finalize(() => {
        this.notificationPreferencesLoading = false;
      })
    ).subscribe({
      next: (preferences) => {
        this.applyNotificationPreferences(preferences);
      },
      error: () => {
        this.notificationService.showError('Impossible de charger les paramètres de notification.');
      }
    });
  }

  private applyNotificationPreferences(preferences: NotificationPreferenceResponse[]): void {
    const patch: Record<string, boolean> = {};

    preferences.forEach((preference) => {
      patch[`${preference.notificationType}_inApp`] = preference.inAppEnabled;
      patch[`${preference.notificationType}_email`] = preference.emailEnabled;
    });

    this.notificationPreferencesForm.patchValue(patch);
  }

  private normalizeLanguage(value?: string | null): 'fr' | 'en' | 'ar' {
    if (value === 'fr' || value === 'en' || value === 'ar') {
      return value;
    }

    return 'fr';
  }

  private applyLanguagePreview(language: 'fr' | 'en' | 'ar'): void {
    document.documentElement.lang = language;
    document.documentElement.dir = language === 'ar' ? 'rtl' : 'ltr';
    localStorage.setItem('language', language);
  }
}
