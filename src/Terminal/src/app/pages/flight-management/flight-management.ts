import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-flight-management',
  imports: [RouterOutlet],
  template: `<router-outlet />`,
})
export class FlightManagementComponent {}
