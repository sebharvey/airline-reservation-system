import { Injectable, signal } from '@angular/core';
import { FlightInventoryGroup } from './inventory.service';

const SESSION_KEY = 'apex_flight_pins';

@Injectable({ providedIn: 'root' })
export class FlightPinService {
  readonly #pins = signal<FlightInventoryGroup[]>(this.#load());
  readonly pins = this.#pins.asReadonly();

  toggle(flight: FlightInventoryGroup): void {
    const current = this.#pins();
    const exists = current.some(p => p.inventoryId === flight.inventoryId);
    const next = exists
      ? current.filter(p => p.inventoryId !== flight.inventoryId)
      : [...current, flight];
    this.#pins.set(next);
    sessionStorage.setItem(SESSION_KEY, JSON.stringify(next));
  }

  #load(): FlightInventoryGroup[] {
    try {
      const raw = sessionStorage.getItem(SESSION_KEY);
      return raw ? (JSON.parse(raw) as FlightInventoryGroup[]) : [];
    } catch {
      return [];
    }
  }
}
