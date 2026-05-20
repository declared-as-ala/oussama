import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-footer-home',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './footer-home.component.html',
  styleUrls: ['./footer-home.component.scss']
})
export class FooterHomeComponent {
  currentYear = new Date().getFullYear();
}
