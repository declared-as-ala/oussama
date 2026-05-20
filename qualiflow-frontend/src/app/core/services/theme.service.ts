import { Injectable, Renderer2, RendererFactory2 } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

@Injectable({
    providedIn: 'root'
})
export class ThemeService {
    private renderer: Renderer2;
    private currentTheme = new BehaviorSubject<'light' | 'dark'>(this.getInitialTheme());
    theme$ = this.currentTheme.asObservable();

    constructor(rendererFactory: RendererFactory2) {
        this.renderer = rendererFactory.createRenderer(null, null);
        this.applyTheme(this.currentTheme.value);
    }

    toggleTheme(): void {
        const newTheme = this.currentTheme.value === 'light' ? 'dark' : 'light';
        this.currentTheme.next(newTheme);
        this.applyTheme(newTheme);
        localStorage.setItem('theme', newTheme);
    }

    setTheme(theme: 'light' | 'dark'): void {
        this.currentTheme.next(theme);
        this.applyTheme(theme);
        localStorage.setItem('theme', theme);
    }

    private getInitialTheme(): 'light' | 'dark' {
        const saved = localStorage.getItem('theme');
        if (saved === 'light' || saved === 'dark') {
            return saved as 'light' | 'dark';
        }
        return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    }

    private applyTheme(theme: 'light' | 'dark'): void {
        if (theme === 'dark') {
            this.renderer.addClass(document.body, 'dark-theme');
        } else {
            this.renderer.removeClass(document.body, 'dark-theme');
        }
    }
}
