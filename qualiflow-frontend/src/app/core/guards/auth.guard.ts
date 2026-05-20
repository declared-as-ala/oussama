import { Injectable } from '@angular/core';
import { CanActivate, CanActivateChild, Router, ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { AuthService } from '../services/auth.service';

@Injectable({
  providedIn: 'root'
})
export class AuthGuard implements CanActivate, CanActivateChild {
  constructor(
    private authService: AuthService,
    private router: Router
  ) { }

  canActivate(route: ActivatedRouteSnapshot, state: RouterStateSnapshot): boolean {
    return this.checkAccess(route, state);
  }

  canActivateChild(childRoute: ActivatedRouteSnapshot, state: RouterStateSnapshot): boolean {
    return this.checkAccess(childRoute, state);
  }

  private checkAccess(route: ActivatedRouteSnapshot, state: RouterStateSnapshot): boolean {
    if (this.authService.isAuthenticated()) {
      const requiredRoles = (route.data['roles'] || route.data['requiredRoles']) as string[] | undefined;
      if (requiredRoles && requiredRoles.length > 0) {
        if (this.authService.hasRole(requiredRoles)) {
          return true;
        } else {
          if (this.authService.hasRole('SUPER_ADMIN')) {
            this.router.navigate(['/super-admin/organizations']);
            return false;
          }
          this.router.navigate(['/forbidden']);
          return false;
        }
      }
      return true;
    }

    this.router.navigate(['/login'], { queryParams: { returnUrl: state.url } });
    return false;
  }
}
