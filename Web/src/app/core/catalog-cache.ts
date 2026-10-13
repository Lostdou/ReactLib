import { GridPage, GridQuery } from './models';

const STORAGE_KEY = 'reactlib.firstPage';

interface CachedPage {
  queryKey: string;
  page: GridPage;
}

export function queryKey(query: GridQuery): string {
  return `${query.sort}|${query.tag ?? ''}|${query.q ?? ''}`;
}

// solo la primera pagina: alcanza para pintar algo mientras MonsterASP despierta el app pool
export function readCachedPage(query: GridQuery): GridPage | null {
  const cached = readRaw();
  return cached && cached.queryKey === queryKey(query) ? cached.page : null;
}

export function writeCachedPage(query: GridQuery, page: GridPage): void {
  try {
    const payload: CachedPage = { queryKey: queryKey(query), page };
    localStorage.setItem(STORAGE_KEY, JSON.stringify(payload));
  } catch {
    // modo privado o cuota llena: el caché es una optimizacion, nunca un requisito
  }
}

export function clearCachedPage(): void {
  try {
    localStorage.removeItem(STORAGE_KEY);
  } catch {
    // idem
  }
}

function readRaw(): CachedPage | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as CachedPage) : null;
  } catch {
    return null;
  }
}
