import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { Api } from '../../core/api';
import { dhashFromPoster } from '../../core/dhash';
import { SubmitResponse, TagSuggestion, VideoPreview } from '../../core/models';
import { Toast } from '../../core/toast';

const MAX_TAGS = 5;
const SUGGEST_DEBOUNCE_MS = 200;

type Step = 'url' | 'tags' | 'done';

@Component({
  selector: 'rl-submit-page',
  imports: [RouterLink],
  templateUrl: './submit-page.html',
  styleUrl: './submit-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SubmitPageComponent {
  readonly step = signal<Step>('url');
  readonly url = signal('');
  readonly preview = signal<VideoPreview | null>(null);
  readonly chosenTags = signal<string[]>([]);
  readonly tagInput = signal('');
  readonly suggestions = signal<TagSuggestion[]>([]);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly result = signal<SubmitResponse | null>(null);

  readonly canAddMore = computed(() => this.chosenTags().length < MAX_TAGS);
  readonly isKnownTag = computed(() =>
    this.suggestions().some((s) => s.name === this.normalized()),
  );

  private readonly api = inject(Api);
  private readonly toast = inject(Toast);
  private suggestTimer: ReturnType<typeof setTimeout> | null = null;
  private dhash: string | null = null;

  async resolve(): Promise<void> {
    const url = this.url().trim();
    if (!url || this.busy()) return;

    this.busy.set(true);
    this.error.set(null);

    try {
      const response = await this.api.resolve(url);
      this.preview.set(response.preview);
      this.step.set('tags');
      void this.loadSuggestions('');
      void this.computeHash(response.preview);
    } catch (failure) {
      this.error.set(this.messageFor(failure));
    } finally {
      this.busy.set(false);
    }
  }

  onTagInput(value: string): void {
    this.tagInput.set(value);
    if (this.suggestTimer) clearTimeout(this.suggestTimer);

    this.suggestTimer = setTimeout(() => void this.loadSuggestions(value), SUGGEST_DEBOUNCE_MS);
  }

  addTag(name: string): void {
    const tag = this.normalize(name);
    if (!tag || !this.canAddMore() || this.chosenTags().includes(tag)) return;

    this.chosenTags.update((tags) => [...tags, tag]);
    this.tagInput.set('');
    void this.loadSuggestions('');
  }

  removeTag(name: string): void {
    this.chosenTags.update((tags) => tags.filter((t) => t !== name));
  }

  async submit(): Promise<void> {
    if (this.busy()) return;

    this.busy.set(true);
    this.error.set(null);

    try {
      const response = await this.api.submit(this.url().trim(), this.chosenTags().join(','), this.dhash);
      this.result.set(response);
      this.step.set('done');
    } catch (failure) {
      this.error.set(this.messageFor(failure));
    } finally {
      this.busy.set(false);
    }
  }

  restart(): void {
    this.step.set('url');
    this.url.set('');
    this.preview.set(null);
    this.chosenTags.set([]);
    this.tagInput.set('');
    this.result.set(null);
    this.error.set(null);
    this.dhash = null;
  }

  normalized(): string {
    return this.normalize(this.tagInput());
  }

  private normalize(value: string): string {
    return value
      .trim()
      .toLowerCase()
      .normalize('NFD')
      .replace(/[̀-ͯ]/g, '')
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/^-+|-+$/g, '')
      .slice(0, 30);
  }

  private async loadSuggestions(query: string): Promise<void> {
    try {
      this.suggestions.set(await this.api.tags(this.normalize(query)));
    } catch {
      this.suggestions.set([]);
    }
  }

  // el hash viaja con el envío: si falla, el video entra igual y solo queda sin dedup perceptual
  private async computeHash(preview: VideoPreview | null): Promise<void> {
    this.dhash = preview?.posterUrl ? await dhashFromPoster(preview.posterUrl) : null;
  }

  private messageFor(failure: unknown): string {
    if (!(failure instanceof HttpErrorResponse)) return 'Algo falló. Probá de nuevo.';

    const outcome = (failure.error as SubmitResponse | null)?.outcome;
    if (failure.status === 429) return 'Enviaste varios seguidos. Esperá un rato.';

    switch (outcome) {
      case 'InvalidUrl':
        return 'Ese link no parece un post de X.';
      case 'AlreadyExists':
        return 'Ese video ya está en la biblioteca.';
      case 'NoVideoFound':
        return 'No encontramos ningún video en ese post.';
      default:
        return 'No pudimos procesarlo. Probá de nuevo en un minuto.';
    }
  }
}
