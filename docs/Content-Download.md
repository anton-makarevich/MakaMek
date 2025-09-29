# Content Download and Caching System

## Overview

MakaMek uses a content downloading and caching system to handle unit data files that are not included in the application binary. This system ensures efficient loading while providing persistent storage across sessions.

## Architecture

### Core Components

1. **MainMenuViewModel** - Entry point for content loading
2. **UnitCachingService** - Orchestrates unit loading and caching
3. **GitHubResourceStreamProvider** - Downloads content from GitHub
4. **IFileCachingService** - Platform-specific caching implementations

## Content Flow

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│ MainMenuViewModel│───▶│UnitCachingService│───▶│GitHubResource   │
│                 │    │                  │    │StreamProvider   │
└─────────────────┘    └──────────────────┘    └─────────────────┘
                                │                        │
                                ▼                        ▼
                       ┌──────────────────┐    ┌─────────────────┐
                       │  Caches unit     │    │IFileCaching     │
                       │  data/images     │    │Service          │
                       └──────────────────┘    └─────────────────┘
```

## Unit Data Format

### MMUX Packages

Unit data is distributed as **MMUX packages** (MakaMek Unit eXchange format):

```
unit.mmux
├── unit.json    # Unit specifications (JSON)
└── unit.png     # Unit sprite image (PNG)
```

**unit.json** contains:
- Model name and specifications
- Component definitions
- Movement and combat stats
- Weight class and type information

## Platform-Specific Implementations

### Desktop/Mobile (FileSystemCachingService)

### WebAssembly Browser (BrowserCachingService)

```csharp
// IndexedDB-based persistent caching
// Same SHA256 hash logic for consistency
// Data stored as Uint8Array in IndexedDB

[JSImport("getFromCache", "cacheStorage")]
[return: JSMarshalAs<JSType.Promise<JSType.Object>>()]
private static partial Task<JSObject> GetFromCacheAsObjectJs(string cacheKey);
```

## Caching Strategy

### Cache Key Generation

Both implementations use **identical SHA256 hashing**:

```csharp
private static string GetHashedCacheKey(string originalKey)
{
    var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(originalKey));
    return Convert.ToHexString(hashBytes).ToLowerInvariant();
}
```

This ensures **cache compatibility** across platforms.

## Initialization Process

### App Startup Sequence

1. **MainMenuViewModel** loads on startup
2. **PreloadUnits()** runs in background
3. **UnitCachingService.EnsureInitialized()** triggers
4. **LoadUnitsFromStreamProviders()** processes each provider
5. **GitHubResourceStreamProvider** downloads unit files
6. **Files cached** for future sessions

### Loading States

```
Loading Content... → Loading Items... → {X} Items Loaded
```

## Error Handling

### Graceful Degradation

- **Network failures**: Continue with cached content
- **Corrupted files**: Skip and continue with other units
- **Provider failures**: Log errors but continue with other providers

## Storage Characteristics

### Desktop/Mobile
- **Location**: User application data directory
- **Format**: Binary files with `.cache` extension
- **Persistence**: Until manually cleared
- **Size**: Limited by available disk space

### Browser (WASM)
- **Location**: IndexedDB (`MakaMekCache` database)
- **Format**: `Uint8Array` objects in `fileCache` store
- **Persistence**: Until browser data cleared
- **Size**: Limited by browser storage quotas (~60% of disk space)

## Browser DevTools Inspection

### IndexedDB Inspection

1. **Open DevTools** (F12)
2. **Go to Application tab**
3. **Navigate to Storage → IndexedDB**
4. **Select MakaMekCache database**
5. **Inspect fileCache object store**

### Console Debugging

```javascript
// Check database contents
const request = indexedDB.open('MakaMekCache');
request.onsuccess = (event) => {
    const db = event.target.result;
    const transaction = db.transaction(['fileCache'], 'readonly');
    const store = transaction.objectStore('fileCache');
    const getAllRequest = store.getAllKeys();
    getAllRequest.onsuccess = () => {
        console.log('Cached files:', getAllRequest.result);
    };
};
```

## Performance Considerations

### Parallel Loading

```csharp
// Process units in parallel batches
var batches = unitIdList.Chunk(MaxDegreeOfParallelism);
foreach (var batch in batches)
{
    var batchTasks = batch.Select(unitId => ProcessUnitAsync(provider, unitId));
    await Task.WhenAll(batchTasks); // Parallel execution
}
```

## Testing and Debugging

### Cache Verification

1. **Clear cache** before testing
2. **Monitor network tab** for initial downloads
3. **Check cache storage** after loading
4. **Refresh page** - should load from cache (no network requests)

### Common Issues

| Issue | Symptom | Solution |
|-------|---------|----------|
| **Cache miss** | Files re-downloaded | Check cache key consistency |
| **Empty cache** | No IndexedDB entries | Verify save operations |
| **Wrong format** | Uint8Array issues | Check marshalling code |
| **Network errors** | Download failures | Check GitHub API access |

## Possible Future Enhancements

- **CDN Integration**: Multiple content sources
- **Delta Updates**: Only download changed files
- **Compression**: Reduce storage size
- **Preloading**: Background content preparation
