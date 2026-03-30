import { Component, signal, computed, inject, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { UserService, UserAccount } from '../../../services/user.service';

type StatusFilter = 'all' | 'active' | 'inactive' | 'locked';

@Component({
  selector: 'app-user-list',
  imports: [FormsModule],
  templateUrl: './user-list.html',
  styleUrl: './user-list.css',
})
export class UserListComponent implements OnInit {
  #userService = inject(UserService);
  #router = inject(Router);

  search = signal('');
  statusFilter = signal<StatusFilter>('all');
  loading = signal(false);
  error = signal('');

  users = signal<UserAccount[]>([]);

  filteredUsers = computed(() => {
    const q = this.search().toLowerCase();
    return this.users().filter(u => {
      const displayName = `${u.firstName} ${u.lastName}`.toLowerCase();
      const matchText = !q ||
        displayName.includes(q) ||
        u.username.toLowerCase().includes(q) ||
        u.email.toLowerCase().includes(q);
      const filter = this.statusFilter();
      const matchStatus =
        filter === 'all' ||
        (filter === 'active' && u.isActive && !u.isLocked) ||
        (filter === 'inactive' && !u.isActive) ||
        (filter === 'locked' && u.isLocked);
      return matchText && matchStatus;
    });
  });

  stats = computed(() => ({
    total: this.users().length,
    active: this.users().filter(u => u.isActive && !u.isLocked).length,
    inactive: this.users().filter(u => !u.isActive).length,
    locked: this.users().filter(u => u.isLocked).length,
  }));

  async ngOnInit(): Promise<void> {
    await this.loadUsers();
  }

  async loadUsers(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const users = await this.#userService.getUsers();
      this.users.set(users);
    } catch {
      this.error.set('Failed to load users.');
    } finally {
      this.loading.set(false);
    }
  }

  setSearch(v: string): void { this.search.set(v); }
  setStatusFilter(v: string): void { this.statusFilter.set(v as StatusFilter); }

  openUser(userId: string): void {
    this.#router.navigate(['/users', userId]);
  }

  newUser(): void {
    this.#router.navigate(['/users', 'new']);
  }

  statusLabel(u: UserAccount): string {
    if (u.isLocked) return 'locked';
    return u.isActive ? 'active' : 'inactive';
  }

  statusBadgeClass(u: UserAccount): string {
    if (u.isLocked) return 'badge-locked';
    return u.isActive ? 'badge-active' : 'badge-inactive';
  }

  formatDate(dateStr: string | null): string {
    if (!dateStr) return 'Never';
    const d = new Date(dateStr);
    return d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }) +
      ' ' + d.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' });
  }
}
