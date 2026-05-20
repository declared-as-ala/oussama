import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { PublicService, OrganizationRequestResponse } from '../../../core/services/public.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ORGANIZATION_TYPE_OPTIONS } from '../../super-admin/models/organization.models';

@Component({
  selector: 'app-organization-request-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSelectModule
  ],
  templateUrl: './organization-request-dialog.component.html',
  styleUrls: ['./organization-request-dialog.component.scss']
})
export class OrganizationRequestDialogComponent {
  requestForm: FormGroup;
  loading = false;
  codeSent = false;
  emailValidated = false;
  organizationTypes = ORGANIZATION_TYPE_OPTIONS;

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
    private readonly fb: FormBuilder,
    private readonly publicService: PublicService,
    private readonly notificationService: NotificationService,
    private readonly dialogRef: MatDialogRef<OrganizationRequestDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: any
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
          // Désactiver l'email pour éviter de changer après avoir envoyé le code
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

  onClose(): void {
    this.dialogRef.close();
  }

  onSubmit(): void {
    if (this.requestForm.invalid) return;

    this.loading = true;
    const request = this.requestForm.getRawValue(); // Utiliser getRawValue pour inclure l'email désactivé

    this.publicService.submitOrganizationRequest(request).subscribe({
      next: (response: OrganizationRequestResponse) => {
        this.loading = false;
        if (response.success) {
          this.notificationService.showSuccess(response.message);
          this.dialogRef.close(true);
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
}
