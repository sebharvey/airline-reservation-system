import { LucideAngularModule } from 'lucide-angular';
import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { InventoryService, FlightInventoryGroup, ManifestEntry, FlightSeatmap, AircraftType } from '../../services/inventory.service';

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

  passengers = signal<ManifestEntry[]>([]);
  passengersLoading = signal(false);
  passengersError = signal('');

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
    this.passengers().filter(p => !!p.seatNumber).length
  );

  seatsOkCount = computed(() => {
    if (!this.seatCheckDone()) return 0;
    const nums = this.newAircraftSeatNumbers();
    return this.passengers().filter(p => p.seatNumber && nums.has(p.seatNumber)).length;
  });

  seatsUnavailableCount = computed(() => {
    if (!this.seatCheckDone()) return 0;
    const nums = this.newAircraftSeatNumbers();
    return this.passengers().filter(p => p.seatNumber && !nums.has(p.seatNumber)).length;
  });

  seatStatus(passenger: ManifestEntry): SeatStatus {
    if (!passenger.seatNumber) return 'no-seat';
    if (!this.seatCheckDone()) return 'unchecked';
    return this.newAircraftSeatNumbers().has(passenger.seatNumber) ? 'ok' : 'unavailable';
  }

  async ngOnInit(): Promise<void> {
    const fn = this.#route.snapshot.paramMap.get('flightNumber') ?? '';
    const dd = this.#route.snapshot.paramMap.get('departureDate') ?? '';
    this.flightNumber.set(fn);
    this.departureDate.set(dd);

    const state = history.state as { flight?: FlightInventoryGroup };
    if (state?.flight?.flightNumber === fn && state.flight.departureDate === dd) {
      this.flight.set(state.flight);
      this.selectedType.set(state.flight.aircraftType);
    } else {
      await this.loadFlightInfo();
    }

    await Promise.all([this.loadPassengers(), this.loadAircraftTypes()]);
  }

  async loadFlightInfo(): Promise<void> {
    this.flightLoading.set(true);
    try {
      const all = await this.#inventoryService.getFlightInventory(this.departureDate());
      const found = [...all.flights, ...all.pinnedFlights].find(f => f.flightNumber === this.flightNumber()) ?? null;
      this.flight.set(found);
      if (found) this.selectedType.set(found.aircraftType);
    } finally {
      this.flightLoading.set(false);
    }
  }

  async loadPassengers(): Promise<void> {
    const f = this.flight();
    if (!f) {
      this.passengersError.set('Flight information could not be loaded.');
      return;
    }
    this.passengersLoading.set(true);
    this.passengersError.set('');
    try {
      const manifest = await this.#inventoryService.getFlightManifest(f.flightNumber, f.departureDate);
      this.passengers.set(manifest?.entries ?? []);
    } catch {
      this.passengersError.set('Failed to load passenger manifest. Please try again.');
    } finally {
      this.passengersLoading.set(false);
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
