import { LucideAngularModule } from 'lucide-angular';
import { Component, signal, inject, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { UserService, UserAccount } from '../../../services/user.service';
import { AuthService } from '../../../services/auth.service';

@Component({
  selector: 'app-user-detail',
  imports: [FormsModule, LucideAngularModule],
  templateUrl: './user-detail.html',
  styleUrl: './user-detail.css',
})
export class UserDetailComponent implements OnInit {
  #route = inject(ActivatedRoute);
  #router = inject(Router);
  #userService = inject(UserService);
  #authService = inject(AuthService);

  userId = '';
  isNew = false;

  user = signal<UserAccount | null>(null);
  loading = signal(false);
  saving = signal(false);
  error = signal('');
  success = signal('');

  // Edit mode
  editing = signal(false);
  editFirstName = signal('');
  editLastName = signal('');
  editEmail = signal('');

  // Reset password
  showResetPassword = signal(false);
  resetPasswordValue = signal('');
  resettingPassword = signal(false);

  // New user creation
  newUsername = signal('');
  newFirstName = signal('');
  newLastName = signal('');
  newEmail = signal('');
  newPassword = signal('');

  ngOnInit(): void {
    this.userId = this.#route.snapshot.paramMap.get('userId') ?? '';
    this.isNew = this.userId === 'new';
    if (!this.isNew) {
      this.loadUser();
    }
  }

  async loadUser(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const u = await this.#userService.getUser(this.userId);
      this.user.set(u);
    } catch {
      this.error.set('Failed to load user.');
    } finally {
      this.loading.set(false);
    }
  }

  startEdit(): void {
    const u = this.user();
    if (!u) return;
    this.editFirstName.set(u.firstName);
    this.editLastName.set(u.lastName);
    this.editEmail.set(u.email);
    this.editing.set(true);
    this.success.set('');
    this.error.set('');
  }

  cancelEdit(): void {
    this.editing.set(false);
    this.error.set('');
  }

  async saveEdit(): Promise<void> {
    const u = this.user();
    if (!u) return;

    const data: Record<string, string> = {};
    if (this.editFirstName() !== u.firstName) data['firstName'] = this.editFirstName();
    if (this.editLastName() !== u.lastName) data['lastName'] = this.editLastName();
    if (this.editEmail() !== u.email) data['email'] = this.editEmail();

    if (Object.keys(data).length === 0) {
      this.editing.set(false);
      return;
    }

    this.saving.set(true);
    this.error.set('');
    this.success.set('');
    try {
      await this.#userService.updateUser(u.userId, data);
      this.success.set('User updated successfully.');
      this.editing.set(false);
      await this.loadUser();
    } catch {
      this.error.set('Failed to update user.');
    } finally {
      this.saving.set(false);
    }
  }

  async toggleStatus(): Promise<void> {
    const u = this.user();
    if (!u) return;
    if (u.isActive && this.isSelf(u)) {
      this.error.set('You cannot deactivate your own account.');
      return;
    }
    this.error.set('');
    this.success.set('');
    try {
      await this.#userService.setUserStatus(u.userId, !u.isActive);
      this.success.set(`User ${u.isActive ? 'deactivated' : 'activated'} successfully.`);
      await this.loadUser();
    } catch {
      this.error.set('Failed to update user status.');
    }
  }

  async unlockUser(): Promise<void> {
    const u = this.user();
    if (!u) return;
    this.error.set('');
    this.success.set('');
    try {
      await this.#userService.unlockUser(u.userId);
      this.success.set('User account unlocked successfully.');
      await this.loadUser();
    } catch {
      this.error.set('Failed to unlock user.');
    }
  }

  openResetPassword(): void {
    this.showResetPassword.set(true);
    this.resetPasswordValue.set('');
    this.error.set('');
    this.success.set('');
  }

  cancelResetPassword(): void {
    this.showResetPassword.set(false);
  }

  async resetPassword(): Promise<void> {
    const u = this.user();
    const pw = this.resetPasswordValue();
    if (!u || !pw) return;
    this.resettingPassword.set(true);
    this.error.set('');
    this.success.set('');
    try {
      await this.#userService.resetPassword(u.userId, pw);
      this.success.set('Password reset successfully.');
      this.showResetPassword.set(false);
      this.resetPasswordValue.set('');
    } catch {
      this.error.set('Failed to reset password.');
    } finally {
      this.resettingPassword.set(false);
    }
  }

  async deleteUser(): Promise<void> {
    const u = this.user();
    if (!u) return;
    if (this.isSelf(u)) {
      this.error.set('You cannot delete your own account.');
      return;
    }
    if (!confirm(`Are you sure you want to permanently delete user "${u.firstName} ${u.lastName}" (${u.username})? This action cannot be undone.`)) {
      return;
    }
    this.error.set('');
    this.success.set('');
    try {
      await this.#userService.deleteUser(u.userId);
      this.#router.navigate(['/users']);
    } catch {
      this.error.set('Failed to delete user.');
    }
  }

  setNewUserField(field: string, val: string): void {
    switch (field) {
      case 'username': this.newUsername.set(val); break;
      case 'firstName': this.newFirstName.set(val); break;
      case 'lastName': this.newLastName.set(val); break;
      case 'email': this.newEmail.set(val); break;
      case 'password': this.newPassword.set(val); break;
    }
  }

  async createUser(): Promise<void> {
    if (!this.newUsername() || !this.newFirstName() || !this.newLastName() || !this.newEmail() || !this.newPassword()) return;
    this.saving.set(true);
    this.error.set('');
    this.success.set('');
    try {
      await this.#userService.createUser({
        username: this.newUsername().toUpperCase(),
        firstName: this.newFirstName(),
        lastName: this.newLastName(),
        email: this.newEmail(),
        password: this.newPassword(),
      });
      this.#router.navigate(['/users']);
    } catch {
      this.error.set('Failed to create user. Username or email may already exist.');
    } finally {
      this.saving.set(false);
    }
  }

  isSelf(u: UserAccount): boolean {
    return u.userId === this.#authService.currentUser()?.userId;
  }

  goBack(): void {
    this.#router.navigate(['/users']);
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
