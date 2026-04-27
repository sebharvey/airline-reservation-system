import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withInMemoryScrolling } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { LUCIDE_ICONS, LucideIconProvider } from 'lucide-angular';
import {
  Accessibility, Armchair, ArrowLeft, ArrowLeftRight, ArrowRight, Ban, Banknote,
  Briefcase, Calendar, Check, ChevronLeft, ChevronRight, CircleAlert, CircleArrowUp,
  CircleCheck, CircleDollarSign, CircleX, ClipboardList, Clock, Copy, CreditCard,
  FileText, Inbox, Lock, LogOut, Luggage, Moon, Package, Plane, PlaneTakeoff, Play,
  Plus, Receipt, RotateCcw, Search, ShoppingBag, ShoppingCart, Star, Sun, Tag,
  Trash2, TriangleAlert, User, Users, X,
} from 'lucide-angular';

import { routes } from './app.routes';
import { loyaltyAuthInterceptor } from './services/loyalty-auth.interceptor';
import { retailAuthInterceptor } from './services/retail-auth.interceptor';
import { httpDebugInterceptor } from './services/http-debug.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withInMemoryScrolling({ scrollPositionRestoration: 'top' })),
    provideHttpClient(withInterceptors([httpDebugInterceptor, loyaltyAuthInterceptor, retailAuthInterceptor])),
    {
      provide: LUCIDE_ICONS,
      multi: true,
      useValue: new LucideIconProvider({
        Accessibility, Armchair, ArrowLeft, ArrowLeftRight, ArrowRight, Ban, Banknote,
        Briefcase, Calendar, Check, ChevronLeft, ChevronRight, CircleAlert, CircleArrowUp,
        CircleCheck, CircleDollarSign, CircleX, ClipboardList, Clock, Copy, CreditCard,
        FileText, Inbox, Lock, LogOut, Luggage, Moon, Package, Plane, PlaneTakeoff, Play,
        Plus, Receipt, RotateCcw, Search, ShoppingBag, ShoppingCart, Star, Sun, Tag,
        Trash2, TriangleAlert, User, Users, X,
      }),
    },
  ]
};
