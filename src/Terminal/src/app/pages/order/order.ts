import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-order',
  imports: [RouterOutlet],
  template: `<router-outlet />`,
})
export class OrderComponent {}
