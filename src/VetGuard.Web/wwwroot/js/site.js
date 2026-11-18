(function () {
    // 1. Закрытие модальных окон и мобильного меню по Escape
    document.addEventListener("keydown", function (event) {
        if (event.key !== "Escape") return;

        const cmdk = document.getElementById("cmdkBackdrop");
        if (cmdk && cmdk.classList.contains("open")) {
            cmdk.classList.remove("open");
            return;
        }

        const vgModal = document.getElementById("vg-modal-backdrop");
        if (vgModal && vgModal.classList.contains("open")) {
            window.closeVgModal();
            return;
        }

        const menu = document.getElementById("mainNavigation");
        const toggle = document.querySelector(".menu-toggle");
        if (menu && toggle && menu.classList.contains("show")) {
            bootstrap.Collapse.getOrCreateInstance(menu, { toggle: false }).hide();
            toggle.focus();
        }
    });

    // 2. Светлая / Тёмная тема оформления (единый стиль с ITS AgroVision V2)
    function updateThemeLabel(theme) {
        const btn = document.getElementById("themeToggleBtn");
        if (btn) {
            btn.textContent = theme === "dark" ? "☀ Светлая" : "☾ Тёмная";
        }
    }

    window.toggleVetGuardTheme = function () {
        const html = document.documentElement;
        const current = html.getAttribute("data-theme") || "light";
        const next = current === "dark" ? "light" : "dark";
        html.setAttribute("data-theme", next);
        html.setAttribute("data-bs-theme", next);
        try { localStorage.setItem("vetguard-theme", next); } catch { }
        updateThemeLabel(next);
    };

    // 3. Живые таймеры обратного отсчета карантина (обновляются каждую секунду)
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

    // 4. HTMX модальные окна
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

    // 5. Интерактивные всплывающие подсказки SVG-графиков и схем
    (function () {
        let tooltip = null;
        function ensureTooltip() {
            if (!tooltip) {
                tooltip = document.createElement("div");
                tooltip.className = "svg-chart-tooltip";
                document.body.appendChild(tooltip);
            }
            return tooltip;
        }

        document.addEventListener("mouseover", function (e) {
            const target = e.target.closest && e.target.closest("[data-tip]");
            if (!target) return;
            const text = target.getAttribute("data-tip");
            if (!text) return;
            const el = ensureTooltip();
            el.textContent = text;
            el.classList.add("visible");
        });

        document.addEventListener("mousemove", function (e) {
            if (!tooltip || !tooltip.classList.contains("visible")) return;
            tooltip.style.left = Math.min(window.innerWidth - 260, e.clientX + 14) + "px";
            tooltip.style.top = Math.max(10, e.clientY - 38) + "px";
        });

        document.addEventListener("mouseout", function (e) {
            const target = e.target.closest && e.target.closest("[data-tip]");
            if (target && tooltip) tooltip.classList.remove("visible");
        });
    })();

    // 6. Академический экспорт графиков удоев и привесов (PNG 300 DPI / SVG)
    function initAcademicChartExport() {
        const svgs = document.querySelectorAll("svg.vet-export-chart");
        svgs.forEach(function (svg, idx) {
            if (svg.dataset.exportReady) return;
            svg.dataset.exportReady = "true";

            const bar = document.createElement("div");
            bar.className = "chart-export-bar no-print";

            const btnPng = document.createElement("button");
            btnPng.type = "button";
            btnPng.className = "chart-export-btn";
            btnPng.textContent = "⬇ Скачать PNG (300 DPI для отчёта)";

            const btnSvg = document.createElement("button");
            btnSvg.type = "button";
            btnSvg.className = "chart-export-btn";
            btnSvg.textContent = "⬇ SVG";

            function cloneChart() {
                const clone = svg.cloneNode(true);
                clone.setAttribute("xmlns", "http://www.w3.org/2000/svg");
                const vb = (svg.getAttribute("viewBox") || "0 0 520 185").split(/[\s,]+/).map(Number);
                const w = vb[2] || 520, h = vb[3] || 185;
                clone.setAttribute("width", w);
                clone.setAttribute("height", h);

                const bg = document.createElementNS("http://www.w3.org/2000/svg", "rect");
                bg.setAttribute("x", "0");
                bg.setAttribute("y", "0");
                bg.setAttribute("width", String(w));
                bg.setAttribute("height", String(h));
                bg.setAttribute("fill", "#ffffff");
                clone.insertBefore(bg, clone.firstChild);

                clone.querySelectorAll("text").forEach(t => {
                    t.setAttribute("fill", "#15241c");
                    t.setAttribute("font-family", "Segoe UI, Arial, sans-serif");
                });
                return { clone, w, h };
            }

            btnSvg.addEventListener("click", function () {
                const { clone } = cloneChart();
                const xml = new XMLSerializer().serializeToString(clone);
                const blob = new Blob([xml], { type: "image/svg+xml;charset=utf-8" });
                const a = document.createElement("a");
                a.href = URL.createObjectURL(blob);
                a.download = `vetguard-chart-${idx + 1}.svg`;
                a.click();
            });

            btnPng.addEventListener("click", function () {
                const { clone, w, h } = cloneChart();
                const xml = new XMLSerializer().serializeToString(clone);
                const img = new Image();
                const scale = 2.5;
                img.onload = function () {
                    const canvas = document.createElement("canvas");
                    canvas.width = w * scale;
                    canvas.height = h * scale;
                    const ctx = canvas.getContext("2d");
                    ctx.scale(scale, scale);
                    ctx.fillStyle = "#ffffff";
                    ctx.fillRect(0, 0, w, h);
                    ctx.drawImage(img, 0, 0, w, h);
                    const a = document.createElement("a");
                    a.href = canvas.toDataURL("image/png");
                    a.download = `vetguard-chart-${idx + 1}.png`;
                    a.click();
                };
                img.src = "data:image/svg+xml;charset=utf-8," + encodeURIComponent(xml);
            });

            bar.appendChild(btnPng);
            bar.appendChild(btnSvg);
            svg.insertAdjacentElement("afterend", bar);
        });
    }

    // 7. Командная палитра быстрого перехода Ctrl + K / Cmd + K (как в ITS AgroVision V2)
    function initCommandPalette() {
        const backdrop = document.getElementById("cmdkBackdrop");
        const input = document.getElementById("cmdkInput");
        const list = document.getElementById("cmdkList");
        const openBtn = document.getElementById("cmdkOpenBtn");
        if (!backdrop || !input || !list) return;

        let items = null;
        let filtered = [];
        let activeIdx = 0;

        async function loadIndex() {
            if (items !== null) return;
            try {
                const res = await fetch("/UiLookup/SearchIndex");
                items = res.ok ? await res.json() : [];
            } catch {
                items = [];
            }
        }

        function render() {
            const q = input.value.trim().toLowerCase();
            filtered = (items || []).filter(x =>
                !q ||
                x.title.toLowerCase().includes(q) ||
                x.subtitle.toLowerCase().includes(q) ||
                x.category.toLowerCase().includes(q)).slice(0, 12);
            activeIdx = 0;
            list.innerHTML = filtered.length === 0
                ? '<li class="p-3 text-center text-muted small">Ничего не найдено</li>'
                : filtered.map((it, idx) =>
                    `<li><a href="${it.url}" class="cmdk-item ${idx === 0 ? "active" : ""}" data-idx="${idx}">` +
                    `<div><strong>${it.title}</strong><small>${it.subtitle}</small></div>` +
                    `<span class="cmdk-tag">${it.category}</span></a></li>`).join("");
        }

        async function openPalette() {
            backdrop.classList.add("open");
            input.value = "";
            await loadIndex();
            render();
            setTimeout(() => input.focus(), 20);
        }

        if (openBtn) openBtn.addEventListener("click", openPalette);
        backdrop.addEventListener("click", (e) => { if (e.target === backdrop) backdrop.classList.remove("open"); });
        input.addEventListener("input", render);

        document.addEventListener("keydown", function (e) {
            if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") {
                e.preventDefault();
                if (backdrop.classList.contains("open")) backdrop.classList.remove("open");
                else openPalette();
                return;
            }
            if (!backdrop.classList.contains("open") || filtered.length === 0) return;
            if (e.key === "ArrowDown") {
                e.preventDefault();
                activeIdx = (activeIdx + 1) % filtered.length;
            } else if (e.key === "ArrowUp") {
                e.preventDefault();
                activeIdx = (activeIdx - 1 + filtered.length) % filtered.length;
            } else if (e.key === "Enter") {
                e.preventDefault();
                if (filtered[activeIdx]) window.location.href = filtered[activeIdx].url;
                return;
            } else return;

            list.querySelectorAll(".cmdk-item").forEach((el, idx) => el.classList.toggle("active", idx === activeIdx));
        });
    }

    // 8. SignalR живые оповещения о карантинах и блокировках партий
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
        initAcademicChartExport();
        initCommandPalette();
        initSignalR();
    });
})();
