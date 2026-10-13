import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Api } from '../../core/api';
import { I18n } from '../../core/i18n';
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
  readonly reported = signal(false);

  readonly aspectRatio = computed(() => {
    const current = this.video();
    return current?.width && current?.height ? `${current.width} / ${current.height}` : '1 / 1';
  });

  readonly i18n = inject(I18n);

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
    const t = this.i18n.t();
    this.toast.show(copied ? t.copied : t.copyFailed, copied ? 'ok' : 'error');
  }

  async report(): Promise<void> {
    const current = this.video();
    if (!current || this.reported()) return;

    this.reported.set(true);
    try {
      await this.api.report(current.seq);
      this.toast.show(this.i18n.t().reportThanks);
    } catch {
      this.toast.show(this.i18n.t().reportFailed, 'error');
      this.reported.set(false);
    }
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
