const COLUMNS = 9;
const ROWS = 8;
const MIN_DISTINCTIVE_BITS = 8;
const MAX_DISTINCTIVE_BITS = 56;

// se calcula acá y no en la API porque el plan de 256 MB no aguanta decodificar imágenes,
// y pbs.twimg.com manda Access-Control-Allow-Origin así que el canvas no queda tainted
export async function dhashFromPoster(posterUrl: string): Promise<string | null> {
  try {
    const image = await loadImage(posterUrl);
    const pixels = toGrayscaleGrid(image);
    const hash = difference(pixels);

    return isDistinctive(hash) ? hash.toString() : null;
  } catch {
    return null;
  }
}

function loadImage(url: string): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const image = new Image();
    image.crossOrigin = 'anonymous';
    image.referrerPolicy = 'no-referrer';
    image.onload = () => resolve(image);
    image.onerror = () => reject(new Error('no se pudo cargar el poster'));
    image.src = url;
  });
}

function toGrayscaleGrid(image: HTMLImageElement): number[] {
  const canvas = document.createElement('canvas');
  canvas.width = COLUMNS;
  canvas.height = ROWS;

  const context = canvas.getContext('2d', { willReadFrequently: true });
  if (!context) throw new Error('sin canvas 2d');

  context.drawImage(image, 0, 0, COLUMNS, ROWS);
  const { data } = context.getImageData(0, 0, COLUMNS, ROWS);

  const grid: number[] = [];
  for (let i = 0; i < data.length; i += 4) {
    grid.push(0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2]);
  }

  return grid;
}

// BigInt y no number: el hash son 64 bits y los bitwise de JS truncan a 32
function difference(pixels: number[]): bigint {
  let hash = 0n;
  let bit = 0n;

  for (let y = 0; y < ROWS; y++) {
    for (let x = 0; x < COLUMNS - 1; x++, bit++) {
      const left = pixels[y * COLUMNS + x];
      const right = pixels[y * COLUMNS + x + 1];
      if (left > right) hash |= 1n << bit;
    }
  }

  return BigInt.asIntN(64, hash);
}

// un frame plano (fade a negro, pantalla blanca) matchearía contra todo el catálogo
function isDistinctive(hash: bigint): boolean {
  let bits = 0;
  for (let value = BigInt.asUintN(64, hash); value > 0n; value >>= 1n) {
    if (value & 1n) bits++;
  }

  return bits >= MIN_DISTINCTIVE_BITS && bits <= MAX_DISTINCTIVE_BITS;
}
