import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { MatMenuModule } from '@angular/material/menu';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { Router, RouterModule } from '@angular/router';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [
    CommonModule,
    MatMenuModule,
    MatIconModule,
    MatDividerModule,
    RouterModule
  ],
  templateUrl: './header.component.html',
  styleUrls: ['./header.component.scss']
})
export class HeaderComponent implements OnInit, OnDestroy {
  private readonly subscriptions = new Subscription();
  profilePhotoObjectUrl: string | null = null;
  currentUser$ = this.authService.currentUser$;

  get currentUser() {
    return this.authService.getCurrentUser();
  }

  constructor(
    private authService: AuthService,
    private router: Router
  ) { }

  ngOnInit(): void {
    this.loadProfilePhoto();

    this.subscriptions.add(
      this.authService.profilePhotoRefresh$.subscribe(() => {
        this.loadProfilePhoto();
      })
    );

    this.subscriptions.add(
      this.currentUser$.subscribe(() => {
        this.loadProfilePhoto();
      })
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
    this.revokeProfilePhotoObjectUrl();
  }

  getInitials(): string {
    const user = this.currentUser;
    if (!user) {
      return 'U';
    }

    const firstInitial = user.firstName ? user.firstName[0] : '';
    const lastInitial = user.lastName ? user.lastName[0] : '';
    const initials = `${firstInitial}${lastInitial}`.toUpperCase();

    if (initials) {
      return initials;
    }

    return user.email ? user.email[0].toUpperCase() : 'U';
  }

  getDisplayName(): string {
    const user = this.currentUser;
    if (!user) {
      return 'Utilisateur';
    }

    const fullName = `${user.firstName} ${user.lastName}`.trim();
    if (fullName) {
      return fullName;
    }

    return user.email || 'Utilisateur';
  }

  goToProfile(): void {
    this.router.navigate(['/profile']);
  }

  logout(): void {
    this.authService.logout().subscribe();
  }

  private loadProfilePhoto(): void {
    if (!this.currentUser) {
      this.revokeProfilePhotoObjectUrl();
      return;
    }

    this.authService.downloadProfilePhoto().subscribe({
      next: (blob: Blob) => {
        if (!blob || blob.size === 0) {
          this.revokeProfilePhotoObjectUrl();
          return;
        }

        this.revokeProfilePhotoObjectUrl();
        this.profilePhotoObjectUrl = URL.createObjectURL(blob);
      },
      error: () => {
        this.revokeProfilePhotoObjectUrl();
      }
    });
  }

  private revokeProfilePhotoObjectUrl(): void {
    if (this.profilePhotoObjectUrl) {
      URL.revokeObjectURL(this.profilePhotoObjectUrl);
      this.profilePhotoObjectUrl = null;
    }
  }
}
