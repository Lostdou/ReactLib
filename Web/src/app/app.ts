import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { environment } from '../environments/environment';
import { I18n, LANGUAGES, Lang } from './core/i18n';
import { Toast } from './core/toast';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink],
  templateUrl: './app.html',
  styleUrl: './app.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  readonly toast = inject(Toast);
  readonly i18n = inject(I18n);
  readonly languages = LANGUAGES;
  readonly hitsBadge = `https://hits.sh/${environment.hitsKey}.svg?style=flat-square&label=visitas`;
  readonly hitsLink = `https://hits.sh/${environment.hitsKey}/`;

  setLang(lang: Lang): void {
    this.i18n.setLang(lang);
  }

  hideBadge(event: Event): void {
    (event.target as HTMLElement).style.display = 'none';
  }
}
