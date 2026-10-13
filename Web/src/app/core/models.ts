export interface VideoCard {
  seq: number;
  handle: string;
  title: string;
  tags: string[];
  copyUrl: string;
  tweetUrl: string;
  posterUrl: string | null;
  mp4Url: string | null;
  durationMs: number | null;
  width: number | null;
  height: number | null;
  copies: number;
}

export interface GridPage {
  items: VideoCard[];
  next: string | null;
}

export interface ApiConfig {
  copyFormat: 'media' | 'tweet';
  catalogVersion: number;
}

export type GridSort = 'new' | 'top';

export interface GridQuery {
  sort: GridSort;
  tag: string | null;
  q: string | null;
}
