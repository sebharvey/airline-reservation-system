import { Component, signal, computed, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FareFamiliesService, FareFamily, CreateFareFamilyRequest } from '../../services/fare-families.service';

@Component({
  selector: 'app-fare-families',
  imports: [FormsModule],
  templateUrl: './fare-families.html',
  styleUrl: './fare-families.css',
})
export class FareFamiliesComponent implements OnInit {
  #fareFamiliesService = inject(FareFamiliesService);

  families = signal<FareFamily[]>([]);
  loading = signal(false);
  error = signal('');
  success = signal('');
  loaded = signal(false);

  showForm = signal(false);
  editing = signal<FareFamily | null>(null);
  saving = signal(false);
  deleting = signal<string | null>(null);

  form = signal<CreateFareFamilyRequest>({
    name: '',
    description: null,
    displayOrder: 0,
  });

  stats = computed(() => {
    const all = this.families();
    return { total: all.length };
  });

  ngOnInit(): void {
    this.loadFamilies();
  }

  async loadFamilies(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const result = await this.#fareFamiliesService.getFareFamilies();
      this.families.set(result);
      this.loaded.set(true);
    } catch {
      this.error.set('Failed to load fare families. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  openCreateForm(): void {
    this.editing.set(null);
    this.form.set({ name: '', description: null, displayOrder: 0 });
    this.showForm.set(true);
    this.error.set('');
    this.success.set('');
  }

  openEditForm(family: FareFamily): void {
    this.editing.set(family);
    this.form.set({
      name: family.name,
      description: family.description,
      displayOrder: family.displayOrder,
    });
    this.showForm.set(true);
    this.error.set('');
    this.success.set('');
  }

  cancelForm(): void {
    this.showForm.set(false);
    this.editing.set(null);
  }

  updateField(field: string, value: unknown): void {
    this.form.update(f => ({ ...f, [field]: value }));
  }

  async saveFamily(): Promise<void> {
    this.saving.set(true);
    this.error.set('');
    this.success.set('');

    const data = this.form();
    const request: CreateFareFamilyRequest = {
      name: data.name.trim(),
      description: data.description?.trim() || null,
      displayOrder: data.displayOrder,
    };

    try {
      const editingFamily = this.editing();
      if (editingFamily) {
        await this.#fareFamiliesService.updateFareFamily(editingFamily.fareFamilyId, request);
        this.success.set('Fare family updated successfully.');
      } else {
        await this.#fareFamiliesService.createFareFamily(request);
        this.success.set('Fare family created successfully.');
      }
      this.showForm.set(false);
      this.editing.set(null);
      await this.loadFamilies();
    } catch {
      this.error.set('Failed to save fare family. Please check the data and try again.');
    } finally {
      this.saving.set(false);
    }
  }

  async deleteFamily(fareFamilyId: string): Promise<void> {
    this.deleting.set(fareFamilyId);
    this.error.set('');
    this.success.set('');
    try {
      await this.#fareFamiliesService.deleteFareFamily(fareFamilyId);
      this.success.set('Fare family deleted successfully.');
      await this.loadFamilies();
    } catch {
      this.error.set('Failed to delete fare family. It may be in use by existing fare rules.');
    } finally {
      this.deleting.set(null);
    }
  }

  formatDate(iso: string): string {
    return new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
  }
}
