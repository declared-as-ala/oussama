import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';

interface FeatureItem {
  icon: string;
  label: string;
}

@Component({
  selector: 'app-features-home',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './features-home.component.html',
  styleUrls: ['./features-home.component.scss']
})
export class FeaturesHomeComponent {
  features: FeatureItem[] = [
    { icon: 'lock', label: 'Authentification sécurisée JWT' },
    { icon: 'admin_panel_settings', label: 'Gestion des rôles' },
    { icon: 'shield_person', label: 'Super Admin pour gérer les organisations' },
    { icon: 'workspace_premium', label: 'Responsable qualité' },
    { icon: 'badge', label: 'Chef de service' },
    { icon: 'person', label: 'Utilisateur simple' },
    { icon: 'upload_file', label: 'Upload, consultation et validation des documents' },
    { icon: 'history', label: 'Historique et traçabilité' },
    { icon: 'campaign', label: 'Notifications automatiques' },
    { icon: 'devices', label: 'Interface web et mobile' },
    { icon: 'manage_search', label: 'Recherche rapide des documents' }
  ];
}
