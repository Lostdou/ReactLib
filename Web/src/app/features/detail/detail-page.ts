import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Api } from '../../core/api';
import { copyText } from '../../core/clipboard';
import { VideoCard } from '../../core/models';
import { Toast } from '../../core/toast';

@Component({
  selector: 'rl-detail-page',
  imports: [RouterLink],
  templateUrl: './detail-page.html',
  styleUrl: './detail-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DetailPageComponent {
  readonly video = signal<VideoCard | null>(null);
  readonly loading = signal(true);
  readonly notFound = signal(false);

  readonly aspectRatio = computed(() => {
    const current = this.video();
    return current?.width && current?.height ? `${current.width} / ${current.height}` : '1 / 1';
  });

  private readonly api = inject(Api);
  private readonly route = inject(ActivatedRoute);
  private readonly toast = inject(Toast);

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      void this.fetch(Number(params.get('seq')));
    });
  }

  async copy(): Promise<void> {
    const current = this.video();
    if (!current) return;

    const copied = await copyText(current.copyUrl);
    this.toast.show(
      copied ? 'Link copiado: pegalo en el texto de tu post' : 'No se pudo copiar',
      copied ? 'ok' : 'error',
    );
  }

  private async fetch(seq: number): Promise<void> {
    this.loading.set(true);
    this.notFound.set(false);

    try {
      this.video.set(await this.api.detail(seq));
    } catch {
      this.video.set(null);
      this.notFound.set(true);
    } finally {
      this.loading.set(false);
    }
  }
}
