import { Component, OnInit, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../../core/services/auth.service';

interface Feature {
    icon: string;
    title: string;
    description: string;
}

interface Module {
    id: string;
    name: string;
    description: string;
    benefit: string;
    icon: string;
}

@Component({
       selector: 'app-home',
    standalone: true,
    imports: [CommonModule, RouterLink, FormsModule],
    templateUrl: './home.component.html',
    styleUrls: ['./home.component.scss']
})
export class HomeComponent implements OnInit {
    isMenuOpen = false;
    isScrolled = false;

    features: Feature[] = [
        {
            icon: 'hub',
            title: 'Gestion des Organisations',
            description: 'Administration centralisée pour multi-instituts avec supervision globale.'
        },
        {
            icon: 'people',
            title: 'Utilisateurs & Rôles',
            description: 'Contrôle d\'accès granulaire basé sur les rôles et structures hiérarchiques.'
        },
        {
            icon: 'account_tree',
            title: 'Management des Processus',
            description: 'Cartographie complète des processus avec objectifs et pilotage.'
        },
        {
            icon: 'description',
            title: 'Gestion Documentaire (GED)',
            description: 'Cycle de vie complet des documents : création, validation, versioning.'
        },
        {
            icon: 'report_problem',
            title: 'Non-Conformités',
            description: 'Détection, déclaration et suivi rigoureux des anomalies qualité.'
        },
        {
            icon: 'check_circle',
            title: 'Actions Correctives',
            description: 'Planification et vérification de l\'efficacité des actions Correctives.'
        },
        {
            icon: 'trending_up',
            title: 'Indicateurs & KPI',
            description: 'Suivi de performance en temps réel avec tableaux de bord analytiques.'
        },
        {
            icon: 'notifications_active',
            title: 'Alertes Automatiques',
            description: 'Notifications intelligentes pour les échéances et validations critiques.'
        }
    ];

    modules: Module[] = [
        {
            id: 'org',
            name: 'Organisations',
            description: 'Gestion multi-organisations avec supervision globale.',
            benefit: 'Transparence et contrôle centralisé.',
            icon: 'corporate_fare'
        },
        {
            id: 'proc',
            name: 'Processus',
            description: 'Cartographie, pilotage, objectifs, responsables.',
            benefit: 'Vision claire de la chaîne de valeur.',
            icon: 'settings_suggest'
        },
        {
            id: 'doc',
            name: 'Documents',
            description: 'Versioning, validation, archivage, traçabilité.',
            benefit: 'Zéro papier et conformité totale.',
            icon: 'folder_shared'
        },
        {
            id: 'nc',
            name: 'Non-conformités',
            description: 'Déclaration, suivi, traitement, analyse.',
            benefit: 'Réduction des coûts de non-qualité.',
            icon: 'error_outline'
        }
    ];

    steps = [
        { title: 'Rejoindre', desc: 'Créez ou rejoignez votre organisation.' },
        { title: 'Structurer', desc: 'Définissez les services et rôles.' },
        { title: 'Gérer', desc: 'Cartographiez vos processus et documents.' },
        { title: 'Optimiser', desc: 'Suivez les KPI et améliorez la qualité.' }
    ];

    constructor(
        private authService: AuthService,
        private router: Router
    ) { }

    ngOnInit(): void {
        if (this.authService.isAuthenticated()) {
            const user = this.authService.getCurrentUser();
            if (user?.role === 'SUPER_ADMIN') {
                this.router.navigate(['/super-admin/dashboard']);
            } else {
                this.router.navigate(['/dashboard']);
            }
            return;
        }
        window.scrollTo(0, 0);
    }

    @HostListener('window:scroll', [])
    onWindowScroll() {
        this.isScrolled = window.scrollY > 50;
    }

    toggleMenu() {
        this.isMenuOpen = !this.isMenuOpen;
    }





    scrollToSection(sectionId: string) {
        const element = document.getElementById(sectionId);
        if (element) {
            element.scrollIntoView({ behavior: 'smooth', block: 'start' });
            this.isMenuOpen = false;
        }
    }

    getYear(): number {
        return new Date().getFullYear();
    }
}
