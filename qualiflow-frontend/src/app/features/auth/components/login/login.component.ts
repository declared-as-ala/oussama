import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService, LoginRequest, LoginResponse, LoginByPhoneRequest } from '../../../../core/services/auth.service';
import { MeResponse } from '../../../../core/services/auth.service';
import { HttpErrorResponse } from '@angular/common/http';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss']
})
export class LoginComponent implements OnInit {
  loginForm!: FormGroup;
  isLoading = false;
  errorMessage: string | null = null;
  loginMode: 'email' | 'phone' = 'email'; // Toggle between email and phone

  constructor(
    private formBuilder: FormBuilder,
    private authService: AuthService,
    private router: Router
  ) { }

  ngOnInit(): void {
    this.createForm();
  }

  createForm(): void {
    if (this.loginMode === 'email') {
      this.loginForm = this.formBuilder.group({
        email: ['', [Validators.required, Validators.email]],
        password: ['', [Validators.required, Validators.minLength(6)]]
      });
    } else {
      this.loginForm = this.formBuilder.group({
        phoneNumber: ['', [Validators.required]],
        password: ['', [Validators.required, Validators.minLength(6)]]
      });
    }
  }

  toggleLoginMode(): void {
    this.loginMode = this.loginMode === 'email' ? 'phone' : 'email';
    this.errorMessage = null;
    this.createForm();
  }

  onSubmit(): void {
    if (this.loginForm.invalid) {
      return;
    }

    this.isLoading = true;
    this.errorMessage = null;

    const submitObservable = this.loginMode === 'email' 
      ? this.submitEmailLogin()
      : this.submitPhoneLogin();

    submitObservable.subscribe({
      next: (_response: LoginResponse) => {
        this.isLoading = false;
        // Load user profile after login
        this.authService.getProfile().subscribe({
          next: (_profile: MeResponse) => {
            this.router.navigate(['/dashboard']);
          },
          error: (_error: HttpErrorResponse) => {
            this.errorMessage = 'Erreur lors du chargement du profil';
          }
        });
      },
      error: (error: HttpErrorResponse) => {
        this.isLoading = false;
        this.errorMessage = error.error?.message || 'Identifiant ou mot de passe incorrect';
      }
    });
  }

  private submitEmailLogin() {
    const request: LoginRequest = {
      email: this.loginForm.value.email,
      password: this.loginForm.value.password
    };
    return this.authService.login(request);
  }

  private submitPhoneLogin() {
    const request: LoginByPhoneRequest = {
      phoneNumber: this.loginForm.value.phoneNumber,
      password: this.loginForm.value.password
    };
    return this.authService.loginByPhone(request);
  }

  get email() {
    return this.loginForm.get('email');
  }

  get password() {
    return this.loginForm.get('password');
  }

  get phoneNumber() {
    return this.loginForm.get('phoneNumber');
  }
}
