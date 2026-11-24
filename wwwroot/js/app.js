// ERPES Operasyon Kulesi — Kurumsal Kaynak, Filo ve Tedarik Zinciri Yönetim Motoru

let map;
let plantLayerGroup;
let landLayerGroup;
let vesselLayerGroup;
let aircraftLayerGroup;
let routeLayerGroup;

let plantMarkers = {};
let activeTruckTimers = [];

let currentState = null;
let currentRadar = null;
let workforceData = null;
let activeRadarFilter = 'all'; // 'all', 'plants', 'land', 'vessels', 'aircraft'
let connection = null;

// Per-plant active tab state: 'vehicles' | 'workers' | 'stock'
let plantSubTabs = {
    'PLANT-A': 'vehicles',
    'PLANT-B': 'vehicles',
    'PLANT-C': 'vehicles'
};

// ==========================================
// 0. GÜVENLİK VE YARDIMCI FONKSİYONLAR
// ==========================================
let sharedAudioContext = null;

function escapeHtml(value) {
    if (value === null || value === undefined) return '';
    return String(value)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

function escapeAttrArg(value) {
    return escapeHtml(JSON.stringify(value == null ? '' : String(value)));
}

function getAudioContext() {
    const AudioCtx = window.AudioContext || window.webkitAudioContext;
    if (!AudioCtx) return null;
    if (!sharedAudioContext) {
        sharedAudioContext = new AudioCtx();
    }
    if (sharedAudioContext.state === 'suspended') {
        sharedAudioContext.resume().catch(() => {});
    }
    return sharedAudioContext;
}

// ==========================================
// 1. SES VE BİLDİRİM MOTORU
// ==========================================
function playAlertTone(type = 'info') {
    try {
        const ctx = getAudioContext();
        if (!ctx) return;
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.connect(gain);
        gain.connect(ctx.destination);

        if (type === 'critical') {
            osc.type = 'sawtooth';
            osc.frequency.setValueAtTime(740, ctx.currentTime);
            osc.frequency.setValueAtTime(520, ctx.currentTime + 0.14);
            gain.gain.setValueAtTime(0.12, ctx.currentTime);
            gain.gain.exponentialRampToValueAtTime(0.008, ctx.currentTime + 0.35);
            osc.start();
            osc.stop(ctx.currentTime + 0.35);
        } else if (type === 'success') {
            osc.type = 'sine';
            osc.frequency.setValueAtTime(523.25, ctx.currentTime);
            osc.frequency.setValueAtTime(659.25, ctx.currentTime + 0.1);
            osc.frequency.setValueAtTime(783.99, ctx.currentTime + 0.2);
            gain.gain.setValueAtTime(0.1, ctx.currentTime);
            gain.gain.exponentialRampToValueAtTime(0.008, ctx.currentTime + 0.35);
            osc.start();
            osc.stop(ctx.currentTime + 0.35);
        } else {
            osc.type = 'sine';
            osc.frequency.setValueAtTime(620, ctx.currentTime);
            gain.gain.setValueAtTime(0.08, ctx.currentTime);
            gain.gain.exponentialRampToValueAtTime(0.008, ctx.currentTime + 0.2);
            osc.start();
            osc.stop(ctx.currentTime + 0.2);
        }
    } catch (_) {}
}

function showToast(title, message, type = 'info') {
    const container = document.getElementById('toast-container');
    if (!container) return;

    const colors = {
        critical: 'border-rose-500/40 bg-[#18181b]/95 text-rose-100',
        warning: 'border-amber-500/40 bg-[#18181b]/95 text-amber-100',
        success: 'border-emerald-500/40 bg-[#18181b]/95 text-emerald-100',
        info: 'border-white/[0.14] bg-[#18181b]/95 text-zinc-100'
    };

    const icons = {
        critical: 'fa-triangle-exclamation text-rose-400',
        warning: 'fa-bolt text-amber-400',
        success: 'fa-circle-check text-emerald-400',
        info: 'fa-bell text-sky-400'
    };

    const toast = document.createElement('div');
    toast.className = `toast-enter flex items-start gap-3 p-3.5 rounded-xl border backdrop-blur-md shadow-2xl max-w-sm ${colors[type] || colors.info}`;
    toast.innerHTML = `
        <i class="fa-solid ${icons[type] || icons.info} text-sm mt-0.5"></i>
        <div class="flex-1 min-w-0">
            <div class="font-semibold text-xs text-white">${escapeHtml(title)}</div>
            <div class="text-[11px] text-zinc-300 mt-0.5 leading-snug">${escapeHtml(message)}</div>
        </div>
        <button onclick="this.parentElement.remove()" class="text-zinc-500 hover:text-white text-xs cursor-pointer">
            <i class="fa-solid fa-xmark"></i>
        </button>
    `;

    container.appendChild(toast);
    setTimeout(() => {
        if (toast.parentElement) toast.remove();
    }, 5500);
}

// ==========================================
// 2. HARİTA VE FİLO KATMANLARI
// ==========================================
function initMap() {
    map = L.map('map', {
        zoomControl: true,
        attributionControl: false
    }).setView([39.75, 28.25], 7);

    const esriDark = L.layerGroup([
        L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/Canvas/World_Dark_Gray_Base/MapServer/tile/{z}/{y}/{x}', {
            maxZoom: 16
        }),
        L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/Canvas/World_Dark_Gray_Reference/MapServer/tile/{z}/{y}/{x}', {
            maxZoom: 16
        })
    ]);

    const osmDark = L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        className: 'osm-dark-tiles'
    });

    esriDark.addTo(map);

    L.control.layers({
        "Koyu Operasyon Haritası (Önerilen)": esriDark,
        "Detaylı Karayolu Haritası": osmDark
    }, null, { position: 'bottomright' }).addTo(map);

    routeLayerGroup = L.layerGroup().addTo(map);
    plantLayerGroup = L.layerGroup().addTo(map);
    landLayerGroup = L.layerGroup().addTo(map);
    vesselLayerGroup = L.layerGroup().addTo(map);
    aircraftLayerGroup = L.layerGroup().addTo(map);

    // Ensure full map tile canvas renders without any dark bottom strip on flex/grid resize
    [100, 350, 800, 1500].forEach(delay => {
        setTimeout(() => {
            if (map) map.invalidateSize();
        }, delay);
    });
    window.addEventListener('resize', () => {
        if (map) map.invalidateSize();
    });
}

function setMapFilter(filter) {
    activeRadarFilter = filter;
    ['all', 'plants', 'land', 'vessels', 'aircraft'].forEach(f => {
        const btn = document.getElementById(`filter-btn-${f}`);
        if (btn) {
            if (f === filter) {
                btn.className = 'px-2.5 py-1 rounded-md text-[11px] font-semibold bg-zinc-800 text-white transition-colors cursor-pointer';
            } else {
                btn.className = 'px-2.5 py-1 rounded-md text-[11px] font-medium text-zinc-400 hover:text-zinc-200 transition-colors cursor-pointer';
            }
        }
    });

    if (!map) return;

    if (routeLayerGroup) map.removeLayer(routeLayerGroup);
    if (plantLayerGroup) map.removeLayer(plantLayerGroup);
    if (landLayerGroup) map.removeLayer(landLayerGroup);
    if (vesselLayerGroup) map.removeLayer(vesselLayerGroup);
    if (aircraftLayerGroup) map.removeLayer(aircraftLayerGroup);

    if ((filter === 'all' || filter === 'land') && routeLayerGroup) routeLayerGroup.addTo(map);
    if ((filter === 'all' || filter === 'plants') && plantLayerGroup) plantLayerGroup.addTo(map);
    if ((filter === 'all' || filter === 'land') && landLayerGroup) landLayerGroup.addTo(map);
    if ((filter === 'all' || filter === 'vessels') && vesselLayerGroup) vesselLayerGroup.addTo(map);
    if ((filter === 'all' || filter === 'aircraft') && aircraftLayerGroup) aircraftLayerGroup.addTo(map);
}

function focusMapOn(lat, lng, zoom = 9) {
    if (!map) return;
    map.flyTo([lat, lng], zoom, { duration: 1.0 });
}

// ==========================================
// 3. TESİS ARASI GERÇEK ROTA HESAPLAYICI (İZMİR, BURSA, KOCAELİ)
// ==========================================
function resolvePlantKey(plantObj, fallbackId) {
    if (plantObj) {
        const code = (plantObj.code || '').toUpperCase();
        const city = (plantObj.city || '').toLocaleLowerCase('tr-TR');
        const name = (plantObj.name || '').toLocaleLowerCase('tr-TR');
        if (code === 'PLANT-C' || city.includes('izmir') || name.includes('izmir')) return 'PLANT-C';
        if (code === 'PLANT-B' || city.includes('bursa') || name.includes('bursa')) return 'PLANT-B';
        if (code === 'PLANT-A' || city.includes('kocaeli') || name.includes('kocaeli')) return 'PLANT-A';
    }
    if (currentState && currentState.plants && fallbackId) {
        const found = currentState.plants.find(p => p.id === fallbackId);
        if (found) return resolvePlantKey(found, null);
    }
    if (fallbackId === 3) return 'PLANT-C';
    if (fallbackId === 2) return 'PLANT-B';
    return 'PLANT-A';
}

function resolvePlantCity(plantObj, fallbackId) {
    if (plantObj?.city) return plantObj.city;
    const key = resolvePlantKey(plantObj, fallbackId);
    if (key === 'PLANT-C') return 'İzmir (Çiğli)';
    if (key === 'PLANT-B') return 'Bursa (Nilüfer)';
    return 'Kocaeli (Gebze)';
}

function getPlantRouteWaypoints(fromPlant, toPlant, fromPlantId, toPlantId) {
    const fromKey = resolvePlantKey(fromPlant, fromPlantId);
    const toKey = resolvePlantKey(toPlant, toPlantId);

    // İzmir (PLANT-C) -> Kocaeli (PLANT-A) O-5 Highway Corridor
    const izmirToKocaeli = [
        [38.5020, 27.0250], // İzmir Çiğli (Tesis C)
        [38.6500, 27.3800], // Manisa
        [39.1200, 27.7200], // Akhisar / Soma
        [39.6484, 27.8826], // Balıkesir
        [40.0200, 28.2200], // Susurluk
        [40.1800, 28.6000], // Karacabey
        [40.2500, 29.0600], // Bursa Kuzey
        [40.4800, 29.2500], // Orhangazi / Gemlik
        [40.6800, 29.4600], // Osmangazi Köprüsü
        [40.8028, 29.4307]  // Kocaeli Gebze (Tesis A)
    ];

    // İzmir (PLANT-C) -> Bursa (PLANT-B)
    const izmirToBursa = [
        [38.5020, 27.0250], // İzmir Çiğli (Tesis C)
        [38.6500, 27.3800], // Manisa
        [39.1200, 27.7200], // Akhisar
        [39.6484, 27.8826], // Balıkesir
        [40.0200, 28.2200], // Susurluk
        [40.1800, 28.6000], // Karacabey
        [40.2110, 28.9320]  // Bursa Nilüfer (Tesis B)
    ];

    // Bursa (PLANT-B) -> Kocaeli (PLANT-A)
    const bursaToKocaeli = [
        [40.2110, 28.9320], // Bursa Nilüfer (Tesis B)
        [40.4300, 29.1500], // Gemlik
        [40.6800, 29.4600], // Osmangazi Köprüsü
        [40.8028, 29.4307]  // Kocaeli Gebze (Tesis A)
    ];

    if (fromKey === 'PLANT-C' && toKey === 'PLANT-A') return izmirToKocaeli;
    if (fromKey === 'PLANT-A' && toKey === 'PLANT-C') return [...izmirToKocaeli].reverse();

    if (fromKey === 'PLANT-C' && toKey === 'PLANT-B') return izmirToBursa;
    if (fromKey === 'PLANT-B' && toKey === 'PLANT-C') return [...izmirToBursa].reverse();

    if (fromKey === 'PLANT-B' && toKey === 'PLANT-A') return bursaToKocaeli;
    if (fromKey === 'PLANT-A' && toKey === 'PLANT-B') return [...bursaToKocaeli].reverse();

    return bursaToKocaeli;
}

// ==========================================
// 4. HARİTA ÜZERİNDE TESİS, ARAÇ VE ROTA ÇİZİMİ
// ==========================================
function renderPlantMarkers(plants) {
    if (!plantLayerGroup) return;
    plantLayerGroup.clearLayers();
    plantMarkers = {};

    plants.forEach(plant => {
        const isCrit = plant.status === 'Critical';
        const isWarn = plant.status === 'Warning';

        const ringColor = isCrit
            ? 'border-rose-500 bg-zinc-950 text-rose-400 radar-ping'
            : isWarn
            ? 'border-amber-400 bg-zinc-950 text-amber-400'
            : 'border-sky-400 bg-zinc-950 text-sky-400';

        const shortCity = (plant.city || '').split(' ')[0];
        const html = `
            <div class="relative flex flex-col items-center">
                <div class="w-8 h-8 rounded-lg border-2 flex items-center justify-center shadow-xl ${ringColor}">
                    <i class="fa-solid fa-industry text-xs"></i>
                </div>
                <div class="mt-1 px-2 py-0.5 rounded-md bg-zinc-950/95 border border-white/15 text-[10px] font-semibold font-mono text-zinc-100 whitespace-nowrap shadow-md">
                    ${escapeHtml(shortCity)} · %${plant.activeCapacityPercent.toFixed(0)}
                </div>
            </div>
        `;

        const icon = L.divIcon({
            className: '',
            html: html,
            iconSize: [72, 52],
            iconAnchor: [36, 20]
        });

        const marker = L.marker([plant.latitude, plant.longitude], { icon }).addTo(plantLayerGroup);

        const invList = (plant.inventoryItems || []).map(i =>
            `<div class="flex justify-between gap-3 py-1 border-b border-white/[0.06] text-[11px]">
                <span class="text-zinc-300">${escapeHtml(i.materialName)}</span>
                <span class="font-mono font-semibold ${i.timeToSurviveDays <= 2 ? 'text-rose-400' : 'text-emerald-400'}">${i.quantity} ${escapeHtml(i.unit)} (${i.timeToSurviveDays}g)</span>
             </div>`
        ).join('');

        marker.bindPopup(`
            <div class="p-1 min-w-[235px]">
                <div class="flex items-center justify-between border-b border-white/[0.08] pb-1.5 mb-2">
                    <span class="font-semibold text-white text-xs">${escapeHtml(plant.name)}</span>
                </div>
                <div class="grid grid-cols-2 gap-2 mb-2 text-[11px]">
                    <div class="bg-zinc-900 p-1.5 rounded-md border border-white/[0.06]">
                        <div class="text-zinc-400 text-[10px]">Üretim Yükü</div>
                        <div class="font-semibold font-mono text-white">%${plant.activeCapacityPercent.toFixed(1)}</div>
                    </div>
                    <div class="bg-zinc-900 p-1.5 rounded-md border border-white/[0.06]">
                        <div class="text-zinc-400 text-[10px]">Personel Kadrosu</div>
                        <div class="font-semibold font-mono text-white">${plant.currentWorkerCount}/${plant.requiredWorkerCount} Kişi</div>
                    </div>
                </div>
                <div class="text-[10px] uppercase font-semibold text-zinc-400 mb-1">Emniyet Stok Durumu</div>
                ${invList}
            </div>
        `);

        plantMarkers[plant.id] = marker;
    });
}

function renderRadarOnMap(radar) {
    if (!radar || !vesselLayerGroup || !aircraftLayerGroup || !landLayerGroup) return;

    const hasActiveAnimatedMat = (currentState?.materialTransfers || []).some(m => m.status === 'InTransit');
    const hasActiveAnimatedWkr = (currentState?.workerTransfers || []).some(w => w.status === 'InTransit');

    // 1. Kara Taşıma Araçları
    landLayerGroup.clearLayers();
    (radar.landVehicles || []).forEach(lv => {
        if ((lv.vehicleCode || '').startsWith('ACİL-SEVKIYAT-') && lv.status === 'EmergencyActive' && hasActiveAnimatedMat) return;
        if ((lv.vehicleCode || '').startsWith('SERVIS-IK-') && hasActiveAnimatedWkr) return;

        const isEmerg = lv.status === 'EmergencyActive' || lv.status === 'WorkerShuttle';
        const iconClass = lv.status === 'WorkerShuttle' ? 'fa-van-shuttle' : 'fa-truck-fast';
        const pinStyle = isEmerg
            ? 'bg-emerald-950 border-emerald-400 text-emerald-300'
            : 'bg-zinc-900/95 border-emerald-500/70 text-emerald-400';

        const icon = L.divIcon({
            className: '',
            html: `<div class="w-6 h-6 rounded-full border flex items-center justify-center shadow-lg ${pinStyle}">
                    <i class="fa-solid ${iconClass} text-[10px]"></i>
                   </div>`,
            iconSize: [24, 24],
            iconAnchor: [12, 12]
        });

        const m = L.marker([lv.latitude, lv.longitude], { icon }).addTo(landLayerGroup);
        m.bindTooltip(`${escapeHtml(lv.plateNumber)} · ${escapeHtml(lv.eta)}`, { direction: 'top', offset: [0, -8] });
        m.on('click', () => openVehicleDetailModal('land', lv.vehicleCode));
    });

    // 2. Deniz Konteyner Gemileri
    vesselLayerGroup.clearLayers();
    (radar.vessels || []).forEach(v => {
        const isStrike = v.status === 'StrikeAlert';
        const pinStyle = isStrike
            ? 'bg-rose-950 border-rose-500 text-rose-400 radar-ping'
            : 'bg-zinc-900/95 border-blue-400/70 text-blue-400';

        const html = `
            <div class="relative flex flex-col items-center">
                <div class="w-6 h-6 rounded-full border flex items-center justify-center shadow-lg ${pinStyle}">
                    <i class="fa-solid fa-ship text-[10px]"></i>
                </div>
                ${isStrike ? `<div class="mt-0.5 px-1.5 py-0.2 rounded bg-rose-950/95 border border-rose-500/70 text-rose-200 text-[9px] font-mono font-semibold whitespace-nowrap">${escapeHtml(v.vesselName)}</div>` : ''}
            </div>
        `;

        const icon = L.divIcon({
            className: '',
            html,
            iconSize: [26, 26],
            iconAnchor: [13, 13]
        });

        const m = L.marker([v.latitude, v.longitude], { icon }).addTo(vesselLayerGroup);
        m.bindTooltip(`${escapeHtml(v.vesselName)} · ${escapeHtml(v.eta)}`, { direction: 'top', offset: [0, -8] });
        m.on('click', () => openVehicleDetailModal('sea', v.mmsi));
    });

    // 3. Hava Kargo Uçakları
    aircraftLayerGroup.clearLayers();
    (radar.aircraft || []).forEach(a => {
        const callsign = a.callsign || '';
        const isCrit = a.status === 'Critical';
        const isCargo = callsign.includes('CARGO') || callsign.includes('DHL') || callsign.includes('MNG');

        const planeColor = isCrit
            ? 'text-rose-400 drop-shadow-[0_0_6px_rgba(244,63,94,0.85)]'
            : isCargo
            ? 'text-amber-400 drop-shadow-[0_0_6px_rgba(245,158,11,0.7)]'
            : 'text-zinc-400 opacity-75';

        const html = `
            <div class="relative flex flex-col items-center">
                <div style="transform: rotate(${Number(a.heading) || 0}deg);" class="transition-transform duration-500">
                    <i class="fa-solid fa-plane-up ${isCargo || isCrit ? 'text-sm' : 'text-[11px]'} ${planeColor}"></i>
                </div>
                ${isCrit ? `
                <div class="mt-0.5 px-1 rounded bg-rose-950/95 border border-rose-500/70 text-rose-200 text-[9px] font-mono font-semibold whitespace-nowrap">
                    ${escapeHtml(callsign)}
                </div>` : ''}
            </div>
        `;

        const icon = L.divIcon({
            className: '',
            html,
            iconSize: [22, 22],
            iconAnchor: [11, 11]
        });

        const m = L.marker([a.latitude, a.longitude], { icon }).addTo(aircraftLayerGroup);
        m.bindTooltip(`${escapeHtml(callsign)} · ${escapeHtml(a.eta)}`, { direction: 'top', offset: [0, -6] });
        m.on('click', () => openVehicleDetailModal('air', callsign));
    });
}

function renderActiveRoutes(materialTransfers, workerTransfers) {
    if (!routeLayerGroup) return;
    routeLayerGroup.clearLayers();
    activeTruckTimers.forEach(t => clearInterval(t));
    activeTruckTimers = [];

    // 1. Malzeme Transfer Rotaları (İzmir ➔ Kocaeli, Bursa ➔ Kocaeli vb. birebir doğru çizilir)
    (materialTransfers || []).forEach(m => {
        if (m.status !== 'Proposed' && m.status !== 'InTransit') return;

        const isApproved = m.status === 'InTransit';
        const waypoints = getPlantRouteWaypoints(m.fromPlant, m.toPlant, m.fromPlantId, m.toPlantId);
        const fromCity = resolvePlantCity(m.fromPlant, m.fromPlantId);
        const toCity = resolvePlantCity(m.toPlant, m.toPlantId);

        const poly = L.polyline(waypoints, {
            color: isApproved ? '#10b981' : '#f59e0b',
            weight: isApproved ? 4 : 3,
            opacity: 0.92,
            dashArray: isApproved ? null : '8, 8'
        }).addTo(routeLayerGroup);

        poly.bindTooltip(escapeHtml(`${fromCity} ➔ ${toCity}: ${m.quantity} ${m.unit} ${m.materialName}`), { sticky: true });

        if (isApproved) {
            animateVehicleOnPath(
                waypoints,
                'fa-truck-fast',
                'emerald',
                escapeHtml(`${fromCity} ➔ ${toCity}: ${m.quantity} ${m.unit} ${m.materialName} (Sevkiyat Yolda)`),
                `ACİL-SEVKIYAT-${m.id}`
            );
        }
    });

    // 2. Personel Transfer Rotaları (Hangi fabrikadan hangi fabrikaya ise o rotayı çizer!)
    const activeWorkerOrders = (workerTransfers || []).filter(w => w.status === 'InTransit' || w.status === 'Proposed');
    const groupedRoutes = {};

    activeWorkerOrders.forEach(w => {
        const fromKey = resolvePlantKey(w.fromPlant, w.fromPlantId);
        const toKey = resolvePlantKey(w.toPlant, w.toPlantId);
        const routeKey = `${fromKey}_${toKey}_${w.status}`;
        if (!groupedRoutes[routeKey]) {
            groupedRoutes[routeKey] = {
                order: w,
                workers: []
            };
        }
        groupedRoutes[routeKey].workers.push(w.worker?.fullName || 'Uzman Operatör');
    });

    Object.values(groupedRoutes).forEach(group => {
        const w = group.order;
        const isApproved = w.status === 'InTransit';
        const workerWaypoints = getPlantRouteWaypoints(w.fromPlant, w.toPlant, w.fromPlantId, w.toPlantId);
        const fromCity = resolvePlantCity(w.fromPlant, w.fromPlantId);
        const toCity = resolvePlantCity(w.toPlant, w.toPlantId);

        const poly = L.polyline(workerWaypoints, {
            color: isApproved ? '#06b6d4' : '#a855f7',
            weight: isApproved ? 4 : 3,
            opacity: 0.9,
            dashArray: isApproved ? null : '6, 6'
        }).addTo(routeLayerGroup);

        poly.bindTooltip(escapeHtml(`${fromCity} ➔ ${toCity} Personel Transferi: ${group.workers.join(', ')}`), { sticky: true });

        if (isApproved) {
            animateVehicleOnPath(
                workerWaypoints,
                'fa-van-shuttle',
                'cyan',
                escapeHtml(`${fromCity} ➔ ${toCity}: ${group.workers.join(', ')} (Transfer Aracı Yolda)`),
                `SERVIS-IK-${w.id}`
            );
        }
    });
}

function animateVehicleOnPath(waypoints, iconClass, colorName, tooltipText, vehicleCode = null) {
    const badgeClass = colorName === 'emerald'
        ? 'bg-emerald-950 border-emerald-400 text-emerald-300 shadow-emerald-500/50'
        : 'bg-cyan-950 border-cyan-400 text-cyan-300 shadow-cyan-500/50';

    const icon = L.divIcon({
        className: '',
        html: `<div class="w-8 h-8 rounded-full border-2 ${badgeClass} shadow-lg flex items-center justify-center">
                <i class="fa-solid ${iconClass} text-xs"></i>
               </div>`,
        iconSize: [32, 32],
        iconAnchor: [16, 16]
    });

    const marker = L.marker(waypoints[0], { icon }).addTo(routeLayerGroup);
    marker.bindTooltip(tooltipText, { permanent: false, direction: 'top' });
    if (vehicleCode) {
        marker.on('click', () => openVehicleDetailModal('land', vehicleCode));
    }

    let step = 0;
    const totalSteps = 140;
    const timer = setInterval(() => {
        step = (step + 1) % totalSteps;
        const progress = step / totalSteps;
        const segments = waypoints.length - 1;
        const segIdx = Math.floor(progress * segments);
        const segProg = (progress * segments) - segIdx;

        const p1 = waypoints[segIdx];
        const p2 = waypoints[Math.min(segIdx + 1, segments)];

        const lat = p1[0] + (p2[0] - p1[0]) * segProg;
        const lng = p1[1] + (p2[1] - p1[1]) * segProg;
        marker.setLatLng([lat, lng]);
    }, 100);

    activeTruckTimers.push(timer);
}

// ==========================================
// 5. EKRAN GÜNCELLEME (KPI, KARAR MERKEZİ, FABRİKA KARTLARI)
// ==========================================
function updateUI(state) {
    if (!state) return;
    currentState = state;

    // Keep worker cache synchronized with latest state so plant cards never render stale IDs
    if (Array.isArray(state.mobileWorkers)) {
        workforceData = workforceData
            ? { ...workforceData, workers: state.mobileWorkers, plants: state.plants || workforceData.plants }
            : { workers: state.mobileWorkers, plants: state.plants || [] };
    }

    // 1. Üst KPI Kartları
    const kpis = state.kpis || {};
    const otifEl = document.getElementById('kpi-otif');
    const ttsEl = document.getElementById('kpi-tts');
    const varEl = document.getElementById('kpi-variance');
    const savEl = document.getElementById('kpi-savings');
    const costEl = document.getElementById('kpi-cost');

    if (otifEl) otifEl.innerText = `%${Number(kpis.otifPercent ?? 98.4).toFixed(1)}`;
    if (ttsEl) ttsEl.innerText = `${Number(kpis.averageTimeToSurviveDays ?? 2.0).toFixed(1)} Gün`;
    if (varEl) varEl.innerText = `%${Number(kpis.multiPlantWorkloadVariance ?? 44.5).toFixed(1)}`;
    if (savEl) savEl.innerText = `€${Number(kpis.totalAvoidedPenaltyEur ?? 0).toLocaleString('tr-TR')}`;
    if (costEl) costEl.innerText = `Acil Sevkiyat ve Görevlendirme Maliyeti: €${Number(kpis.totalEmergencyLogisticsCostEur ?? 0).toLocaleString('tr-TR')}`;

    const otifStatusEl = document.getElementById('kpi-otif-status');
    if (otifStatusEl) {
        const otifVal = Number(kpis.otifPercent ?? 98.4);
        otifStatusEl.innerHTML = otifVal >= 95
            ? '<i class="fa-solid fa-arrow-trend-up mr-1"></i>Optimal'
            : '<i class="fa-solid fa-triangle-exclamation mr-1"></i>Hedef Altı';
        otifStatusEl.className = otifVal >= 95 ? 'text-emerald-400 font-medium whitespace-nowrap' : 'text-rose-400 font-medium whitespace-nowrap';
    }

    // Update dynamic TTS subtitle from most critical inventory item
    const allItems = (state.plants || []).flatMap(p => (p.inventoryItems || []).map(i => ({ plant: p, item: i })));
    if (allItems.length > 0) {
        allItems.sort((a, b) => (a.item.timeToSurviveDays ?? 99) - (b.item.timeToSurviveDays ?? 99));
        const lowest = allItems[0];
        const ttsMatEl = document.getElementById('kpi-tts-material');
        const ttsConsEl = document.getElementById('kpi-tts-consumption');
        if (ttsMatEl) ttsMatEl.innerText = `${lowest.plant.city} — ${lowest.item.materialName}`;
        if (ttsConsEl) ttsConsEl.innerText = `${lowest.item.dailyConsumptionRate} ${lowest.item.unit}/Gün`;
    }

    // Update dynamic plant load breakdown
    const varBreakEl = document.getElementById('kpi-variance-breakdown');
    if (varBreakEl && Array.isArray(state.plants) && state.plants.length > 0) {
        varBreakEl.innerText = state.plants
            .map(p => `${(p.city || p.name).split(' ')[0]} (%${Number(p.activeCapacityPercent).toFixed(1)})`)
            .join(' — ');
    }

    const crisisEl = document.getElementById('crisis-counter');
    if (crisisEl) {
        const count = Number(kpis.activeCrisisCount ?? 0);
        crisisEl.innerHTML = count > 0
            ? `<i class="fa-solid fa-triangle-exclamation mr-1"></i>${count} AKTİF RİSK`
            : `<i class="fa-solid fa-circle-check mr-1"></i>OPERASYON STABİL`;
        crisisEl.className = count > 0
            ? 'px-2.5 py-1 text-[11px] font-semibold rounded-md bg-rose-500/15 border border-rose-500/35 text-rose-300 animate-pulse'
            : 'px-2.5 py-1 text-[11px] font-semibold rounded-md bg-emerald-500/10 border border-emerald-500/25 text-emerald-400';
    }

    // 2. Harita Katmanları
    if (map) {
        setTimeout(() => map.invalidateSize(), 50);
    }
    renderPlantMarkers(state.plants || []);
    renderActiveRoutes(state.materialTransfers || [], state.workerTransfers || []);

    // 3. Fabrika Bazlı Kaynak, Taşıma Filosu ve İşçi Listesi Kartları
    renderPlantCards(state.plants || []);

    // 4. Kriz Çözüm ve Kaynak Dengeleme Merkezi
    renderPrescriptiveDecisionCenter(state.materialTransfers || [], state.workerTransfers || [], state.activeAlerts || []);

    // 5. Aktif Operasyon Özeti
    renderActiveOperationsSummary(state.materialTransfers || [], state.workerTransfers || []);
}

function switchPlantSubTab(plantCode, tabName) {
    plantSubTabs[plantCode] = tabName;
    if (currentState && currentState.plants) {
        renderPlantCards(currentState.plants);
    }
}

function getVehiclesForPlant(plantCode) {
    const land = (currentRadar?.landVehicles || []).filter(v => v.targetPlantCode === plantCode || v.sourcePlantCode === plantCode);
    const sea = (currentRadar?.vessels || []).filter(v => v.targetPlantCode === plantCode);
    const air = (currentRadar?.aircraft || []).filter(a => a.targetPlantCode === plantCode);
    return { land, sea, air, total: land.length + sea.length + air.length };
}

function getWorkersForPlant(plant) {
    const allWorkers = currentState?.mobileWorkers || workforceData?.workers || [];
    return allWorkers.filter(w => w.currentPlantId === plant.id || w.homePlantId === plant.id);
}

function renderPlantCards(plants) {
    const container = document.getElementById('plant-cards-container');
    if (!container) return;

    container.innerHTML = plants.map(plant => {
        const plantCode = plant.code || 'PLANT-A';
        const activeSubTab = plantSubTabs[plantCode] || 'vehicles';

        const isCrit = plant.status === 'Critical';
        const isWarn = plant.status === 'Warning';

        const statusBadge = isCrit
            ? `<span class="px-2 py-0.5 rounded-md text-[10px] font-semibold bg-rose-500/15 text-rose-300 border border-rose-500/30 whitespace-nowrap">KRİTİK RİSK</span>`
            : isWarn
            ? `<span class="px-2 py-0.5 rounded-md text-[10px] font-semibold bg-amber-500/15 text-amber-300 border border-amber-500/30 whitespace-nowrap">UYARI</span>`
            : `<span class="px-2 py-0.5 rounded-md text-[10px] font-semibold bg-emerald-500/10 text-emerald-400 border border-emerald-500/25 whitespace-nowrap">DENGELİ</span>`;

        const capPercent = Math.min(100, plant.activeCapacityPercent);
        const capBarColor = plant.activeCapacityPercent > 100 ? 'bg-rose-500' : plant.activeCapacityPercent > 85 ? 'bg-amber-400' : 'bg-sky-400';

        const fleet = getVehiclesForPlant(plantCode);
        const plantWorkers = getWorkersForPlant(plant);
        const activeWorkersInPlant = plantWorkers.filter(w => w.currentPlantId === plant.id);

        // Active in-transit worker transfers for this specific plant
        const incomingTransfers = (currentState?.workerTransfers || []).filter(
            t => t.toPlantId === plant.id && t.status === 'InTransit'
        );
        const outgoingTransfers = (currentState?.workerTransfers || []).filter(
            t => t.fromPlantId === plant.id && t.status === 'InTransit'
        );
        const proposedIncoming = (currentState?.workerTransfers || []).filter(
            t => t.toPlantId === plant.id && t.status === 'Proposed'
        );

        let transferBannerHtml = '';
        if (incomingTransfers.length > 0 || outgoingTransfers.length > 0 || proposedIncoming.length > 0) {
            const inBadges = incomingTransfers.map(t => {
                const fromCity = escapeHtml(t.fromPlant?.city || 'Diğer Tesis');
                return `
                    <div class="flex items-center justify-between gap-2 px-2.5 py-1.5 rounded-lg bg-cyan-950/90 border border-cyan-500/60 text-cyan-200 text-[11px]">
                        <div class="flex items-center gap-1.5 min-w-0">
                            <i class="fa-solid fa-van-shuttle text-cyan-400"></i>
                            <span class="font-bold text-white truncate">${escapeHtml(t.worker?.fullName || 'Uzman')}</span>
                            <span class="text-[10px] opacity-90 truncate">(${fromCity} ➔ Bu Fabrikaya Transfer Ediliyor / Görevde)</span>
                        </div>
                        <button onclick="recallWorker(${Number(t.id)})" class="px-2 py-0.5 rounded bg-slate-900 hover:bg-slate-800 text-[10px] text-cyan-300 border border-cyan-700 shrink-0 cursor-pointer">
                            Geri Döndür
                        </button>
                    </div>
                `;
            }).join('');

            const propBadges = proposedIncoming.map(t => {
                const fromCity = escapeHtml(t.fromPlant?.city || 'Diğer Tesis');
                return `
                    <div class="flex items-center justify-between gap-2 px-2.5 py-1.5 rounded-lg bg-amber-950/90 border border-amber-500/60 text-amber-200 text-[11px]">
                        <div class="flex items-center gap-1.5 min-w-0">
                            <i class="fa-solid fa-user-clock text-amber-400"></i>
                            <span class="font-bold text-white truncate">${escapeHtml(t.worker?.fullName || 'Uzman')}</span>
                            <span class="text-[10px] opacity-90 truncate">(${fromCity} ➔ Bu Fabrikaya Görevlendirme Önerildi)</span>
                        </div>
                        <button onclick="approveWorkerTransfer(${Number(t.id)})" class="px-2 py-0.5 rounded bg-amber-500 hover:bg-amber-400 text-slate-950 font-extrabold text-[10px] shrink-0 cursor-pointer">
                            Onayla
                        </button>
                    </div>
                `;
            }).join('');

            const outBadges = outgoingTransfers.map(t => {
                const toCity = escapeHtml(t.toPlant?.city || 'Diğer Tesis');
                return `
                    <div class="flex items-center justify-between gap-2 px-2.5 py-1.5 rounded-lg bg-purple-950/70 border border-purple-500/50 text-purple-200 text-[11px]">
                        <div class="flex items-center gap-1.5 min-w-0">
                            <i class="fa-solid fa-route text-purple-400"></i>
                            <span class="font-bold text-white truncate">${escapeHtml(t.worker?.fullName || 'Uzman')}</span>
                            <span class="text-[10px] opacity-90 truncate">(${toCity} Tesisine Transfer Edildi)</span>
                        </div>
                    </div>
                `;
            }).join('');

            transferBannerHtml = `
                <div class="mb-3 p-2.5 rounded-xl bg-slate-950/95 border border-cyan-500/40 space-y-1.5">
                    <div class="text-[10px] font-extrabold uppercase tracking-wider text-cyan-400 flex items-center gap-1.5">
                        <span class="w-2 h-2 rounded-full bg-cyan-400 animate-ping"></span>
                        Transfer Edilen & Görevlendirilen Personeller
                    </div>
                    ${inBadges}
                    ${propBadges}
                    ${outBadges}
                </div>
            `;
        }

        // Sub-tab Content Builder
        let subTabContentHtml = '';

        if (activeSubTab === 'vehicles') {
            const landRows = fleet.land.map(lv => {
                const isEmerg = lv.status === 'EmergencyActive' || lv.status === 'WorkerShuttle';
                return `
                    <div onclick="openVehicleDetailModal('land', ${escapeAttrArg(lv.vehicleCode)})"
                         class="p-2.5 rounded-lg bg-zinc-900/70 hover:bg-zinc-900 border ${isEmerg ? 'border-emerald-500/40' : 'border-white/[0.06]'} flex items-center justify-between gap-2 cursor-pointer transition-colors">
                        <div class="flex items-center gap-2.5 min-w-0">
                            <div class="w-7 h-7 rounded-md bg-emerald-500/10 border border-emerald-500/25 flex items-center justify-center text-emerald-400 shrink-0">
                                <i class="fa-solid ${lv.status === 'WorkerShuttle' ? 'fa-van-shuttle' : 'fa-truck-fast'} text-[11px]"></i>
                            </div>
                            <div class="min-w-0">
                                <div class="flex items-center gap-1.5">
                                    <span class="text-[9px] font-mono font-semibold px-1.5 py-0.2 rounded bg-zinc-800 text-emerald-400">KARA</span>
                                    <span class="font-medium text-xs text-zinc-100 truncate">${escapeHtml(lv.plateNumber)} · ${escapeHtml(lv.vehicleType)}</span>
                                </div>
                                <div class="text-[11px] text-zinc-400 truncate mt-0.5">${escapeHtml(lv.cargoDescription)} (${escapeHtml(lv.quantityText)})</div>
                            </div>
                        </div>
                        <div class="text-right shrink-0">
                            <span class="px-2 py-0.5 rounded text-[10px] font-mono bg-zinc-950 text-emerald-400 border border-white/[0.06] block">
                                ${escapeHtml(lv.eta)}
                            </span>
                        </div>
                    </div>
                `;
            }).join('');

            const airRows = fleet.air.map(a => {
                const isCritFlight = a.status === 'Critical';
                return `
                    <div onclick="openVehicleDetailModal('air', ${escapeAttrArg(a.callsign)})"
                         class="p-2.5 rounded-lg bg-zinc-900/70 hover:bg-zinc-900 border ${isCritFlight ? 'border-rose-500/40' : 'border-white/[0.06]'} flex items-center justify-between gap-2 cursor-pointer transition-colors">
                        <div class="flex items-center gap-2.5 min-w-0">
                            <div class="w-7 h-7 rounded-md ${isCritFlight ? 'bg-rose-500/15 border-rose-500/35 text-rose-400' : 'bg-amber-500/10 border-amber-500/25 text-amber-400'} border flex items-center justify-center shrink-0">
                                <i class="fa-solid fa-plane-up text-[11px]"></i>
                            </div>
                            <div class="min-w-0">
                                <div class="flex items-center gap-1.5">
                                    <span class="text-[9px] font-mono font-semibold px-1.5 py-0.2 rounded bg-zinc-800 text-amber-400">HAVA</span>
                                    <span class="font-medium text-xs text-zinc-100 truncate">${escapeHtml(a.callsign)}</span>
                                </div>
                                <div class="text-[11px] text-zinc-400 truncate mt-0.5">${escapeHtml(a.cargoType)}</div>
                            </div>
                        </div>
                        <div class="text-right shrink-0">
                            <span class="px-2 py-0.5 rounded text-[10px] font-mono ${isCritFlight ? 'bg-rose-950/80 text-rose-300 border-rose-500/40' : 'bg-zinc-950 text-zinc-300 border-white/[0.06]'} border block">
                                ${escapeHtml(a.eta)}
                            </span>
                        </div>
                    </div>
                `;
            }).join('');

            const seaRows = fleet.sea.map(v => {
                const isStrike = v.status === 'StrikeAlert';
                return `
                    <div onclick="openVehicleDetailModal('sea', ${escapeAttrArg(v.mmsi)})"
                         class="p-2.5 rounded-lg bg-zinc-900/70 hover:bg-zinc-900 border ${isStrike ? 'border-rose-500/40' : 'border-white/[0.06]'} flex items-center justify-between gap-2 cursor-pointer transition-colors">
                        <div class="flex items-center gap-2.5 min-w-0">
                            <div class="w-7 h-7 rounded-md ${isStrike ? 'bg-rose-500/15 border-rose-500/35 text-rose-400' : 'bg-blue-500/10 border-blue-500/25 text-blue-400'} border flex items-center justify-center shrink-0">
                                <i class="fa-solid fa-ship text-[11px]"></i>
                            </div>
                            <div class="min-w-0">
                                <div class="flex items-center gap-1.5">
                                    <span class="text-[9px] font-mono font-semibold px-1.5 py-0.2 rounded bg-zinc-800 text-blue-400">DENİZ</span>
                                    <span class="font-medium text-xs text-zinc-100 truncate">${escapeHtml(v.vesselName)}</span>
                                </div>
                                <div class="text-[11px] text-zinc-400 truncate mt-0.5">${escapeHtml(v.cargoManifest)}</div>
                            </div>
                        </div>
                        <div class="text-right shrink-0">
                            <span class="px-2 py-0.5 rounded text-[10px] font-mono ${isStrike ? 'bg-rose-950/80 text-rose-300 border-rose-500/40' : 'bg-zinc-950 text-zinc-300 border-white/[0.06]'} border block">
                                ${escapeHtml(v.eta)}
                            </span>
                        </div>
                    </div>
                `;
            }).join('');

            subTabContentHtml = `
                <div class="space-y-1.5 max-h-[320px] overflow-y-auto pr-1">
                    <div class="text-[10px] text-zinc-500 flex items-center justify-between px-0.5 pb-1">
                        <span>Detay için araca tıklayın</span>
                        <span class="font-mono text-zinc-400">${fleet.land.length} Kara · ${fleet.air.length} Hava · ${fleet.sea.length} Deniz</span>
                    </div>
                    ${landRows}
                    ${airRows}
                    ${seaRows}
                </div>
            `;
        } else if (activeSubTab === 'workers') {
            const sortedWorkers = [...plantWorkers].sort((a, b) => {
                const aIn = a.homePlantId !== a.currentPlantId && a.currentPlantId === plant.id ? 1 : 0;
                const bIn = b.homePlantId !== b.currentPlantId && b.currentPlantId === plant.id ? 1 : 0;
                return bIn - aIn;
            });

            const workerRows = sortedWorkers.map(w => {
                const isRotational = w.isMobilityAccepted;
                const isTransferredHere = w.homePlantId !== w.currentPlantId && w.currentPlantId === plant.id;
                const isDispatchedAway = w.homePlantId === plant.id && w.currentPlantId !== plant.id;
                const isInTransit = w.status === 'InTransit';

                const statusTag = isDispatchedAway
                    ? `<span class="text-[10px] px-1.5 py-0.5 rounded bg-violet-500/15 text-violet-300 border border-violet-500/30 font-medium">${escapeHtml(w.currentPlant?.city || 'Diğer Fabrika')} Tesisine Transfer</span>`
                    : isTransferredHere
                    ? `<span class="text-[10px] px-1.5 py-0.5 rounded bg-sky-500/15 text-sky-300 border border-sky-500/30 font-medium">${escapeHtml(w.homePlant?.city || 'Diğer Fabrika')} Tesisinden Görevde</span>`
                    : isRotational
                    ? `<span class="text-[10px] px-1.5 py-0.5 rounded bg-emerald-500/10 text-emerald-400 border border-emerald-500/25">Rotasyona Uygun (€${w.mobilityAllowanceRate}/gün)</span>`
                    : `<span class="text-[10px] px-1.5 py-0.5 rounded bg-zinc-800 text-zinc-400 border border-white/[0.06]">Sabit Kadro</span>`;

                const dispatchBtn = isRotational && !isDispatchedAway && !isTransferredHere && !isInTransit
                    ? `<button onclick="openDispatchModal(${Number(w.id)})"
                               class="px-2.5 py-1 rounded-md bg-zinc-100 hover:bg-white text-zinc-950 font-semibold text-[10px] flex items-center gap-1 cursor-pointer shrink-0">
                            <i class="fa-solid fa-paper-plane text-[9px]"></i> Görevlendir
                       </button>`
                    : isTransferredHere || isInTransit
                    ? `<span class="text-[10px] font-medium text-sky-400 shrink-0">Görevde</span>`
                    : isDispatchedAway
                    ? `<span class="text-[10px] font-medium text-violet-400 shrink-0">Transferde</span>`
                    : `<button onclick="attemptLockedWorkerDispatch(${escapeAttrArg(w.fullName)})"
                               class="px-2 py-1 rounded-md bg-zinc-900 border border-white/[0.06] text-zinc-500 text-[10px] cursor-not-allowed shrink-0">
                            <i class="fa-solid fa-lock text-[9px]"></i> Sabit
                       </button>`;

                return `
                    <div class="p-2.5 rounded-lg ${isTransferredHere ? 'bg-sky-950/20 border-sky-500/35' : isDispatchedAway ? 'bg-zinc-900/40 border-white/[0.05] opacity-75' : 'bg-zinc-900/70 border-white/[0.06]'} border flex items-center justify-between gap-2 text-xs">
                        <div class="min-w-0">
                            <div class="flex items-center gap-2">
                                <span class="font-medium text-zinc-100">${escapeHtml(w.fullName)}</span>
                                <span class="text-[10px] font-mono text-zinc-400">${w.skillScore}/100</span>
                            </div>
                            <div class="text-[11px] text-zinc-400 mt-0.5">${escapeHtml(w.roleOrSkill)}</div>
                            <div class="mt-1">${statusTag}</div>
                        </div>
                        ${dispatchBtn}
                    </div>
                `;
            }).join('');

            subTabContentHtml = `
                <div class="space-y-1.5 max-h-[320px] overflow-y-auto pr-1">
                    <div class="flex items-center justify-between text-[10px] text-zinc-500 px-0.5 pb-1">
                        <span>Aktif Görev Yapan: <strong class="text-zinc-200">${activeWorkersInPlant.length} Uzman</strong></span>
                        <span class="text-emerald-400 font-medium">Hedef Kadro: ${plant.requiredWorkerCount}</span>
                    </div>
                    ${workerRows}
                </div>
            `;
        } else {
            const itemsHtml = (plant.inventoryItems || []).map(item => {
                const ttsCrit = item.timeToSurviveDays <= 2.5;
                return `
                    <div class="p-2.5 rounded-lg bg-zinc-900/70 border ${ttsCrit ? 'border-rose-500/35' : 'border-white/[0.06]'} flex items-center justify-between text-xs">
                        <div class="pr-2 min-w-0">
                            <div class="font-medium text-zinc-100 truncate">${escapeHtml(item.materialName)}</div>
                            <div class="text-[10px] text-zinc-400 mt-0.5 font-mono">${escapeHtml(item.materialCode)} · ${item.dailyConsumptionRate} ${escapeHtml(item.unit)}/gün</div>
                            <div class="text-[10px] text-zinc-500">Eşik: ${item.safetyStockThreshold} ${escapeHtml(item.unit)}</div>
                        </div>
                        <div class="text-right shrink-0">
                            <div class="font-mono font-semibold text-xs text-white">${Number(item.quantity).toLocaleString('tr-TR')} ${escapeHtml(item.unit)}</div>
                            <div class="text-[10px] font-medium mt-0.5 ${ttsCrit ? 'text-rose-400' : 'text-emerald-400'}">
                                ${item.timeToSurviveDays} Gün
                            </div>
                        </div>
                    </div>
                `;
            }).join('');

            subTabContentHtml = `
                <div class="space-y-1.5 max-h-[320px] overflow-y-auto pr-1">
                    <div class="text-[10px] text-zinc-500 px-0.5 pb-1">Emniyet Stokları</div>
                    ${itemsHtml}
                </div>
            `;
        }

        return `
            <div class="surface-card rounded-xl p-4 border ${isCrit ? 'border-rose-500/40' : isWarn ? 'border-amber-500/35' : 'border-white/[0.08]'} flex flex-col justify-between">
                <div>
                    <div class="flex items-start justify-between gap-2 mb-3">
                        <div onclick="focusMapOn(${Number(plant.latitude)}, ${Number(plant.longitude)}, 9)" class="cursor-pointer group">
                            <span class="text-[10px] font-mono uppercase tracking-wider text-zinc-400 group-hover:text-sky-400 transition-colors">
                                ${escapeHtml(plant.city)}
                            </span>
                            <h3 class="font-semibold text-sm text-white group-hover:text-sky-300 transition-colors mt-0.5">${escapeHtml(plant.name)}</h3>
                        </div>
                        ${statusBadge}
                    </div>

                    <div class="grid grid-cols-2 gap-2 mb-3">
                        <div class="bg-zinc-900/80 p-2.5 rounded-lg border border-white/[0.06]">
                            <div class="flex justify-between text-[11px] text-zinc-400 mb-1.5">
                                <span>Hat Yükü</span>
                                <span class="font-mono font-semibold text-white">%${plant.activeCapacityPercent.toFixed(1)}</span>
                            </div>
                            <div class="w-full h-1 bg-zinc-800 rounded-full overflow-hidden">
                                <div class="${capBarColor} h-full rounded-full transition-all duration-500" style="width: ${capPercent}%"></div>
                            </div>
                        </div>
                        <div class="bg-zinc-900/80 p-2.5 rounded-lg border border-white/[0.06]">
                            <div class="flex justify-between text-[11px] text-zinc-400 mb-1">
                                <span>Kadro</span>
                                <span class="font-mono font-semibold text-white">${activeWorkersInPlant.length}/${plant.requiredWorkerCount}</span>
                            </div>
                            <div class="text-[10px] ${activeWorkersInPlant.length > plant.requiredWorkerCount ? 'text-emerald-400' : 'text-zinc-400'} truncate">
                                ${activeWorkersInPlant.length > plant.requiredWorkerCount
                                    ? `+${activeWorkersInPlant.length - plant.requiredWorkerCount} Destek Verebilir`
                                    : 'Vardiya Dengede'}
                            </div>
                        </div>
                    </div>

                    ${transferBannerHtml}

                    <div class="grid grid-cols-3 gap-1 bg-zinc-900/90 p-1 rounded-lg border border-white/[0.06] mb-3">
                        <button onclick="switchPlantSubTab('${plantCode}', 'vehicles')"
                                class="py-1.5 px-2 rounded-md text-[11px] font-medium flex items-center justify-center gap-1 transition-colors cursor-pointer ${activeSubTab === 'vehicles' ? 'bg-zinc-800 text-white' : 'text-zinc-400 hover:text-zinc-200'}">
                            Filo (${fleet.total})
                        </button>
                        <button onclick="switchPlantSubTab('${plantCode}', 'workers')"
                                class="py-1.5 px-2 rounded-md text-[11px] font-medium flex items-center justify-center gap-1 transition-colors cursor-pointer ${activeSubTab === 'workers' ? 'bg-zinc-800 text-white' : 'text-zinc-400 hover:text-zinc-200'}">
                            İşçiler (${activeWorkersInPlant.length})
                        </button>
                        <button onclick="switchPlantSubTab('${plantCode}', 'stock')"
                                class="py-1.5 px-2 rounded-md text-[11px] font-medium flex items-center justify-center gap-1 transition-colors cursor-pointer ${activeSubTab === 'stock' ? 'bg-zinc-800 text-white' : 'text-zinc-400 hover:text-zinc-200'}">
                            Stok (${(plant.inventoryItems || []).length})
                        </button>
                    </div>

                    ${subTabContentHtml}
                </div>
            </div>
        `;
    }).join('');
}

// ==========================================
// 6. TAŞIMA ARACI DETAY MODALI (KARA / HAVA / DENİZ)
// ==========================================
function openVehicleDetailModal(type, code) {
    const modal = document.getElementById('vehicle-detail-modal');
    const iconBox = document.getElementById('veh-modal-icon-box');
    const iconEl = document.getElementById('veh-modal-icon');
    const catEl = document.getElementById('veh-modal-category');
    const titleEl = document.getElementById('veh-modal-title');
    const bodyEl = document.getElementById('veh-modal-body');
    const focusBtn = document.getElementById('veh-modal-focus-btn');

    if (!modal || !bodyEl) return;

    if (type === 'land') {
        const lv = (currentRadar?.landVehicles || []).find(x => x.vehicleCode === code);
        if (!lv) return;

        iconBox.className = 'w-10 h-10 rounded-xl bg-emerald-500/20 border border-emerald-500/40 flex items-center justify-center text-emerald-300 text-lg';
        iconEl.className = `fa-solid ${lv.status === 'WorkerShuttle' ? 'fa-van-shuttle' : 'fa-truck-fast'}`;
        catEl.className = 'text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded bg-emerald-950 text-emerald-300 border border-emerald-700';
        catEl.innerText = 'KARA LOJİSTİK VE TAŞIMA ARACI';
        titleEl.innerText = `${lv.plateNumber} — ${lv.vehicleType}`;

        bodyEl.innerHTML = `
            <div class="grid grid-cols-2 gap-2.5 bg-slate-950 p-3 rounded-xl border border-slate-800">
                <div>
                    <div class="text-[10px] text-slate-400">Araç Kodu / Plaka</div>
                    <div class="font-bold text-white font-mono">${escapeHtml(lv.vehicleCode)} • ${escapeHtml(lv.plateNumber)}</div>
                </div>
                <div>
                    <div class="text-[10px] text-slate-400">Sürücü & İletişim</div>
                    <div class="font-bold text-white">${escapeHtml(lv.driverName)}</div>
                    <div class="text-[10px] text-cyan-300 font-mono">${escapeHtml(lv.driverPhone)}</div>
                </div>
            </div>
            <div class="p-3 rounded-xl bg-slate-950 border border-slate-800 space-y-2">
                <div>
                    <div class="text-[10px] text-slate-400">Taşınan Kaynak / Yük</div>
                    <div class="font-bold text-cyan-300 text-sm">${escapeHtml(lv.cargoDescription)}</div>
                    <div class="text-xs text-white font-mono mt-0.5">Miktar: ${escapeHtml(lv.quantityText)}</div>
                </div>
                <div class="pt-2 border-t border-slate-800/80 grid grid-cols-2 gap-2">
                    <div>
                        <div class="text-[10px] text-slate-400">Çıkış Noktası</div>
                        <div class="font-semibold text-slate-200">${escapeHtml(lv.originLocation)}</div>
                    </div>
                    <div>
                        <div class="text-[10px] text-slate-400">Varış Fabrikası</div>
                        <div class="font-bold text-white">${escapeHtml(lv.destinationLocation)}</div>
                    </div>
                </div>
            </div>
            <div class="grid grid-cols-3 gap-2 text-center bg-slate-950 p-2.5 rounded-xl border border-slate-800">
                <div>
                    <div class="text-[10px] text-slate-400">Anlık Hız</div>
                    <div class="font-mono font-bold text-white">${Number(lv.speedKmh)} km/s</div>
                </div>
                <div class="border-x border-slate-800">
                    <div class="text-[10px] text-slate-400">Konum Koordinatı</div>
                    <div class="font-mono font-bold text-slate-200">${Number(lv.latitude).toFixed(3)}, ${Number(lv.longitude).toFixed(3)}</div>
                </div>
                <div>
                    <div class="text-[10px] text-slate-400">Tahmini Varış</div>
                    <div class="font-bold text-emerald-400">${escapeHtml(lv.eta)}</div>
                </div>
            </div>
        `;

        focusBtn.onclick = () => {
            closeVehicleDetailModal();
            focusMapOn(lv.latitude, lv.longitude, 9);
        };
    } else if (type === 'air') {
        const a = (currentRadar?.aircraft || []).find(x => x.callsign === code);
        if (!a) return;

        iconBox.className = 'w-10 h-10 rounded-xl bg-amber-500/20 border border-amber-500/40 flex items-center justify-center text-amber-300 text-lg';
        iconEl.className = 'fa-solid fa-plane-up';
        catEl.className = 'text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded bg-amber-950 text-amber-300 border border-amber-700';
        catEl.innerText = 'HAVA KARGO TAŞIMA ARACI';
        titleEl.innerText = `${a.callsign} — Hava Kargo Seferi`;

        bodyEl.innerHTML = `
            <div class="grid grid-cols-2 gap-2.5 bg-slate-950 p-3 rounded-xl border border-slate-800">
                <div>
                    <div class="text-[10px] text-slate-400">Uçuş Kodu / Kayıt</div>
                    <div class="font-bold text-white font-mono">${escapeHtml(a.callsign)} (${escapeHtml(a.icao24)})</div>
                </div>
                <div>
                    <div class="text-[10px] text-slate-400">Operatör & Ekip</div>
                    <div class="font-bold text-white">${escapeHtml(a.pilotOrOperator || 'Hava Kargo Filosu')}</div>
                    <div class="text-[10px] text-amber-300">${escapeHtml(a.originCountry)}</div>
                </div>
            </div>
            <div class="p-3 rounded-xl bg-slate-950 border border-slate-800 space-y-2">
                <div>
                    <div class="text-[10px] text-slate-400">Taşınan Kritik Komponent</div>
                    <div class="font-bold text-cyan-300 text-sm">${escapeHtml(a.cargoType)}</div>
                </div>
                <div class="pt-2 border-t border-slate-800/80 grid grid-cols-2 gap-2">
                    <div>
                        <div class="text-[10px] text-slate-400">Kalkış Havalimanı</div>
                        <div class="font-semibold text-slate-200">${escapeHtml(a.originAirport || 'Avrupa Kargo Hub')}</div>
                    </div>
                    <div>
                        <div class="text-[10px] text-slate-400">Hedef Tesis & Havalimanı</div>
                        <div class="font-bold text-white">${escapeHtml(a.destinationAirport)}</div>
                        <div class="text-[10px] text-cyan-400">${escapeHtml(a.targetPlantName)}</div>
                    </div>
                </div>
            </div>
            <div class="grid grid-cols-3 gap-2 text-center bg-slate-950 p-2.5 rounded-xl border border-slate-800">
                <div>
                    <div class="text-[10px] text-slate-400">Seyir İrtifası</div>
                    <div class="font-mono font-bold text-white">${Number(a.altitudeMeters)} m</div>
                </div>
                <div class="border-x border-slate-800">
                    <div class="text-[10px] text-slate-400">Hız / Yön</div>
                    <div class="font-mono font-bold text-slate-200">${Number(a.velocityKnots)} kn (${Number(a.heading)}°)</div>
                </div>
                <div>
                    <div class="text-[10px] text-slate-400">Sevkiyat Durumu</div>
                    <div class="font-bold ${a.status === 'Critical' ? 'text-rose-400' : 'text-emerald-400'}">${escapeHtml(a.eta)}</div>
                </div>
            </div>
        `;

        focusBtn.onclick = () => {
            closeVehicleDetailModal();
            focusMapOn(a.latitude, a.longitude, 8);
        };
    } else if (type === 'sea') {
        const v = (currentRadar?.vessels || []).find(x => x.mmsi === code);
        if (!v) return;

        iconBox.className = 'w-10 h-10 rounded-xl bg-blue-500/20 border border-blue-500/40 flex items-center justify-center text-blue-300 text-lg';
        iconEl.className = 'fa-solid fa-ship';
        catEl.className = 'text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded bg-blue-950 text-blue-300 border border-blue-700';
        catEl.innerText = 'DENİZ KONTEYNER GEMİSİ';
        titleEl.innerText = `${v.vesselName} (${v.flag})`;

        bodyEl.innerHTML = `
            <div class="grid grid-cols-2 gap-2.5 bg-slate-950 p-3 rounded-xl border border-slate-800">
                <div>
                    <div class="text-[10px] text-slate-400">Gemi Adı & Deniz Kodu</div>
                    <div class="font-bold text-white font-mono">${escapeHtml(v.vesselName)} • ${escapeHtml(v.mmsi)}</div>
                </div>
                <div>
                    <div class="text-[10px] text-slate-400">Deniz Acentesi & Kaptan</div>
                    <div class="font-bold text-white">${escapeHtml(v.captainOrAgency || 'Deniz Lojistik Hattı')}</div>
                </div>
            </div>
            <div class="p-3 rounded-xl bg-slate-950 border border-slate-800 space-y-2">
                <div>
                    <div class="text-[10px] text-slate-400">Konteyner Yük Manifestosu</div>
                    <div class="font-bold text-cyan-300 text-sm">${escapeHtml(v.cargoManifest)}</div>
                </div>
                <div class="pt-2 border-t border-slate-800/80 grid grid-cols-2 gap-2">
                    <div>
                        <div class="text-[10px] text-slate-400">Çıkış Limanı</div>
                        <div class="font-semibold text-slate-200">${escapeHtml(v.originPort)}</div>
                    </div>
                    <div>
                        <div class="text-[10px] text-slate-400">Varış Limanı & Fabrika</div>
                        <div class="font-bold text-white">${escapeHtml(v.destinationPort)}</div>
                        <div class="text-[10px] text-cyan-400">${escapeHtml(v.targetPlantName)}</div>
                    </div>
                </div>
            </div>
            <div class="grid grid-cols-3 gap-2 text-center bg-slate-950 p-2.5 rounded-xl border border-slate-800">
                <div>
                    <div class="text-[10px] text-slate-400">Gemi Hızı</div>
                    <div class="font-mono font-bold text-white">${Number(v.speedKnots)} Deniz Mili/s</div>
                </div>
                <div class="border-x border-slate-800">
                    <div class="text-[10px] text-slate-400">Rota Açısı</div>
                    <div class="font-mono font-bold text-slate-200">${Number(v.course)}°</div>
                </div>
                <div>
                    <div class="text-[10px] text-slate-400">Tahmini Varış (ETA)</div>
                    <div class="font-bold ${v.status === 'StrikeAlert' ? 'text-rose-400' : 'text-emerald-400'}">${escapeHtml(v.eta)}</div>
                </div>
            </div>
        `;

        focusBtn.onclick = () => {
            closeVehicleDetailModal();
            focusMapOn(v.latitude, v.longitude, 9);
        };
    }

    modal.classList.remove('hidden');
    modal.classList.add('flex');
}

function closeVehicleDetailModal() {
    const modal = document.getElementById('vehicle-detail-modal');
    if (modal) {
        modal.classList.add('hidden');
        modal.classList.remove('flex');
    }
}

// ==========================================
// 7. KRİZ ÇÖZÜM MERKEZİ & AKTİF OPERASYON ÖZETİ
// ==========================================
function renderPrescriptiveDecisionCenter(materialTransfers, workerTransfers, alerts) {
    const container = document.getElementById('prescriptive-action-container');
    if (!container) return;

    const proposedMaterials = materialTransfers.filter(m => m.status === 'Proposed');
    const inTransitMaterials = materialTransfers.filter(m => m.status === 'InTransit');
    const proposedWorkers = workerTransfers.filter(w => w.status === 'Proposed');
    const inTransitWorkers = workerTransfers.filter(w => w.status === 'InTransit');

    let cardsHtml = '';

    // 1. Önerilen Malzeme Sevkiyat Kararları
    proposedMaterials.forEach(m => {
        const netRoi = m.preventedPenaltyAmount - m.estimatedCost;
        const fromCity = resolvePlantCity(m.fromPlant, m.fromPlantId);
        const toCity = resolvePlantCity(m.toPlant, m.toPlantId);

        cardsHtml += `
            <div class="glass-panel rounded-2xl p-4 border-2 border-rose-500/80 shadow-xl shadow-rose-950/40 relative overflow-hidden">
                <div class="flex items-center justify-between mb-2">
                    <span class="px-2.5 py-0.5 rounded-full text-[10px] font-extrabold bg-rose-950 text-rose-300 border border-rose-500 uppercase tracking-wider flex items-center gap-1.5">
                        <span class="w-2 h-2 rounded-full bg-rose-400 animate-ping"></span>
                        Acil Kaynak Kaydırma Önerisi
                    </span>
                    <span class="text-[11px] font-mono text-slate-400">SEVKİYAT #${Number(m.id)}</span>
                </div>

                <h4 class="font-bold text-sm text-white mb-1">
                    ${escapeHtml(m.materialName)} (${escapeHtml(m.materialCode)}) — Fabrikalar Arası Sevkiyat
                </h4>
                <p class="text-xs text-slate-300 mb-3 leading-relaxed">
                    Dış tedarik gecikmesi nedeniyle <strong>${escapeHtml(m.toPlant?.name || toCity)}</strong> üretim hattında duruş riski tespit edildi.
                    En uygun emniyet stoğu <strong>${escapeHtml(m.fromPlant?.name || fromCity)}</strong> deposunda belirlendi.
                </p>

                <div class="grid grid-cols-3 gap-2 mb-3 text-center bg-slate-950/80 p-2.5 rounded-xl border border-slate-800">
                    <div>
                        <div class="text-[10px] text-slate-400">Sevk Miktarı</div>
                        <div class="text-xs font-extrabold font-mono text-cyan-300">${Number(m.quantity).toLocaleString('tr-TR')} ${escapeHtml(m.unit)}</div>
                    </div>
                    <div class="border-x border-slate-800">
                        <div class="text-[10px] text-slate-400">Nakliye Maliyeti</div>
                        <div class="text-xs font-extrabold font-mono text-amber-400">€${Number(m.estimatedCost).toLocaleString('tr-TR')}</div>
                    </div>
                    <div>
                        <div class="text-[10px] text-slate-400">Önlenen Ceza</div>
                        <div class="text-xs font-extrabold font-mono text-emerald-400">€${Number(m.preventedPenaltyAmount).toLocaleString('tr-TR')}</div>
                    </div>
                </div>

                <div class="flex items-center justify-between text-[11px] text-slate-300 mb-3 px-1">
                    <span class="font-bold text-cyan-300"><i class="fa-solid fa-route mr-1"></i> Güzergah: ${escapeHtml(fromCity)} ➔ ${escapeHtml(toCity)}</span>
                    <span class="text-emerald-400 font-bold">Net Tasarruf: +€${Number(netRoi).toLocaleString('tr-TR')}</span>
                </div>

                <button onclick="approveMaterialTransfer(${Number(m.id)}, ${escapeAttrArg(fromCity)}, ${escapeAttrArg(toCity)})"
                        class="w-full py-2.5 px-4 rounded-xl bg-gradient-to-r from-emerald-600 via-emerald-500 to-teal-500 hover:from-emerald-500 hover:to-teal-400 text-white font-extrabold text-xs uppercase tracking-wider flex items-center justify-center gap-2 shadow-lg shadow-emerald-950 transition-all cursor-pointer">
                    <i class="fa-solid fa-truck-fast"></i> Sevkiyatı Onayla ve Ekspres Aracı Yola Çıkar
                </button>
            </div>
        `;
    });

    // 2. Önerilen İş Gücü Görevlendirme Kararları (Hedef fabrikaya göre ayrı kartlar)
    if (proposedWorkers.length > 0) {
        const workersByTarget = {};
        proposedWorkers.forEach(w => {
            const key = w.toPlantId || 1;
            if (!workersByTarget[key]) workersByTarget[key] = [];
            workersByTarget[key].push(w);
        });

        Object.values(workersByTarget).forEach(group => {
            const primary = group[0];
            const targetPlantName = primary.toPlant?.name || 'Hedef Fabrika';
            const targetCityName = resolvePlantCity(primary.toPlant, primary.toPlantId);
            const roleName = primary.worker?.roleOrSkill || 'Uzman Operatör';
            const totalCost = group.reduce((acc, w) => acc + Number(w.estimatedCost || 0), 0);
            const totalSaved = group.reduce((acc, w) => acc + Number(w.preventedPenaltyAmount || 0), 0);
            const workerBadges = group.map(w =>
                `<span class="px-2 py-1 rounded-lg bg-slate-900 border border-amber-500/40 text-[11px] text-amber-200 font-medium flex items-center gap-1.5">
                    <i class="fa-solid fa-user-check text-amber-400"></i> ${escapeHtml(w.worker?.fullName || 'Uzman Operatör')}
                    <span class="text-[10px] font-mono text-cyan-300">(${escapeHtml(w.fromPlant?.city || resolvePlantCity(w.fromPlant, w.fromPlantId))} • Skor: ${Number(w.worker?.skillScore || 90)})</span>
                 </span>`
            ).join('');

            cardsHtml += `
                <div class="glass-panel rounded-2xl p-4 border-2 border-amber-500/80 shadow-xl shadow-amber-950/30">
                    <div class="flex items-center justify-between mb-2">
                        <span class="px-2.5 py-0.5 rounded-full text-[10px] font-extrabold bg-amber-950 text-amber-300 border border-amber-500 uppercase tracking-wider flex items-center gap-1.5">
                            <i class="fa-solid fa-users-gear"></i>
                            Fabrikalar Arası Uzman Personel Takviyesi
                        </span>
                        <span class="text-[11px] font-mono text-slate-400">${group.length} UZMAN PERSONEL</span>
                    </div>

                    <h4 class="font-bold text-sm text-white mb-1">
                        ${escapeHtml(targetPlantName)} — ${escapeHtml(roleName)} Görevlendirmesi
                    </h4>
                    <p class="text-xs text-slate-300 mb-2.5">
                        <strong>${escapeHtml(targetCityName)}</strong> tesisindeki iş gücü darboğazını çözmek için rotasyona uygun sözleşmesi bulunan en yüksek yetkinlikli uzmanlar belirlendi:
                    </p>

                    <div class="flex flex-wrap gap-1.5 mb-3">
                        ${workerBadges}
                    </div>

                    <div class="grid grid-cols-2 gap-2 mb-3 text-center bg-slate-950/80 p-2 rounded-xl border border-slate-800 text-xs">
                        <div>
                            <span class="text-slate-400 text-[10px] block">7 Günlük Görevlendirme & Ulaşım</span>
                            <span class="font-mono font-bold text-amber-400">€${totalCost.toLocaleString('tr-TR')}</span>
                        </div>
                        <div class="border-l border-slate-800">
                            <span class="text-slate-400 text-[10px] block">Önlenen Teslimat Cezası</span>
                            <span class="font-mono font-bold text-emerald-400">€${totalSaved.toLocaleString('tr-TR')}</span>
                        </div>
                    </div>

                    <button onclick="approveWorkerTransfer(${Number(primary.id)})"
                            class="w-full py-2.5 px-4 rounded-xl bg-gradient-to-r from-amber-600 to-orange-500 hover:from-amber-500 hover:to-orange-400 text-white font-extrabold text-xs uppercase tracking-wider flex items-center justify-center gap-2 shadow-lg transition-all cursor-pointer">
                        <i class="fa-solid fa-user-check"></i> ${group.length} Uzman Personelin Görevlendirmesini Onayla
                    </button>
                </div>
            `;
        });
    }

    // 3. Aktif Yoldaki Sevkiyatlar
    inTransitMaterials.forEach(m => {
        const fromCity = resolvePlantCity(m.fromPlant, m.fromPlantId);
        const toCity = resolvePlantCity(m.toPlant, m.toPlantId);
        cardsHtml += `
            <div class="glass-panel rounded-2xl p-3.5 border border-emerald-500/60 bg-emerald-950/15">
                <div class="flex items-center justify-between mb-1">
                    <span class="text-[10px] font-bold uppercase px-2 py-0.5 rounded bg-emerald-950 text-emerald-300 border border-emerald-500/50">
                        <i class="fa-solid fa-truck-fast mr-1"></i> EKSPRES SEVKİYAT YOLDA
                    </span>
                    <span class="text-xs font-mono font-bold text-emerald-400">+€${Number(m.preventedPenaltyAmount).toLocaleString('tr-TR')} Korundu</span>
                </div>
                <div class="text-xs font-bold text-white">
                    ${escapeHtml(fromCity)} ➔ ${escapeHtml(toCity)}: ${m.quantity} ${escapeHtml(m.unit)} ${escapeHtml(m.materialName)}
                </div>
                <div class="text-[11px] text-slate-300 mt-0.5">
                    Taşıma aracı harita üzerinde <strong>${escapeHtml(fromCity)} ➔ ${escapeHtml(toCity)}</strong> güzergahında canlı takip ediliyor.
                </div>
            </div>
        `;
    });

    if (inTransitWorkers.length > 0) {
        const sourceCities = [...new Set(inTransitWorkers.map(w => resolvePlantCity(w.fromPlant, w.fromPlantId)))].join(' / ');
        const targetCities = [...new Set(inTransitWorkers.map(w => resolvePlantCity(w.toPlant, w.toPlantId)))].join(' / ');

        cardsHtml += `
            <div class="glass-panel rounded-2xl p-3.5 border border-cyan-500/60 bg-cyan-950/15">
                <div class="flex items-center justify-between mb-1">
                    <span class="text-[10px] font-bold uppercase px-2 py-0.5 rounded bg-cyan-950 text-cyan-300 border border-cyan-500/50">
                        <i class="fa-solid fa-van-shuttle mr-1"></i> PERSONEL TRANSFERİ AKTİF
                    </span>
                    <span class="text-xs font-mono font-bold text-cyan-300">${inTransitWorkers.length} Uzman Görevde</span>
                </div>
                <div class="text-xs font-bold text-white">
                    ${escapeHtml(sourceCities)} ➔ ${escapeHtml(targetCities)}: Uzman Operatör Ekibi Görevlendirildi
                </div>
            </div>
        `;
    }

    if (!cardsHtml) {
        cardsHtml = `
            <div class="rounded-lg p-5 text-center border border-white/[0.06] bg-zinc-900/50">
                <div class="w-9 h-9 rounded-lg bg-emerald-500/10 border border-emerald-500/20 flex items-center justify-center mx-auto mb-2.5 text-emerald-400 text-sm">
                    <i class="fa-solid fa-shield-halved"></i>
                </div>
                <h4 class="font-medium text-xs text-zinc-100">Tüm Tesisler ve Tedarik Hatları Dengede</h4>
                <p class="text-[11px] text-zinc-400 mt-1 leading-relaxed">
                    Sevkiyatlar planlanan takvimde ilerliyor. Kriz simülasyonu için üst çubuktaki senaryoları kullanabilirsiniz.
                </p>
            </div>
        `;
    }

    container.innerHTML = cardsHtml;

    // Bildirim Akışı
    const alertsEl = document.getElementById('recent-alerts-list');
    if (alertsEl) {
        if (!alerts || alerts.length === 0) {
            alertsEl.innerHTML = `<div class="text-xs text-slate-500 text-center py-4">Henüz aktif operasyon uyarısı bulunmuyor.</div>`;
        } else {
            alertsEl.innerHTML = alerts.slice(0, 8).map(a => {
                const isCrit = a.severity === 'Critical';
                const badge = a.isResolved
                    ? `<span class="text-[9px] font-bold px-1.5 py-0.5 rounded bg-emerald-950 text-emerald-400 border border-emerald-700">ÇÖZÜLDÜ</span>`
                    : `<span class="text-[9px] font-bold px-1.5 py-0.5 rounded ${isCrit ? 'bg-rose-950 text-rose-300 border border-rose-600' : 'bg-amber-950 text-amber-300 border border-amber-600'}">AKTİF</span>`;

                return `
                    <div class="p-3 rounded-xl bg-slate-950/70 border ${a.isResolved ? 'border-slate-800/70 opacity-75' : isCrit ? 'border-rose-500/50' : 'border-amber-500/50'} text-xs transition-all">
                        <div class="flex items-center justify-between gap-2 mb-1">
                            <span class="font-bold text-slate-100 truncate">${escapeHtml(a.title)}</span>
                            <div class="flex items-center gap-1.5 shrink-0">
                                ${badge}
                                <span class="text-[10px] font-mono text-slate-500">${new Date(a.createdAt).toLocaleTimeString('tr-TR')}</span>
                            </div>
                        </div>
                        <p class="text-[11px] text-slate-400 leading-snug">${escapeHtml(a.description)}</p>
                    </div>
                `;
            }).join('');
        }
    }
}

function renderActiveOperationsSummary(materialTransfers, workerTransfers) {
    const container = document.getElementById('active-operations-summary');
    if (!container) return;

    const rows = [];

    (materialTransfers || []).forEach(m => {
        const fromCity = resolvePlantCity(m.fromPlant, m.fromPlantId);
        const toCity = resolvePlantCity(m.toPlant, m.toPlantId);
        const isProp = m.status === 'Proposed';

        rows.push(`
            <div class="p-3 rounded-xl bg-slate-950/80 border ${isProp ? 'border-amber-500/60' : 'border-emerald-500/50'} flex flex-wrap items-center justify-between gap-3 text-xs">
                <div class="flex items-center gap-3">
                    <div class="w-9 h-9 rounded-xl ${isProp ? 'bg-amber-500/20 text-amber-400' : 'bg-emerald-500/20 text-emerald-400'} flex items-center justify-center">
                        <i class="fa-solid fa-truck-fast"></i>
                    </div>
                    <div>
                        <div class="font-bold text-white flex items-center gap-2">
                            <span>Malzeme Sevkiyatı: ${escapeHtml(m.materialName)} (${Number(m.quantity)} ${escapeHtml(m.unit)})</span>
                            <span class="text-[10px] font-bold px-2 py-0.5 rounded bg-slate-900 text-cyan-300 border border-slate-700">
                                ${escapeHtml(fromCity)} ➔ ${escapeHtml(toCity)}
                            </span>
                        </div>
                        <div class="text-[11px] text-slate-400 mt-0.5">
                            Sevkiyat Maliyeti: €${Number(m.estimatedCost).toLocaleString('tr-TR')} • Önlenen Gecikme Cezası: €${Number(m.preventedPenaltyAmount).toLocaleString('tr-TR')}
                        </div>
                    </div>
                </div>
                <div>
                    ${isProp ? `
                        <button onclick="approveMaterialTransfer(${Number(m.id)}, ${escapeAttrArg(fromCity)}, ${escapeAttrArg(toCity)})"
                                class="px-3 py-1.5 rounded-xl bg-emerald-600 hover:bg-emerald-500 text-white font-bold text-xs cursor-pointer">
                            <i class="fa-solid fa-check mr-1"></i> Onayla & Başlat
                        </button>
                    ` : `
                        <span class="px-2.5 py-1 rounded-lg bg-emerald-950 text-emerald-300 border border-emerald-600/50 font-bold text-[11px]">
                            <i class="fa-solid fa-route mr-1"></i> Araç Yolda (${escapeHtml(fromCity)} ➔ ${escapeHtml(toCity)})
                        </span>
                    `}
                </div>
            </div>
        `);
    });

    (workerTransfers || []).forEach(o => {
        if (o.status === 'Cancelled') return;
        const fromCity = resolvePlantCity(o.fromPlant, o.fromPlantId);
        const toCity = resolvePlantCity(o.toPlant, o.toPlantId);
        const isProp = o.status === 'Proposed';
        const isActive = o.status === 'InTransit';

        rows.push(`
            <div class="p-3 rounded-xl bg-slate-950/80 border ${isProp ? 'border-amber-500/60' : isActive ? 'border-cyan-500/50' : 'border-slate-800'} flex flex-wrap items-center justify-between gap-3 text-xs">
                <div class="flex items-center gap-3">
                    <div class="w-9 h-9 rounded-xl ${isProp ? 'bg-amber-500/20 text-amber-400' : isActive ? 'bg-cyan-500/20 text-cyan-400' : 'bg-slate-800 text-slate-400'} flex items-center justify-center">
                        <i class="fa-solid fa-user-check"></i>
                    </div>
                    <div>
                        <div class="font-bold text-white flex items-center gap-2">
                            <span>Personel Görevlendirme: ${escapeHtml(o.worker?.fullName || 'Uzman Operatör')} (${escapeHtml(o.worker?.roleOrSkill || 'Uzman')})</span>
                            <span class="text-[10px] font-bold px-2 py-0.5 rounded bg-slate-900 text-cyan-300 border border-slate-700">
                                ${escapeHtml(fromCity)} ➔ ${escapeHtml(toCity)}
                            </span>
                        </div>
                        <div class="text-[11px] text-slate-400 mt-0.5">${escapeHtml(o.reason)}</div>
                    </div>
                </div>
                <div class="flex items-center gap-2">
                    ${isProp ? `
                        <button onclick="approveWorkerTransfer(${Number(o.id)})" class="px-3 py-1.5 rounded-xl bg-amber-500 hover:bg-amber-400 text-slate-950 font-extrabold text-xs cursor-pointer">
                            <i class="fa-solid fa-check mr-1"></i> Onayla & Görevlendir
                        </button>
                    ` : isActive ? `
                        <button onclick="recallWorker(${Number(o.id)})" class="px-3 py-1.5 rounded-xl bg-slate-800 hover:bg-cyan-700 text-slate-200 border border-slate-700 font-bold text-xs cursor-pointer">
                            <i class="fa-solid fa-rotate-left mr-1"></i> Görevi Tamamla & Ana Fabrikaya Döndür
                        </button>
                    ` : `
                        <span class="px-2.5 py-1 rounded-lg bg-slate-900 text-slate-400 font-semibold text-[11px]">TAMAMLANDI</span>
                    `}
                </div>
            </div>
        `);
    });

    if (rows.length === 0) {
        container.innerHTML = `<div class="text-xs text-slate-500 text-center py-4">Henüz aktif malzeme sevkiyatı veya geçici personel görevlendirmesi bulunmuyor.</div>`;
    } else {
        container.innerHTML = rows.join('');
    }
}

async function parseApiError(res, fallback) {
    try {
        const data = await res.json();
        return data?.error || data?.message || fallback;
    } catch (_) {
        return `${fallback} (HTTP ${res.status})`;
    }
}

function isLiveStreamConnected() {
    return Boolean(connection && window.signalR && connection.state === signalR.HubConnectionState.Connected);
}

// ==========================================
// 8. SİMÜLASYON VE ONAY AKSİYONLARI
// ==========================================
async function triggerRotterdamSimulation() {
    try {
        const res = await fetch('/api/v1/operations/simulate/rotterdam-crisis', { method: 'POST' });
        if (res.ok) {
            const data = await res.json();
            playAlertTone('critical');
            showToast('Deniz Lojistik Krizi Başlatıldı', data.scenarioTitle || 'Deniz sevkiyat gecikmesi tespit edildi. Alternatif stok aranıyor...', 'critical');
            if (!isLiveStreamConnected()) {
                setTimeout(() => {
                    fetchState();
                    fetchRadar();
                }, 350);
            }
            focusMapOn(40.55, 28.9, 8);
        } else {
            showToast('Senaryo Başlatılamadı', await parseApiError(res, 'Deniz krizi başlatılırken hata oluştu.'), 'critical');
        }
    } catch (e) {
        console.error(e);
        showToast('Bağlantı Hatası', 'Sunucuya ulaşılamadı.', 'critical');
    }
}

async function triggerAirCargoSimulation() {
    try {
        const res = await fetch('/api/v1/operations/simulate/air-cargo-delay', { method: 'POST' });
        if (res.ok) {
            const data = await res.json();
            playAlertTone('critical');
            showToast('Hava Kargo Rötarı Başlatıldı', data.scenarioTitle || 'Hava kargo sevkiyatı gecikti. En yakın fabrika emniyet stoğu analiz ediliyor...', 'warning');
            if (!isLiveStreamConnected()) {
                setTimeout(() => {
                    fetchState();
                    fetchRadar();
                }, 350);
            }
            focusMapOn(39.55, 28.1, 7);
        } else {
            showToast('Senaryo Başlatılamadı', await parseApiError(res, 'Hava kargo krizi başlatılırken hata oluştu.'), 'critical');
        }
    } catch (e) {
        console.error(e);
        showToast('Bağlantı Hatası', 'Sunucuya ulaşılamadı.', 'critical');
    }
}

async function triggerWorkforceSimulation() {
    try {
        const res = await fetch('/api/v1/operations/simulate/workforce-bottleneck', { method: 'POST' });
        if (res.ok) {
            const data = await res.json();
            playAlertTone('critical');
            showToast('Personel Darboğazı Başlatıldı', data.scenarioTitle || 'Üretim hattı için rotasyona uygun uzman personel taranıyor...', 'warning');
            if (!isLiveStreamConnected()) {
                setTimeout(() => {
                    fetchState();
                    fetchWorkforceOverview();
                }, 350);
            }
        } else {
            showToast('Senaryo Başlatılamadı', await parseApiError(res, 'Personel darboğazı başlatılırken hata oluştu.'), 'critical');
        }
    } catch (e) {
        console.error(e);
        showToast('Bağlantı Hatası', 'Sunucuya ulaşılamadı.', 'critical');
    }
}

async function triggerRandomCrisisSimulation() {
    try {
        const res = await fetch('/api/v1/operations/simulate/random-crisis', { method: 'POST' });
        if (res.ok) {
            const data = await res.json();
            playAlertTone('critical');
            showToast('Rastgele Kriz Senaryosu Başlatıldı', data.scenarioTitle || 'Yeni operasyonel kriz tespit edildi, çözüm önerisi hazırlandı.', 'critical');
            if (!isLiveStreamConnected()) {
                setTimeout(() => {
                    fetchState();
                    fetchWorkforceOverview();
                    fetchRadar();
                }, 350);
            }
            focusMapOn(39.75, 28.35, 7);
        } else {
            showToast('Senaryo Başlatılamadı', await parseApiError(res, 'Rastgele kriz başlatılırken hata oluştu.'), 'critical');
        }
    } catch (e) {
        console.error(e);
        showToast('Bağlantı Hatası', 'Sunucuya ulaşılamadı.', 'critical');
    }
}

async function approveMaterialTransfer(orderId, fromCity = '', toCity = '') {
    try {
        const res = await fetch('/api/v1/operations/approve-action', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                actionType: 'MaterialTransfer',
                orderId: orderId,
                approvedBy: 'Kaynak Yönetim Uzmanı'
            })
        });
        if (res.ok) {
            playAlertTone('success');
            const isIzmirRoute =
                fromCity.toLocaleLowerCase('tr-TR').includes('izmir') ||
                toCity.toLocaleLowerCase('tr-TR').includes('izmir');
            showToast(
                'Sevkiyat Emri Onaylandı',
                `${fromCity || 'Donör Fabrika'} ➔ ${toCity || 'Kocaeli'} güzergahında ekspres taşıma aracı yola çıktı!`,
                'success'
            );
            if (!isLiveStreamConnected()) {
                await fetchState();
                await fetchRadar();
            }
            if (isIzmirRoute) {
                focusMapOn(39.65, 28.25, 7);
            } else {
                focusMapOn(40.52, 29.22, 9);
            }
        } else {
            playAlertTone('critical');
            showToast('Onay Hatası', await parseApiError(res, 'Malzeme transferi onaylanamadı.'), 'critical');
        }
    } catch (e) {
        console.error(e);
        showToast('Bağlantı Hatası', 'Sunucuya ulaşılamadı.', 'critical');
    }
}

async function approveWorkerTransfer(orderId) {
    try {
        const res = await fetch('/api/v1/operations/approve-action', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                actionType: 'WorkerTransfer',
                orderId: orderId,
                approvedBy: 'Kaynak Yönetim Uzmanı'
            })
        });
        if (res.ok) {
            playAlertTone('success');
            showToast('Personel Görevlendirme Onaylandı', 'Uzman operatör ekibi hedef fabrikaya sevk edildi.', 'success');
            if (!isLiveStreamConnected()) {
                await fetchState();
                await fetchWorkforceOverview();
                await fetchRadar();
            }
            focusMapOn(39.9, 28.6, 7);
        } else {
            playAlertTone('critical');
            showToast('Onay Hatası', await parseApiError(res, 'Personel görevlendirme onaylanamadı.'), 'critical');
        }
    } catch (e) {
        console.error(e);
        showToast('Bağlantı Hatası', 'Sunucuya ulaşılamadı.', 'critical');
    }
}

async function triggerResetSystem() {
    try {
        const res = await fetch('/api/v1/operations/reset', { method: 'POST' });
        if (res.ok) {
            playAlertTone('info');
            showToast('Operasyon Sıfırlandı', 'Tüm fabrikalar, stoklar, taşıma filosu ve personel kadroları başlangıç durumuna döndürüldü.', 'info');
            if (!isLiveStreamConnected()) {
                await fetchState();
                await fetchWorkforceOverview();
                await fetchRadar();
            }
            focusMapOn(39.75, 28.25, 7);
        } else {
            showToast('Sıfırlama Hatası', await parseApiError(res, 'Sistem sıfırlanamadı.'), 'critical');
        }
    } catch (e) {
        console.error(e);
        showToast('Bağlantı Hatası', 'Sunucuya ulaşılamadı.', 'critical');
    }
}

// ==========================================
// 9. İŞÇİ GÖREVLENDİRME VE AKILLI EŞLEŞTİRME
// ==========================================
async function fetchWorkforceOverview() {
    try {
        const res = await fetch('/api/v1/workforce/overview');
        if (res.ok) {
            workforceData = await res.json();
            if (currentState && currentState.plants) {
                renderPlantCards(currentState.plants);
            }
        }
    } catch (e) {
        console.error("Workforce fetch error:", e);
    }
}

function openSmartWorkforceMatcherModal() {
    const optPlant = document.getElementById('wf-opt-plant');
    if (optPlant && currentState && currentState.plants) {
        optPlant.innerHTML = currentState.plants.map(p => `<option value="${Number(p.id)}">${escapeHtml(p.name)}</option>`).join('');
    }
    const optRole = document.getElementById('wf-opt-role');
    if (optRole) {
        const rolesFromApi = workforceData?.roles && workforceData.roles.length > 0
            ? workforceData.roles
            : [...new Set((currentState?.mobileWorkers || []).map(w => w.roleOrSkill).filter(Boolean))].sort();
        if (rolesFromApi.length > 0) {
            optRole.innerHTML = rolesFromApi.map(r => `<option value="${escapeHtml(r)}">${escapeHtml(r)}</option>`).join('') +
                '<option value="Tümü">Tüm Uzmanlık Alanları</option>';
        }
    }
    const resultsBox = document.getElementById('wf-opt-results');
    if (resultsBox) {
        resultsBox.innerHTML = `<div class="text-xs text-slate-500 text-center py-3">Kriterleri seçip yukarıdaki butona tıklayarak uygun adayları görüntüleyebilirsiniz.</div>`;
    }
    const modal = document.getElementById('smart-matcher-modal');
    if (modal) {
        modal.classList.remove('hidden');
        modal.classList.add('flex');
    }
}

function closeSmartWorkforceMatcherModal() {
    const modal = document.getElementById('smart-matcher-modal');
    if (modal) {
        modal.classList.add('hidden');
        modal.classList.remove('flex');
    }
}

async function runWorkforceOptimizer() {
    const targetPlantId = parseInt(document.getElementById('wf-opt-plant')?.value || '1', 10);
    const requiredRoleOrSkill = document.getElementById('wf-opt-role')?.value || 'CNC Operatörü';
    const deficitCount = parseInt(document.getElementById('wf-opt-count')?.value || '3', 10);
    const durationDays = parseInt(document.getElementById('wf-opt-days')?.value || '7', 10);

    try {
        const res = await fetch('/api/v1/workforce/optimize', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                targetPlantId,
                requiredRoleOrSkill,
                deficitCount,
                durationDays
            })
        });

        if (res.ok) {
            const data = await res.json();
            playAlertTone('info');
            const box = document.getElementById('wf-opt-results');
            if (!box) return;

            if (!data.candidates || data.candidates.length === 0) {
                box.innerHTML = `<div class="p-3 rounded-xl bg-rose-950/60 border border-rose-500/50 text-xs text-rose-200">
                    Seçilen kriterlerde diğer fabrikalarda kadro fazlası ve rotasyon onayı bulunan personel bulunamadı.
                </div>`;
                return;
            }

            box.innerHTML = `
                <div class="flex items-center justify-between text-xs mb-2">
                    <span class="font-bold text-emerald-400"><i class="fa-solid fa-circle-check mr-1"></i> ${Number(data.matchedCount)} Uygun Uzman Bulundu</span>
                    <span class="font-mono text-amber-300">Toplam Maliyet: €${Number(data.totalCost).toLocaleString('tr-TR')}</span>
                </div>
                <div class="space-y-2">
                    ${data.candidates.map((c, idx) => `
                        <div class="p-2.5 rounded-xl bg-slate-950 border border-slate-800 flex items-center justify-between gap-2 text-xs">
                            <div>
                                <div class="font-bold text-white">#${idx + 1} ${escapeHtml(c.worker.fullName)} <span class="text-cyan-400 font-mono">(Yetkinlik: ${Number(c.worker.skillScore)}/100)</span></div>
                                <div class="text-[10px] text-slate-400">${escapeHtml(c.worker.currentPlant?.city || 'Bursa')} • ${escapeHtml(c.worker.roleOrSkill)} • Günlük Harcırah: €${Number(c.dailyAllowance)}</div>
                            </div>
                            <button onclick="quickDispatchWorkerFromMatcher(${Number(c.worker.id)}, ${targetPlantId}, ${durationDays})"
                                    class="px-2.5 py-1.5 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white font-bold text-[11px] shrink-0 cursor-pointer">
                                <i class="fa-solid fa-paper-plane mr-1"></i> Görevlendir (€${Number(c.totalEstimatedCost)})
                            </button>
                        </div>
                    `).join('')}
                </div>
            `;
        } else {
            showToast('Eşleştirme Hatası', await parseApiError(res, 'Uygun personel listesi getirilemedi.'), 'critical');
        }
    } catch (e) {
        console.error(e);
        showToast('Bağlantı Hatası', 'Sunucuya ulaşılamadı.', 'critical');
    }
}

function attemptLockedWorkerDispatch(fullName) {
    playAlertTone('critical');
    showToast('Sözleşme Kısıtı', `${fullName} isimli personelin sözleşmesi sabit kadro statüsünde olduğu için başka bir fabrikaya görevlendirilemez.`, 'critical');
}

function openDispatchModal(workerId) {
    const pool = currentState?.mobileWorkers || workforceData?.workers || [];
    const worker = pool.find(w => w.id === workerId);
    if (!worker) return;

    document.getElementById('dispatch-worker-id').value = worker.id;
    document.getElementById('dispatch-worker-name').innerText = `${worker.fullName} (${worker.employeeCode})`;
    document.getElementById('dispatch-worker-meta').innerText = `${worker.roleOrSkill} • Yetkinlik: ${worker.skillScore}/100 • Günlük Harcırah: €${worker.mobilityAllowanceRate} • Mevcut Fabrika: ${worker.currentPlant?.city || resolvePlantCity(worker.currentPlant, worker.currentPlantId)}`;

    const durationEl = document.getElementById('dispatch-duration-days');
    if (durationEl) durationEl.value = '7';
    const reasonEl = document.getElementById('dispatch-reason');
    if (reasonEl) reasonEl.value = 'Üretim hattı kapasite dengeleme ve uzman personel takviyesi';

    const targetSelect = document.getElementById('dispatch-target-plant');
    if (targetSelect && currentState && currentState.plants) {
        const otherPlants = currentState.plants.filter(p => p.id !== worker.currentPlantId);
        targetSelect.innerHTML = otherPlants.map(p => `<option value="${Number(p.id)}">${escapeHtml(p.name)}</option>`).join('');
    }

    const modal = document.getElementById('worker-dispatch-modal');
    if (modal) {
        modal.classList.remove('hidden');
        modal.classList.add('flex');
    }
}

function closeDispatchModal() {
    const modal = document.getElementById('worker-dispatch-modal');
    if (modal) {
        modal.classList.add('hidden');
        modal.classList.remove('flex');
    }
}

async function confirmDirectWorkerDispatch() {
    const workerId = parseInt(document.getElementById('dispatch-worker-id').value, 10);
    const toPlantId = parseInt(document.getElementById('dispatch-target-plant').value, 10);
    const durationDays = parseInt(document.getElementById('dispatch-duration-days').value || '7', 10);
    const reason = document.getElementById('dispatch-reason').value;

    const ok = await quickDispatchWorker(workerId, toPlantId, durationDays, reason);
    if (ok) {
        closeDispatchModal();
    }
}

async function quickDispatchWorkerFromMatcher(workerId, toPlantId, durationDays = 7) {
    const ok = await quickDispatchWorker(workerId, toPlantId, durationDays);
    if (ok) {
        closeSmartWorkforceMatcherModal();
    }
}

async function quickDispatchWorker(workerId, toPlantId, durationDays = 7, reason = 'Üretim hattı kapasite dengeleme görevlendirmesi') {
    try {
        const res = await fetch('/api/v1/workforce/dispatch', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                workerId,
                toPlantId,
                durationDays,
                reason
            })
        });

        const data = await res.json();
        if (res.ok) {
            playAlertTone('success');
            const targetPlantObj = (currentState?.plants || []).find(p => p.id === toPlantId);
            if (targetPlantObj && targetPlantObj.code) {
                plantSubTabs[targetPlantObj.code] = 'workers';
            }
            const workerName = data.order?.worker?.fullName || 'Uzman Personel';
            const targetCity = targetPlantObj?.city || 'Hedef Fabrika';
            showToast('Personel Transfer Ediliyor', `${workerName} ➔ ${targetCity} tesisine atandı ve fabrika kadrosuna eklendi!`, 'success');
            if (!isLiveStreamConnected()) {
                await fetchState();
                await fetchWorkforceOverview();
                await fetchRadar();
            }
            return true;
        } else {
            playAlertTone('critical');
            showToast('Görevlendirme Yapılamadı', data.error || 'İşlem gerçekleştirilemedi.', 'critical');
            return false;
        }
    } catch (e) {
        console.error(e);
        showToast('Bağlantı Hatası', 'Sunucuya ulaşılamadı.', 'critical');
        return false;
    }
}

async function recallWorker(orderId) {
    try {
        const res = await fetch(`/api/v1/workforce/recall/${orderId}`, { method: 'POST' });
        if (res.ok) {
            playAlertTone('info');
            showToast('Görev Tamamlandı', 'Personel ana fabrikasına döndürüldü ve kadro sayıları güncellendi.', 'info');
            if (!isLiveStreamConnected()) {
                await fetchState();
                await fetchWorkforceOverview();
                await fetchRadar();
            }
        } else {
            playAlertTone('critical');
            showToast('Geri Döndürme Hatası', await parseApiError(res, 'Personel geri döndürülemedi.'), 'critical');
        }
    } catch (e) {
        console.error(e);
        showToast('Bağlantı Hatası', 'Sunucuya ulaşılamadı.', 'critical');
    }
}

// ==========================================
// 10. VERİ AKIŞI VE GERÇEK ZAMANLI GÜNCELLEME
// ==========================================
async function fetchState() {
    try {
        const res = await fetch('/api/v1/operations/state');
        if (res.ok) {
            const state = await res.json();
            updateUI(state);
        }
    } catch (e) {
        console.error("State fetch error:", e);
    }
}

async function fetchRadar() {
    try {
        const res = await fetch('/api/v1/operations/radar');
        if (res.ok) {
            const radar = await res.json();
            currentRadar = radar;
            renderRadarOnMap(radar);

            const landCountEl = document.getElementById('live-land-count');
            const flightCountEl = document.getElementById('live-flight-count');
            const vesselCountEl = document.getElementById('live-vessel-count');
            const sourceBadgeEl = document.getElementById('radar-source-badge');

            if (landCountEl) landCountEl.innerText = (radar.landVehicles || []).length;
            if (flightCountEl) flightCountEl.innerText = (radar.aircraft || []).length;
            if (vesselCountEl) vesselCountEl.innerText = (radar.vessels || []).length;
            if (sourceBadgeEl) {
                const totalVehicles = (radar.landVehicles || []).length + (radar.aircraft || []).length + (radar.vessels || []).length;
                sourceBadgeEl.innerText = `Canlı Filo Takibi (${totalVehicles} Aktif Araç)`;
            }

            if (currentState && currentState.plants) {
                renderPlantCards(currentState.plants);
            }
        }
    } catch (e) {
        console.error("Radar fetch error:", e);
    }
}

function setLiveStreamStatus(status, label) {
    const statusEl = document.getElementById('live-stream-badge');
    const dotEl = document.getElementById('live-stream-dot');
    if (statusEl) {
        statusEl.innerText = label;
        statusEl.className = status === 'connected'
            ? 'text-emerald-400 text-xs font-bold'
            : status === 'reconnecting'
            ? 'text-amber-300 text-xs font-bold'
            : 'text-rose-400 text-xs font-bold';
    }
    if (dotEl) {
        dotEl.className = status === 'connected'
            ? 'w-2 h-2 rounded-full bg-emerald-400 animate-ping'
            : status === 'reconnecting'
            ? 'w-2 h-2 rounded-full bg-amber-400 animate-pulse'
            : 'w-2 h-2 rounded-full bg-rose-500';
    }
}

function setupLiveStream() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/control-tower')
        .withAutomaticReconnect([0, 2000, 5000, 10000])
        .build();

    connection.on('ConnectedConfirmation', () => {
        setLiveStreamStatus('connected', 'Canlı Veri Akışı Aktif');
    });

    connection.on('ReceiveStateUpdate', (state) => {
        updateUI(state);
        fetchRadar();
        fetchWorkforceOverview();
    });

    connection.on('ReceiveTelemetryAlert', (alert) => {
        if (!alert) return;
        const tone = alert.severity === 'Critical' ? 'critical' : 'warning';
        playAlertTone(tone);
        showToast(alert.title || 'Yeni Operasyon Uyarısı', alert.description || '', tone);
    });

    connection.on('ReceiveActionApproved', (payload) => {
        if (!payload?.message) return;
        playAlertTone('success');
        showToast('Operasyon Kararı Onaylandı', payload.message, 'success');
    });

    connection.onreconnecting(() => {
        setLiveStreamStatus('reconnecting', 'Yeniden Bağlanıyor...');
    });

    connection.onreconnected(() => {
        setLiveStreamStatus('connected', 'Canlı Veri Akışı Aktif');
        fetchState();
        fetchRadar();
        fetchWorkforceOverview();
    });

    connection.onclose(() => {
        setLiveStreamStatus('disconnected', 'Bağlantı Kesildi');
        setTimeout(() => {
            if (connection && window.signalR && connection.state === signalR.HubConnectionState.Disconnected) {
                connection.start()
                    .then(() => {
                        setLiveStreamStatus('connected', 'Canlı Veri Akışı Aktif');
                        fetchState();
                        fetchRadar();
                        fetchWorkforceOverview();
                    })
                    .catch(err => console.error('Live reconnect error:', err));
            }
        }, 5000);
    });

    connection.start().then(() => {
        setLiveStreamStatus('connected', 'Canlı Veri Akışı Aktif');
    }).catch(err => {
        console.error('Live connection error:', err);
        setLiveStreamStatus('disconnected', 'Canlı Bağlantı Beklemede');
    });
}

document.addEventListener('DOMContentLoaded', () => {
    initMap();
    setupLiveStream();
    fetchState();
    fetchWorkforceOverview();
    fetchRadar();

    // Her 10 saniyede bir canlı filo konumlarını yenile
    setInterval(fetchRadar, 10000);

    // Canlı Saat
    const updateClock = () => {
        const el = document.getElementById('live-clock');
        if (el) el.innerText = new Date().toLocaleTimeString('tr-TR');
    };
    updateClock();
    setInterval(updateClock, 1000);

    // Escape key closes any active modal
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') {
            closeVehicleDetailModal();
            closeDispatchModal();
            closeSmartWorkforceMatcherModal();
        }
    });
});
