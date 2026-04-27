import { LucideAngularModule } from 'lucide-angular';
import { Component, signal, computed, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { UserService, UserAccount } from '../../services/user.service';
import { AuthService } from '../../services/auth.service';

type StatusFilter = 'all' | 'active' | 'inactive' | 'locked';

@Component({
  selector: 'app-users',
   [FormsModule]:imports: [FormsModule, LucideAngularModule]: [FormsModule],
  templateUrl: './users.html',
  styleUrl: './users.css',
})
export class UsersComponent implements OnInit {
  #userService = inject(UserService);
  #authService = inject(AuthService);

  search = signal('');
  statusFilter = signal<StatusFilter>('all');
  selectedUser = signal<UserAccount | null>(null);
  showAddForm = signal(false);
  loading = signal(false);
  error = signal('');
  success = signal('');

  editingUser = signal<UserAccount | null>(null);
  editFirstName = signal('');
  editLastName = signal('');
  editEmail = signal('');

  showResetPassword = signal(false);
  resetPasswordValue = signal('');

  newUser = signal({
    username: '',
    firstName: '',
    lastName: '',
    email: '',
    password: '',
  });

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

  selectUser(u: UserAccount): void {
    if (this.selectedUser()?.userId === u.userId) {
      this.selectedUser.set(null);
      this.editingUser.set(null);
      this.showResetPassword.set(false);
    } else {
      this.selectedUser.set(u);
      this.editingUser.set(null);
      this.showResetPassword.set(false);
    }
  }

  startEdit(u: UserAccount): void {
    this.editingUser.set(u);
    this.editFirstName.set(u.firstName);
    this.editLastName.set(u.lastName);
    this.editEmail.set(u.email);
  }

  cancelEdit(): void {
    this.editingUser.set(null);
  }

  async saveEdit(): Promise<void> {
    const u = this.editingUser();
    if (!u) return;

    this.error.set('');
    this.success.set('');

    const data: Record<string, string> = {};
    if (this.editFirstName() !== u.firstName) data['firstName'] = this.editFirstName();
    if (this.editLastName() !== u.lastName) data['lastName'] = this.editLastName();
    if (this.editEmail() !== u.email) data['email'] = this.editEmail();

    if (Object.keys(data).length === 0) {
      this.editingUser.set(null);
      return;
    }

    try {
      await this.#userService.updateUser(u.userId, data);
      this.success.set('User updated successfully.');
      this.editingUser.set(null);
      await this.loadUsers();
      this.clearMessages();
    } catch {
      this.error.set('Failed to update user.');
    }
  }

  async toggleStatus(u: UserAccount): Promise<void> {
    if (u.isActive && this.isSelf(u)) {
      this.error.set('You cannot deactivate your own account.');
      this.clearMessages();
      return;
    }

    this.error.set('');
    this.success.set('');
    try {
      await this.#userService.setUserStatus(u.userId, !u.isActive);
      this.success.set(`User ${u.isActive ? 'deactivated' : 'activated'} successfully.`);
      await this.loadUsers();
      this.clearMessages();
    } catch {
      this.error.set('Failed to update user status.');
    }
  }

  async unlockUser(u: UserAccount): Promise<void> {
    this.error.set('');
    this.success.set('');
    try {
      await this.#userService.unlockUser(u.userId);
      this.success.set('User account unlocked successfully.');
      await this.loadUsers();
      this.clearMessages();
    } catch {
      this.error.set('Failed to unlock user.');
    }
  }

  showResetPasswordForm(u: UserAccount): void {
    this.showResetPassword.set(true);
    this.resetPasswordValue.set('');
  }

  async resetPassword(u: UserAccount): Promise<void> {
    const pw = this.resetPasswordValue();
    if (!pw) return;

    this.error.set('');
    this.success.set('');
    try {
      await this.#userService.resetPassword(u.userId, pw);
      this.success.set('Password reset successfully.');
      this.showResetPassword.set(false);
      this.resetPasswordValue.set('');
      this.clearMessages();
    } catch {
      this.error.set('Failed to reset password.');
    }
  }

  async addUser(): Promise<void> {
    const n = this.newUser();
    if (!n.username || !n.firstName || !n.lastName || !n.email || !n.password) return;

    this.error.set('');
    this.success.set('');
    try {
      await this.#userService.createUser({
        username: n.username.toUpperCase(),
        firstName: n.firstName,
        lastName: n.lastName,
        email: n.email,
        password: n.password,
      });
      this.success.set('User created successfully.');
      this.newUser.set({ username: '', firstName: '', lastName: '', email: '', password: '' });
      this.showAddForm.set(false);
      await this.loadUsers();
      this.clearMessages();
    } catch {
      this.error.set('Failed to create user. Username or email may already exist.');
    }
  }

  isSelf(u: UserAccount): boolean {
    return u.userId === this.#authService.currentUser()?.userId;
  }

  async deleteUser(u: UserAccount): Promise<void> {
    if (this.isSelf(u)) {
      this.error.set('You cannot delete your own account.');
      this.clearMessages();
      return;
    }

    if (!confirm(`Are you sure you want to permanently delete user "${u.firstName} ${u.lastName}" (${u.username})? This action cannot be undone.`)) {
      return;
    }

    this.error.set('');
    this.success.set('');
    try {
      await this.#userService.deleteUser(u.userId);
      this.success.set('User deleted successfully.');
      this.selectedUser.set(null);
      await this.loadUsers();
      this.clearMessages();
    } catch {
      this.error.set('Failed to delete user.');
    }
  }

  setNewUser(field: string, val: string): void {
    this.newUser.update(u => ({ ...u, [field]: val }));
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

  private clearMessages(): void {
    setTimeout(() => {
      this.success.set('');
      this.error.set('');
    }, 3000);
  }
}
