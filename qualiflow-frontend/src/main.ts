import { bootstrapApplication } from '@angular/platform-browser';
import 'apexcharts';
import { AppComponent } from './app/app.component';
import { provideRouter } from '@angular/router';
import { provideAnimations } from '@angular/platform-browser/animations';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { routes } from './app/app-routing.module';
import { authInterceptor } from './app/core/interceptors/auth.interceptor';
import { errorInterceptor } from './app/core/interceptors/error.interceptor';
import { loadingInterceptor } from './app/core/interceptors/loading.interceptor';

bootstrapApplication(AppComponent, {
  providers: [
    provideRouter(routes),
    provideAnimations(),
    provideHttpClient(
      withInterceptors([
        errorInterceptor,
        authInterceptor,
        loadingInterceptor
      ])
    )
  ]
})
  .then(() => initResponsiveTables())
  .catch(err => console.error(err));

function initResponsiveTables(): void {
  if (typeof document === 'undefined') {
    return;
  }

  const decorateTables = (root: ParentNode = document): void => {
    root.querySelectorAll('table').forEach((table) => {
      decorateTable(table as HTMLTableElement);
    });
  };

  const decorateFilterPanels = (root: ParentNode = document): void => {
    root
      .querySelectorAll('.glass-filters, .search-card, .project-search-card')
      .forEach((panel) => decorateFilterPanel(panel as HTMLElement));
  };

  const decorateTable = (table: HTMLTableElement): void => {
    if (table.closest('.no-mobile-cards')) {
      return;
    }

    const headers = Array.from(table.querySelectorAll('thead th, tr:first-child th'))
      .map((header) => (header.textContent ?? '').replace(/\s+/g, ' ').trim())
      .filter(Boolean);

    if (!headers.length) {
      return;
    }

    table.classList.add('mobile-card-table');

    table.querySelectorAll('tbody tr, tr.mat-mdc-row, tr.mdc-data-table__row').forEach((row) => {
      Array.from(row.children).forEach((cell, index) => {
        if (cell instanceof HTMLElement && cell.tagName.toLowerCase() === 'td') {
          cell.dataset['label'] = headers[index] ?? '';
        }
      });
    });
  };

  const decorateFilterPanel = (panel: HTMLElement): void => {
    if (panel.classList.contains('mobile-collapsible-filters') || panel.closest('.no-mobile-filter-toggle')) {
      return;
    }

    panel.classList.add('mobile-collapsible-filters');
    panel.id ||= `mobile-filter-panel-${Math.random().toString(36).slice(2, 9)}`;

    const toggle = document.createElement('button');
    toggle.type = 'button';
    toggle.className = 'mobile-filter-toggle';
    toggle.setAttribute('aria-expanded', 'false');
    toggle.setAttribute('aria-controls', panel.id);
    toggle.innerHTML = '<span class="material-icons" aria-hidden="true">manage_search</span><span class="filter-toggle-label">Recherche et filtres</span>';

    toggle.addEventListener('click', () => {
      const isOpen = panel.classList.toggle('is-mobile-filter-open');
      toggle.classList.toggle('is-open', isOpen);
      toggle.setAttribute('aria-expanded', String(isOpen));
      toggle.querySelector('.filter-toggle-label')!.textContent = isOpen ? 'Masquer les filtres' : 'Recherche et filtres';
    });

    panel.parentElement?.insertBefore(toggle, panel);
  };

  decorateTables();
  decorateFilterPanels();

  const observer = new MutationObserver((mutations) => {
    mutations.forEach((mutation) => {
      mutation.addedNodes.forEach((node) => {
        if (node instanceof HTMLTableElement) {
          decorateTable(node);
          return;
        }

        if (node instanceof HTMLElement) {
          const table = node.closest('table');
          if (table instanceof HTMLTableElement) {
            decorateTable(table);
          }
          decorateTables(node);
          decorateFilterPanels(node);
        }
      });
    });
  });

  observer.observe(document.body, {
    childList: true,
    subtree: true
  });
}
