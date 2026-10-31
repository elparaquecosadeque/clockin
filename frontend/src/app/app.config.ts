import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { MatIconRegistry } from '@angular/material/icon';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { correlationIdInterceptor } from './core/interceptors/correlation-id.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';
import { SessionService } from './core/services/session.service';
import { TIME_FORMAT } from './shared/models/time-format.model';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    // The error interceptor is outermost, so the auth interceptor sees a raw 401 and can refresh.
    provideHttpClient(
      withInterceptors([errorInterceptor, correlationIdInterceptor, authInterceptor]),
    ),
    {
      provide: TIME_FORMAT,
      useFactory: () => {
        const session = inject(SessionService);
        return () => session.user()?.timeFormat ?? '24h';
      },
    },
    provideAppInitializer(() => {
      // index.html loads Material Symbols; icons use `fontIcon`, so no ligature text reaches the DOM.
      inject(MatIconRegistry).setDefaultFontSetClass(
        'material-symbols-outlined',
        'mat-ligature-font',
      );
      return inject(SessionService).restore();
    }),
  ],
};
