# Browser Caching Service

## Overview

The `BrowserCachingService` provides persistent caching for the MakaMek WASM application using **IndexedDB** through JavaScript interop. This replaces the previous in-memory implementation that lost all cached data between browser sessions.

## Implementation Details

### Architecture

1. **C# Service** (`BrowserCachingService.cs`):
   - Implements `IFileCachingService` interface
   - Uses .NET 9 JS interop with `[JSImport]` attributes
   - Handles initialization and error handling
   - Provides async methods for cache operations

2. **JavaScript Module** (`wwwroot/cacheStorage.js`):
   - Manages IndexedDB database (`MakaMekCache`)
   - Stores cached files as `Uint8Array` (byte arrays)
   - Provides CRUD operations for cache entries
   - Handles database initialization and upgrades

### Key Features

- **Persistent Storage**: Data persists across browser sessions
- **Async Operations**: All operations are asynchronous and non-blocking
- **Error Handling**: Graceful fallback on errors (returns null/false)
- **Type Safety**: Uses byte arrays for binary data storage
- **Thread Safety**: Uses `SemaphoreSlim` for initialization synchronization

### Cache Operations

- `TryGetCachedFile(string cacheKey)`: Retrieves cached file as byte array
- `SaveToCache(string cacheKey, byte[] content)`: Stores file in IndexedDB
- `IsCached(string cacheKey)`: Checks if file exists without loading it
- `RemoveFromCache(string cacheKey)`: Removes specific cached file
- `ClearCache()`: Removes all cached files

### Browser Compatibility

IndexedDB is supported in all modern browsers:
- Chrome/Edge: Full support
- Firefox: Full support
- Safari: Full support
- Opera: Full support

### Storage Limits

IndexedDB storage limits vary by browser:
- Chrome/Edge: ~60% of available disk space
- Firefox: ~50% of available disk space
- Safari: ~1GB (with user prompt for more)

### Usage

The service is automatically registered in the DI container via `BrowserServices.RegisterBrowserServices()` and used by `GitHubResourceStreamProvider` for caching downloaded unit files.

### Testing

To verify the cache is working:
1. Run the WASM app in browser
2. Download some unit files (they will be cached)
3. Refresh the page
4. The files should load from cache (no network requests)
5. Check browser DevTools > Application > IndexedDB > MakaMekCache

### Debugging

Use browser DevTools to inspect the cache:
- **Chrome/Edge**: DevTools > Application > Storage > IndexedDB > MakaMekCache
- **Firefox**: DevTools > Storage > IndexedDB > MakaMekCache
- **Safari**: Web Inspector > Storage > IndexedDB > MakaMekCache

You can also call `getCacheStats()` from the browser console to see cache statistics.
