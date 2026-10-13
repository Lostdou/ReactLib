import { ChangeDetectionStrategy, Component, ElementRef, computed, inject, input, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { copyText } from '../../core/clipboard';
import { I18n } from '../../core/i18n';
import { VideoCard } from '../../core/models';
import { Preview } from '../../core/preview';
import { Toast } from '../../core/toast';

@Component({
  selector: 'rl-video-card',
  imports: [RouterLink],
  templateUrl: './video-card.html',
  styleUrl: './video-card.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VideoCardComponent {
  readonly video = input.required<VideoCard>();

  readonly i18n = inject(I18n);

  private readonly preview = inject(Preview);
  private readonly toast = inject(Toast);
  private readonly previewEl = viewChild<ElementRef<HTMLVideoElement>>('previewEl');

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
    const t = this.i18n.t();
    this.toast.show(copied ? t.copied : t.copyFailed, copied ? 'ok' : 'error');
  }
}
