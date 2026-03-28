import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environment';

export interface UserAccount {
  userId: string;
  username: string;
  email: string;
  firstName: string;
  lastName: string;
  isActive: boolean;
  isLocked: boolean;
  lastLoginAt: string | null;
  createdAt: string;
}

export interface CreateUserRequest {
  username: string;
  email: string;
  password: string;
  firstName: string;
  lastName: string;
}

export interface CreateUserResponse {
  userId: string;
}

export interface UpdateUserRequest {
  firstName?: string;
  lastName?: string;
  email?: string;
}

@Injectable({ providedIn: 'root' })
export class UserService {
  #http = inject(HttpClient);
  #baseUrl = `${environment.adminApiUrl}/api/v1/admin/users`;

  async getUsers(): Promise<UserAccount[]> {
    return firstValueFrom(
      this.#http.get<UserAccount[]>(this.#baseUrl)
    );
  }

  async getUser(userId: string): Promise<UserAccount> {
    return firstValueFrom(
      this.#http.get<UserAccount>(`${this.#baseUrl}/${userId}`)
    );
  }

  async createUser(request: CreateUserRequest): Promise<CreateUserResponse> {
    return firstValueFrom(
      this.#http.post<CreateUserResponse>(this.#baseUrl, request)
    );
  }

  async updateUser(userId: string, data: UpdateUserRequest): Promise<void> {
    await firstValueFrom(
      this.#http.patch(`${this.#baseUrl}/${userId}`, data)
    );
  }

  async setUserStatus(userId: string, isActive: boolean): Promise<void> {
    await firstValueFrom(
      this.#http.patch(`${this.#baseUrl}/${userId}/status`, { isActive })
    );
  }

  async unlockUser(userId: string): Promise<void> {
    await firstValueFrom(
      this.#http.post(`${this.#baseUrl}/${userId}/unlock`, {})
    );
  }

  async resetPassword(userId: string, newPassword: string): Promise<void> {
    await firstValueFrom(
      this.#http.post(`${this.#baseUrl}/${userId}/reset-password`, { newPassword })
    );
  }

  async deleteUser(userId: string): Promise<void> {
    await firstValueFrom(
      this.#http.delete(`${this.#baseUrl}/${userId}`)
    );
  }
}
