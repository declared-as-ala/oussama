import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Output } from '@angular/core';
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

  navItems = [
    { label: 'Accueil', section: 'accueil' },
    { label: 'Services', section: 'services' },
    { label: 'ISO 21001', section: 'iso' },
    { label: 'Demander un Espace', section: 'request-org' },
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
