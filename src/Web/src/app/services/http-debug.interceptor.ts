import { inject } from '@angular/core';
import { HttpInterceptorFn, HttpResponse, HttpErrorResponse } from '@angular/common/http';
import { tap } from 'rxjs';
import { HttpDebugService } from './http-debug.service';

export const httpDebugInterceptor: HttpInterceptorFn = (req, next) => {
  const debugService = inject(HttpDebugService);
  const startTime = Date.now();

  const requestHeaders: Record<string, string> = {};
  req.headers.keys().forEach(key => {
    requestHeaders[key] = req.headers.get(key) ?? '';
  });

  return next(req).pipe(
    tap({
      next: event => {
        if (event instanceof HttpResponse) {
          const responseHeaders: Record<string, string> = {};
          event.headers.keys().forEach(key => {
            responseHeaders[key] = event.headers.get(key) ?? '';
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
            responseHeaders[key] = error.headers.get(key) ?? '';
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
