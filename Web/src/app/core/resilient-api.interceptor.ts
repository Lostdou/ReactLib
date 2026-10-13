import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { retry, timer, timeout, throwError } from 'rxjs';

const COLD_START_BUDGET_MS = 27_000;
const RETRY_DELAYS_MS = [600, 2_500];
const RETRYABLE_STATUSES = new Set([0, 502, 503, 504]);

export const resilientApi: HttpInterceptorFn = (req, next) => {
  // los POST no se reintentan: sin idempotencia un envio duplicado crea dos filas en la cola
  const retries = req.method === 'GET' ? RETRY_DELAYS_MS.length : 0;

  return next(req).pipe(
    timeout({ each: COLD_START_BUDGET_MS }),
    retry({
      count: retries,
      delay: (error, attempt) =>
        isRetryable(error) ? timer(RETRY_DELAYS_MS[attempt - 1]) : throwError(() => error),
    }),
  );
};

function isRetryable(error: unknown): boolean {
  if (error instanceof HttpErrorResponse) return RETRYABLE_STATUSES.has(error.status);

  return error instanceof Error && error.name === 'TimeoutError';
}
