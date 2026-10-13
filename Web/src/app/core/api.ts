import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { ApiConfig, GridPage, GridQuery, VideoCard } from './models';

@Injectable({ providedIn: 'root' })
export class Api {
  private readonly http = inject(HttpClient);

  grid(query: GridQuery, after: string | null, size?: number): Promise<GridPage> {
    let params = new HttpParams().set('sort', query.sort);

    if (query.tag) params = params.set('tag', query.tag);
    if (query.q) params = params.set('q', query.q);
    if (after) params = params.set('after', after);
    if (size) params = params.set('size', size);

    return firstValueFrom(
      this.http.get<GridPage>(`${environment.apiBase}/api/videos`, { params }),
    );
  }

  detail(seq: number): Promise<VideoCard> {
    return firstValueFrom(this.http.get<VideoCard>(`${environment.apiBase}/api/videos/${seq}`));
  }

  config(): Promise<ApiConfig> {
    return firstValueFrom(this.http.get<ApiConfig>(`${environment.apiBase}/api/config`));
  }
}
