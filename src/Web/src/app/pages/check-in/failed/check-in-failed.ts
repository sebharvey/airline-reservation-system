import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LucideAngularModule } from 'lucide-angular';

@Component({
  selector: 'app-check-in-failed',
  standalone: true,
  imports: [RouterLink, LucideAngularModule],
  templateUrl: './check-in-failed.html',
  styleUrl: './check-in-failed.css'
})
export class CheckInFailedComponent {}
