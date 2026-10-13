import { Injectable, computed, signal } from '@angular/core';

export type Lang = 'es' | 'en' | 'pt';

export const LANGUAGES: { code: Lang; label: string }[] = [
  { code: 'es', label: 'ES' },
  { code: 'en', label: 'EN' },
  { code: 'pt', label: 'PT' },
];

export interface Dict {
  documentTitle: string;
  addVideo: string;
  madeBy: string;
  ariaLanguage: string;
  ariaSort: string;
  ariaRemoveTag: string;
  ariaVisits: string;

  searchPlaceholder: string;
  sortNew: string;
  sortTop: string;
  filteringBy: string;
  removeFilter: string;
  waking: string;
  stillWaking: string;
  loadError: string;
  loadMoreError: string;
  emptyState: string;
  seeAll: string;
  loadMore: string;
  loadingMore: string;

  copyLink: string;
  original: string;
  copied: string;
  copyFailed: string;

  back: string;
  loading: string;
  notFound: string;
  noPreview: string;
  seeOriginal: string;
  detailHint: (handle: string) => string;
  report: string;
  reported: string;
  reportThanks: string;
  reportFailed: string;

  submitTitle: string;
  submitLead: string;
  urlPlaceholder: string;
  findVideo: string;
  searching: string;
  chooseTags: string;
  tagsHint: string;
  tagPlaceholder: string;
  propose: string;
  publish: string;
  publishing: string;
  cancel: string;
  published: string;
  seeVideo: string;
  proposedPending: string;
  maybeDuplicate: string;
  addAnother: string;

  errorGeneric: string;
  errorRateLimited: string;
  errorInvalidUrl: string;
  errorAlreadyExists: string;
  errorNoVideo: string;
  errorProcessing: string;
}

const es: Dict = {
  documentTitle: 'ReactLib — videos de Twitter para pegar',
  addVideo: 'Sumar video',
  madeBy: 'Hecho por',
  ariaLanguage: 'Idioma',
  ariaSort: 'Orden',
  ariaRemoveTag: 'Quitar',
  ariaVisits: 'visitas',

  searchPlaceholder: 'Buscar: gato, llanto, me voy…',
  sortNew: 'Recientes',
  sortTop: 'Más usados',
  filteringBy: 'Filtrando por',
  removeFilter: 'quitar',
  waking: 'Despertando el servidor, va a tardar unos segundos…',
  stillWaking: 'Sigue despertando… ya falta poco.',
  loadError: 'No pudimos cargar la biblioteca.',
  loadMoreError: 'No pudimos traer más resultados.',
  emptyState: 'No hay nada con esa búsqueda todavía.',
  seeAll: 'Ver todo',
  loadMore: 'Cargar más',
  loadingMore: 'Cargando…',

  copyLink: 'Copiar link',
  original: 'Original',
  copied: 'Link copiado: pegalo en el texto de tu post',
  copyFailed: 'No se pudo copiar',

  back: 'Volver a la biblioteca',
  loading: 'Cargando…',
  notFound: 'Ese video no existe o lo dimos de baja.',
  noPreview: 'Sin preview disponible',
  seeOriginal: 'Ver original en X',
  detailHint: (handle) =>
    `Pegá el link en el texto de tu post en X y el video se adjunta solo, con la atribución a @${handle}. No hace falta subir nada ni citar.`,
  report: 'Reportar este video',
  reported: 'Reportado',
  reportThanks: 'Gracias: lo vamos a revisar',
  reportFailed: 'No se pudo enviar el reporte',

  submitTitle: 'Sumar un video',
  submitLead: 'Pegá el link de un post de X que tenga el video que querés agregar. Se publica al instante.',
  urlPlaceholder: 'https://x.com/usuario/status/123456789',
  findVideo: 'Buscar video',
  searching: 'Buscando…',
  chooseTags: 'Elegí hasta 5 tags',
  tagsHint:
    'Usá los que ya existen siempre que puedas: así la gente encuentra las cosas. Si proponés uno nuevo, el video se publica igual y el tag queda esperando revisión.',
  tagPlaceholder: 'Buscar o proponer un tag…',
  propose: 'proponer',
  publish: 'Publicar',
  publishing: 'Publicando…',
  cancel: 'Cancelar',
  published: '¡Listo, ya está en la biblioteca!',
  seeVideo: 'Ver el video',
  proposedPending: 'Estos tags todavía no existían y quedaron esperando revisión:',
  maybeDuplicate: 'Puede que sea parecido a',
  addAnother: 'Sumar otro',

  errorGeneric: 'Algo falló. Probá de nuevo.',
  errorRateLimited: 'Enviaste varios seguidos. Esperá un rato.',
  errorInvalidUrl: 'Ese link no parece un post de X.',
  errorAlreadyExists: 'Ese video ya está en la biblioteca.',
  errorNoVideo: 'No encontramos ningún video en ese post.',
  errorProcessing: 'No pudimos procesarlo. Probá de nuevo en un minuto.',
};

const en: Dict = {
  documentTitle: 'ReactLib — Twitter videos ready to paste',
  addVideo: 'Add video',
  madeBy: 'Made by',
  ariaLanguage: 'Language',
  ariaSort: 'Sort',
  ariaRemoveTag: 'Remove',
  ariaVisits: 'visits',

  searchPlaceholder: 'Search: cat, crying, walking away…',
  sortNew: 'Recent',
  sortTop: 'Most used',
  filteringBy: 'Filtering by',
  removeFilter: 'clear',
  waking: 'Waking up the server, this takes a few seconds…',
  stillWaking: 'Still waking up… almost there.',
  loadError: "We couldn't load the library.",
  loadMoreError: "We couldn't load more results.",
  emptyState: 'Nothing matches that search yet.',
  seeAll: 'See everything',
  loadMore: 'Load more',
  loadingMore: 'Loading…',

  copyLink: 'Copy link',
  original: 'Original',
  copied: 'Link copied: paste it into your post',
  copyFailed: "Couldn't copy",

  back: 'Back to the library',
  loading: 'Loading…',
  notFound: "That video doesn't exist or was taken down.",
  noPreview: 'No preview available',
  seeOriginal: 'See original on X',
  detailHint: (handle) =>
    `Paste the link into your post on X and the video attaches itself, credited to @${handle}. No uploading, no quoting.`,
  report: 'Report this video',
  reported: 'Reported',
  reportThanks: "Thanks: we'll take a look",
  reportFailed: "Couldn't send the report",

  submitTitle: 'Add a video',
  submitLead: 'Paste the link to an X post containing the video you want to add. It goes live instantly.',
  urlPlaceholder: 'https://x.com/username/status/123456789',
  findVideo: 'Find video',
  searching: 'Searching…',
  chooseTags: 'Pick up to 5 tags',
  tagsHint:
    'Use the existing ones whenever you can: that is how people find things. If you propose a new one, the video still goes live and the tag waits for review.',
  tagPlaceholder: 'Search or propose a tag…',
  propose: 'propose',
  publish: 'Publish',
  publishing: 'Publishing…',
  cancel: 'Cancel',
  published: "Done, it's in the library!",
  seeVideo: 'See the video',
  proposedPending: "These tags didn't exist yet and are waiting for review:",
  maybeDuplicate: 'This might be similar to',
  addAnother: 'Add another',

  errorGeneric: 'Something went wrong. Try again.',
  errorRateLimited: 'You sent several in a row. Give it a minute.',
  errorInvalidUrl: "That link doesn't look like an X post.",
  errorAlreadyExists: 'That video is already in the library.',
  errorNoVideo: "We couldn't find a video in that post.",
  errorProcessing: "We couldn't process it. Try again in a minute.",
};

const pt: Dict = {
  documentTitle: 'ReactLib — vídeos do Twitter prontos para colar',
  addVideo: 'Adicionar vídeo',
  madeBy: 'Feito por',
  ariaLanguage: 'Idioma',
  ariaSort: 'Ordem',
  ariaRemoveTag: 'Remover',
  ariaVisits: 'visitas',

  searchPlaceholder: 'Buscar: gato, choro, indo embora…',
  sortNew: 'Recentes',
  sortTop: 'Mais usados',
  filteringBy: 'Filtrando por',
  removeFilter: 'remover',
  waking: 'Acordando o servidor, vai levar alguns segundos…',
  stillWaking: 'Ainda acordando… já falta pouco.',
  loadError: 'Não conseguimos carregar a biblioteca.',
  loadMoreError: 'Não conseguimos trazer mais resultados.',
  emptyState: 'Nada encontrado para essa busca ainda.',
  seeAll: 'Ver tudo',
  loadMore: 'Carregar mais',
  loadingMore: 'Carregando…',

  copyLink: 'Copiar link',
  original: 'Original',
  copied: 'Link copiado: cole no texto do seu post',
  copyFailed: 'Não foi possível copiar',

  back: 'Voltar para a biblioteca',
  loading: 'Carregando…',
  notFound: 'Esse vídeo não existe ou foi retirado.',
  noPreview: 'Sem preview disponível',
  seeOriginal: 'Ver original no X',
  detailHint: (handle) =>
    `Cole o link no texto do seu post no X e o vídeo é anexado sozinho, com crédito para @${handle}. Não precisa subir nada nem citar.`,
  report: 'Denunciar este vídeo',
  reported: 'Denunciado',
  reportThanks: 'Obrigado: vamos revisar',
  reportFailed: 'Não foi possível enviar a denúncia',

  submitTitle: 'Adicionar um vídeo',
  submitLead: 'Cole o link de um post do X que tenha o vídeo que você quer adicionar. É publicado na hora.',
  urlPlaceholder: 'https://x.com/usuario/status/123456789',
  findVideo: 'Buscar vídeo',
  searching: 'Buscando…',
  chooseTags: 'Escolha até 5 tags',
  tagsHint:
    'Use as que já existem sempre que puder: é assim que as pessoas encontram as coisas. Se propuser uma nova, o vídeo é publicado do mesmo jeito e a tag fica aguardando revisão.',
  tagPlaceholder: 'Buscar ou propor uma tag…',
  propose: 'propor',
  publish: 'Publicar',
  publishing: 'Publicando…',
  cancel: 'Cancelar',
  published: 'Pronto, já está na biblioteca!',
  seeVideo: 'Ver o vídeo',
  proposedPending: 'Estas tags ainda não existiam e ficaram aguardando revisão:',
  maybeDuplicate: 'Pode ser parecido com',
  addAnother: 'Adicionar outro',

  errorGeneric: 'Algo deu errado. Tente de novo.',
  errorRateLimited: 'Você enviou vários seguidos. Espere um pouco.',
  errorInvalidUrl: 'Esse link não parece um post do X.',
  errorAlreadyExists: 'Esse vídeo já está na biblioteca.',
  errorNoVideo: 'Não encontramos nenhum vídeo nesse post.',
  errorProcessing: 'Não conseguimos processar. Tente de novo em um minuto.',
};

const DICTS: Record<Lang, Dict> = { es, en, pt };
const STORAGE_KEY = 'reactlib.lang';

@Injectable({ providedIn: 'root' })
export class I18n {
  readonly lang = signal<Lang>(detectLang());
  readonly t = computed(() => DICTS[this.lang()]);

  constructor() {
    this.apply(this.lang());
  }

  setLang(lang: Lang): void {
    this.lang.set(lang);
    this.apply(lang);

    try {
      localStorage.setItem(STORAGE_KEY, lang);
    } catch {
      // modo privado: el idioma vale solo para esta sesión
    }
  }

  private apply(lang: Lang): void {
    document.documentElement.lang = lang;
    document.title = DICTS[lang].documentTitle;
  }
}

function detectLang(): Lang {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored === 'es' || stored === 'en' || stored === 'pt') return stored;
  } catch {
    // seguimos con la detección del navegador
  }

  const preferred = navigator.languages?.[0] ?? navigator.language ?? 'es';
  if (preferred.startsWith('pt')) return 'pt';
  if (preferred.startsWith('es')) return 'es';

  return 'en';
}
