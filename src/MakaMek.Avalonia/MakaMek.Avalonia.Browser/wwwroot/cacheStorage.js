// IndexedDB-based persistent cache storage for MakaMek WASM app
// This provides persistent caching across browser sessions

const DB_NAME = 'MakaMekCache';
const DB_VERSION = 1;
const STORE_NAME = 'fileCache';

let dbPromise = null;

// Initialize the IndexedDB database
function initDB() {
    if (dbPromise) return dbPromise;
    
    dbPromise = new Promise((resolve, reject) => {
        const request = indexedDB.open(DB_NAME, DB_VERSION);
        
        request.onerror = () => {
            console.error('Failed to open IndexedDB:', request.error);
            reject(request.error);
        };
        
        request.onsuccess = () => {
            resolve(request.result);
        };
        
        request.onupgradeneeded = (event) => {
            const db = event.target.result;
            
            // Create object store if it doesn't exist
            if (!db.objectStoreNames.contains(STORE_NAME)) {
                db.createObjectStore(STORE_NAME);
            }
        };
    });
    
    return dbPromise;
}

// Save data to cache
export async function saveToCache(cacheKey, dataArray) {
    try {
        const db = await initDB();
        
        return new Promise((resolve, reject) => {
            const transaction = db.transaction([STORE_NAME], 'readwrite');
            const store = transaction.objectStore(STORE_NAME);
            
            // Convert the .NET byte array to Uint8Array for storage
            const uint8Array = new Uint8Array(dataArray);
            const request = store.put(uint8Array, cacheKey);
            
            request.onsuccess = () => resolve(true);
            request.onerror = () => {
                console.error('Failed to save to cache:', request.error);
                reject(request.error);
            };
        });
    } catch (error) {
        console.error('Error in saveToCache:', error);
        return false;
    }
}

// Get data from cache
export async function getFromCache(cacheKey) {
    try {
        const db = await initDB();
        
        return new Promise((resolve, reject) => {
            const transaction = db.transaction([STORE_NAME], 'readonly');
            const store = transaction.objectStore(STORE_NAME);
            const request = store.get(cacheKey);
            
            request.onsuccess = () => {
                const result = request.result;
                if (result) {
                    // Return the raw Uint8Array for proper marshalling to .NET
                    resolve(result);
                } else {
                    // Return empty array instead of null for .NET interop compatibility
                    resolve([]);
                }
            };
            
            request.onerror = () => {
                console.error('Failed to get from cache:', request.error);
                reject(request.error);
            };
        });
    } catch (error) {
        console.error('Error in getFromCache:', error);
        // Return empty array on error for .NET interop compatibility
        return [];
    }
}

// Check if a key exists in cache
export async function isCached(cacheKey) {
    try {
        const db = await initDB();
        
        return new Promise((resolve, reject) => {
            const transaction = db.transaction([STORE_NAME], 'readonly');
            const store = transaction.objectStore(STORE_NAME);
            const request = store.getKey(cacheKey);
            
            request.onsuccess = () => {
                resolve(request.result !== undefined);
            };
            
            request.onerror = () => {
                console.error('Failed to check cache:', request.error);
                reject(request.error);
            };
        });
    } catch (error) {
        console.error('Error in isCached:', error);
        return false;
    }
}

// Remove a specific item from cache
export async function removeFromCache(cacheKey) {
    try {
        const db = await initDB();
        
        return new Promise((resolve, reject) => {
            const transaction = db.transaction([STORE_NAME], 'readwrite');
            const store = transaction.objectStore(STORE_NAME);
            const request = store.delete(cacheKey);
            
            request.onsuccess = () => resolve(true);
            request.onerror = () => {
                console.error('Failed to remove from cache:', request.error);
                reject(request.error);
            };
        });
    } catch (error) {
        console.error('Error in removeFromCache:', error);
        return false;
    }
}

// Clear all cached data
export async function clearCache() {
    try {
        const db = await initDB();
        
        return new Promise((resolve, reject) => {
            const transaction = db.transaction([STORE_NAME], 'readwrite');
            const store = transaction.objectStore(STORE_NAME);
            const request = store.clear();
            
            request.onsuccess = () => resolve(true);
            request.onerror = () => {
                console.error('Failed to clear cache:', request.error);
                reject(request.error);
            };
        });
    } catch (error) {
        console.error('Error in clearCache:', error);
        return false;
    }
}

// Get cache statistics (useful for debugging)
export async function getCacheStats() {
    try {
        const db = await initDB();
        
        return new Promise((resolve, reject) => {
            const transaction = db.transaction([STORE_NAME], 'readonly');
            const store = transaction.objectStore(STORE_NAME);
            const request = store.count();
            
            request.onsuccess = () => {
                resolve({ itemCount: request.result });
            };
            
            request.onerror = () => {
                console.error('Failed to get cache stats:', request.error);
                reject(request.error);
            };
        });
    } catch (error) {
        console.error('Error in getCacheStats:', error);
        return { itemCount: 0 };
    }
}

// Unwrap JSObject back to byte array for .NET interop
// This is needed because Task<byte[]> is not supported in JS interop
export function unwrapByteArray(jsObject) {
    if (jsObject && (jsObject.constructor.name === 'Uint8Array' || Array.isArray(jsObject))) {
        return Array.from(jsObject);
    }
    return [];
}
