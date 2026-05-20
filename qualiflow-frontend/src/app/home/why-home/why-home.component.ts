import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';

@Component({
  selector: 'app-why-home',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './why-home.component.html',
  styleUrls: ['./why-home.component.scss']
})
export class WhyHomeComponent {
  whyChooseQualiFlow = [
    'Centralisation des informations',
    'Gain de temps',
    'Meilleure traçabilité',
    'Réduction des erreurs',
    'Amélioration de la conformité ISO 21001',
    'Interface simple et moderne',
    'Solution adaptée aux établissements éducatifs'
  ];
}
