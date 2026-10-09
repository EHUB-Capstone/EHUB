// Small in-memory cache of fully downloaded preview documents so that closing and reopening
// the same file is instant. Entries expire quickly (access can change) and the cache is
// emptied on logout, so a document never outlives the session or user that opened it.

import type { PDFDocumentProxy } from 'pdfjs-dist';

export const PREVIEW_CACHE_MAX_ENTRIES = 3;
export const PREVIEW_CACHE_TTL_MS = 10 * 60_000;

interface Entry<T> {
  value: T;
  expiresAt: number;
}

export class PreviewDocumentCache<T> {
  private readonly entries = new Map<string, Entry<T>>();
  private readonly maxEntries: number;
  private readonly onEvict: (key: string, value: T) => void;
  private readonly now: () => number;

  constructor(
    maxEntries: number = PREVIEW_CACHE_MAX_ENTRIES,
    onEvict: (key: string, value: T) => void = () => undefined,
    now: () => number = () => Date.now(),
  ) {
    this.maxEntries = maxEntries;
    this.onEvict = onEvict;
    this.now = now;
  }

  /** Returns a live entry and marks it as most recently used; expired entries are evicted. */
  get(key: string): T | undefined {
    const entry = this.entries.get(key);
    if (!entry) return undefined;
    if (entry.expiresAt <= this.now()) {
      this.delete(key);
      return undefined;
    }

    this.entries.delete(key);
    this.entries.set(key, entry);
    return entry.value;
  }

  /** True when this exact value is the live entry for the key (used to decide whether the owner may destroy it). */
  holds(key: string, value: T): boolean {
    const entry = this.entries.get(key);
    return entry !== undefined && entry.value === value && entry.expiresAt > this.now();
  }

  set(key: string, value: T, ttlMs: number = PREVIEW_CACHE_TTL_MS): void {
    const previous = this.entries.get(key);
    this.entries.delete(key);
    if (previous && previous.value !== value) this.onEvict(key, previous.value);
    this.entries.set(key, { value, expiresAt: this.now() + ttlMs });

    while (this.entries.size > this.maxEntries) {
      const oldest = this.entries.keys().next().value as string;
      this.delete(oldest);
    }
  }

  delete(key: string): void {
    const entry = this.entries.get(key);
    if (!entry) return;
    this.entries.delete(key);
    this.onEvict(key, entry.value);
  }

  clear(): void {
    for (const key of [...this.entries.keys()]) this.delete(key);
  }

  get size(): number {
    return this.entries.size;
  }
}

// Shared by the preview modal and the logout flow. This module must stay free of runtime pdfjs
// imports (type-only above) so that AuthContext can use it without pulling PDF.js into the main bundle.
export const previewDocumentCache = new PreviewDocumentCache<PDFDocumentProxy>(
  PREVIEW_CACHE_MAX_ENTRIES,
  (_key, document) => {
    // Destroying the loading task also releases the document and its worker resources.
    void document.loadingTask.destroy();
  },
);

/** Call on logout so previews opened by one user are never kept for the next. */
export function clearPreviewDocumentCache(): void {
  previewDocumentCache.clear();
}
