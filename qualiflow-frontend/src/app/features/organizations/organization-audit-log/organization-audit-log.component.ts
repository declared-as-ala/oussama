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
  loading = true;

  constructor(private readonly auditService: OrganizationAuditService) {}

  ngOnInit(): void {
    this.loadActivities();
  }

  loadActivities(): void {
    this.loading = true;
    this.auditService.getRecentActivities().subscribe({
      next: (data) => {
        this.activities = data;
        this.loading = false;
      },
      error: () => {
        this.activities = [];
        this.loading = false;
      }
    });
  }

  getActivityIcon(type: string): string {
    const t = type.toLowerCase();
    if (t.includes('create')) return 'add_circle_outline';
    if (t.includes('update') || t.includes('edit')) return 'edit';
    if (t.includes('delete')) return 'delete_outline';
    if (t.includes('status')) return 'published_with_changes';
    if (t.includes('login')) return 'login';
    if (t.includes('document')) return 'description';
    if (t.includes('process')) return 'account_tree';
    if (t.includes('nc') || t.includes('conformity')) return 'warning_amber';
    return 'history';
  }

  getActivityColor(type: string): string {
    const t = type.toLowerCase();
    if (t.includes('create')) return '#10b981'; // Green
    if (t.includes('delete')) return '#ef4444'; // Red
    if (t.includes('update')) return '#3b82f6'; // Blue
    if (t.includes('nc') || t.includes('warning')) return '#f59e0b'; // Amber
    return '#64748b'; // Slate
  }

  getModuleLabel(module?: string | null): string {
    if (!module) return 'Système';
    return module.charAt(0).toUpperCase() + module.slice(1);
  }
}
