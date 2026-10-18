const splashStatus = document.getElementById("splash-status");
const splashProgressFill = document.getElementById("splash-progress-fill");
const splashPercent = document.getElementById("splash-percent");
const splashBar = document.querySelector(".splash-progress");
const splashError = document.getElementById("splash-error");
const splashRoot = document.querySelector(".avalonia-splash");

export function setStatus(text) {
    if (splashStatus) {
        splashStatus.textContent = text;
    }
}

export function setProgress(loaded, total) {
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

export function showError(message) {
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
