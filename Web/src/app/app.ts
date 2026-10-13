import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { environment } from '../environments/environment';
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
  readonly hitsBadge = `https://hits.sh/${environment.hitsKey}.svg?style=flat-square&label=visitas`;
  readonly hitsLink = `https://hits.sh/${environment.hitsKey}/`;

  hideBadge(event: Event): void {
    (event.target as HTMLElement).style.display = 'none';
  }
}
