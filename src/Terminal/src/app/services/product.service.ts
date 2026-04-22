import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environment';

export interface ProductPrice {
  priceId: string;
  productId: string;
  offerId: string;
  currencyCode: string;
  price: number;
  tax: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface Product {
  productId: string;
  productGroupId: string;
  name: string;
  description: string;
  isSegmentSpecific: boolean;
  ssrCode: string | null;
  imageBase64: string | null;
  availableChannels: string;
  isActive: boolean;
  prices: ProductPrice[];
  createdAt: string;
  updatedAt: string;
}

export const ALL_CHANNELS = ['WEB', 'APP', 'NDC', 'KIOSK', 'CC', 'AIRPORT'] as const;
export type ChannelCode = typeof ALL_CHANNELS[number];
export const ALL_CHANNELS_JSON = JSON.stringify(ALL_CHANNELS);

export interface CreateProductRequest {
  productGroupId: string;
  name: string;
  description: string;
  isSegmentSpecific: boolean;
  ssrCode?: string | null;
  imageBase64?: string | null;
  availableChannels: string;
}

export interface UpdateProductRequest {
  productGroupId: string;
  name: string;
  description: string;
  isSegmentSpecific: boolean;
  ssrCode?: string | null;
  imageBase64?: string | null;
  availableChannels: string;
  isActive: boolean;
}

export interface CreateProductPriceRequest {
  currencyCode: string;
  price: number;
  tax: number;
}

export interface UpdateProductPriceRequest {
  price: number;
  tax: number;
  isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class ProductService {
  #http = inject(HttpClient);
  #baseUrl = `${environment.operationsApiUrl}/api/v1/admin/products`;

  async getAll(): Promise<Product[]> {
    return firstValueFrom(this.#http.get<Product[]>(this.#baseUrl));
  }

  async getById(productId: string): Promise<Product> {
    return firstValueFrom(this.#http.get<Product>(`${this.#baseUrl}/${productId}`));
  }

  async create(request: CreateProductRequest): Promise<Product> {
    return firstValueFrom(this.#http.post<Product>(this.#baseUrl, request));
  }

  async update(productId: string, request: UpdateProductRequest): Promise<Product> {
    return firstValueFrom(this.#http.put<Product>(`${this.#baseUrl}/${productId}`, request));
  }

  async delete(productId: string): Promise<void> {
    await firstValueFrom(this.#http.delete(`${this.#baseUrl}/${productId}`));
  }

  async createPrice(productId: string, request: CreateProductPriceRequest): Promise<ProductPrice> {
    return firstValueFrom(
      this.#http.post<ProductPrice>(`${this.#baseUrl}/${productId}/prices`, request)
    );
  }

  async updatePrice(productId: string, priceId: string, request: UpdateProductPriceRequest): Promise<ProductPrice> {
    return firstValueFrom(
      this.#http.put<ProductPrice>(`${this.#baseUrl}/${productId}/prices/${priceId}`, request)
    );
  }

  async deletePrice(productId: string, priceId: string): Promise<void> {
    await firstValueFrom(this.#http.delete(`${this.#baseUrl}/${productId}/prices/${priceId}`));
  }
}
