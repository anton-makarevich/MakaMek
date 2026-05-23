# MakaMek.Services.Avalonia.Browser

Browser/WASM-specific service implementations for MakaMek.

## Contents

- `BrowserCachingService` — IndexedDB-based file caching via `IFileCachingService`

## Requirements

This library requires the `cacheStorage.js` file to be deployed alongside your WASM app.
It is automatically included as a linked content item when consuming via NuGet.
For project references, you must add it manually in the consuming project:

```xml
<Content Include="..\MakaMek.Services.Avalonia.Browser\wwwroot\cacheStorage.js" Link="wwwroot\cacheStorage.js" />
```

The JavaScript file provides the IndexedDB interop layer that the `BrowserCachingService` depends on.
