import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environment';

export interface ProductGroup {
  productGroupId: string;
  name: string;
  sortOrder: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateProductGroupRequest {
  name: string;
  sortOrder: number;
}

export interface UpdateProductGroupRequest {
  name: string;
  sortOrder: number;
  isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class ProductGroupService {
  #http = inject(HttpClient);
  #baseUrl = `${environment.operationsApiUrl}/api/v1/admin/product-groups`;

  async getAll(): Promise<ProductGroup[]> {
    return firstValueFrom(this.#http.get<ProductGroup[]>(this.#baseUrl));
  }

  async getById(groupId: string): Promise<ProductGroup> {
    return firstValueFrom(this.#http.get<ProductGroup>(`${this.#baseUrl}/${groupId}`));
  }

  async create(request: CreateProductGroupRequest): Promise<ProductGroup> {
    return firstValueFrom(this.#http.post<ProductGroup>(this.#baseUrl, request));
  }

  async update(groupId: string, request: UpdateProductGroupRequest): Promise<ProductGroup> {
    return firstValueFrom(this.#http.put<ProductGroup>(`${this.#baseUrl}/${groupId}`, request));
  }

  async delete(groupId: string): Promise<void> {
    await firstValueFrom(this.#http.delete(`${this.#baseUrl}/${groupId}`));
  }
}
