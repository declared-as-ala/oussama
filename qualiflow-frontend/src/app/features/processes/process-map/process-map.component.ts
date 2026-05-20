import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { forkJoin } from 'rxjs';
import { NotificationService } from '../../../core/services/notification.service';
import {
  ProcessListItemResponse,
  ProcessMapResponse,
  ProcessStatisticsResponse
} from '../models/process.models';
import { ProcessService } from '../services/process.service';

@Component({
  selector: 'app-process-map',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './process-map.component.html',
  styleUrls: ['./process-map.component.scss']
})
export class ProcessMapComponent implements OnInit {
  loading = false;
  map: ProcessMapResponse | null = null;
  stats: ProcessStatisticsResponse | null = null;

  constructor(
    private readonly processService: ProcessService,
    private readonly notificationService: NotificationService,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    this.loading = true;

    forkJoin({
      map: this.processService.getProcessMap(),
      stats: this.processService.getProcessStatistics()
    }).subscribe({
      next: ({ map, stats }) => {
        this.map = map;
        this.stats = stats;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Impossible de charger la cartographie des processus.');
      }
    });
  }

  backToList(): void {
    this.router.navigate(['/processes']);
  }

  openDetails(process: ProcessListItemResponse): void {
    this.router.navigate(['/processes', process.id]);
  }
}
