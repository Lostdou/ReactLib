import { Injectable, computed, inject, signal } from '@angular/core';
import { Api } from './api';
import { readCachedPage, writeCachedPage } from './catalog-cache';
import { GridQuery, VideoCard } from './models';

const WAKING_HINT_MS = 3_000;
const STILL_WAKING_HINT_MS = 15_000;

export type Slowness = 'none' | 'waking' | 'stillWaking';

@Injectable({ providedIn: 'root' })
export class CatalogStore {
  private readonly api = inject(Api);

  readonly items = signal<VideoCard[]>([]);
  readonly loading = signal(false);
  readonly loadingMore = signal(false);
  readonly error = signal<string | null>(null);
  readonly slowness = signal<Slowness>('none');
  readonly query = signal<GridQuery>({ sort: 'new', tag: null, q: null });

  readonly hasMore = computed(() => this.cursor() !== null);
  readonly isEmpty = computed(() => !this.loading() && this.items().length === 0);

  private readonly cursor = signal<string | null>(null);
  private hintTimers: ReturnType<typeof setTimeout>[] = [];
  private requestId = 0;

  async load(query: GridQuery): Promise<void> {
    this.query.set(query);
    this.error.set(null);

    const cached = readCachedPage(query);
    if (cached) {
      this.items.set(cached.items);
      this.cursor.set(cached.next);
    } else {
      this.items.set([]);
      this.cursor.set(null);
    }

    const id = ++this.requestId;
    this.loading.set(true);
    this.startSlownessHints();

    try {
      const page = await this.api.grid(query, null);
      if (id !== this.requestId) return;

      this.items.set(page.items);
      this.cursor.set(page.next);
      writeCachedPage(query, page);
    } catch {
      if (id !== this.requestId) return;
      if (this.items().length === 0) this.error.set('No pudimos cargar la biblioteca.');
    } finally {
      if (id === this.requestId) {
        this.loading.set(false);
        this.stopSlownessHints();
      }
    }
  }

  async loadMore(): Promise<void> {
    const after = this.cursor();
    if (!after || this.loadingMore()) return;

    this.loadingMore.set(true);
    try {
      const page = await this.api.grid(this.query(), after);
      this.items.update((current) => [...current, ...page.items]);
      this.cursor.set(page.next);
    } catch {
      this.error.set('No pudimos traer más resultados.');
    } finally {
      this.loadingMore.set(false);
    }
  }

  private startSlownessHints(): void {
    this.stopSlownessHints();
    this.slowness.set('none');
    this.hintTimers = [
      setTimeout(() => this.slowness.set('waking'), WAKING_HINT_MS),
      setTimeout(() => this.slowness.set('stillWaking'), STILL_WAKING_HINT_MS),
    ];
  }

  private stopSlownessHints(): void {
    this.hintTimers.forEach(clearTimeout);
    this.hintTimers = [];
    this.slowness.set('none');
  }
}
