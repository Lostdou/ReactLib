import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CatalogStore } from '../../core/catalog-store';
import { I18n } from '../../core/i18n';
import { GridQuery, GridSort } from '../../core/models';
import { VideoCardComponent } from './video-card';

const SEARCH_DEBOUNCE_MS = 350;
const SKELETON_COUNT = 8;

@Component({
  selector: 'rl-grid-page',
  imports: [VideoCardComponent, RouterLink, FormsModule],
  templateUrl: './grid-page.html',
  styleUrl: './grid-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GridPageComponent {
  readonly store = inject(CatalogStore);
  readonly i18n = inject(I18n);
  readonly search = signal('');
  readonly skeletons = Array.from({ length: SKELETON_COUNT });

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const query: GridQuery = {
        sort: params.get('sort') === 'top' ? 'top' : 'new',
        tag: params.get('tag'),
        q: params.get('q'),
      };

      this.search.set(query.q ?? '');
      void this.store.load(query);
    });
  }

  onSearchInput(value: string): void {
    this.search.set(value);
    if (this.searchTimer) clearTimeout(this.searchTimer);

    this.searchTimer = setTimeout(
      () => this.navigate({ q: value.trim() || null }),
      SEARCH_DEBOUNCE_MS,
    );
  }

  setSort(sort: GridSort): void {
    this.navigate({ sort: sort === 'new' ? null : sort });
  }

  clearTag(): void {
    this.navigate({ tag: null });
  }

  private navigate(params: Record<string, string | null>): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: params,
      queryParamsHandling: 'merge',
    });
  }
}
