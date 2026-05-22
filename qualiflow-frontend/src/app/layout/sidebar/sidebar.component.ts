import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { TranslatePipe } from '../../shared/pipes/translate.pipe';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, RouterModule, TranslatePipe],
  templateUrl: './sidebar.component.html',
  styleUrls: ['./sidebar.component.scss']
})
export class SidebarComponent {
  @Input() sidebarOpen = false;
  @Output() navClicked = new EventEmitter<void>();

  constructor(
    private router: Router,
    private authService: AuthService
  ) { }

  onNavClick(): void {
    this.navClicked.emit();
  }

  get homeRoute(): string {
    return this.isSuperAdmin ? '/super-admin/dashboard' : '/dashboard';
  }

  get canAccessQuality(): boolean {
    return this.authService.hasRole([
      'ADMIN_ORG',
      'RESPONSABLE_QUALITE',
      'UTILISATEUR'
    ]);
  }

  get canReadNotifications(): boolean {
    return this.authService.hasRole([
      'ADMIN_ORG',
      'RESPONSABLE_QUALITE',
      'UTILISATEUR',
      'SUPER_ADMIN'
    ]);
  }

  get canAccessDocuments(): boolean {
    return this.authService.hasRole([
      'ADMIN_ORG',
      'RESPONSABLE_QUALITE',
      'UTILISATEUR'
    ]);
  }

  get canReadProcesses(): boolean {
    return this.authService.hasRole([
      'ADMIN_ORG',
      'RESPONSABLE_QUALITE',
      'UTILISATEUR'
    ]);
  }

  get canWriteProcesses(): boolean {
    return this.authService.hasRole([
      'ADMIN_ORG',
      'RESPONSABLE_QUALITE'
    ]);
  }

  get canReadProcedures(): boolean {
    return this.authService.hasRole([
      'ADMIN_ORG',
      'RESPONSABLE_QUALITE',
      'UTILISATEUR'
    ]);
  }

  get canReadNonConformities(): boolean {
    return this.authService.hasRole([
      'ADMIN_ORG',
      'RESPONSABLE_QUALITE',
      'UTILISATEUR'
    ]);
  }

  get canReadCorrectiveActions(): boolean {
    return this.authService.hasRole([
      'ADMIN_ORG',
      'RESPONSABLE_QUALITE',
      'UTILISATEUR'
    ]);
  }

  get canReadIndicators(): boolean {
    return this.authService.hasRole([
      'ADMIN_ORG',
      'RESPONSABLE_QUALITE',
      'UTILISATEUR'
    ]);
  }


  get isAdminOrg(): boolean {
    return this.authService.hasRole('ADMIN_ORG');
  }

  get isSuperAdmin(): boolean {
    return this.authService.hasRole('SUPER_ADMIN');
  }

}
