import { Injectable, signal } from '@angular/core';

export interface HttpLogEntry {
  id: number;
  timestamp: string;
  method: string;
  url: string;
  requestHeaders: Record<string, string>;
  requestBody: unknown;
  responseStatus: number | null;
  responseHeaders: Record<string, string>;
  responseBody: unknown;
  durationMs: number | null;
  isError: boolean;
}

@Injectable({ providedIn: 'root' })
export class HttpDebugService {
  private readonly _entries = signal<HttpLogEntry[]>([]);
  readonly entries = this._entries.asReadonly();
  private _nextId = 0;

  log(entry: Omit<HttpLogEntry, 'id'>): void {
    this._entries.update(list => [{ id: this._nextId++, ...entry }, ...list]);
  }

  clear(): void {
    this._entries.set([]);
    this._nextId = 0;
  }
}
