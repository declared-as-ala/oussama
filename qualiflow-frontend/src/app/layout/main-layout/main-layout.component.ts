import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { Subscription } from 'rxjs';
import { filter } from 'rxjs/operators';
import { AuthService } from '../../core/services/auth.service';
import { OrganizationService } from '../../core/services/organization.service';
import { SidebarComponent } from '../sidebar/sidebar.component';
import { HeaderComponent } from '../header/header.component';
import { LoadingComponent } from '../../shared/components/loading/loading.component';
import { NotificationDropdownComponent } from '../../features/notifications/notification-dropdown/notification-dropdown.component';
import { NotificationSignalRService } from '../../features/notifications/services/notification-signalr.service';
import { NotificationService as UserNotificationService } from '../../features/notifications/services/notification.service';
import { BrowserNotificationService } from '../../features/notifications/services/browser-notification.service';
import { NotificationService as UiNotificationService } from '../../core/services/notification.service';
import { NotificationSignalRMessage } from '../../features/notifications/models/notification.models';

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    SidebarComponent,
    HeaderComponent,
    LoadingComponent,
    NotificationDropdownComponent
  ],
  templateUrl: './main-layout.component.html',
  styleUrls: ['./main-layout.component.scss']
})
export class MainLayoutComponent implements OnInit, OnDestroy {
  private readonly subscriptions = new Subscription();
  private logoObjectUrl: string | null = null;

  brandLogoUrl = 'assets/logo.png';
  sidebarOpen = false;

  constructor(
    private readonly authService: AuthService,
    private readonly organizationService: OrganizationService,
    private readonly notificationSignalRService: NotificationSignalRService,
    private readonly userNotificationService: UserNotificationService,
    private readonly browserNotificationService: BrowserNotificationService,
    private readonly uiNotificationService: UiNotificationService,
    private readonly router: Router
  ) { }

  ngOnInit(): void {
    this.tryLoadOrganizationLogo();

    this.subscriptions.add(
      this.organizationService.logoRefresh$.subscribe(() => {
        this.tryLoadOrganizationLogo();
      })
    );

    // Close sidebar on route change (mobile)
    this.subscriptions.add(
      this.router.events
        .pipe(filter(e => e instanceof NavigationEnd))
        .subscribe(() => {
          this.sidebarOpen = false;
        })
    );

    this.subscriptions.add(
      this.authService.isAuthenticated$.subscribe(isAuthenticated => {
        if (isAuthenticated && this.canShowNotifications) {
          void this.browserNotificationService.registerServiceWorker();
          void this.browserNotificationService.requestBrowserPermission().then(permission => {
            if (permission === 'granted') {
              void this.browserNotificationService.syncWebPushSubscription();
            }
          });
          void this.notificationSignalRService.startConnection();
          return;
        }

        void this.notificationSignalRService.stopConnection();
      })
    );

    this.subscriptions.add(
      this.notificationSignalRService.unreadCount$.subscribe(count => {
        this.userNotificationService.setUnreadCount(count);
      })
    );

    this.subscriptions.add(
      this.notificationSignalRService.notificationReceived$.subscribe(notification => {
        this.showRealtimeNotificationAlert(notification);
      })
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
    this.revokeLogoObjectUrl();
    void this.notificationSignalRService.stopConnection();
  }

  get canShowNotifications(): boolean {
    return this.authService.hasRole([
      'ADMIN_ORG',
      'RESPONSABLE_QUALITE',
      'CHEF_SERVICE',
      'UTILISATEUR',
      'SUPER_ADMIN'
    ]);
  }

  toggleSidebar(): void {
    this.sidebarOpen = !this.sidebarOpen;
  }

  closeSidebar(): void {
    this.sidebarOpen = false;
  }

  private tryLoadOrganizationLogo(): void {
    if (this.authService.hasRole('SUPER_ADMIN')) {
      this.revokeLogoObjectUrl();
      this.brandLogoUrl = 'assets/logo.png';
      return;
    }

    this.organizationService.downloadMyOrganizationLogo().subscribe({
      next: (blob) => {
        this.revokeLogoObjectUrl();
        this.logoObjectUrl = URL.createObjectURL(blob);
        this.brandLogoUrl = this.logoObjectUrl;
      },
      error: () => {
        this.revokeLogoObjectUrl();
        this.brandLogoUrl = 'assets/logo.png';
      }
    });
  }

  private revokeLogoObjectUrl(): void {
    if (this.logoObjectUrl) {
      URL.revokeObjectURL(this.logoObjectUrl);
      this.logoObjectUrl = null;
    }
  }

  private showRealtimeNotificationAlert(notification: NotificationSignalRMessage): void {
    void this.browserNotificationService.playNotificationSound();

    if (this.browserNotificationService.isDocumentHidden()) {
      void this.browserNotificationService.showSystemNotification(notification);
      return;
    }

    if (this.router.url.startsWith('/notifications')) {
      return;
    }

    this.uiNotificationService.showRealtimeNotification(
      notification.title,
      notification.message,
      notification.category
    );
  }
}
