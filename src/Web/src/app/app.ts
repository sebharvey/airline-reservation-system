import { Component, inject } from '@angular/core';
import { Router, NavigationEnd, RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map, startWith } from 'rxjs';
import { ThemeService } from './services/theme.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  theme = inject(ThemeService);

  #router = inject(Router);

  isBoardingPass = toSignal(
    this.#router.events.pipe(
      filter(e => e instanceof NavigationEnd),
      map(() => this.#router.url.startsWith('/check-in/boarding-pass')),
      startWith(this.#router.url.startsWith('/check-in/boarding-pass'))
    ),
    { initialValue: false }
  );
}
