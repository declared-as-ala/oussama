import { CommonModule } from '@angular/common';
import { AfterViewInit, Component, OnDestroy } from '@angular/core';
import { AccueilHomeComponent } from './accueil-home/accueil-home.component';
import { ContactHomeComponent } from './contact-home/contact-home.component';
import { FeaturesHomeComponent } from './features-home/features-home.component';
import { FooterHomeComponent } from './footer-home/footer-home.component';
import { HeaderHomeComponent } from './header-home/header-home.component';
import { NewsHomeComponent } from './news-home/news-home.component';
import { ServicesHomeComponent } from './services-home/services-home.component';
import { StatsHomeComponent } from './stats-home/stats-home.component';
import { WhyHomeComponent } from './why-home/why-home.component';

import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { PublicService, OrganizationRequestResponse } from '../core/services/public.service';
import { NotificationService } from '../core/services/notification.service';
import { OrganizationRequestDialogComponent } from '../features/public/organization-request-dialog/organization-request-dialog.component';
import { ORGANIZATION_TYPE_OPTIONS } from '../features/super-admin/models/organization.models';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    HeaderHomeComponent,
    AccueilHomeComponent,
    ServicesHomeComponent,
    FeaturesHomeComponent,
    NewsHomeComponent,
    WhyHomeComponent,
    StatsHomeComponent,
    ContactHomeComponent,
    FooterHomeComponent,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.scss']
})
export class HomeComponent implements AfterViewInit, OnDestroy {
  currentSection: 'all' | 'accueil' | 'services' | 'iso' | 'contact' | 'request-org' | 'reclamations' = 'all';
  private observer?: IntersectionObserver;

  // Propriétés du formulaire en ligne
  requestForm: FormGroup;
  loading = false;
  codeSent = false;
  emailValidated = false;
  organizationTypes = ORGANIZATION_TYPE_OPTIONS;

  // Propriétés de la Réclamation
  reclamationForm: FormGroup;
  reclamationLoading = false;
  reclamationSubmitted = false;
  isReclamationPopupOpen = false;
  reclamationCaptchaNum1 = 0;
  reclamationCaptchaNum2 = 0;
  reclamationCaptchaExpected = 0;
  reclamationCaptchaError = '';

  declarantTypes = [
    { value: 'LEARNER', label: 'Apprenant / Étudiant' },
    { value: 'TEACHER', label: 'Enseignant / Formateur' },
    { value: 'PARENT', label: 'Parent d\'élève / Tuteur' },
    { value: 'STAFF', label: 'Personnel Administratif' },
    { value: 'OTHER', label: 'Autre Bénéficiaire' }
  ];

  urgencyLevels = [
    { value: 'LOW', label: 'Faible' },
    { value: 'MEDIUM', label: 'Moyen' },
    { value: 'HIGH', label: 'Élevé' }
  ];

  concernedServices = [
    { value: 'PEDAGOGY', label: 'Enseignement / Pédagogie' },
    { value: 'ADMINISTRATION', label: 'Administration / Secrétariat' },
    { value: 'EXAMS', label: 'Examens / Évaluations' },
    { value: 'INFRASTRUCTURE', label: 'Locaux / Équipements' },
    { value: 'CATERING', label: 'Hébergement / Restauration' },
    { value: 'OTHER', label: 'Autre Service' }
  ];

  countries = [
    { name: 'Maroc', code: '+212' },
    { name: 'France', code: '+33' },
    { name: 'Belgique', code: '+32' },
    { name: 'Suisse', code: '+41' },
    { name: 'Canada', code: '+1' },
    { name: 'Sénégal', code: '+221' },
    { name: 'Côte d\'Ivoire', code: '+225' },
    { name: 'Algérie', code: '+213' },
    { name: 'Tunisie', code: '+216' }
  ];

  constructor(
    private readonly dialog: MatDialog,
    private readonly fb: FormBuilder,
    private readonly publicService: PublicService,
    private readonly notificationService: NotificationService
  ) {
    this.requestForm = this.fb.group({
      fullName: ['', Validators.required],
      email: ['', [Validators.required, Validators.email]],
      phone: ['', Validators.required],
      country: ['', Validators.required],
      jobTitle: ['', Validators.required],
      organizationName: ['', Validators.required],
      organizationType: ['', Validators.required],
      message: ['', Validators.required],
      validationCode: ['', [Validators.required, Validators.minLength(6), Validators.maxLength(6)]]
    });

    this.reclamationForm = this.fb.group({
      declarantType: ['', Validators.required],
      fullName: ['', Validators.required],
      email: ['', [Validators.required, Validators.email]],
      phone: ['', Validators.required],
      concernedService: ['', Validators.required],
      urgencyLevel: ['', Validators.required],
      subject: ['', Validators.required],
      description: ['', Validators.required],
      captchaAnswer: ['', [Validators.required, Validators.pattern(/^\d+$/)]]
    });

    this.regenerateReclamationCaptcha();
  }

  onCountryChange(countryName: string): void {
    const country = this.countries.find(c => c.name === countryName);
    if (country) {
      this.requestForm.get('phone')?.setValue(country.code + ' ');
    }
  }

  onSendCode(): void {
    const email = this.requestForm.get('email')?.value;
    if (!email || this.requestForm.get('email')?.invalid) return;

    this.loading = true;
    this.publicService.sendVerificationCode(email).subscribe({
      next: (res) => {
        this.loading = false;
        if (res.success) {
          this.codeSent = true;
          this.notificationService.showInfo(res.message);
          this.requestForm.get('email')?.disable();
        } else {
          this.notificationService.showError(res.message);
        }
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError("Erreur lors de l'envoi du code.");
      }
    });
  }

  onVerifyCode(): void {
    const email = this.requestForm.get('email')?.value;
    const code = this.requestForm.get('validationCode')?.value;
    if (!email || !code || this.requestForm.get('validationCode')?.invalid) return;

    this.loading = true;
    this.publicService.verifyCode(email, code).subscribe({
      next: (res) => {
        this.loading = false;
        if (res.success) {
          this.emailValidated = true;
          this.notificationService.showSuccess(res.message);
          this.requestForm.get('validationCode')?.disable();
        } else {
          this.notificationService.showError(res.message);
        }
      },
      error: (err) => {
        this.loading = false;
        this.notificationService.showError(err.error?.message || "Le code de validation est incorrect ou a expiré.");
      }
    });
  }

  onSubmitRequest(): void {
    if (this.requestForm.invalid) return;

    this.loading = true;
    const request = this.requestForm.getRawValue();

    this.publicService.submitOrganizationRequest(request).subscribe({
      next: (response: OrganizationRequestResponse) => {
        this.loading = false;
        if (response.success) {
          this.notificationService.showSuccess(response.message);
          this.requestForm.reset();
          this.codeSent = false;
          this.emailValidated = false;
        } else {
          this.notificationService.showError(response.message);
        }
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError("Une erreur est survenue lors de l'envoi de votre demande.");
      }
    });
  }

  regenerateReclamationCaptcha(): void {
    this.reclamationCaptchaNum1 = Math.floor(Math.random() * 9) + 1;
    this.reclamationCaptchaNum2 = Math.floor(Math.random() * 9) + 1;
    this.reclamationCaptchaExpected = this.reclamationCaptchaNum1 + this.reclamationCaptchaNum2;
    this.reclamationCaptchaError = '';
    this.reclamationForm.get('captchaAnswer')?.setValue('');
  }

  onSubmitReclamation(): void {
    if (this.reclamationForm.invalid) {
      this.reclamationForm.markAllAsTouched();
      return;
    }

    const captchaVal = Number(this.reclamationForm.get('captchaAnswer')?.value);
    if (captchaVal !== this.reclamationCaptchaExpected) {
      this.reclamationCaptchaError = 'Calcul incorrect. Veuillez reessayer.';
      this.regenerateReclamationCaptcha();
      return;
    }

    this.reclamationLoading = true;
    setTimeout(() => {
      this.reclamationLoading = false;
      this.reclamationSubmitted = true;
      const refCode = `QF-REC-${new Date().getFullYear()}-${Math.floor(Math.random() * 9000) + 1000}`;
      this.notificationService.showSuccess(`Votre reclamation a ete enregistree avec succes sous la reference ${refCode}. Un e-mail de confirmation a ete envoye.`);
      this.reclamationForm.reset();
      this.regenerateReclamationCaptcha();
      this.reclamationSubmitted = false;
      this.closeReclamationPopup();
    }, 1500);
  }

  openReclamationPopup(): void {
    this.reclamationCaptchaError = '';
    this.isReclamationPopupOpen = true;
    document.body.classList.add('modal-open');
  }

  closeReclamationPopup(): void {
    this.isReclamationPopupOpen = false;
    document.body.classList.remove('modal-open');
  }

  openRequestDialog(): void {
    this.dialog.open(OrganizationRequestDialogComponent, {
      width: '600px',
      maxWidth: '95vw',
      panelClass: 'glass-dialog-panel'
    });
  }

  setSection(section: string): void {
    this.currentSection = section as any;
    // On relance les animations après un petit délai pour laisser le DOM se mettre à jour
    setTimeout(() => this.initRevealAnimations(), 50);
  }

  ngAfterViewInit(): void {
    // Déclenche les animations d'apparition des sections.
    this.initRevealAnimations();
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
    document.body.classList.remove('modal-open');
  }

  private initRevealAnimations(): void {
    const revealElements = document.querySelectorAll('.reveal');

    this.observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            entry.target.classList.add('revealed');
            this.observer?.unobserve(entry.target);
          }
        });
      },
      {
        threshold: 0.15,
        rootMargin: '0px 0px -40px 0px'
      }
    );

    revealElements.forEach((element) => this.observer?.observe(element));
  }
}
