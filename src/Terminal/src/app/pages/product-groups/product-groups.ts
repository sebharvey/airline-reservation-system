import { Component, signal, computed, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ProductGroupService,
  ProductGroup,
  CreateProductGroupRequest,
  UpdateProductGroupRequest,
} from '../../services/product-group.service';

@Component({
  selector: 'app-product-groups',
  imports: [FormsModule],
  templateUrl: './product-groups.html',
  styleUrl: './product-groups.css',
})
export class ProductGroupsComponent implements OnInit {
  #service = inject(ProductGroupService);

  groups = signal<ProductGroup[]>([]);
  loading = signal(false);
  error = signal('');
  success = signal('');
  loaded = signal(false);

  showForm = signal(false);
  editing = signal<ProductGroup | null>(null);
  saving = signal(false);
  deleting = signal<string | null>(null);
  reordering = signal(false);

  createForm = signal<CreateProductGroupRequest>({ name: '', sortOrder: 1 });
  updateForm = signal<UpdateProductGroupRequest>({ name: '', sortOrder: 0, isActive: true });

  sortedGroups = computed(() => [...this.groups()].sort((a, b) => a.sortOrder - b.sortOrder));

  stats = computed(() => {
    const all = this.groups();
    const active = all.filter(g => g.isActive).length;
    return { total: all.length, active };
  });

  ngOnInit(): void {
    this.loadGroups();
  }

  async loadGroups(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const result = await this.#service.getAll();
      this.groups.set(result);
      this.loaded.set(true);
    } catch {
      this.error.set('Failed to load product groups. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  openCreateForm(): void {
    this.editing.set(null);
    const groups = this.groups();
    const nextOrder = groups.length > 0 ? Math.max(...groups.map(g => g.sortOrder)) + 1 : 1;
    this.createForm.set({ name: '', sortOrder: nextOrder });
    this.showForm.set(true);
    this.error.set('');
    this.success.set('');
  }

  openEditForm(group: ProductGroup): void {
    this.editing.set(group);
    this.updateForm.set({ name: group.name, sortOrder: group.sortOrder, isActive: group.isActive });
    this.showForm.set(true);
    this.error.set('');
    this.success.set('');
  }

  cancelForm(): void {
    this.showForm.set(false);
    this.editing.set(null);
  }

  updateCreateField<K extends keyof CreateProductGroupRequest>(field: K, value: CreateProductGroupRequest[K]): void {
    this.createForm.update(f => ({ ...f, [field]: value }));
  }

  updateUpdateField<K extends keyof UpdateProductGroupRequest>(field: K, value: UpdateProductGroupRequest[K]): void {
    this.updateForm.update(f => ({ ...f, [field]: value }));
  }

  async save(): Promise<void> {
    this.saving.set(true);
    this.error.set('');
    this.success.set('');
    try {
      const editingGroup = this.editing();
      if (editingGroup) {
        await this.#service.update(editingGroup.productGroupId, this.updateForm());
        this.success.set('Product group updated successfully.');
      } else {
        await this.#service.create(this.createForm());
        this.success.set('Product group created successfully.');
      }
      this.showForm.set(false);
      this.editing.set(null);
      await this.loadGroups();
    } catch {
      this.error.set('Failed to save product group. Check the data and try again.');
    } finally {
      this.saving.set(false);
    }
  }

  async deleteGroup(groupId: string): Promise<void> {
    this.deleting.set(groupId);
    this.error.set('');
    this.success.set('');
    try {
      await this.#service.delete(groupId);
      this.success.set('Product group deleted successfully.');
      await this.loadGroups();
    } catch (err) {
      if (err instanceof HttpErrorResponse && err.status === 409) {
        this.error.set('This product group cannot be deleted because it has products assigned to it.');
      } else {
        this.error.set('Failed to delete product group. Please try again.');
      }
    } finally {
      this.deleting.set(null);
    }
  }

  async moveUp(group: ProductGroup): Promise<void> {
    const sorted = this.sortedGroups();
    const idx = sorted.findIndex(g => g.productGroupId === group.productGroupId);
    if (idx <= 0) return;
    await this.swapOrder(sorted[idx], sorted[idx - 1]);
  }

  async moveDown(group: ProductGroup): Promise<void> {
    const sorted = this.sortedGroups();
    const idx = sorted.findIndex(g => g.productGroupId === group.productGroupId);
    if (idx < 0 || idx >= sorted.length - 1) return;
    await this.swapOrder(sorted[idx], sorted[idx + 1]);
  }

  private async swapOrder(a: ProductGroup, b: ProductGroup): Promise<void> {
    this.reordering.set(true);
    this.error.set('');
    try {
      await Promise.all([
        this.#service.update(a.productGroupId, { name: a.name, sortOrder: b.sortOrder, isActive: a.isActive }),
        this.#service.update(b.productGroupId, { name: b.name, sortOrder: a.sortOrder, isActive: b.isActive }),
      ]);
      await this.loadGroups();
    } catch {
      this.error.set('Failed to reorder product groups. Please try again.');
    } finally {
      this.reordering.set(false);
    }
  }

  formatDate(iso: string): string {
    return new Date(iso).toLocaleDateString('en-GB', {
      day: '2-digit',
      month: 'short',
      year: 'numeric',
    });
  }
}
