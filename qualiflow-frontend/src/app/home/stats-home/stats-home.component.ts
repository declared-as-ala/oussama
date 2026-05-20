import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';

interface StatItem {
  value: string;
  label: string;
  icon: string;
}

@Component({
  selector: 'app-stats-home',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './stats-home.component.html',
  styleUrls: ['./stats-home.component.scss']
})
export class StatsHomeComponent {
  stats: StatItem[] = [
    { value: '+100', label: 'documents gérés', icon: 'description' },
    { value: '+20', label: 'processus suivis', icon: 'sync_alt' },
    { value: '+10', label: 'rôles et profils', icon: 'supervisor_account' },
    { value: '24/7', label: 'notifications', icon: 'notifications' }
  ];
}
