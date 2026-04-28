import { Component, signal, computed, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LucideAngularModule } from 'lucide-angular';
import {
  WatchlistService,
  WatchlistEntry,
  CreateWatchlistEntryRequest,
  UpdateWatchlistEntryRequest,
} from '../../../services/watchlist.service';

@Component({
  selector: 'app-watchlist',
  standalone: true,
  imports: [FormsModule, LucideAngularModule],
  templateUrl: './watchlist.html',
  styleUrl: './watchlist.css',
})
export class WatchlistComponent implements OnInit {
  #service = inject(WatchlistService);

  entries = signal<WatchlistEntry[]>([]);
  filter = signal('');
  loading = signal(false);
  error = signal('');
  success = signal('');
  loaded = signal(false);

  showForm = signal(false);
  editing = signal<WatchlistEntry | null>(null);
  saving = signal(false);
  deleting = signal<string | null>(null);
  confirmDelete = signal<string | null>(null);

  form = signal<CreateWatchlistEntryRequest>({
    givenName: '',
    surname: '',
    dateOfBirth: '',
    passportNumber: '',
    notes: '',
  });

  filtered = computed(() => {
    const q = this.filter().toLowerCase().trim();
    const all = this.entries();
    if (!q) return all;
    return all.filter(
      e =>
        e.surname.toLowerCase().includes(q) ||
        e.givenName.toLowerCase().includes(q) ||
        e.passportNumber.toLowerCase().includes(q)
    );
  });

  stats = computed(() => {
    const all = this.entries();
    return { total: all.length };
  });

  ngOnInit(): void {
    this.loadEntries();
  }

  async loadEntries(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const result = await this.#service.getAll();
      this.entries.set(result.sort((a, b) => a.surname.localeCompare(b.surname) || a.givenName.localeCompare(b.givenName)));
      this.loaded.set(true);
    } catch {
      this.error.set('Failed to load watchlist. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  setFilter(val: string): void {
    this.filter.set(val);
  }

  openCreateForm(): void {
    this.editing.set(null);
    this.form.set({ givenName: '', surname: '', dateOfBirth: '', passportNumber: '', notes: '' });
    this.showForm.set(true);
    this.error.set('');
    this.success.set('');
  }

  openEditForm(entry: WatchlistEntry): void {
    this.editing.set(entry);
    this.form.set({
      givenName: entry.givenName,
      surname: entry.surname,
      dateOfBirth: entry.dateOfBirth,
      passportNumber: entry.passportNumber,
      notes: entry.notes ?? '',
    });
    this.showForm.set(true);
    this.error.set('');
    this.success.set('');
  }

  cancelForm(): void {
    this.showForm.set(false);
    this.editing.set(null);
  }

  updateField(field: keyof CreateWatchlistEntryRequest, value: string): void {
    this.form.update(f => ({ ...f, [field]: value }));
  }

  async save(): Promise<void> {
    const f = this.form();
    if (!f.givenName.trim() || !f.surname.trim() || !f.dateOfBirth || !f.passportNumber.trim()) {
      this.error.set('Given name, surname, date of birth and passport number are all required.');
      return;
    }

    this.saving.set(true);
    this.error.set('');
    this.success.set('');
    try {
      const editingEntry = this.editing();
      const payload: CreateWatchlistEntryRequest = {
        givenName: f.givenName.trim(),
        surname: f.surname.trim(),
        dateOfBirth: f.dateOfBirth,
        passportNumber: f.passportNumber.trim(),
        notes: f.notes?.trim() || undefined,
      };

      if (editingEntry) {
        await this.#service.update(editingEntry.watchlistId, payload as UpdateWatchlistEntryRequest);
        this.success.set('Watchlist entry updated successfully.');
      } else {
        await this.#service.create(payload);
        this.success.set('Passenger added to watchlist.');
      }
      this.showForm.set(false);
      this.editing.set(null);
      await this.loadEntries();
    } catch (err: unknown) {
      if (err instanceof Error && err.message.includes('409')) {
        this.error.set('This passport number is already on the watchlist.');
      } else {
        this.error.set('Failed to save watchlist entry. Check the data and try again.');
      }
    } finally {
      this.saving.set(false);
    }
  }

  requestDelete(watchlistId: string): void {
    this.confirmDelete.set(watchlistId);
  }

  cancelDelete(): void {
    this.confirmDelete.set(null);
  }

  async confirmDeleteEntry(watchlistId: string): Promise<void> {
    this.deleting.set(watchlistId);
    this.error.set('');
    this.success.set('');
    this.confirmDelete.set(null);
    try {
      await this.#service.delete(watchlistId);
      this.success.set('Passenger removed from watchlist.');
      await this.loadEntries();
    } catch {
      this.error.set('Failed to remove watchlist entry. Please try again.');
    } finally {
      this.deleting.set(null);
    }
  }

  formatDob(dob: string): string {
    if (!dob) return '—';
    try {
      const d = new Date(dob + 'T00:00:00');
      return d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
    } catch {
      return dob;
    }
  }
}
