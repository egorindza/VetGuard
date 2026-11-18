(function () {
    // 1. Светлая / Темная тема
    window.toggleVetGuardTheme = function () {
        const html = document.documentElement;
        const current = html.getAttribute("data-theme") || "light";
        const next = current === "dark" ? "light" : "dark";
        html.setAttribute("data-theme", next);
        html.setAttribute("data-bs-theme", next);
        localStorage.setItem("vetguard-theme", next);
        updateThemeLabel(next);
    };

    function updateThemeLabel(theme) {
        const btn = document.getElementById("vg-theme-btn");
        if (btn) {
            btn.textContent = theme === "dark" ? "☀️ Светлая" : "🌙 Тёмная";
        }
    }

    // 2. Живые таймеры обратного отсчета карантина (обновляются каждую секунду)
    function tickQuarantineTimers() {
        const nodes = document.querySelectorAll("[data-countdown-utc]");
        const nowMs = Date.now();

        nodes.forEach(function (el) {
            const iso = el.getAttribute("data-countdown-utc");
            if (!iso) return;
            const targetMs = Date.parse(iso);
            if (isNaN(targetMs)) return;

            const diffSec = Math.floor((targetMs - nowMs) / 1000);
            if (diffSec <= 0) {
                el.textContent = "Срок выведения истёк ✓";
                el.classList.remove("vg-timer-pill");
                el.classList.add("vg-badge-healthy");
                return;
            }

            const hours = Math.floor(diffSec / 3600);
            const mins = Math.floor((diffSec % 3600) / 60);
            const secs = diffSec % 60;
            const pad = (n) => (n < 10 ? "0" + n : "" + n);
            el.textContent = `${hours} ч ${pad(mins)} м ${pad(secs)} с`;
        });
    }

    // 3. Управление HTMX модальным окном
    window.openVgModal = function () {
        const backdrop = document.getElementById("vg-modal-backdrop");
        if (backdrop) backdrop.classList.add("open");
    };

    window.closeVgModal = function () {
        const backdrop = document.getElementById("vg-modal-backdrop");
        const content = document.getElementById("vg-modal-content");
        if (backdrop) backdrop.classList.remove("open");
        if (content) content.innerHTML = "";
    };

    document.addEventListener("htmx:afterSwap", function (evt) {
        if (evt.detail && evt.detail.target && evt.detail.target.id === "vg-modal-content") {
            window.openVgModal();
        }
        tickQuarantineTimers();
    });

    // 4. SignalR живые оповещения о карантинах и блокировках партий
    function showSignalRToast(title, bodyHtml, variant) {
        const container = document.getElementById("signalr-toast-container");
        if (!container) return;

        const item = document.createElement("div");
        item.className = "vg-live-toast " + (variant || "");
        item.innerHTML = `
            <div class="d-flex justify-content-between align-items-start gap-2 mb-1">
                <strong class="small text-uppercase">${title}</strong>
                <button type="button" class="btn-close btn-sm" aria-label="Close"></button>
            </div>
            <div class="small">${bodyHtml}</div>
        `;
        item.querySelector("button").addEventListener("click", function () {
            item.remove();
        });

        container.appendChild(item);
        setTimeout(function () {
            if (item.parentNode) item.remove();
        }, 12000);
    }

    function initSignalR() {
        if (typeof signalR === "undefined") return;

        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/vet-alerts")
            .withAutomaticReconnect()
            .build();

        connection.on("QuarantineLockActivated", function (data) {
            showSignalRToast(
                "🔴 АКТИВИРОВАН КАРАНТИННЫЙ ЗАМОК",
                `Животное <b>${data.animalName} [${data.animalTag}]</b> получило препарат «<b>${data.drugName}</b>».<br/>` +
                `Браковка молока: <b>${data.milkHours} ч</b> (до ${data.clearanceText} ИКТ).`,
                "toast-warning"
            );
        });

        connection.on("QuarantineViolationBlocked", function (data) {
            showSignalRToast(
                "🚨 ЗАБЛОКИРОВАНА СДАЧА КАРАНТИННОГО СЫРЬЯ!",
                `Партия <b>${data.batchNumber}</b> отклонена!<br/>` +
                `Корова <b>${data.animalName} [${data.animalTag}]</b> под действием «<b>${data.drugName}</b>» ` +
                `(осталось <b>${data.remainingHours} ч</b> карантина). Оператор: ${data.attemptedBy}.`,
                "toast-danger"
            );
        });

        connection.on("QuarantineExpired", function (data) {
            showSignalRToast(
                "🟢 ПЕРИОД ВЫВЕДЕНИЯ ЗАВЕРШЕН",
                `У животного <b>${data.animalName} [${data.animalTag}]</b> полностью выведен препарат «${data.drugName}». Допуск открыт.`,
                "toast-success"
            );
        });

        connection.on("EpizooticReminder", function (data) {
            showSignalRToast(
                "💉 ПРОТИВОЭПИЗООТИЧЕСКИЙ ПЛАНЕР",
                data.message,
                ""
            );
        });

        connection.start().then(function () {
            const badge = document.getElementById("signalr-status-text");
            if (badge) badge.textContent = "SignalR Live";
        }).catch(function () {
            const badge = document.getElementById("signalr-status-text");
            if (badge) badge.textContent = "SignalR Offline";
        });
    }

    document.addEventListener("DOMContentLoaded", function () {
        const currentTheme = document.documentElement.getAttribute("data-theme") || "light";
        updateThemeLabel(currentTheme);
        tickQuarantineTimers();
        setInterval(tickQuarantineTimers, 1000);
        initSignalR();
    });
})();
