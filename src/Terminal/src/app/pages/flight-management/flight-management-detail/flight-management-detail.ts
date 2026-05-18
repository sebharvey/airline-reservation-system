import { LucideAngularModule } from 'lucide-angular';
import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { UpperCasePipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import {
  InventoryService,
  FlightInventoryGroup,
  FlightManifest,
  FlightSeatmap,
  ManifestEntry,
  SeatmapSeat,
  AutoAssignSeatsResponse,
  AircraftType,
} from '../../../services/inventory.service';

interface SeatCell {
  seat: SeatmapSeat | null;
  aisleBefore: boolean;
}

interface CabinGrid {
  cabinCode: string;
  cabinName: string;
  headers: { label: string; aisleBefore: boolean }[];
  rows: { rowNumber: number; cells: SeatCell[] }[];
}

interface EntryGroup {
  bookingReference: string;
  bookingType: string;
  entries: ManifestEntry[];
}

@Component({
  selector: 'app-flight-management-detail',
  standalone: true,
  imports: [LucideAngularModule, FormsModule, UpperCasePipe],
  templateUrl: './flight-management-detail.html',
  styleUrl: './flight-management-detail.css',
})
export class FlightManagementDetailComponent implements OnInit {
  #route = inject(ActivatedRoute);
  #router = inject(Router);
  #inventoryService = inject(InventoryService);

  flight = signal<FlightInventoryGroup | null>(null);
  manifest = signal<FlightManifest | null>(null);
  seatmap = signal<FlightSeatmap | null>(null);
  loading = signal(true);
  error = signal('');
  selectedEntry = signal<ManifestEntry | null>(null);
  paxFilter = signal<'all' | 'confirmed' | 'standby'>('all');

  copiedRef = signal<string | null>(null);

  // Seat action state
  pendingSeat = signal<SeatmapSeat | null>(null);
  seatOpLoading = signal(false);
  seatOpError = signal('');
  cabinMismatchModalOpen = signal(false);

  // Computed helpers
  inSeatSelectMode = computed(() => this.selectedEntry() !== null);

  canReleaseSeat = computed(() => {
    const entry = this.selectedEntry();
    return !!entry?.seatNumber && !this.seatOpLoading();
  });

  canConfirmSeat = computed(() =>
    this.pendingSeat() !== null && !this.seatOpLoading()
  );

  manifestStats = computed(() => {
    const m = this.manifest();
    if (!m) return null;
    const standby = m.entries.filter(e => e.bookingType === 'Standby').length;
    return {
      total: m.entries.length,
      checkedIn: m.entries.filter(e => e.checkedIn).length,
      confirmed: m.entries.length - standby,
      standby,
    };
  });

  cabinStats = computed(() => {
    const f = this.flight();
    if (!f) return [];
    const codes = ['f', 'j', 'w', 'y'] as const;
    return codes
      .filter(k => f[k] !== null)
      .map(k => ({ code: k.toUpperCase(), ...f[k]! }));
  });

  groupedEntries = computed<EntryGroup[]>(() => {
    const filter = this.paxFilter();
    const allEntries = this.manifest()?.entries ?? [];
    const entries = filter === 'standby'
      ? allEntries.filter(e => e.bookingType === 'Standby')
      : filter === 'confirmed'
      ? allEntries.filter(e => e.bookingType !== 'Standby')
      : allEntries;
    const sorted = [...entries].sort((a, b) => {
      const refCmp = a.bookingReference.localeCompare(b.bookingReference);
      if (refCmp !== 0) return refCmp;
      const surnameCmp = a.surname.localeCompare(b.surname);
      if (surnameCmp !== 0) return surnameCmp;
      return a.givenName.localeCompare(b.givenName);
    });
    const groups: EntryGroup[] = [];
    for (const entry of sorted) {
      const last = groups[groups.length - 1];
      if (last && last.bookingReference === entry.bookingReference) {
        last.entries.push(entry);
      } else {
        groups.push({
          bookingReference: entry.bookingReference,
          bookingType: entry.bookingType,
          entries: [entry],
        });
      }
    }
    return groups;
  });

  cabinGrids = computed<CabinGrid[]>(() => {
    const sm = this.seatmap();
    if (!sm) return [];

    return sm.cabins.map(cabin => {
      const groups = cabin.layout.split('-').map(Number).filter(n => n > 0);
      const boundaries = new Set<number>();
      let pos = 0;
      for (let g = 0; g < groups.length - 1; g++) {
        pos += groups[g];
        boundaries.add(pos);
      }

      const headers = cabin.columns.map((label, idx) => ({
        label,
        aisleBefore: idx > 0 && boundaries.has(idx),
      }));

      const byRow = new Map<number, SeatmapSeat[]>();
      for (const seat of cabin.seats) {
        if (!byRow.has(seat.rowNumber)) byRow.set(seat.rowNumber, []);
        byRow.get(seat.rowNumber)!.push(seat);
      }

      const rows = [...byRow.entries()]
        .sort((a, b) => a[0] - b[0])
        .map(([rowNumber, seats]) => {
          const byCols = new Map(seats.map(s => [s.column, s]));
          const cells: SeatCell[] = cabin.columns.map((col, idx) => ({
            seat: byCols.get(col) ?? null,
            aisleBefore: idx > 0 && boundaries.has(idx),
          }));
          return { rowNumber, cells };
        });

      return { cabinCode: cabin.cabinCode, cabinName: cabin.cabinName, headers, rows };
    });
  });

  // Auto-assign seats state
  autoAssignLoading = signal(false);
  autoAssignError   = signal('');
  autoAssignResult  = signal<AutoAssignSeatsResponse | null>(null);
  autoAssignModalOpen = signal(false);

  unassignedConfirmedCount = computed(() => {
    const m = this.manifest();
    if (!m) return 0;
    return m.entries.filter(
      e => !e.seatNumber && e.bookingType !== 'Standby'
    ).length;
  });

  // Baggage details modal state
  baggageModalEntry = signal<ManifestEntry | null>(null);

  openBaggageModal(entry: ManifestEntry, event: Event): void {
    event.stopPropagation();
    this.baggageModalEntry.set(entry);
  }

  closeBaggageModal(): void {
    this.baggageModalEntry.set(null);
  }

  baggageTotalKg(entry: ManifestEntry): number | null {
    if (entry.baggage.every(b => b.weightKg === null)) return null;
    return entry.baggage.reduce((sum, b) => sum + (b.weightKg ?? 0), 0);
  }

  // Disruption modal state
  disruptionModalOpen = signal(false);
  cancelConfirmMode = signal(false);
  cancelLoading = signal(false);
  cancelError = signal('');

  openDisruptionModal(): void {
    this.cancelConfirmMode.set(false);
    this.cancelError.set('');
    this.disruptionModalOpen.set(true);
  }

  closeDisruptionModal(): void {
    this.disruptionModalOpen.set(false);
    this.cancelConfirmMode.set(false);
    this.cancelError.set('');
  }

  // Departure time change modal state
  depChangeModalOpen = signal(false);
  newLocalDep = signal('');
  newLocalArr = signal('');
  newLocalArrOffset = signal(0);
  newUtcDep = signal('');
  newUtcArr = signal('');
  newUtcArrOffset = signal(0);
  localUtcOffsetMins = signal<number | null>(null);

  utcOffsetLabel = computed(() => {
    const offset = this.localUtcOffsetMins();
    if (offset === null) return null;
    const abs = Math.abs(offset);
    const h = Math.floor(abs / 60);
    const m = abs % 60;
    const sign = offset >= 0 ? '+' : '−';
    return m > 0
      ? `UTC${sign}${h}:${String(m).padStart(2, '0')}`
      : `UTC${sign}${h}`;
  });

  calculatedFlightTime = computed(() => {
    const dep = this.newUtcDep();
    const arr = this.newUtcArr();
    const arrOffset = this.newUtcArrOffset();
    if (!dep || !arr || !/^\d{2}:\d{2}$/.test(dep) || !/^\d{2}:\d{2}$/.test(arr)) return null;
    const [dh, dm] = dep.split(':').map(Number);
    const [ah, am] = arr.split(':').map(Number);
    const diff = (ah * 60 + am) - (dh * 60 + dm) + arrOffset * 24 * 60;
    if (diff <= 0) return null;
    const h = Math.floor(diff / 60);
    const m = diff % 60;
    return m > 0 ? `${h}h ${m}m` : `${h}h`;
  });

  openDepChangeModal(): void {
    this.closeDisruptionModal();
    const f = this.flight();
    if (!f) return;
    this.newLocalDep.set(f.departureTime);
    const initialArr = this.#calcArrival(f.departureTime);
    this.newLocalArr.set(initialArr.time);
    this.newLocalArrOffset.set(initialArr.dayOffset);
    this.newUtcDep.set('');
    this.newUtcArr.set('');
    this.newUtcArrOffset.set(0);
    this.localUtcOffsetMins.set(null);
    this.depChangeModalOpen.set(true);
  }

  closeDepChangeModal(): void {
    this.depChangeModalOpen.set(false);
  }

  onNewLocalDepChange(value: string): void {
    this.newLocalDep.set(value);
    if (!/^\d{2}:\d{2}$/.test(value)) return;
    const localArr = this.#calcArrival(value);
    this.newLocalArr.set(localArr.time);
    this.newLocalArrOffset.set(localArr.dayOffset);
    const offset = this.localUtcOffsetMins();
    if (offset !== null) {
      const utcDep = this.#toUtc(value, offset);
      const utcArr = this.#calcArrival(utcDep);
      this.newUtcDep.set(utcDep);
      this.newUtcArr.set(utcArr.time);
      this.newUtcArrOffset.set(utcArr.dayOffset);
    }
  }

  onNewLocalArrChange(value: string): void {
    this.newLocalArr.set(value);
    const offset = this.localUtcOffsetMins();
    if (offset !== null && /^\d{2}:\d{2}$/.test(value)) {
      const utcArr = this.#toUtc(value, offset);
      const utcDep = this.newUtcDep();
      this.newUtcArr.set(utcArr);
      if (utcDep && /^\d{2}:\d{2}$/.test(utcDep)) {
        this.newUtcArrOffset.set(this.#calcArrival(utcDep).dayOffset);
      }
    }
  }

  onNewUtcDepChange(value: string): void {
    this.newUtcDep.set(value);
    if (!/^\d{2}:\d{2}$/.test(value)) return;
    const localDep = this.newLocalDep();
    if (localDep && /^\d{2}:\d{2}$/.test(localDep)) {
      this.localUtcOffsetMins.set(this.#deriveOffset(localDep, value));
    }
    const utcArr = this.#calcArrival(value);
    this.newUtcArr.set(utcArr.time);
    this.newUtcArrOffset.set(utcArr.dayOffset);
  }

  onNewUtcArrChange(value: string): void {
    this.newUtcArr.set(value);
  }

  #deriveOffset(localTime: string, utcTime: string): number {
    const [lh, lm] = localTime.split(':').map(Number);
    const [uh, um] = utcTime.split(':').map(Number);
    let offset = (lh * 60 + lm) - (uh * 60 + um);
    // Normalise to the range [-12h, +14h] (covers all real-world UTC offsets)
    while (offset > 14 * 60) offset -= 24 * 60;
    while (offset < -12 * 60) offset += 24 * 60;
    return offset;
  }

  #toUtc(localTime: string, offsetMins: number): string {
    const [h, m] = localTime.split(':').map(Number);
    const utcTotal = (((h * 60 + m) - offsetMins) % (24 * 60) + 24 * 60) % (24 * 60);
    return `${String(Math.floor(utcTotal / 60)).padStart(2, '0')}:${String(utcTotal % 60).padStart(2, '0')}`;
  }

  depChangeDurationLabel = computed(() => {
    const mins = this.#flightDurationMins();
    const h = Math.floor(mins / 60);
    const m = mins % 60;
    return m > 0 ? `${h}h ${m}m` : `${h}h`;
  });

  #flightDurationMins(): number {
    const f = this.flight();
    if (!f) return 0;
    const [dh, dm] = f.departureTime.split(':').map(Number);
    const [ah, am] = f.arrivalTime.split(':').map(Number);
    return (ah * 60 + am) - (dh * 60 + dm) + f.arrivalDayOffset * 24 * 60;
  }

  #calcArrival(depTime: string): { time: string; dayOffset: number } {
    const [h, m] = depTime.split(':').map(Number);
    const total = h * 60 + m + this.#flightDurationMins();
    const dayOffset = Math.floor(total / (24 * 60));
    const remaining = total % (24 * 60);
    const rh = Math.floor(remaining / 60);
    const rm = remaining % 60;
    return {
      time: `${String(rh).padStart(2, '0')}:${String(rm).padStart(2, '0')}`,
      dayOffset,
    };
  }

  async cancelFlight(): Promise<void> {
    if (this.cancelLoading()) return;
    this.cancelLoading.set(true);
    this.cancelError.set('');
    try {
      await this.#inventoryService.cancelFlightInventoryOnly(this.#flightNumber, this.#departureDate);
      this.disruptionModalOpen.set(false);
      void this.#router.navigate(['/flight-management/disruption', this.#flightNumber, this.#departureDate]);
    } catch {
      this.cancelError.set('Failed to cancel flight. Please try again.');
    } finally {
      this.cancelLoading.set(false);
    }
  }

  goToIrops(): void {
    this.closeDisruptionModal();
    void this.#router.navigate(['/flight-management/disruption', this.#flightNumber, this.#departureDate]);
  }

  equipmentChangeClick(): void {
    this.closeDisruptionModal();
    void this.#router.navigate(
      ['/aircraft-swap', this.#flightNumber, this.#departureDate],
      { state: { flight: this.flight() } }
    );
  }

  // Aircraft swap modal state
  acSwapModalOpen = signal(false);
  acSwapTypes = signal<AircraftType[]>([]);
  acSwapTypesLoading = signal(false);
  acSwapTypesError = signal('');
  acSwapSelectedType = signal('');
  acSwapLoading = signal(false);
  acSwapError = signal('');
  acSwapRegInput = signal('');
  acSwapSeatsProcessing = signal(false);

  startAircraftSwap(): void {
    this.closeDisruptionModal();
    this.acSwapSelectedType.set('');
    this.acSwapRegInput.set('');
    this.acSwapError.set('');
    this.acSwapTypesError.set('');
    this.acSwapModalOpen.set(true);
    void this.#loadAircraftTypes();
  }

  closeAcSwapModal(): void {
    if (this.acSwapLoading()) return;
    this.acSwapModalOpen.set(false);
  }

  async #loadAircraftTypes(): Promise<void> {
    this.acSwapTypesLoading.set(true);
    this.acSwapTypesError.set('');
    try {
      const types = await this.#inventoryService.getAircraftTypes();
      this.acSwapTypes.set(types.filter(t => t.isActive));
    } catch {
      this.acSwapTypesError.set('Failed to load aircraft types. Please try again.');
    } finally {
      this.acSwapTypesLoading.set(false);
    }
  }

  async confirmAircraftSwap(): Promise<void> {
    const newType = this.acSwapSelectedType();
    if (!newType || this.acSwapLoading()) return;

    const newReg = this.acSwapRegInput().trim() || null;

    this.acSwapLoading.set(true);
    this.acSwapError.set('');
    try {
      const [newSeatmap] = await Promise.all([
        this.#inventoryService.getFlightSeatmap(this.#inventoryId, this.#flightNumber, newType),
        this.#inventoryService.changeAircraftType(this.#flightNumber, this.#departureDate, newType, newReg),
      ]);

      // Update the flight type, registration, and seatmap immediately, close modal, clear seat selection
      this.#aircraftType = newType;
      this.flight.update(f => f ? { ...f, aircraftType: newType, aircraftRegistration: newReg } : f);
      this.seatmap.set(newSeatmap);
      this.selectedEntry.set(null);
      this.pendingSeat.set(null);
      this.seatOpError.set('');
      this.acSwapModalOpen.set(false);

      // Build the set of valid seat numbers on the new aircraft
      const validSeats = new Set<string>(
        newSeatmap.cabins.flatMap(c => c.seats.map(s => s.seatNumber))
      );

      // Release seats that do not exist on the new aircraft
      const entriesToRelease = (this.manifest()?.entries ?? []).filter(
        e => e.seatNumber && !validSeats.has(e.seatNumber)
      );

      if (entriesToRelease.length > 0) {
        this.acSwapSeatsProcessing.set(true);
        try {
          await Promise.allSettled(
            entriesToRelease.map(e => this.#inventoryService.releaseSeat(
              e.eTicketNumber,
              e.bookingReference,
              e.passengerId,
              this.#inventoryId,
              e.orderId,
              e.cabinCode,
            ))
          );
          await this.#silentRefresh();
        } finally {
          this.acSwapSeatsProcessing.set(false);
        }
      }
    } catch {
      this.acSwapError.set('Failed to change aircraft type. Please try again.');
    } finally {
      this.acSwapLoading.set(false);
    }
  }

  // Operational data modal state
  gateModalOpen = signal(false);
  gateInput = signal('');
  gateOpLoading = signal(false);
  gateOpError = signal('');

  regModalOpen = signal(false);
  regInput = signal('');
  regOpLoading = signal(false);
  regOpError = signal('');

  // Params cached for seat operations
  #inventoryId = '';
  #flightNumber = '';
  #departureDate = '';
  #aircraftType = '';

  async ngOnInit(): Promise<void> {
    this.#inventoryId   = this.#route.snapshot.paramMap.get('inventoryId') ?? '';
    this.#flightNumber  = this.#route.snapshot.queryParamMap.get('fn') ?? '';
    this.#departureDate = this.#route.snapshot.queryParamMap.get('date') ?? '';
    this.#aircraftType  = this.#route.snapshot.queryParamMap.get('ac') ?? '';

    const stateFlight = history.state?.flight as FlightInventoryGroup | undefined;
    if (stateFlight) this.flight.set(stateFlight);

    if (stateFlight?.status === 'Cancelled') {
      void this.#router.navigate(['/flight-management/disruption', this.#flightNumber, this.#departureDate], { replaceUrl: true });
      return;
    }

    await this.#loadData();
  }

  async #loadData(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const [manifest, seatmap, inventoryOrders] = await Promise.all([
        this.#inventoryService.getFlightManifest(this.#flightNumber, this.#departureDate),
        this.#inventoryService.getFlightSeatmap(this.#inventoryId, this.#flightNumber, this.#aircraftType),
        this.#inventoryService.getInventoryOrders(this.#inventoryId),
      ]);
      this.manifest.set(manifest);
      this.seatmap.set(seatmap);
      if (inventoryOrders.cabins) {
        const cabins = inventoryOrders.cabins;
        this.flight.update(f => f ? { ...f, f: cabins.f, j: cabins.j, w: cabins.w, y: cabins.y } : f);
      }
    } catch {
      this.error.set('Failed to load passengers or seatmap. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  // Refreshes manifest + seatmap + cabin counts without hiding the UI (used after seat ops)
  async #silentRefresh(): Promise<void> {
    try {
      const [manifest, seatmap, inventoryOrders] = await Promise.all([
        this.#inventoryService.getFlightManifest(this.#flightNumber, this.#departureDate),
        this.#inventoryService.getFlightSeatmap(this.#inventoryId, this.#flightNumber, this.#aircraftType),
        this.#inventoryService.getInventoryOrders(this.#inventoryId),
      ]);
      this.manifest.set(manifest);
      this.seatmap.set(seatmap);
      if (inventoryOrders.cabins) {
        const cabins = inventoryOrders.cabins;
        this.flight.update(f => f ? { ...f, f: cabins.f, j: cabins.j, w: cabins.w, y: cabins.y } : f);
      }
    } catch {
      this.seatOpError.set('Data refresh failed — displayed data may be stale.');
    }
  }

  async refresh(): Promise<void> {
    this.selectedEntry.set(null);
    this.pendingSeat.set(null);
    this.seatOpError.set('');
    await this.#loadData();
  }

  goBack(): void {
    this.#router.navigate(['/flight-management']);
  }

  openGateModal(): void {
    this.gateInput.set(this.flight()?.departureGate ?? '');
    this.gateOpError.set('');
    this.gateModalOpen.set(true);
  }

  closeGateModal(): void {
    this.gateModalOpen.set(false);
    this.gateOpError.set('');
  }

  async saveGate(): Promise<void> {
    if (this.gateOpLoading()) return;
    this.gateOpLoading.set(true);
    this.gateOpError.set('');
    try {
      const gate = this.gateInput().trim() || null;
      await this.#inventoryService.setInventoryOperationalData(
        this.#inventoryId, gate, this.flight()?.aircraftRegistration ?? null);
      this.flight.update(f => f ? { ...f, departureGate: gate } : f);
      this.gateModalOpen.set(false);
    } catch {
      this.gateOpError.set('Failed to save gate. Please try again.');
    } finally {
      this.gateOpLoading.set(false);
    }
  }

  openRegModal(): void {
    this.regInput.set(this.flight()?.aircraftRegistration ?? '');
    this.regOpError.set('');
    this.regModalOpen.set(true);
  }

  closeRegModal(): void {
    this.regModalOpen.set(false);
    this.regOpError.set('');
  }

  async saveReg(): Promise<void> {
    if (this.regOpLoading()) return;
    this.regOpLoading.set(true);
    this.regOpError.set('');
    try {
      const reg = this.regInput().trim() || null;
      await this.#inventoryService.setInventoryOperationalData(
        this.#inventoryId, this.flight()?.departureGate ?? null, reg);
      this.flight.update(f => f ? { ...f, aircraftRegistration: reg } : f);
      this.regModalOpen.set(false);
    } catch {
      this.regOpError.set('Failed to save registration. Please try again.');
    } finally {
      this.regOpLoading.set(false);
    }
  }

  selectEntry(entry: ManifestEntry): void {
    const isSame = this.selectedEntry()?.eTicketNumber === entry.eTicketNumber;
    this.selectedEntry.set(isSame ? null : entry);
    this.pendingSeat.set(null);
    this.seatOpError.set('');
  }

  selectSeatFromMap(seat: SeatmapSeat): void {
    if (!this.inSeatSelectMode()) return;
    if (seat.availability !== 'available') return;
    this.pendingSeat.set(
      this.pendingSeat()?.seatNumber === seat.seatNumber ? null : seat
    );
    this.seatOpError.set('');
  }

  async releaseSeat(): Promise<void> {
    const entry = this.selectedEntry();
    if (!entry?.seatNumber || this.seatOpLoading()) return;

    this.seatOpLoading.set(true);
    this.seatOpError.set('');
    try {
      await this.#inventoryService.releaseSeat(
        entry.eTicketNumber,
        entry.bookingReference,
        entry.passengerId,
        this.#inventoryId,
        entry.orderId,
        entry.cabinCode,
      );
      await this.#silentRefresh();
      const updated = this.manifest()?.entries.find(e => e.eTicketNumber === entry.eTicketNumber);
      this.selectedEntry.set(updated ?? null);
      this.pendingSeat.set(null);
    } catch {
      this.seatOpError.set('Failed to release seat. Please try again.');
    } finally {
      this.seatOpLoading.set(false);
    }
  }

  async confirmSeat(): Promise<void> {
    const entry = this.selectedEntry();
    const seat  = this.pendingSeat();
    if (!entry || !seat || this.seatOpLoading()) return;

    if (seat.cabinCode !== entry.cabinCode) {
      this.cabinMismatchModalOpen.set(true);
      return;
    }

    await this.#doAssignSeat();
  }

  confirmCabinMismatch(): void {
    this.cabinMismatchModalOpen.set(false);
    void this.#doAssignSeat();
  }

  cancelCabinMismatch(): void {
    this.cabinMismatchModalOpen.set(false);
  }

  async #doAssignSeat(): Promise<void> {
    const entry = this.selectedEntry();
    const seat  = this.pendingSeat();
    if (!entry || !seat || this.seatOpLoading()) return;

    this.seatOpLoading.set(true);
    this.seatOpError.set('');
    try {
      await this.#inventoryService.assignSeat(
        entry.eTicketNumber,
        entry.bookingReference,
        entry.passengerId,
        this.#inventoryId,
        seat.seatNumber,
        entry.orderId,
      );
      await this.#silentRefresh();
      const updated = this.manifest()?.entries.find(e => e.eTicketNumber === entry.eTicketNumber);
      this.selectedEntry.set(updated ?? null);
      this.pendingSeat.set(null);
    } catch {
      this.seatOpError.set('Failed to confirm seat. Please try again.');
    } finally {
      this.seatOpLoading.set(false);
    }
  }

  statusClass(status: string): string {
    if (status === 'Active') return 'badge-active';
    if (status === 'Ticketing Closed') return 'badge-warning';
    return 'badge-inactive';
  }

  cabinLabel(code: string): string {
    return ({ F: 'First', J: 'Business', W: 'Premium Economy', Y: 'Economy' } as Record<string, string>)[code] ?? code;
  }

  bookingTypeBadgeClass(bookingType: string): string {
    return bookingType === 'Standby' ? 'booking-type-standby' : 'booking-type-confirmed';
  }

  seatClass(seat: SeatmapSeat | null): string {
    if (!seat) return 'seat-gap';
    const pending = this.pendingSeat();
    if (pending?.seatNumber === seat.seatNumber) return 'seat-pending';
    const selected = this.selectedEntry();
    if (selected?.seatNumber === seat.seatNumber) return 'seat-selected';
    if (seat.availability === 'available') {
      return this.inSeatSelectMode() ? 'seat-open seat-selectable' : 'seat-open';
    }
    const entry = this.manifest()?.entries.find(e => e.seatNumber === seat.seatNumber);
    return entry?.checkedIn            ? 'seat-checked-in'
         : seat.availability === 'sold' ? 'seat-booked'
         : 'seat-held';
  }

  seatTooltip(seat: SeatmapSeat | null): string {
    if (!seat) return '';
    const pending = this.pendingSeat();
    if (pending?.seatNumber === seat.seatNumber) return `${seat.seatNumber} — Selected (pending)`;
    if (seat.availability === 'available') return `${seat.seatNumber} — Open`;
    const entry = this.manifest()?.entries.find(e => e.seatNumber === seat.seatNumber);
    return entry
      ? `${seat.seatNumber} — ${entry.surname.toUpperCase()}/${entry.givenName.toUpperCase()}${entry.checkedIn ? ' (Checked in)' : ''}`
      : `${seat.seatNumber} — ${seat.availability}`;
  }

  setFilter(filter: 'all' | 'confirmed' | 'standby'): void {
    this.paxFilter.set(filter);
    this.selectedEntry.set(null);
    this.pendingSeat.set(null);
    this.seatOpError.set('');
  }

  copyBookingRef(text: string, event?: Event): void {
    event?.stopPropagation();
    navigator.clipboard.writeText(text).then(() => {
      this.copiedRef.set(text);
      setTimeout(() => this.copiedRef.set(null), 2000);
    });
  }

  navigateToOrder(bookingReference: string, event?: Event): void {
    event?.stopPropagation();
    void this.#router.navigate(['/order', bookingReference]);
  }

  async autoAssign(): Promise<void> {
    if (this.autoAssignLoading() || this.unassignedConfirmedCount() === 0) return;
    this.autoAssignLoading.set(true);
    this.autoAssignError.set('');
    this.autoAssignResult.set(null);
    try {
      const result = await this.#inventoryService.autoAssignSeats(
        this.#inventoryId,
        this.#flightNumber,
        this.#departureDate,
        this.#aircraftType,
      );
      this.autoAssignResult.set(result);
      this.autoAssignModalOpen.set(true);
      await this.#silentRefresh();
    } catch {
      this.autoAssignError.set('Auto-assign failed. Please try again.');
    } finally {
      this.autoAssignLoading.set(false);
    }
  }

  closeAutoAssignModal(): void {
    this.autoAssignModalOpen.set(false);
    this.autoAssignResult.set(null);
    this.autoAssignError.set('');
  }
}
