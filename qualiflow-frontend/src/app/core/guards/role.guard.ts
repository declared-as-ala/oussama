import { Injectable } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivate, CanActivateChild, Router, RouterStateSnapshot } from '@angular/router';
import { AuthService } from '../services/auth.service';

@Injectable({
  providedIn: 'root'
})
export class RoleGuard implements CanActivate, CanActivateChild {
  constructor(
    private readonly authService: AuthService,
    private readonly router: Router
  ) {}

  canActivate(route: ActivatedRouteSnapshot, _state: RouterStateSnapshot): boolean {
    return this.checkRoles(route);
  }

  canActivateChild(route: ActivatedRouteSnapshot, _state: RouterStateSnapshot): boolean {
    return this.checkRoles(route);
  }

  private checkRoles(route: ActivatedRouteSnapshot): boolean {
    const requiredRoles = (route.data['roles'] || route.data['requiredRoles']) as string[] | undefined;

    if (!requiredRoles || requiredRoles.length === 0) {
      return true;
    }

    if (this.authService.hasRole(requiredRoles)) {
      return true;
    }

    this.router.navigate(['/forbidden']);
    return false;
  }
}
