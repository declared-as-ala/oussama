import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';

interface NewsItem {
  date: string;
  title: string;
  description: string;
}

@Component({
  selector: 'app-news-home',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './news-home.component.html',
  styleUrls: ['./news-home.component.scss']
})
export class NewsHomeComponent {
  news: NewsItem[] = [
    {
      date: '12 Mars 2026',
      title: 'Nouveau module de suivi des actions correctives',
      description: 'QualiFlow introduit un suivi plus fin des actions correctives avec échéances intelligentes.'
    },
    {
      date: '28 Février 2026',
      title: 'Amélioration continue des workflows qualité',
      description: 'Les circuits de validation et de revue documentaire ont été optimisés pour plus de fluidité.'
    },
    {
      date: '15 Janvier 2026',
      title: 'Conseils pratiques GED & ISO 21001',
      description: 'Une nouvelle série d’actualités partage des bonnes pratiques pour renforcer votre conformité.'
    }
  ];
}
