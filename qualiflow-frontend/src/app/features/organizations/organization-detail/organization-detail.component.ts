import { Component, OnInit, OnDestroy, Optional } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import {
  OrganizationResponse,
  OrganizationService,
  UpdateOrganizationRequest
} from '../../../core/services/organization.service';

@Component({
  selector: 'app-organization-detail',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatProgressSpinnerModule,
    MatTabsModule
  ],
  templateUrl: './organization-detail.component.html',
  styleUrls: ['./organization-detail.component.scss']
})
export class OrganizationDetailComponent implements OnInit, OnDestroy {
  readonly typeOptions = ['UNIVERSITE', 'INSTITUT', 'CENTRE', 'ENTREPRISE'];
  
  get isDialog(): boolean {
    return !!this.dialogRef;
  }

  get isSuperAdmin(): boolean {
    return this.authService.hasRole('SUPER_ADMIN');
  }

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

  public loading = false;
  public savingOrganization = false;
  public uploadingLogo = false;
  public locatingAddress = false;

  organization: OrganizationResponse | null = null;
  logoObjectUrl: string | null = null;
  selectedLogo: File | null = null;

  constructor(
    private readonly fb: FormBuilder,
    private readonly http: HttpClient,
    @Optional() private readonly dialogRef: MatDialogRef<OrganizationDetailComponent>,
    private readonly organizationService: OrganizationService,
    private readonly notificationService: NotificationService,
    private readonly authService: AuthService
  ) {}

  ngOnInit(): void {
    this.loadOrganization();
    if (!this.isSuperAdmin) {
      this.organizationForm.controls.subscriptionDaysRemaining.disable();
      this.organizationForm.controls.subscriptionMonitorEnabled.disable();
    }
  }

  ngOnDestroy(): void {
    this.revokeLogoObjectUrl();
  }

  loadOrganization(): void {
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

  loadLogo(): void {
    this.revokeLogoObjectUrl();
    this.organizationService.downloadMyOrganizationLogo().subscribe({
      next: (blob) => {
        if (blob && blob.size > 0) {
          this.logoObjectUrl = URL.createObjectURL(blob);
        }
      },
      error: () => {
        this.logoObjectUrl = null;
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

  uploadLogo(): void {
    if (!this.selectedLogo) {
      return;
    }

    this.uploadingLogo = true;
    this.organizationService.uploadMyOrganizationLogo(this.selectedLogo).subscribe({
      next: () => {
        this.uploadingLogo = false;
        this.selectedLogo = null;
        this.notificationService.showSuccess('Logo organisation mis à jour.');
        this.organizationService.notifyLogoUpdated();
        this.loadLogo();
      },
      error: () => {
        this.uploadingLogo = false;
        this.notificationService.showError('Upload logo impossible.');
      }
    });
  }

  saveOrganization(): void {
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
      subscriptionDaysRemaining: raw.subscriptionDaysRemaining,
      subscriptionMonitorEnabled: raw.subscriptionMonitorEnabled
    };

    this.savingOrganization = true;
    this.organizationService.updateMyOrganization(payload).subscribe({
      next: () => {
        this.savingOrganization = false;
        this.notificationService.showSuccess('Informations organisation mises à jour.');
        if (this.dialogRef) {
          this.dialogRef.close(true);
        }
      },
      error: () => {
        this.savingOrganization = false;
        this.notificationService.showError('Mise à jour organisation impossible.');
      }
    });
  }

  fillAddressFromGps(): void {
    if (this.locatingAddress) {
      return;
    }

    if (!navigator.geolocation) {
      this.notificationService.showError('La géolocalisation n\'est pas supportée par ce navigateur.');
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
            this.notificationService.showSuccess('Adresse remplie via GPS.');
          },
          error: () => {
            this.locatingAddress = false;
            this.notificationService.showError('Géocodage impossible.');
          }
        });
      },
      () => {
        this.locatingAddress = false;
        this.notificationService.showError('Accès position refusé.');
      },
      {
        enableHighAccuracy: true,
        timeout: 10000,
        maximumAge: 0
      }
    );
  }

  onClose(): void {
    if (this.dialogRef) {
      this.dialogRef.close();
    }
  }

  private revokeLogoObjectUrl(): void {
    if (this.logoObjectUrl) {
      URL.revokeObjectURL(this.logoObjectUrl);
      this.logoObjectUrl = null;
    }
  }
}
