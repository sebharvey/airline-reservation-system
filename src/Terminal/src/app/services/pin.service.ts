import { Injectable, signal } from '@angular/core';
import { FlightInventoryGroup } from './inventory.service';

const IDS_KEY  = 'pinnedFlightIds';
const DATA_KEY = 'pinnedFlightData';

@Injectable({ providedIn: 'root' })
export class PinService {
  pinnedIds     = signal<string[]>(this.#loadIds());
  pinnedFlights = signal<FlightInventoryGroup[]>(this.#loadData());

  #loadIds(): string[] {
    try { return JSON.parse(localStorage.getItem(IDS_KEY) ?? '[]'); }
    catch { return []; }
  }

  #loadData(): FlightInventoryGroup[] {
    try { return JSON.parse(sessionStorage.getItem(DATA_KEY) ?? '[]'); }
    catch { return []; }
  }

  isPinned(inventoryId: string): boolean {
    return this.pinnedIds().includes(inventoryId);
  }

  pin(flight: FlightInventoryGroup): void {
    if (this.isPinned(flight.inventoryId)) return;
    const ids     = [...this.pinnedIds(), flight.inventoryId];
    const flights = [...this.pinnedFlights(), flight];
    this.#persist(ids, flights);
  }

  unpin(inventoryId: string): void {
    const ids     = this.pinnedIds().filter(id => id !== inventoryId);
    const flights = this.pinnedFlights().filter(f => f.inventoryId !== inventoryId);
    this.#persist(ids, flights);
  }

  /** Merge fresh API data into pinned flights (called after a day-change API response). */
  mergePinnedData(apiPinned: FlightInventoryGroup[]): void {
    if (apiPinned.length === 0) return;
    const byId = new Map(apiPinned.map(f => [f.inventoryId, f]));
    const merged = this.pinnedFlights().map(f => byId.get(f.inventoryId) ?? f);
    // Add any pinned IDs that weren't in session yet (new session scenario)
    for (const f of apiPinned) {
      if (!merged.some(m => m.inventoryId === f.inventoryId)) merged.push(f);
    }
    this.pinnedFlights.set(merged);
    sessionStorage.setItem(DATA_KEY, JSON.stringify(merged));
  }

  #persist(ids: string[], flights: FlightInventoryGroup[]): void {
    this.pinnedIds.set(ids);
    this.pinnedFlights.set(flights);
    localStorage.setItem(IDS_KEY, JSON.stringify(ids));
    sessionStorage.setItem(DATA_KEY, JSON.stringify(flights));
  }
}
