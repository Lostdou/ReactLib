import { Injectable, signal } from '@angular/core';

const DWELL_MS = 350;
const PREVIEW_MS = 4_000;
const START_AT_SECONDS = 1;

@Injectable({ providedIn: 'root' })
export class Preview {
  readonly activeSeq = signal<number | null>(null);

  private dwellTimer: ReturnType<typeof setTimeout> | null = null;
  private stopTimer: ReturnType<typeof setTimeout> | null = null;
  private active: HTMLVideoElement | null = null;

  get enabled(): boolean {
    return (
      matchMedia('(hover: hover) and (pointer: fine)').matches &&
      !matchMedia('(prefers-reduced-motion: reduce)').matches &&
      !this.onSlowConnection()
    );
  }

  request(seq: number, video: HTMLVideoElement): void {
    if (!this.enabled) return;

    this.cancel();
    this.dwellTimer = setTimeout(() => this.play(seq, video), DWELL_MS);
  }

  cancel(): void {
    if (this.dwellTimer) clearTimeout(this.dwellTimer);
    if (this.stopTimer) clearTimeout(this.stopTimer);
    this.dwellTimer = null;
    this.stopTimer = null;
    this.release();
  }

  private async play(seq: number, video: HTMLVideoElement): Promise<void> {
    this.release();
    this.active = video;
    this.activeSeq.set(seq);

    try {
      video.currentTime = START_AT_SECONDS;
      await video.play();
      this.stopTimer = setTimeout(() => this.cancel(), PREVIEW_MS);
    } catch {
      this.cancel();
    }
  }

  private release(): void {
    const video = this.active;
    this.active = null;
    this.activeSeq.set(null);
    if (!video) return;

    video.pause();
    // sin esto Chrome sigue buffereando el mp4 aunque el elemento ya no se vea
    video.removeAttribute('src');
    video.load();
  }

  private onSlowConnection(): boolean {
    const connection = (navigator as Navigator & { connection?: { saveData?: boolean; effectiveType?: string } })
      .connection;

    if (!connection) return false;
    if (connection.saveData) return true;

    return ['slow-2g', '2g', '3g'].includes(connection.effectiveType ?? '');
  }
}
