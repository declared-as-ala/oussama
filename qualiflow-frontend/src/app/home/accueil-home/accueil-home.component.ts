import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'app-accueil-home',
  standalone: true,
  imports: [
    CommonModule, 
    RouterLink, 
    MatIconModule, 
    MatTooltipModule, 
    MatButtonModule
  ],
  templateUrl: './accueil-home.component.html',
  styleUrls: ['./accueil-home.component.scss']
})
export class AccueilHomeComponent {
  features = [
    {
      title: 'Gestion Documentaire',
      description: 'Cycle de vie complet : création, révision, approbation et diffusion sécurisée.',
      icon: 'description',
      color: '#00875a'
    },
    {
      title: 'Non-Conformités',
      description: 'Déclaration et traitement des écarts avec workflow d\'approbation intégré.',
      icon: 'report_problem',
      color: '#ef4444'
    },
    {
      title: 'Actions Correctives',
      description: 'Pilotez vos plans d\'actions et assurez l\'amélioration continue de vos processus.',
      icon: 'task_alt',
      color: '#3b82f6'
    },
    {
      title: 'Indicateurs & KPI',
      description: 'Tableaux de bord en temps réel pour un pilotage stratégique de la performance.',
      icon: 'bar_chart',
      color: '#f59e0b'
    },
    {
      title: 'Conformité qualité',
      description: 'Pilotez vos exigences ISO (9001, 21001) avec une vision claire.',
      icon: 'fact_check',
      color: '#8b5cf6'
    },
    {
      title: 'Notifications Smart',
      description: 'Alertes automatiques pour les révisions, rappels d\'échéances et validations.',
      icon: 'notifications_active',
      color: '#10b981'
    }
  ];

  projectHighlights = [
    'Documents qualité',
    'Procédures',
    'Processus',
    'Non-conformités',
    'Actions correctives',
    'Indicateurs de performance',
    'Notifications automatiques',
    'Utilisateurs et rôles par organisation'
  ];

  isoBenefits = [
    'Gestion documentaire structurée',
    'Suivi des processus',
    'Gestion des non-conformités',
    'Actions correctives',
    'Tableaux de bord et indicateurs',
    'Traçabilité et historique'
  ];
}
