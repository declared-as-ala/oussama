import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { OrganizationAuditService, OrganizationActivityResponse } from '../services/organization-audit.service';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';

@Component({
  selector: 'app-organization-audit-log',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    TranslatePipe
  ],
  templateUrl: './organization-audit-log.component.html',
  styleUrls: ['./organization-audit-log.component.scss']
})
export class OrganizationAuditLogComponent implements OnInit {
  activities: OrganizationActivityResponse[] = [];
  filteredActivities: OrganizationActivityResponse[] = [];
  loading = true;
  searchTerm = '';
  selectedActionType = 'ALL';
  selectedModule = 'ALL';
  expandedIndex: number | null = null;

  private readonly mojibakeReplacements: Array<[RegExp, string]> = [
    [/ProcÃƒÂ©dure/g, 'Procédure'],
    [/procÃƒÂ©dure/g, 'procédure'],
    [/crÃƒÂ©ÃƒÂ©e/g, 'créée'],
    [/CrÃƒÂ©ation/g, 'Création'],
    [/crÃƒÂ©ation/g, 'création'],
    [/MÃƒÂ©tadonnÃƒÂ©es/g, 'Métadonnées'],
    [/mÃƒÂ©tadonnÃƒÂ©es/g, 'métadonnées'],
    [/modifiÃƒÂ©es/g, 'modifiées'],
    [/modifiÃƒÂ©e/g, 'modifiée'],
    [/supprimÃƒÂ©e/g, 'supprimée'],
    [/changÃƒÂ©/g, 'changé'],
    [/ajoutÃƒÂ©e/g, 'ajoutée'],
    [/effectuÃƒÂ©e/g, 'effectuée'],
    [/SystÃƒÂ¨me/g, 'Système'],
    [/liÃƒÂ©e/g, 'liée'],
    [/dÃƒÂ©liÃƒÂ©e/g, 'déliée'],
    [/ÃƒÂ /g, 'à'],
    [/ÃƒÂ©/g, 'é'],
    [/ÃƒÂ¨/g, 'è'],
    [/ÃƒÂª/g, 'ê'],
    [/ÃƒÂ´/g, 'ô'],
    [/ÃƒÂ§/g, 'ç'],
    [/Ã¢â€ â€™/g, '→'],
    [/Ã¢â‚¬â„¢/g, '’'],
    [/â€™/g, '’'],
    [/â€œ|â€/g, '"']
  ];

  constructor(private readonly auditService: OrganizationAuditService) {}

  ngOnInit(): void {
    this.loadActivities();
  }

  loadActivities(): void {
    this.loading = true;
    this.auditService.getRecentActivities().subscribe({
      next: (data) => {
        this.activities = data;
        this.applyFilters();
        this.loading = false;
      },
      error: () => {
        this.activities = [];
        this.applyFilters();
        this.loading = false;
      }
    });
  }

  applyFilters(): void {
    let result = [...this.activities];

    // Search filter
    if (this.searchTerm && this.searchTerm.trim()) {
      const term = this.searchTerm.toLowerCase();
      result = result.filter(a => 
        this.getActivityTitle(a).toLowerCase().includes(term) ||
        this.displayText(a.description).toLowerCase().includes(term) ||
        this.displayText(a.actorName).toLowerCase().includes(term) ||
        (a.type && a.type.toLowerCase().includes(term))
      );
    }

    // Action Type filter
    if (this.selectedActionType !== 'ALL') {
      if (this.selectedActionType === 'VALIDATION') {
        result = result.filter(a => {
          const at = a.actionType?.toUpperCase();
          return at === 'SUBMIT' || at === 'APPROVE' || at === 'REJECT';
        });
      } else {
        result = result.filter(a => a.actionType?.toUpperCase() === this.selectedActionType);
      }
    }

    // Module filter
    if (this.selectedModule !== 'ALL') {
      result = result.filter(a => a.type?.toUpperCase() === this.selectedModule);
    }

    this.filteredActivities = result;
    this.expandedIndex = null;
  }

  selectActionType(type: string): void {
    this.selectedActionType = type;
    this.applyFilters();
  }

  selectModule(mod: string): void {
    this.selectedModule = mod;
    this.applyFilters();
  }

  onSearch(event: any): void {
    this.searchTerm = event.target.value;
    this.applyFilters();
  }

  toggleDetails(index: number): void {
    this.expandedIndex = this.expandedIndex === index ? null : index;
  }

  countByActionType(type: string): number {
    if (type === 'ALL') return this.activities.length;
    
    if (type === 'VALIDATION') {
      return this.activities.filter(a => {
        const at = a.actionType?.toUpperCase();
        return at === 'SUBMIT' || at === 'APPROVE' || at === 'REJECT';
      }).length;
    }
    
    return this.activities.filter(a => a.actionType?.toUpperCase() === type).length;
  }

  countByModule(mod: string): number {
    if (mod === 'ALL') return this.activities.length;
    return this.activities.filter(a => a.type?.toUpperCase() === mod).length;
  }

  getActivityIcon(actionType: string, type: string): string {
    const act = (actionType || '').toUpperCase();
    const t = (type || '').toUpperCase();

    if (act === 'CREATE') return 'add_circle_outline';
    if (act === 'UPDATE') return 'edit';
    if (act === 'DELETE') return 'delete_outline';
    if (act === 'LOGIN') return 'vpn_key';
    if (act === 'UPLOAD') return 'cloud_upload';
    if (act === 'SUBMIT') return 'send';
    if (act === 'APPROVE') return 'check_circle';
    if (act === 'REJECT') return 'cancel';
    if (act === 'ARCHIVE') return 'archive';

    if (t.includes('CREATE')) return 'add_circle_outline';
    if (t.includes('UPDATE') || t.includes('EDIT')) return 'edit';
    if (t.includes('DELETE')) return 'delete_outline';
    if (t.includes('STATUS')) return 'published_with_changes';
    if (t.includes('LOGIN')) return 'login';
    if (t.includes('DOCUMENT')) return 'description';
    if (t.includes('PROCESS')) return 'account_tree';
    if (t.includes('NC') || t.includes('CONFORMITY')) return 'warning_amber';
    return 'history';
  }

  getActivityColor(actionType: string, type: string): string {
    const act = (actionType || '').toUpperCase();
    const t = (type || '').toUpperCase();

    if (act === 'CREATE' || act === 'APPROVE') return '#10b981'; // Emerald Green
    if (act === 'DELETE' || act === 'REJECT') return '#f43f5e'; // Rose Red
    if (act === 'UPDATE' || act === 'SUBMIT') return '#3b82f6'; // Indigo Blue
    if (act === 'LOGIN') return '#8b5cf6'; // Violet
    if (act === 'UPLOAD') return '#06b6d4'; // Cyan

    if (t.includes('CREATE')) return '#10b981';
    if (t.includes('DELETE')) return '#f43f5e';
    if (t.includes('UPDATE')) return '#3b82f6';
    if (t.includes('NC') || t.includes('WARNING')) return '#f59e0b'; // Amber
    return '#64748b'; // Slate
  }

  getModuleLabel(module?: string | null): string {
    if (!module) return 'Système';
    const m = module.toUpperCase();
    if (m === 'DOCUMENT') return 'Document';
    if (m === 'PROCESS') return 'Processus';
    if (m === 'PROCEDURE') return 'Procédure';
    if (m === 'USER') return 'Utilisateur';
    if (m === 'NC' || m === 'NONCONFORMITY') return 'Non-Conformité';
    if (m === 'CORRECTIVE_ACTION') return 'Action Corrective';
    return module.charAt(0).toUpperCase() + module.slice(1).toLowerCase();
  }

  getActionLabel(actionType?: string | null): string {
    const act = (actionType || '').toUpperCase();
    if (act === 'CREATE' || act === 'CREATED') return 'Création';
    if (act === 'UPDATE' || act === 'UPDATED') return 'Modification';
    if (act === 'DELETE' || act === 'DELETED') return 'Suppression';
    if (act === 'ROLE_CHANGE') return 'Changement de rôle';
    if (act === 'ACTIVATE') return 'Activation';
    if (act === 'DEACTIVATE') return 'Désactivation';
    if (act === 'STATUS_TOGGLED') return 'Statut modifié';
    if (act === 'INSTRUCTION_ADDED') return 'Instruction ajoutée';
    if (act === 'INSTRUCTION_UPDATED') return 'Instruction modifiée';
    if (act === 'INSTRUCTION_DELETED') return 'Instruction supprimée';
    if (act === 'LOGIN') return 'Connexion';
    if (act === 'UPLOAD') return 'Import';
    if (act === 'SUBMIT') return 'Soumission';
    if (act === 'APPROVE') return 'Approbation';
    if (act === 'REJECT') return 'Rejet';
    if (act === 'ARCHIVE') return 'Archivage';
    return this.displayText(actionType || 'Action');
  }

  getActivityTitle(activity: OrganizationActivityResponse): string {
    return this.displayText(activity.title)
      .replace(/\bPROCEDURE_UPDATED\b/g, 'procédure modifiée')
      .replace(/\bPROCEDURE_CREATED\b/g, 'procédure créée')
      .replace(/\bPROCEDURE_DELETED\b/g, 'procédure supprimée')
      .replace(/\bROLE_CHANGE\b/g, 'changement de rôle')
      .replace(/\bUSER_MANAGEMENT\b/g, 'gestion utilisateur');
  }

  displayText(value?: string | null): string {
    if (!value) return '';

    let text = value;
    for (const [pattern, replacement] of this.mojibakeReplacements) {
      text = text.replace(pattern, replacement);
    }

    return text;
  }

  getActionBadgeClass(actionType?: string | null): string {
    const act = (actionType || '').toUpperCase();
    if (act === 'CREATE' || act === 'APPROVE') return 'badge-success';
    if (act === 'DELETE' || act === 'REJECT') return 'badge-danger';
    if (act === 'UPDATE' || act === 'SUBMIT') return 'badge-primary';
    if (act === 'LOGIN') return 'badge-purple';
    if (act === 'UPLOAD') return 'badge-cyan';
    return 'badge-secondary';
  }
}
