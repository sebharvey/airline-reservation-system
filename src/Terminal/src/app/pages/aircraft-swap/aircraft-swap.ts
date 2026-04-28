import { LucideAngularModule } from 'lucide-angular';
import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { InventoryService, FlightInventoryGroup, InventoryHold, FlightSeatmap, AircraftType } from '../../services/inventory.service';

type SeatStatus = 'no-seat' | 'unchecked' | 'ok' | 'unavailable';

@Component({
  selector: 'app-aircraft-swap',
  templateUrl: './aircraft-swap.html',
  styleUrl: './aircraft-swap.css',
  imports: [RouterLink, LucideAngularModule],
})
export class AircraftSwapComponent implements OnInit {
  #inventoryService = inject(InventoryService);
  #route = inject(ActivatedRoute);

  flightNumber = signal('');
  departureDate = signal('');
  flight = signal<FlightInventoryGroup | null>(null);
  flightLoading = signal(false);

  holds = signal<InventoryHold[]>([]);
  holdsLoading = signal(false);
  holdsError = signal('');

  aircraftTypes = signal<AircraftType[]>([]);
  aircraftTypesLoading = signal(false);
  aircraftTypesError = signal('');

  selectedType = signal('');
  confirmedType = signal('');

  seatmap = signal<FlightSeatmap | null>(null);
  seatmapLoading = signal(false);
  seatmapError = signal('');
  seatCheckDone = signal(false);

  newAircraftSeatNumbers = computed(() => {
    const sm = this.seatmap();
    if (!sm) return new Set<string>();
    const nums = new Set<string>();
    for (const cabin of sm.cabins)
      for (const seat of cabin.seats)
        nums.add(seat.seatNumber);
    return nums;
  });

  seatsWithAssignment = computed(() =>
    this.holds().filter(h => !!h.seatNumber).length
  );

  seatsOkCount = computed(() => {
    if (!this.seatCheckDone()) return 0;
    const nums = this.newAircraftSeatNumbers();
    return this.holds().filter(h => h.seatNumber && nums.has(h.seatNumber)).length;
  });

  seatsUnavailableCount = computed(() => {
    if (!this.seatCheckDone()) return 0;
    const nums = this.newAircraftSeatNumbers();
    return this.holds().filter(h => h.seatNumber && !nums.has(h.seatNumber)).length;
  });

  seatStatus(hold: InventoryHold): SeatStatus {
    if (!hold.seatNumber) return 'no-seat';
    if (!this.seatCheckDone()) return 'unchecked';
    return this.newAircraftSeatNumbers().has(hold.seatNumber) ? 'ok' : 'unavailable';
  }

  async ngOnInit(): Promise<void> {
    const fn = this.#route.snapshot.paramMap.get('flightNumber') ?? '';
    const dd = this.#route.snapshot.paramMap.get('departureDate') ?? '';
    this.flightNumber.set(fn);
    this.departureDate.set(dd);

    // Use router state passed from inventory page if available
    const state = history.state as { flight?: FlightInventoryGroup };
    if (state?.flight?.flightNumber === fn && state.flight.departureDate === dd) {
      this.flight.set(state.flight);
      this.selectedType.set(state.flight.aircraftType);
    } else {
      await this.loadFlightInfo();
    }

    await Promise.all([this.loadHolds(), this.loadAircraftTypes()]);
  }

  async loadFlightInfo(): Promise<void> {
    this.flightLoading.set(true);
    try {
      const all = await this.#inventoryService.getFlightInventory(this.departureDate());
      const found = all.find(f => f.flightNumber === this.flightNumber()) ?? null;
      this.flight.set(found);
      if (found) this.selectedType.set(found.aircraftType);
    } finally {
      this.flightLoading.set(false);
    }
  }

  async loadHolds(): Promise<void> {
    const f = this.flight();
    if (!f) {
      this.holdsError.set('Flight information could not be loaded.');
      return;
    }
    this.holdsLoading.set(true);
    this.holdsError.set('');
    try {
      const result = await this.#inventoryService.getInventoryHolds(f.inventoryId);
      this.holds.set(result.filter(h => h.holdType === 'Revenue'));
    } catch {
      this.holdsError.set('Failed to load passenger holds. Please try again.');
    } finally {
      this.holdsLoading.set(false);
    }
  }

  async loadAircraftTypes(): Promise<void> {
    this.aircraftTypesLoading.set(true);
    this.aircraftTypesError.set('');
    try {
      const types = await this.#inventoryService.getAircraftTypes();
      const currentType = this.flight()?.aircraftType;
      this.aircraftTypes.set(types.filter(t => t.isActive || t.aircraftTypeCode === currentType));
    } catch {
      this.aircraftTypesError.set('Failed to load aircraft types. Please try again.');
    } finally {
      this.aircraftTypesLoading.set(false);
    }
  }

  async confirmAircraftChange(): Promise<void> {
    const f = this.flight();
    if (!f) return;
    const newType = this.selectedType();
    this.seatmapLoading.set(true);
    this.seatmapError.set('');
    try {
      const [sm] = await Promise.all([
        this.#inventoryService.getFlightSeatmap(f.inventoryId, f.flightNumber, newType),
        this.#inventoryService.changeAircraftType(f.flightNumber, f.departureDate, newType),
      ]);
      this.seatmap.set(sm);
      this.confirmedType.set(newType);
      this.seatCheckDone.set(true);
      this.flight.update(fi => fi ? { ...fi, aircraftType: newType } : fi);
    } catch {
      this.seatmapError.set('Failed to confirm aircraft change. Please try again.');
    } finally {
      this.seatmapLoading.set(false);
    }
  }
}
