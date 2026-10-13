import { ChangeDetectionStrategy, Component, ElementRef, computed, inject, input, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { copyText } from '../../core/clipboard';
import { VideoCard } from '../../core/models';
import { Preview } from '../../core/preview';
import { Toast } from '../../core/toast';

const FALLBACK_RATIO = '1 / 1';

@Component({
  selector: 'rl-video-card',
  imports: [RouterLink],
  templateUrl: './video-card.html',
  styleUrl: './video-card.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VideoCardComponent {
  readonly video = input.required<VideoCard>();

  private readonly preview = inject(Preview);
  private readonly toast = inject(Toast);
  private readonly previewEl = viewChild<ElementRef<HTMLVideoElement>>('previewEl');

  readonly aspectRatio = computed(() => {
    const { width, height } = this.video();
    return width && height ? `${width} / ${height}` : FALLBACK_RATIO;
  });

  readonly duration = computed(() => {
    const ms = this.video().durationMs;
    if (!ms) return null;

    const totalSeconds = Math.round(ms / 1000);
    return `${Math.floor(totalSeconds / 60)}:${String(totalSeconds % 60).padStart(2, '0')}`;
  });

  readonly initial = computed(() => this.video().handle.charAt(0).toUpperCase());

  startPreview(): void {
    const element = this.previewEl()?.nativeElement;
    const source = this.video().mp4Url;
    if (!element || !source) return;

    if (!element.getAttribute('src')) element.setAttribute('src', source);
    this.preview.request(this.video().seq, element);
  }

  stopPreview(): void {
    this.preview.cancel();
  }

  async copy(): Promise<void> {
    const copied = await copyText(this.video().copyUrl);
    this.toast.show(
      copied ? 'Link copiado: pegalo en el texto de tu post' : 'No se pudo copiar',
      copied ? 'ok' : 'error',
    );
  }
}
