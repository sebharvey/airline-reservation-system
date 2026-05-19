import { inject } from '@angular/core';
import { HttpInterceptorFn, HttpResponse, HttpErrorResponse } from '@angular/common/http';
import { tap } from 'rxjs';
import { HttpDebugService } from '../services/http-debug.service';

// Keeps the token type (e.g. "Bearer") visible for debugging while hiding the credential.
function maskToken(value: string): string {
  const spaceIndex = value.indexOf(' ');
  if (spaceIndex === -1) return '***';
  return `${value.substring(0, spaceIndex)} ***`;
}

export const httpDebugInterceptor: HttpInterceptorFn = (req, next) => {
  const debugService = inject(HttpDebugService);
  const startTime = Date.now();

  const requestHeaders: Record<string, string> = {};
  req.headers.keys().forEach(key => {
    const value = req.headers.get(key) ?? '';
    requestHeaders[key] = key.toLowerCase() === 'authorization' ? maskToken(value) : value;
  });

  return next(req).pipe(
    tap({
      next: event => {
        if (event instanceof HttpResponse) {
          const responseHeaders: Record<string, string> = {};
          event.headers.keys().forEach(key => {
            const value = event.headers.get(key) ?? '';
            responseHeaders[key] = key.toLowerCase() === 'authorization' ? maskToken(value) : value;
          });
          debugService.log({
            timestamp: new Date().toISOString(),
            method: req.method,
            url: req.urlWithParams,
            requestHeaders,
            requestBody: req.body,
            responseStatus: event.status,
            responseHeaders,
            responseBody: event.body,
            durationMs: Date.now() - startTime,
            isError: false
          });
        }
      },
      error: (error: HttpErrorResponse) => {
        const responseHeaders: Record<string, string> = {};
        if (error.headers) {
          error.headers.keys().forEach(key => {
            const value = error.headers.get(key) ?? '';
            responseHeaders[key] = key.toLowerCase() === 'authorization' ? maskToken(value) : value;
          });
        }
        debugService.log({
          timestamp: new Date().toISOString(),
          method: req.method,
          url: req.urlWithParams,
          requestHeaders,
          requestBody: req.body,
          responseStatus: error.status,
          responseHeaders,
          responseBody: error.error,
          durationMs: Date.now() - startTime,
          isError: true
        });
      }
    })
  );
};
