let map = null;
let mapElementId = null;
let markers = {};
let initialLocationSet = false;
let watchId = null;
let heartbeatId = null;
let latestPosition = null;
let latestBatteryLevel = null;
let batteryPromise = null;
const locationHeartbeatMs = 60000;

window.initLeafletMap = (elementId, lat, lng) => {
    console.log("initLeafletMap called", elementId, lat, lng);
    const element = document.getElementById(elementId);

    if (!element) {
        console.error("Map element not found:", elementId);
        return;
    }

    if (typeof L === "undefined") {
        console.error("Leaflet is not loaded.");
        element.innerHTML = "<h2 style='padding:20px'>Leaflet map library not loaded.</h2>";
        return;
    }

    if (map && mapElementId === elementId) {
        setTimeout(() => map.invalidateSize(), 200);
        return;
    }

    element.innerHTML = "";

    mapElementId = elementId;
    map = L.map(elementId).setView([lat, lng], 14);
    window.rideMateMap = map;

    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        maxZoom: 19,
        attribution: "© OpenStreetMap contributors"
    }).addTo(map);

    setTimeout(() => map.invalidateSize(), 300);
    setTimeout(() => map.invalidateSize(), 1000);
};

window.invalidateRideMateMap = () => {
    if (map) {
        map.invalidateSize();
    }
};

window.startLocationTracking = (dotNetHelper) => {
    window.currentRideMateDotNetHelper = dotNetHelper;
    batteryPromise = refreshBatteryLevel();

    if (!navigator.geolocation) {
        dotNetHelper.invokeMethodAsync("ReportLocationError", "Location not supported.", latestBatteryLevel, false);
        return;
    }

    const sendPosition = async (position) => {
        latestPosition = position;
        await sendRideMatePosition(position, dotNetHelper, "GPS");
    };

    const handleError = (error) => {
        let msg = "Unable to get location.";

        const permissionDenied = error.code === 1;

        if (permissionDenied) msg = "Location permission denied. Use Allow Location or browser site settings to enable it.";
        if (error.code === 2) msg = "Location unavailable.";
        if (error.code === 3) msg = "Location request timed out.";

        dotNetHelper.invokeMethodAsync("ReportLocationError", msg, latestBatteryLevel, permissionDenied);
    };

    navigator.geolocation.getCurrentPosition(sendPosition, handleError, {
        enableHighAccuracy: true,
        timeout: 15000,
        maximumAge: 0
    });

    if (watchId) {
        navigator.geolocation.clearWatch(watchId);
    }

    watchId = navigator.geolocation.watchPosition(sendPosition, handleError, {
        enableHighAccuracy: true,
        timeout: 15000,
        maximumAge: 0
    });

    if (heartbeatId) {
        clearInterval(heartbeatId);
    }

    heartbeatId = setInterval(() => {
        if (!latestPosition || !window.currentRideMateDotNetHelper) return;
        sendRideMatePosition(latestPosition, window.currentRideMateDotNetHelper, "heartbeat");
    }, locationHeartbeatMs);
};

window.requestRideMateLocationPermission = () => {
    if (!navigator.geolocation || !window.currentRideMateDotNetHelper) return;

    batteryPromise = refreshBatteryLevel();

    navigator.geolocation.getCurrentPosition(
        async position => {
            await refreshBatteryLevel();
            latestPosition = position;

            sendRideMatePosition(
                position,
                window.currentRideMateDotNetHelper,
                "permission"
            ).catch(err => console.error("Location permission callback failed:", err));
        },
        error => {
            const permissionDenied = error.code === 1;
            const msg = permissionDenied
                ? "Location is still blocked. Open browser site settings and allow location for this app."
                : "Location could not be refreshed.";

            window.currentRideMateDotNetHelper.invokeMethodAsync(
                "ReportLocationError",
                msg,
                latestBatteryLevel,
                permissionDenied
            ).catch(err => console.error("Location permission error callback failed:", err));
        },
        {
            enableHighAccuracy: true,
            timeout: 15000,
            maximumAge: 0
        }
    );
};

window.stopRideMateLocationTracking = () => {
    batteryPromise = refreshBatteryLevel();

    if (watchId) {
        navigator.geolocation.clearWatch(watchId);
        watchId = null;
    }

    if (heartbeatId) {
        clearInterval(heartbeatId);
        heartbeatId = null;
    }

    if (window.currentRideMateDotNetHelper) {
        window.currentRideMateDotNetHelper.invokeMethodAsync(
            "ReportLocationError",
            "Location sharing paused.",
            latestBatteryLevel,
            false
        ).catch(err => console.error("Location stop callback failed:", err));
    }
};

window.updateUserMarker = (userId, lat, lng, avatarUrl, displayName, isMe, isOffline, isLocationPermissionDenied) => {
    if (!map || typeof L === "undefined") {
        console.error("Map not ready for marker.");
        return;
    }

    const name = displayName || "RideMate User";
    const avatar = avatarUrl || "/favicon.png";
    const unavailable = Boolean(isOffline || isLocationPermissionDenied);
    const ringColor = unavailable ? "#94a3b8" : (isMe ? "#2563eb" : "#16a34a");
    const badgeColor = unavailable ? "#475569" : "#111827";
    const opacity = unavailable ? ".58" : "1";
    const statusText = isLocationPermissionDenied ? "Location off" : (unavailable ? "Offline" : name);

    const icon = L.divIcon({
        className: "",
        html: `
            <div style="display:flex;flex-direction:column;align-items:center;">
                <div style="height:48px;width:48px;border-radius:999px;border:4px solid white;background:white;box-shadow:0 12px 28px rgba(15,23,42,.22),0 0 0 4px ${ringColor};overflow:hidden;opacity:${opacity};filter:${unavailable ? "grayscale(1)" : "none"};transition:transform .18s ease, box-shadow .18s ease;">
                    <img src="${avatar}" style="height:100%;width:100%;object-fit:cover;" />
                </div>
                <div style="margin-top:7px;background:${badgeColor};color:white;padding:5px 9px;border-radius:999px;font-size:12px;font-weight:800;box-shadow:0 10px 22px rgba(15,23,42,.18);">
                    ${escapeHtml(statusText)}
                </div>
            </div>
        `,
        iconSize: [90, 70],
        iconAnchor: [45, 35]
    });

    const offset = getMarkerOffset(userId, lat, lng);
    const pos = [lat + offset.lat, lng + offset.lng];

    if (markers[userId]) {
        markers[userId].setLatLng(pos);
        markers[userId].setIcon(icon);
    } else {
        markers[userId] = L.marker(pos, { icon }).addTo(map);
    }

    markers[userId].off("click");
    markers[userId].on("click", () => {
        const currentPos = markers[userId].getLatLng();
        map.setView(currentPos, 18, { animate: true });
        const status = isLocationPermissionDenied ? "Location permission denied" : (unavailable ? "Offline" : "Live now");
        markers[userId].bindPopup(`<b>${escapeHtml(name)}</b><br><span>${escapeHtml(status)}</span>`).openPopup();
    });

    if (isMe && !initialLocationSet) {
        map.setView(pos, 17);
        initialLocationSet = true;
    }
};

window.fitAllRideMateMarkers = () => {
    if (!map) return;

    const allMarkers = Object.values(markers);

    if (allMarkers.length === 0) return;

    const group = L.featureGroup(allMarkers);
    map.fitBounds(group.getBounds(), {
        padding: [80, 80],
        maxZoom: 16
    });
};

window.focusRideMateMarker = (userId) => {
    if (!map || !markers[userId]) return;

    const pos = markers[userId].getLatLng();
    map.setView(pos, 18, { animate: true });
    markers[userId].fire("click");
};

function escapeHtml(value) {
    return String(value)
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#039;");
}

function getMarkerOffset(userId, lat, lng) {
    const sameSpotCount = Object.values(markers).filter(marker => {
        const p = marker.getLatLng();
        return Math.abs(p.lat - lat) < 0.00005 &&
               Math.abs(p.lng - lng) < 0.00005;
    }).length;

    if (sameSpotCount === 0) {
        return { lat: 0, lng: 0 };
    }

    const angle = sameSpotCount * 45 * Math.PI / 180;
    const distance = 0.00012;

    return {
        lat: Math.sin(angle) * distance,
        lng: Math.cos(angle) * distance
    };
}

window.clearRideMateMarkers = () => {
    if (!map) return;

    Object.values(markers).forEach(marker => {
        map.removeLayer(marker);
    });

    markers = {};
    initialLocationSet = false;
};

window.removeRideMateMarker = (userId) => {
    if (!map || !markers[userId]) return;

    map.removeLayer(markers[userId]);
    delete markers[userId];
};

window.requestCurrentRideMateLocation = () => {
    if (!navigator.geolocation || !window.currentRideMateDotNetHelper) return;

    batteryPromise = refreshBatteryLevel();

    navigator.geolocation.getCurrentPosition(
        async position => {
            await refreshBatteryLevel();
            latestPosition = position;

            sendRideMatePosition(
                position,
                window.currentRideMateDotNetHelper,
                "refresh"
            ).catch(err => console.error("Location refresh failed:", err));
        },
        error => {
            console.error("Location refresh error:", error);

            const permissionDenied = error.code === 1;
            const msg = permissionDenied
                ? "Location permission denied. Enable it from location settings to share live location."
                : "Location unavailable. Showing your last known location.";

            window.currentRideMateDotNetHelper.invokeMethodAsync(
                "ReportLocationError",
                msg,
                latestBatteryLevel,
                permissionDenied
            ).catch(err => console.error("Location refresh report failed:", err));
        },
        {
            enableHighAccuracy: true,
            timeout: 15000,
            maximumAge: 0
        }
    );
};

async function sendRideMatePosition(position, dotNetHelper, source) {
    if (!position || !dotNetHelper) return;

    await refreshBatteryLevel();
    console.log("GPS SENT TO BLAZOR:", source, position.coords.latitude, position.coords.longitude);

    return dotNetHelper.invokeMethodAsync(
        "UpdateUserPosition",
        position.coords.latitude,
        position.coords.longitude,
        latestBatteryLevel
    );
}

async function refreshBatteryLevel() {
    if (batteryPromise) {
        try {
            await batteryPromise;
        } finally {
            batteryPromise = null;
        }
    }

    try {
        if (!navigator.getBattery) {
            latestBatteryLevel = null;
            return;
        }

        const battery = await navigator.getBattery();
        const updateLevel = () => {
            latestBatteryLevel = Math.round(battery.level * 100);
        };

        updateLevel();
        battery.removeEventListener?.("levelchange", updateLevel);
        battery.addEventListener?.("levelchange", updateLevel);
    } catch {
        latestBatteryLevel = null;
    }
}
