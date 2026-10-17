const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

const splashStatus = document.getElementById("splash-status");
const splashProgressFill = document.getElementById("splash-progress-fill");
const splashPercent = document.getElementById("splash-percent");
const splashBar = document.querySelector(".splash-progress");
const splashError = document.getElementById("splash-error");
const splashRoot = document.querySelector(".avalonia-splash");

function setStatus(text) {
    if (splashStatus) {
        splashStatus.textContent = text;
    }
}

function setProgress(loaded, total) {
    if (total > 0) {
        const pct = Math.min(100, Math.max(0, (loaded / total) * 100)).toFixed(0);
        if (splashProgressFill) {
            splashProgressFill.style.width = pct + "%";
        }
        if (splashPercent) {
            splashPercent.textContent = pct + "%";
        }
        if (splashBar) {
            splashBar.setAttribute("aria-valuenow", pct);
        }
    }
}

function showError(message) {
    if (splashRoot) {
        splashRoot.classList.add("splash-error");
    }
    if (splashError) {
        splashError.style.display = "block";
        splashError.innerHTML = message;
    }
    if (splashPercent) {
        splashPercent.textContent = "0%";
    }
}

try {
    const { dotnet } = await import('./_framework/dotnet.js');

    const dotnetRuntime = await dotnet
        .withDiagnosticTracing(false)
        .withApplicationArgumentsFromQuery()
        .withModuleConfig({
            onDownloadResourceProgress: (loaded, total) => {
                setProgress(loaded, total);
                if (total > 0) {
                    setStatus(`Downloading application (${loaded} of ${total} files)…`);
                }
            }
        })
        .create();

    const config = dotnetRuntime.getConfig();
    setProgress(100, 100);
    setStatus("Starting MakaMek…");
    await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
} catch (error) {
    console.error(error);
    showError("Failed to load MakaMek. Check your connection and reload the page. <a href=\"./\">Reload</a>");
}
