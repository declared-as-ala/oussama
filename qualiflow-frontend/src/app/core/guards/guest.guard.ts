import { Injectable } from '@angular/core';
import { CanActivate, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

@Injectable({
  providedIn: 'root'
})
export class GuestGuard implements CanActivate {
  constructor(
    private authService: AuthService,
    private router: Router
  ) { }

  canActivate(): boolean {
    if (this.authService.isAuthenticated()) {
      const currentUser = this.authService.getCurrentUser();
      if (currentUser?.role === 'SUPER_ADMIN') {
        this.router.navigate(['/super-admin/dashboard']);
      } else {
        this.router.navigate(['/dashboard']);
      }
      return false;
    }
    return true;
  }
}
