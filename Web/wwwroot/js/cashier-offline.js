/**
 * Cashier offline layer.
 *
 * Exposes namespaced modules on window for Blazor JSInterop:
 *   CashierImageCache — Cache API store for product thumbnails, ETag-aware.
 *   CashierIdb        — IndexedDB store: credential, profile, stores, products,
 *                       shifts, orders, sync_queue. Powered by `idb` from CDN.
 *   CashierNetwork    — online/offline event bridge into managed code.
 *
 * This file is standalone; it does NOT interfere with app-utils.js. All state lives
 * inside the module closure or in browser-managed stores (IndexedDB / Cache API).
 */
(function (window) {
    'use strict';

    // ===========================================
    // Image cache — Cache API, ETag-aware
    // ===========================================
    // Stores product thumbnails keyed by URL. The cached Response carries the
    // server ETag in a custom header so we can skip downloads on next sync if
    // the server says the asset hasn't changed.
    const CACHE_NAME = 'cashier-product-thumbnails-v1';
    const ETAG_HEADER = 'x-cashier-cached-etag';

    function isHttpUrl(u) {
        return typeof u === 'string' && (u.startsWith('http://') || u.startsWith('https://') || u.startsWith('/'));
    }

    async function openCache() {
        return await caches.open(CACHE_NAME);
    }

    const CashierImageCache = {
        // products: [{ url, etag }]. Re-downloads only when ETag differs.
        // Returns { synced, skipped, failed, removed } counts.
        async syncImages(products) {
            if (!Array.isArray(products)) return { synced: 0, skipped: 0, failed: 0, removed: 0 };

            const cache = await openCache();
            const wantedUrls = new Set();
            let synced = 0, skipped = 0, failed = 0;

            for (const p of products) {
                if (!p || !isHttpUrl(p.url)) continue;
                wantedUrls.add(p.url);

                const cached = await cache.match(p.url);
                if (cached && p.etag && cached.headers.get(ETAG_HEADER) === p.etag) {
                    skipped++;
                    continue;
                }

                try {
                    const response = await fetch(p.url, { cache: 'no-store' });
                    if (!response.ok) { failed++; continue; }

                    // Re-wrap the response so we can attach our own ETag header.
                    const blob = await response.blob();
                    const headers = new Headers(response.headers);
                    if (p.etag) headers.set(ETAG_HEADER, p.etag);
                    const wrapped = new Response(blob, { status: 200, headers });
                    await cache.put(p.url, wrapped);
                    synced++;
                } catch (e) {
                    failed++;
                }
            }

            const removed = await this._pruneExcept(wantedUrls);
            return { synced, skipped, failed, removed };
        },

        // Returns a blob: URL for the cached image, or null if not cached.
        async getLocalUrl(url) {
            if (!isHttpUrl(url)) return null;
            try {
                const cache = await openCache();
                const cached = await cache.match(url);
                if (!cached) return null;
                const blob = await cached.blob();
                return URL.createObjectURL(blob);
            } catch (e) {
                return null;
            }
        },

        async pruneStale(activeUrls) {
            const wanted = new Set(Array.isArray(activeUrls) ? activeUrls.filter(isHttpUrl) : []);
            return await this._pruneExcept(wanted);
        },

        async _pruneExcept(wantedSet) {
            const cache = await openCache();
            const requests = await cache.keys();
            let removed = 0;
            for (const req of requests) {
                if (!wantedSet.has(req.url)) {
                    if (await cache.delete(req)) removed++;
                }
            }
            return removed;
        }
    };

    // ===========================================
    // IndexedDB store — wraps the `idb` library
    // ===========================================
    // Loads `idb` from a CDN. The cashier panel is the only consumer, so we don't
    // need to ship idb as a Blazor static asset.
    const IDB_CDN = 'https://cdn.jsdelivr.net/npm/idb@8/build/index.js';
    const DB_NAME = 'pos-offline';
    // v2: products store key changed from single 'productId' to compound
    // ['storeId', 'productId'] so the same product cached for multiple assigned
    // stores doesn't overwrite itself. Bump this any time a store's keyPath or
    // indexes change.
    const DB_VERSION = 2;

    let _dbPromise = null;
    let _idbModulePromise = null;

    async function loadIdbModule() {
        if (!_idbModulePromise) {
            _idbModulePromise = import(IDB_CDN).catch(err => {
                _idbModulePromise = null;
                throw err;
            });
        }
        return _idbModulePromise;
    }

    async function getDb() {
        if (!_dbPromise) {
            _dbPromise = (async () => {
                const { openDB } = await loadIdbModule();
                return await openDB(DB_NAME, DB_VERSION, {
                    upgrade(db, oldVersion) {
                        if (oldVersion < 1) {
                            db.createObjectStore('credential', { keyPath: 'userId' });
                            db.createObjectStore('profile', { keyPath: 'userId' });
                            db.createObjectStore('stores', { keyPath: 'storeId' });

                            const shifts = db.createObjectStore('shifts', { keyPath: 'shiftId' });
                            shifts.createIndex('storeId', 'storeId');

                            const orders = db.createObjectStore('orders', { keyPath: 'orderId' });
                            orders.createIndex('storeId', 'storeId');
                            orders.createIndex('storeId_createdAt', ['storeId', 'createdAt']);

                            db.createObjectStore('sync_queue', { keyPath: 'seq', autoIncrement: true });
                        }

                        if (oldVersion < 2) {
                            // Compound key migration for products: the v1 schema
                            // used 'productId' alone, which collided when a
                            // product was cached for multiple stores. Drop and
                            // recreate — the next pull repopulates from the API.
                            if (db.objectStoreNames.contains('products')) {
                                db.deleteObjectStore('products');
                            }
                            const products = db.createObjectStore('products', { keyPath: ['storeId', 'productId'] });
                            products.createIndex('storeId', 'storeId');
                        }
                    }
                });
            })().catch(err => {
                _dbPromise = null;
                throw err;
            });
        }
        return _dbPromise;
    }

    const CashierIdb = {
        async upsert(storeName, value) {
            const db = await getDb();
            await db.put(storeName, value);
            return true;
        },

        async bulkUpsert(storeName, values) {
            if (!Array.isArray(values) || values.length === 0) return 0;
            const db = await getDb();
            const tx = db.transaction(storeName, 'readwrite');
            await Promise.all(values.map(v => tx.store.put(v)));
            await tx.done;
            return values.length;
        },

        async getByKey(storeName, key) {
            const db = await getDb();
            const value = await db.get(storeName, key);
            return value ?? null;
        },

        async getAll(storeName) {
            const db = await getDb();
            return await db.getAll(storeName);
        },

        async getByIndex(storeName, indexName, key) {
            const db = await getDb();
            return await db.getAllFromIndex(storeName, indexName, key);
        },

        async deleteByKey(storeName, key) {
            const db = await getDb();
            await db.delete(storeName, key);
            return true;
        },

        async clear(storeName) {
            const db = await getDb();
            await db.clear(storeName);
            return true;
        },

        // Atomically replace a store's contents in a single transaction. If any
        // put fails the whole transaction aborts and the store stays as it was,
        // so callers can't end up with a half-wiped cache. Used by the pull path
        // to swap in fresh server data without a window where the store is empty.
        async replaceAll(storeName, values) {
            const db = await getDb();
            const tx = db.transaction(storeName, 'readwrite');
            await tx.store.clear();
            if (Array.isArray(values) && values.length > 0) {
                await Promise.all(values.map(v => tx.store.put(v)));
            }
            await tx.done;
            return Array.isArray(values) ? values.length : 0;
        },

        // Appends a record using the auto-incrementing key and returns the assigned seq.
        // If the value carries the keyPath field set to null/undefined (System.Text.Json
        // does that for `long?` properties), strip it — IDB rejects an explicit null
        // even on stores that would otherwise auto-assign.
        async append(storeName, value) {
            const db = await getDb();
            const tx = db.transaction(storeName, 'readwrite');
            const keyPath = tx.store.keyPath;
            let toAdd = value;
            if (value && typeof value === 'object' && typeof keyPath === 'string' && value[keyPath] == null) {
                toAdd = { ...value };
                delete toAdd[keyPath];
            }
            const seq = await tx.store.add(toAdd);
            await tx.done;
            return seq;
        },

        async count(storeName) {
            const db = await getDb();
            return await db.count(storeName);
        }
    };

    // ===========================================
    // Network event bridge — calls back into Blazor on online/offline transitions.
    // ===========================================
    // We expose a tiny attach/detach API so the CashierLayout can subscribe without
    // having to manage raw window listeners from C#.
    const CashierNetwork = {
        _handlers: new Map(),

        attach(token, dotNetRef) {
            if (!dotNetRef || this._handlers.has(token)) return false;
            const onOnline = () => { try { dotNetRef.invokeMethodAsync('OnOnline'); } catch (e) { } };
            const onOffline = () => { try { dotNetRef.invokeMethodAsync('OnOffline'); } catch (e) { } };
            window.addEventListener('online', onOnline);
            window.addEventListener('offline', onOffline);
            this._handlers.set(token, { onOnline, onOffline });
            return true;
        },

        detach(token) {
            const h = this._handlers.get(token);
            if (!h) return false;
            window.removeEventListener('online', h.onOnline);
            window.removeEventListener('offline', h.onOffline);
            this._handlers.delete(token);
            return true;
        },

        isOnline() {
            return typeof navigator !== 'undefined' && navigator.onLine !== false;
        }
    };

    // ===========================================
    // Export
    // ===========================================
    window.CashierImageCache = CashierImageCache;
    window.CashierIdb = CashierIdb;
    window.CashierNetwork = CashierNetwork;
})(window);
