import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';
import { environment } from './environments/environment';

// despierta el app pool de MonsterASP mientras Angular todavia esta parseando su propio bundle
fetch(`${environment.apiBase}/api/health`, { mode: 'cors', cache: 'no-store' }).catch(() => {});

bootstrapApplication(App, appConfig).catch((err) => console.error(err));
