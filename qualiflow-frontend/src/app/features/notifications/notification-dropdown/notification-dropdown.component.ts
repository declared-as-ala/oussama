import { Component } from '@angular/core';
import { NotificationPanelComponent } from '../notification-panel/notification-panel.component';

@Component({
  selector: 'app-notification-dropdown',
  standalone: true,
  imports: [NotificationPanelComponent],
  template: '<app-notification-panel></app-notification-panel>'
})
export class NotificationDropdownComponent {}
