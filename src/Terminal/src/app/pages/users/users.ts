import { Component, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';

type UserRole = 'agent' | 'supervisor' | 'admin';
type UserStatus = 'active' | 'suspended' | 'pending';

interface AgentRecord {
  id: string;
  username: string;
  displayName: string;
  email: string;
  role: UserRole;
  status: UserStatus;
  station: string;
  lastLogin: string;
}

@Component({
  selector: 'app-users',
  imports: [FormsModule],
  templateUrl: './users.html',
  styleUrl: './users.css',
})
export class UsersComponent {
  search = signal('');
  roleFilter = signal<'all' | UserRole>('all');
  statusFilter = signal<'all' | UserStatus>('all');
  selectedUser = signal<AgentRecord | null>(null);
  showAddForm = signal(false);

  newUser = signal({ username: '', displayName: '', email: '', role: 'agent' as UserRole, station: 'LHR' });

  users = signal<AgentRecord[]>([
    { id: 'LH1001', username: 'JSMITH',   displayName: 'John Smith',    email: 'j.smith@apexair.com',    role: 'supervisor', status: 'active',    station: 'LHR', lastLogin: '2026-03-20 08:42' },
    { id: 'LH1002', username: 'SWILSON',  displayName: 'Sarah Wilson',  email: 's.wilson@apexair.com',   role: 'agent',      status: 'active',    station: 'LHR', lastLogin: '2026-03-20 09:15' },
    { id: 'LH1003', username: 'MBROWN',   displayName: 'Mark Brown',    email: 'm.brown@apexair.com',    role: 'agent',      status: 'active',    station: 'LHR', lastLogin: '2026-03-19 17:30' },
    { id: 'MAN001', username: 'KJONES',   displayName: 'Karen Jones',   email: 'k.jones@apexair.com',    role: 'agent',      status: 'suspended', station: 'MAN', lastLogin: '2026-03-10 11:00' },
    { id: 'EDI001', username: 'RPATEL',   displayName: 'Raj Patel',     email: 'r.patel@apexair.com',    role: 'agent',      status: 'active',    station: 'EDI', lastLogin: '2026-03-20 07:50' },
    { id: 'LH0001', username: 'ADMIN',    displayName: 'System Admin',  email: 'admin@apexair.com',      role: 'admin',      status: 'active',    station: 'LHR', lastLogin: '2026-03-20 06:00' },
    { id: 'GLA001', username: 'TLEE',     displayName: 'Tracy Lee',     email: 't.lee@apexair.com',      role: 'agent',      status: 'pending',   station: 'GLA', lastLogin: 'Never' },
    { id: 'LH1004', username: 'DCLARK',   displayName: 'David Clark',   email: 'd.clark@apexair.com',    role: 'agent',      status: 'active',    station: 'LHR', lastLogin: '2026-03-18 14:22' },
  ]);

  filteredUsers = computed(() => {
    const q = this.search().toLowerCase();
    return this.users().filter(u => {
      const matchText = !q || u.displayName.toLowerCase().includes(q) || u.username.toLowerCase().includes(q) || u.email.toLowerCase().includes(q) || u.station.toLowerCase().includes(q);
      const matchRole = this.roleFilter() === 'all' || u.role === this.roleFilter();
      const matchStatus = this.statusFilter() === 'all' || u.status === this.statusFilter();
      return matchText && matchRole && matchStatus;
    });
  });

  stats = computed(() => ({
    total: this.users().length,
    active: this.users().filter(u => u.status === 'active').length,
    suspended: this.users().filter(u => u.status === 'suspended').length,
    pending: this.users().filter(u => u.status === 'pending').length,
  }));

  setSearch(v: string): void { this.search.set(v); }
  setRoleFilter(v: string): void { this.roleFilter.set(v as 'all' | UserRole); }
  setStatusFilter(v: string): void { this.statusFilter.set(v as 'all' | UserStatus); }

  selectUser(u: AgentRecord): void {
    this.selectedUser.set(this.selectedUser()?.id === u.id ? null : u);
  }

  toggleStatus(u: AgentRecord): void {
    const next: UserStatus = u.status === 'active' ? 'suspended' : 'active';
    this.users.update(list => list.map(x => x.id === u.id ? { ...x, status: next } : x));
    if (this.selectedUser()?.id === u.id) {
      this.selectedUser.update(sel => sel ? { ...sel, status: next } : null);
    }
  }

  resetPassword(u: AgentRecord): void {
    // In future: call API
    alert(`Password reset email sent to ${u.email}`);
  }

  addUser(): void {
    const n = this.newUser();
    if (!n.username || !n.displayName || !n.email) return;
    const id = `${n.station}${String(1000 + this.users().length + 1).slice(1)}`;
    this.users.update(l => [...l, {
      id,
      username: n.username.toUpperCase(),
      displayName: n.displayName,
      email: n.email,
      role: n.role,
      status: 'pending',
      station: n.station,
      lastLogin: 'Never',
    }]);
    this.newUser.set({ username: '', displayName: '', email: '', role: 'agent', station: 'LHR' });
    this.showAddForm.set(false);
  }

  setNewUser(field: string, val: string): void {
    this.newUser.update(u => ({ ...u, [field]: val }));
  }

  roleBadgeClass(role: UserRole): string {
    return { agent: 'badge-agent', supervisor: 'badge-super', admin: 'badge-admin' }[role] ?? '';
  }

  statusBadgeClass(status: UserStatus): string {
    return { active: 'badge-active', suspended: 'badge-suspended', pending: 'badge-pending' }[status] ?? '';
  }
}
