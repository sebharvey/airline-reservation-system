import { LucideAngularModule } from 'lucide-angular';
import { Component, signal, computed, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ScheduleService, ScheduleSummary, ScheduleGroupSummary, ImportSsimResponse } from '../../services/schedule.service';

@Component({
  selector: 'app-schedules',
  templateUrl: './schedules.html',
  styleUrl: './schedules.css',
   [FormsModule]:imports: [FormsModule, LucideAngularModule]: [FormsModule],
})
export class SchedulesComponent implements OnInit {
  #scheduleService = inject(ScheduleService);

  // Schedule groups
  groups = signal<ScheduleGroupSummary[]>([]);
  selectedGroupId = signal<string>('');
  loadingGroups = signal(false);

  // Create/edit group modal
  showGroupModal = signal(false);
  editingGroup = signal<ScheduleGroupSummary | null>(null);
  groupForm = signal({ name: '', seasonStart: '', seasonEnd: '', isActive: false });
  savingGroup = signal(false);
  groupError = signal('');

  // Schedules
  schedules = signal<ScheduleSummary[]>([]);
  filter = signal('');
  loading = signal(false);
  error = signal('');
  loaded = signal(false);

  // Import to inventory state
  showImportModal = signal(false);
  importing = signal(false);
  importError = signal('');
  importSuccess = signal('');

  // Import SSIM state
  showSsimModal = signal(false);
  importingSsim = signal(false);
  ssimFileName = signal('');
  ssimFileContent = signal('');
  ssimError = signal('');
  ssimResult = signal<ImportSsimResponse | null>(null);

  ngOnInit(): void {
    this.loadGroups();
  }

  selectedGroup = computed(() => {
    const id = this.selectedGroupId();
    return this.groups().find(g => g.scheduleGroupId === id) ?? null;
  });

  filtered = computed(() => {
    const q = this.filter().toLowerCase().trim();
    const all = this.schedules();
    if (!q) return all;
    return all.filter(
      s =>
        s.flightNumber.toLowerCase().includes(q) ||
        s.origin.toLowerCase().includes(q) ||
        s.destination.toLowerCase().includes(q) ||
        s.aircraftType.toLowerCase().includes(q)
    );
  });

  stats = computed(() => {
    const all = this.schedules();
    const routes = new Set(all.map(s => `${s.origin}-${s.destination}`));
    const aircraft = new Set(all.map(s => s.aircraftType));
    const dailyFlights = all.filter(s => s.daysOfWeek === 127).length;
    const totalOperating = all.reduce((sum, s) => sum + s.operatingDateCount, 0);
    const totalInventory = all.reduce((sum, s) => sum + s.flightsCreated, 0);
    return {
      total: all.length,
      routes: routes.size,
      aircraftTypes: aircraft.size,
      dailyFlights,
      inventoryCoverage: totalOperating > 0 ? Math.round((totalInventory / totalOperating) * 100) : 0,
    };
  });

  async loadGroups(preferredGroupId?: string): Promise<void> {
    this.loadingGroups.set(true);
    this.error.set('');
    try {
      const result = await this.#scheduleService.getScheduleGroups();
      this.groups.set(result.groups);
      const preferred = preferredGroupId ? result.groups.find(g => g.scheduleGroupId === preferredGroupId) : null;
      const active = result.groups.find(g => g.isActive);
      const toSelect = preferred ?? active ?? result.groups[0];
      if (toSelect) {
        this.selectedGroupId.set(toSelect.scheduleGroupId);
        await this.loadSchedules();
      } else {
        this.loaded.set(true);
      }
    } catch {
      this.error.set('Failed to load schedule groups.');
    } finally {
      this.loadingGroups.set(false);
    }
  }

  async onGroupChange(scheduleGroupId: string): Promise<void> {
    this.selectedGroupId.set(scheduleGroupId);
    this.filter.set('');
    this.importSuccess.set('');
    await this.loadSchedules();
  }

  async loadSchedules(): Promise<void> {
    const groupId = this.selectedGroupId();
    if (!groupId) return;
    this.loading.set(true);
    this.error.set('');
    try {
      const result = await this.#scheduleService.getSchedules(groupId);
      this.schedules.set(result.schedules);
      this.loaded.set(true);
    } catch {
      this.error.set('Failed to load schedules. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  setFilter(val: string): void {
    this.filter.set(val);
  }

  formatTime(time: string): string {
    return time.substring(0, 5);
  }

  formatDate(iso: string): string {
    return new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
  }

  formatDaysOfWeek(bitmask: number): string {
    const days = ['M', 'T', 'W', 'T', 'F', 'S', 'S'];
    return days.map((d, i) => (bitmask & (1 << i) ? d : '.')).join('');
  }

  formatDuration(departureTime: string, arrivalTime: string, arrivalDayOffset: number): string {
    const [depH, depM] = departureTime.substring(0, 5).split(':').map(Number);
    const [arrH, arrM] = arrivalTime.substring(0, 5).split(':').map(Number);
    let totalMinutes = (arrH * 60 + arrM) - (depH * 60 + depM) + arrivalDayOffset * 1440;
    if (totalMinutes < 0) totalMinutes += 1440;
    const hours = Math.floor(totalMinutes / 60);
    const minutes = totalMinutes % 60;
    return `${hours}h ${minutes.toString().padStart(2, '0')}m`;
  }

  // ── Import to Inventory ─────────────────────────────────────────────────────

  openImportModal(): void {
    this.importError.set('');
    this.importSuccess.set('');
    this.showImportModal.set(true);
  }

  closeImportModal(): void {
    this.showImportModal.set(false);
  }

  async importToInventory(): Promise<void> {
    this.importing.set(true);
    this.importError.set('');
    this.importSuccess.set('');
    try {
      const scheduleGroupId = this.selectedGroupId() || undefined;
      const cutoff = new Date();
      cutoff.setMonth(cutoff.getMonth() + 3);
      const toDate = cutoff.toISOString().substring(0, 10);
      const result = await this.#scheduleService.importSchedulesToInventory({ scheduleGroupId, toDate });
      this.importSuccess.set(
        `Import complete: ${result.schedulesProcessed} schedules processed, ${result.inventoriesCreated} inventories created, ${result.inventoriesSkipped} skipped.`
      );
      this.showImportModal.set(false);
      await this.loadSchedules();
    } catch {
      this.importError.set('Failed to import schedules to inventory. Please try again.');
    } finally {
      this.importing.set(false);
    }
  }

  // ── Import SSIM ─────────────────────────────────────────────────────────────

  openSsimModal(): void {
    this.ssimFileName.set('');
    this.ssimFileContent.set('');
    this.ssimError.set('');
    this.ssimResult.set(null);
    this.showSsimModal.set(true);
  }

  closeSsimModal(): void {
    this.showSsimModal.set(false);
  }

  onSsimFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.ssimFileName.set(file.name);
    this.ssimError.set('');
    const reader = new FileReader();
    reader.onload = () => {
      this.ssimFileContent.set(reader.result as string);
    };
    reader.onerror = () => {
      this.ssimError.set('Failed to read the selected file.');
    };
    reader.readAsText(file);
  }

  async importSsimFile(): Promise<void> {
    const groupId = this.selectedGroupId();
    const content = this.ssimFileContent();
    if (!groupId || !content) return;
    this.importingSsim.set(true);
    this.ssimError.set('');
    this.ssimResult.set(null);
    try {
      const result = await this.#scheduleService.importSsim(groupId, content);
      this.ssimResult.set(result);
      await this.loadGroups(groupId);
    } catch {
      this.ssimError.set('Failed to import SSIM file. Please check the file format and try again.');
    } finally {
      this.importingSsim.set(false);
    }
  }

  // ── Schedule Group CRUD ─────────────────────────────────────────────────────

  openCreateGroupModal(): void {
    this.editingGroup.set(null);
    this.groupForm.set({ name: '', seasonStart: '', seasonEnd: '', isActive: false });
    this.groupError.set('');
    this.showGroupModal.set(true);
  }

  openEditGroupModal(): void {
    const group = this.selectedGroup();
    if (!group) return;
    this.editingGroup.set(group);
    this.groupForm.set({
      name: group.name,
      seasonStart: group.seasonStart,
      seasonEnd: group.seasonEnd,
      isActive: group.isActive,
    });
    this.groupError.set('');
    this.showGroupModal.set(true);
  }

  closeGroupModal(): void {
    this.showGroupModal.set(false);
  }

  updateGroupForm(field: string, value: string | boolean): void {
    this.groupForm.update(f => ({ ...f, [field]: value }));
  }

  async saveGroup(): Promise<void> {
    const form = this.groupForm();
    if (!form.name.trim() || !form.seasonStart || !form.seasonEnd) {
      this.groupError.set('Name, season start, and season end are required.');
      return;
    }
    this.savingGroup.set(true);
    this.groupError.set('');
    try {
      const editing = this.editingGroup();
      let preferredGroupId: string | undefined;
      if (editing) {
        await this.#scheduleService.updateScheduleGroup(editing.scheduleGroupId, {
          name: form.name,
          seasonStart: form.seasonStart,
          seasonEnd: form.seasonEnd,
          isActive: form.isActive,
        });
      } else {
        const created = await this.#scheduleService.createScheduleGroup({
          name: form.name,
          seasonStart: form.seasonStart,
          seasonEnd: form.seasonEnd,
          isActive: form.isActive,
          createdBy: 'ops-admin',
        });
        preferredGroupId = created.scheduleGroupId;
      }
      this.showGroupModal.set(false);
      await this.loadGroups(preferredGroupId);
    } catch {
      this.groupError.set('Failed to save schedule group.');
    } finally {
      this.savingGroup.set(false);
    }
  }

  async deleteGroup(): Promise<void> {
    const group = this.selectedGroup();
    if (!group) return;
    if (!confirm(`Delete "${group.name}" and all its schedules?`)) return;
    try {
      await this.#scheduleService.deleteScheduleGroup(group.scheduleGroupId);
      this.selectedGroupId.set('');
      this.schedules.set([]);
      await this.loadGroups();
    } catch {
      this.error.set('Failed to delete schedule group.');
    }
  }
}
