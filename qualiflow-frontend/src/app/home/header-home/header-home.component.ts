import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Output, HostListener } from '@angular/core';
import { RouterLink } from '@angular/router';

import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '../../shared/pipes/translate.pipe';

@Component({
  selector: 'app-header-home',
  standalone: true,
  imports: [CommonModule, RouterLink, MatIconModule, TranslatePipe],
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

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent) {
    const target = event.target as HTMLElement;
    if (this.isLangOpen && !target.closest('.lang-selector')) {
      this.isLangOpen = false;
    }
    if (this.isMenuOpen && !target.closest('.topbar')) {
      this.isMenuOpen = false;
    }
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
    { label: 'Accueil', key: 'home.nav.accueil', section: 'accueil' },
    { label: 'Services', key: 'home.nav.services', section: 'services' },
    { label: 'ISO 21001', key: 'home.nav.iso', section: 'iso' },
    { label: 'Demander un Espace', key: 'home.nav.requestOrg', section: 'request-org' },
    { label: 'Contact', key: 'home.nav.contact', section: 'contact' }
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
