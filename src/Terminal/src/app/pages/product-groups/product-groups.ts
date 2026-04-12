import { Component, signal, computed, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
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

  createForm = signal<CreateProductGroupRequest>({ name: '' });
  updateForm = signal<UpdateProductGroupRequest>({ name: '', isActive: true });

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
    this.createForm.set({ name: '' });
    this.showForm.set(true);
    this.error.set('');
    this.success.set('');
  }

  openEditForm(group: ProductGroup): void {
    this.editing.set(group);
    this.updateForm.set({ name: group.name, isActive: group.isActive });
    this.showForm.set(true);
    this.error.set('');
    this.success.set('');
  }

  cancelForm(): void {
    this.showForm.set(false);
    this.editing.set(null);
  }

  updateCreateField(field: keyof CreateProductGroupRequest, value: unknown): void {
    this.createForm.update(f => ({ ...f, [field]: value }));
  }

  updateUpdateField(field: keyof UpdateProductGroupRequest, value: unknown): void {
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
    } catch {
      this.error.set('Failed to delete product group. It may still have products assigned to it.');
    } finally {
      this.deleting.set(null);
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
