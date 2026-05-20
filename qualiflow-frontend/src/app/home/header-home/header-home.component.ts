import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Output, HostListener } from '@angular/core';
import { RouterLink } from '@angular/router';

import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-header-home',
  standalone: true,
  imports: [CommonModule, RouterLink, MatIconModule],
  templateUrl: './header-home.component.html',
  styleUrls: ['./header-home.component.scss']
})
export class HeaderHomeComponent {
  @Output() sectionChange = new EventEmitter<string>();
  activeSection: string = 'accueil';
  isMenuOpen: boolean = false;
  isLangOpen: boolean = false;
  isScrolled: boolean = false;

  @HostListener('window:scroll', [])
  onWindowScroll() {
    this.isScrolled = window.scrollY > 40;
  }

  languages = [
    { code: 'fr', label: 'FR', name: 'Français', flag: '🇫🇷' },
    { code: 'en', label: 'EN', name: 'English', flag: '🇬🇧' },
    { code: 'ar', label: 'AR', name: 'العربية', flag: '🇹🇳' }
  ];
  currentLang = localStorage.getItem('language') || 'fr';

  get currentFlag(): string {
    return this.languages.find(l => l.code === this.currentLang)?.flag || '🇫🇷';
  }

  toggleLangDropdown(event?: Event): void {
    if (event) {
      event.stopPropagation();
    }
    this.isLangOpen = !this.isLangOpen;
  }

  changeLang(lang: string): void {
    localStorage.setItem('language', lang);
    document.documentElement.lang = lang;
    document.documentElement.dir = lang === 'ar' ? 'rtl' : 'ltr';
    this.isLangOpen = false;
    window.location.reload();
  }

  navItems = [
    { label: 'Accueil', section: 'accueil' },
    { label: 'Services', section: 'services' },
    { label: 'ISO 21001', section: 'iso' },
    { label: 'Demander un Espace', section: 'request-org' },
    { label: 'Réclamations', section: 'reclamations' },
    { label: 'Contact', section: 'contact' }
  ];

  toggleMenu(): void {
    this.isMenuOpen = !this.isMenuOpen;
  }

  selectSection(section: string): void {
    this.activeSection = section;
    this.isMenuOpen = false;
    this.sectionChange.emit(section);
    
    if (section === 'accueil') {
      window.scrollTo({ top: 0, behavior: 'smooth' });
    } else {
      setTimeout(() => {
        const element = document.getElementById(section);
        if (element) {
          element.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
      }, 100);
    }
  }
}
