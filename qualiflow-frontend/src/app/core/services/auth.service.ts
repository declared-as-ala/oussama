import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, Subject, of } from 'rxjs';
import { catchError, switchMap, tap } from 'rxjs/operators';
import { Router } from '@angular/router';
import { environment } from '../../../environments/environment';

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginByPhoneRequest {
  phoneNumber: string;
  password: string;
}

export interface RegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  phone?: string;
  nationality?: string;
  organizationCode: string;
  birthDate: string;
  password: string;
  confirmPassword: string;
  captchaNum1: number;
  captchaNum2: number;
  captchaAnswer: number;
}

export interface RegisterResponse {
  id: number;
  firstName: string;
  lastName: string;
  email: string;
  role: string;
  createdAt: string;
  requiresEmailVerification: boolean;
  verificationEmailSent?: boolean;
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  userId: number;
  firstName?: string;
  lastName?: string;
  email?: string;
  role?: string;
  organizationId?: number;
}

export interface MeResponse {
  id: number;
  organizationId?: number;
  organizationName?: string;
  firstName: string;
  lastName: string;
  email: string;
  role: string;
  function?: string;
  department?: string;
  phone?: string;
  city?: string;
  birthDate?: string;
  preferredLanguage?: 'fr' | 'en' | 'ar';
  profilePhotoPath?: string | null;
  isActive: boolean;
  createdAt: string;
}

export interface UpdateProfileRequest {
  firstName: string;
  lastName: string;
  birthDate?: string | null;
  phone?: string | null;
  city?: string | null;
  preferredLanguage: 'fr' | 'en' | 'ar';
}

export interface ProfilePhotoResponse {
  userId: number;
  profilePhotoPath?: string | null;
  updatedAt?: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  email: string;
  code: string;
  newPassword: string;
  confirmPassword: string;
}

export interface RefreshTokenRequest {
  refreshToken: string;
}

export interface VerifyEmailCodeRequest {
  email: string;
  code: string;
}

export interface ResendVerificationCodeRequest {
  email: string;
}

export interface RequestEmailChangeCodeRequest {
  newEmail: string;
}

export interface ConfirmEmailChangeRequest {
  newEmail: string;
  code: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private apiUrl = `${environment.apiUrl}/api/auth`;
  private currentUserSubject = new BehaviorSubject<MeResponse | null>(this.getUserFromStorage());
  public currentUser$ = this.currentUserSubject.asObservable();
  private profilePhotoRefreshSubject = new Subject<void>();
  public profilePhotoRefresh$ = this.profilePhotoRefreshSubject.asObservable();

  private isAuthenticatedSubject = new BehaviorSubject<boolean>(this.hasToken());
  public isAuthenticated$ = this.isAuthenticatedSubject.asObservable();

  constructor(private http: HttpClient, private router: Router) {
    this.loadCurrentUser();
  }

  register(request: RegisterRequest): Observable<RegisterResponse> {
    return this.http.post<RegisterResponse>(`${this.apiUrl}/register`, request);
  }

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.apiUrl}/login`, request).pipe(
      tap(response => {
        if (response.accessToken && response.refreshToken) {
          this.setTokens(response.accessToken, response.refreshToken);
          this.isAuthenticatedSubject.next(true);
        }
      })
    );
  }

  loginByPhone(request: LoginByPhoneRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.apiUrl}/login-by-phone`, request).pipe(
      tap(response => {
        if (response.accessToken && response.refreshToken) {
          this.setTokens(response.accessToken, response.refreshToken);
          this.isAuthenticatedSubject.next(true);
        }
      })
    );
  }

  logout(): Observable<any> {
    return this.http.post(`${this.apiUrl}/logout`, {}).pipe(
      // Keep UX consistent: even if API logout fails, clear local session.
      catchError(() => of(null)),
      tap(() => {
        this.forceLogout();
      })
    );
  }

  forceLogout(): void {
    this.clearTokens();
    this.currentUserSubject.next(null);
    this.isAuthenticatedSubject.next(false);
    this.applyPlatformLanguage('fr');
    this.router.navigate(['/login']);
  }

  refreshToken(): Observable<LoginResponse> {
    const refreshToken = this.getRefreshToken();
    if (!refreshToken) {
      throw new Error('No refresh token available');
    }

    return this.http.post<LoginResponse>(`${this.apiUrl}/refresh-token`, { refreshToken }).pipe(
      tap(response => {
        this.setTokens(response.accessToken, response.refreshToken);
      })
    );
  }

  changePassword(request: ChangePasswordRequest): Observable<any> {
    return this.http.post(`${this.apiUrl}/change-password`, request);
  }

  updateProfile(request: UpdateProfileRequest): Observable<MeResponse> {
    return this.http.put<MeResponse>(`${this.apiUrl}/me`, request).pipe(
      tap(user => {
        this.currentUserSubject.next(user);
        this.saveUserToStorage(user);
        this.applyPlatformLanguage(user.preferredLanguage);
      })
    );
  }

  updatePreferredLanguage(preferredLanguage: 'fr' | 'en' | 'ar'): Observable<MeResponse> {
    const currentUser = this.getCurrentUser();
    const normalizedLanguage = this.normalizeLanguage(preferredLanguage);

    if (this.canBuildProfileUpdateRequest(currentUser)) {
      return this.updateProfile(this.buildProfileUpdateRequest(currentUser, normalizedLanguage));
    }

    return this.getProfile().pipe(
      switchMap(profile => this.updateProfile(this.buildProfileUpdateRequest(profile, normalizedLanguage)))
    );
  }

  uploadProfilePhoto(file: File): Observable<ProfilePhotoResponse> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<ProfilePhotoResponse>(`${this.apiUrl}/me/photo`, formData).pipe(
      tap(() => this.profilePhotoRefreshSubject.next())
    );
  }

  downloadProfilePhoto(): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/me/photo`, { responseType: 'blob' });
  }

  forgotPassword(request: ForgotPasswordRequest): Observable<any> {
    return this.http.post(`${this.apiUrl}/forgot-password`, request);
  }

  resetPassword(request: ResetPasswordRequest): Observable<any> {
    return this.http.post(`${this.apiUrl}/reset-password`, request);
  }

  verifyResetCode(request: { email: string, code: string }): Observable<any> {
    return this.http.post(`${this.apiUrl}/verify-reset-code`, request);
  }

  verifyEmail(token: string): Observable<any> {
    return this.http.get(`${this.apiUrl}/verify-email`, { params: { token } });
  }

  verifyEmailCode(request: VerifyEmailCodeRequest): Observable<any> {
    return this.http.post(`${this.apiUrl}/verify-email-code`, request);
  }

  resendVerificationCode(request: ResendVerificationCodeRequest): Observable<any> {
    return this.http.post(`${this.apiUrl}/resend-verification-code`, request);
  }

  requestEmailChangeCode(request: RequestEmailChangeCodeRequest): Observable<any> {
    return this.http.post(`${this.apiUrl}/me/email-change/request-code`, request);
  }

  confirmEmailChange(request: ConfirmEmailChangeRequest): Observable<MeResponse> {
    return this.http.post<MeResponse>(`${this.apiUrl}/me/email-change/confirm`, request).pipe(
      tap(user => {
        this.currentUserSubject.next(user);
        this.saveUserToStorage(user);
        this.applyPlatformLanguage(user.preferredLanguage);
      })
    );
  }

  getProfile(): Observable<MeResponse> {
    return this.http.get<MeResponse>(`${this.apiUrl}/me`).pipe(
      tap(user => {
        this.currentUserSubject.next(user);
        this.saveUserToStorage(user);
        this.applyPlatformLanguage(user.preferredLanguage);
      })
    );
  }

  getCurrentUser(): MeResponse | null {
    return this.currentUserSubject.value;
  }

  private loadCurrentUser(): void {
    if (this.hasToken()) {
      const storedUser = this.getUserFromStorage();
      if (storedUser) {
        this.currentUserSubject.next(storedUser);
        this.applyPlatformLanguage(storedUser.preferredLanguage);
      } else {
        this.applyPlatformLanguage('fr');
      }
    } else {
      this.applyPlatformLanguage('fr');
    }
  }

  private setTokens(accessToken: string, refreshToken: string): void {
    localStorage.setItem('accessToken', accessToken);
    localStorage.setItem('refreshToken', refreshToken);
  }

  private clearTokens(): void {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('currentUser');
  }

  getAccessToken(): string | null {
    return localStorage.getItem('accessToken');
  }

  getRefreshToken(): string | null {
    return localStorage.getItem('refreshToken');
  }

  private hasToken(): boolean {
    return !!localStorage.getItem('accessToken');
  }

  private saveUserToStorage(user: MeResponse): void {
    localStorage.setItem('currentUser', JSON.stringify(user));
  }

  private getUserFromStorage(): MeResponse | null {
    const user = localStorage.getItem('currentUser');
    return user ? JSON.parse(user) : null;
  }

  isAuthenticated(): boolean {
    return this.hasToken();
  }

  hasRole(role: string | string[]): boolean {
    const user = this.getCurrentUser();
    if (!user) return false;

    if (Array.isArray(role)) {
      return role.includes(user.role);
    }
    return user.role === role;
  }

  private applyPlatformLanguage(language?: 'fr' | 'en' | 'ar' | string): void {
    const normalized = this.normalizeLanguage(language);
    document.documentElement.lang = normalized;
    document.documentElement.dir = normalized === 'ar' ? 'rtl' : 'ltr';
    localStorage.setItem('language', normalized);
  }

  private canBuildProfileUpdateRequest(user: MeResponse | null): user is MeResponse {
    return !!user && !!user.firstName?.trim() && !!user.lastName?.trim();
  }

  private buildProfileUpdateRequest(user: MeResponse, preferredLanguage: 'fr' | 'en' | 'ar'): UpdateProfileRequest {
    return {
      firstName: user.firstName.trim(),
      lastName: user.lastName.trim(),
      birthDate: user.birthDate ?? null,
      phone: user.phone?.trim() || null,
      city: user.city?.trim() || null,
      preferredLanguage
    };
  }

  private normalizeLanguage(language?: string | null): 'fr' | 'en' | 'ar' {
    if (language === 'en' || language === 'ar' || language === 'fr') {
      return language;
    }

    return 'fr';
  }
}
