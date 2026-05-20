import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

@Component({
  selector: 'app-contact-home',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './contact-home.component.html',
  styleUrls: ['./contact-home.component.scss']
})
export class ContactHomeComponent {
  socialLinks = [
    { icon: 'public', label: 'Site web', href: '#' },
    { icon: 'alternate_email', label: 'LinkedIn', href: '#' },
    { icon: 'groups', label: 'Facebook', href: '#' }
  ];

  contactForm = this.fb.group({
    name: ['', [Validators.required, Validators.minLength(2)]],
    email: ['', [Validators.required, Validators.email]],
    subject: ['', [Validators.required, Validators.minLength(4)]],
    message: ['', [Validators.required, Validators.minLength(10)]]
  });

  constructor(private readonly fb: FormBuilder) {}

  onSubmit(): void {
    if (this.contactForm.invalid) {
      this.contactForm.markAllAsTouched();
      return;
    }

    console.info('Message de contact envoyé :', this.contactForm.value);
    this.contactForm.reset();
  }
}
