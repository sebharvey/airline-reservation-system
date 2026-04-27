import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withInMemoryScrolling } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';

import { routes } from './app.routes';
import { loyaltyAuthInterceptor } from './services/loyalty-auth.interceptor';
import { retailAuthInterceptor } from './services/retail-auth.interceptor';
import { httpDebugInterceptor } from './services/http-debug.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withInMemoryScrolling({ scrollPositionRestoration: 'top' })),
    provideHttpClient(withInterceptors([httpDebugInterceptor, loyaltyAuthInterceptor, retailAuthInterceptor]))
  ]
};
