import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

interface ServiceItem {
  icon: string;
  title: string;
  subtitle: string;
  description: string;
  features: string[];
  color: string;
}

@Component({
  selector: 'app-services-home',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  templateUrl: './services-home.component.html',
  styleUrls: ['./services-home.component.scss']
})
export class ServicesHomeComponent {
  services: ServiceItem[] = [
    { 
      icon: 'folder_managed', 
      title: 'Maîtrise Documentaire (GED)', 
      subtitle: 'Conformité ISO 9001 & 21001',
      description: 'Une gestion rigoureuse du cycle de vie de vos documents, de la création à l\'archivage.',
      features: ['Circuit d\'approbation automatisé', 'Versionnage et historique complet', 'Diffusion ciblée par rôle'],
      color: '#00875a'
    },
    { 
      icon: 'rule', 
      title: 'Processus & Procédures', 
      subtitle: 'Standardisation des méthodes',
      description: 'Formalisez vos savoir-faire pour garantir une qualité de service constante.',
      features: ['Cartographie des processus', 'Lien direct avec les formulaires', 'Révision périodique assistée'],
      color: '#2563eb'
    },
    { 
      icon: 'report_problem', 
      title: 'Gestion des Écarts (NC)', 
      subtitle: 'Réactivité et Traçabilité',
      description: 'Détectez et traitez les non-conformités avant qu\'elles n\'impactent vos clients.',
      features: ['Déclaration simplifiée (photo/web)', 'Workflow de résolution pas à pas', 'Analyse des causes racines'],
      color: '#ef4444'
    },
    { 
      icon: 'auto_fix_high', 
      title: 'Amélioration Continue', 
      subtitle: 'Plans d\'actions (CAPA)',
      description: 'Transformez vos problèmes en opportunités d\'amélioration stratégique.',
      features: ['Suivi des actions correctives', 'Assignation et rappels automatiques', 'Mesure de l\'efficacité'],
      color: '#f59e0b'
    },
    { 
      icon: 'analytics', 
      title: 'Pilotage Qualité', 
      subtitle: 'KPI et Tableaux de bord',
      description: 'Prenez des décisions basées sur des données précises et actualisées.',
      features: ['Statistiques en temps réel', 'Exports rapports qualité', 'Suivi des objectifs annuels'],
      color: '#10b981'
    },
    { 
      icon: 'devices', 
      title: 'Écosystème Connecté', 
      subtitle: 'Web, Mobile et Cloud',
      description: 'Accédez à votre système qualité partout, tout le temps, en toute sécurité.',
      features: ['Application mobile native', 'Notifications temps réel', 'Hébergement haute disponibilité'],
      color: '#6366f1'
    }
  ];
}
