import { Injectable, signal } from '@angular/core';

const VISIBLE_MS = 2_600;

export interface ToastMessage {
  text: string;
  tone: 'ok' | 'error';
}

@Injectable({ providedIn: 'root' })
export class Toast {
  readonly current = signal<ToastMessage | null>(null);

  private timer: ReturnType<typeof setTimeout> | null = null;

  show(text: string, tone: ToastMessage['tone'] = 'ok'): void {
    if (this.timer) clearTimeout(this.timer);

    this.current.set({ text, tone });
    this.timer = setTimeout(() => this.current.set(null), VISIBLE_MS);
  }
}
