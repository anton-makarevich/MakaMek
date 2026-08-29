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

## R2 Release Pipeline

Unit and terrain assets under `data/` are also published to a **Cloudflare R2 bucket** by a dedicated release workflow (`.github/workflows/deploy-data-release.yml`). Git remains the source of truth; the bucket is a flat mirror of the `data/` folder at the last released tag.

The bucket is always **flat** — there are no versioned subfolders and no per-release copies. Re-releases simply sync the single copy in place. Versioning happens at the **bucket level**: a breaking change (one the app's manifest/format can't read) is published to a **new bucket**, and the app's base URL is switched to point at it. Within any given bucket the layout stays flat.

### Pipeline Flow

0. **Bucket provisioning**: the R2 bucket is created upfront via Pulumi (`src/MakaMek.Infra/MakaMek.Infra.Data`, run by `.github/workflows/infra-data.yml`, Cloudflare provider). See [hub-deployment.md](hub-deployment.md) for the same ad-hoc workflow pattern. A **new** bucket (and corresponding base URL) is provisioned whenever a breaking change ships; non-breaking releases reuse the existing flat bucket.
1. **Trigger**: push of a `v*` tag, or a manual `workflow_dispatch` run.
2. **Manifest generation**: `.github/scripts/generate-data-manifest.cs` (a .NET 10 file-based app run via `dotnet run --file`) scans `data/` recursively and writes `manifest.json` to the workspace root.
3. **Upload**: `aws s3 sync` (S3-compatible endpoint, region `auto`) mirrors the whole `data/` folder into the bucket with `--delete`, so removed files disappear from the bucket. `manifest.json` is excluded from the sync (it lives outside `data/`, so `--delete` would otherwise treat the root copy as remote-only and remove it) and is then published to the bucket root after the sync completes.

Configuration uses repository secrets (`R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY`, `CLOUDFLARE_ACCOUNT_ID`, `CLOUDFLARE_R2_BUCKET`) and the public base URL variable `vars.DATA_R2_BASE_URL`.

### manifest.json Schema

```json
{
  "version": "0.63.6",
  "generatedAtUtc": "2026-08-27T00:00:00.000Z",
  "fileCount": 200,
  "files": [
    {
      "path": "units/mechs/Atlas.mmux",
      "name": "Atlas.mmux",
      "hash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "url": "https://<data-r2-domain>/units/mechs/Atlas.mmux"
    }
  ]
}
```

#### Fields

Top level:

- **version**: App release version (`<VersionPrefix>` from `Directory.Build.props`, picked up by the deploy pipeline and passed to the manifest generator). Lets clients detect when a new app release ships updated downloadable content.
- **generatedAtUtc**: ISO-8601 timestamp of generation
- **fileCount**: Number of entries in `files`
- **files**: Array of file entries

Per-file:

- **path**: Path relative to `data/` (forward slashes, includes subfolders)
- **name**: File name (last segment of `path`)
- **hash**: SHA-256 of the raw file bytes (used as an opaque cache-version marker)
- **url**: Public download URL (`DATA_R2_BASE_URL` + `/` + `path`)

> This top-level manifest is unrelated to the per-package `manifest.json` inside each `.mmux`/`.mmtx` archive — those schemas are unchanged.

### Current Status

The existing GitHub-based runtime path (`GitHubResourceStreamProvider`) stays active until the application-side switch to R2 is implemented (tracked separately). This pipeline realizes the "CDN Integration" future enhancement: once switched, the app consumes `manifest.json` from the bucket and downloads files via their `url` field.

## Possible Future Enhancements

- **Delta Updates**: Only download changed files
- **Delta Updates**: Only download changed files
- **Compression**: Reduce storage size
- **Preloading**: Background content preparation
