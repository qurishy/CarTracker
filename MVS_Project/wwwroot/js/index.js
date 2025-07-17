// Global variables
let map;
let markers = {};
let carPaths = {};
let connection;
let isTracking = false;
let autoRefreshInterval;
let selectedCarId = null;
// routing auto suggesion and rout info adding
// Global variables
let routingControl = null;
let routeMarkers = [];
let routeLine = null;
const ORS_API_KEY = 'eyJvcmciOiI1YjNjZTM1OTc4NTExMTAwMDFjZjYyNDgiLCJpZCI6Ijc4YzU1OGVlODQyNzQ5Yzc4MmE3MDc3ZjcwZGYyMzMyIiwiaCI6Im11cm11cjY0In0='


// Initialize map
function initMap() {
    // Afghanistan boundaries
    const afgBounds = L.latLngBounds(
        L.latLng(29.3772, 60.5042), // SW corner
        L.latLng(38.4911, 74.9157)  // NE corner
    );

    map = L.map('map', {
        center: [33.93911, 67.709953],
        zoom: 6,
        maxBounds: afgBounds,
        maxBoundsViscosity: 1.0
    });

    L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', {
        attribution: 'Tiles © Esri'
    }).addTo(map);
}

// Initialize routing functionality
function initRouting() {
    // Set up autocomplete for inputs
    document.getElementById('origin').addEventListener('input', function() {
        fetchSuggestions(this.value, 'originSuggestions');
    });

    document.getElementById('destination').addEventListener('input', function() {
        fetchSuggestions(this.value, 'destinationSuggestions');
    });

    // Event listener for find route button
    document.getElementById('findRouteBtn').addEventListener('click', getRoute);
    document.getElementById('clearRouteBtn').addEventListener('click',clearRoute);

}

// Fetch autocomplete suggestions
function fetchSuggestions(query, datalistId) {
    if (!query || query.length < 3) return;

    fetch(`https://api.openrouteservice.org/geocode/autocomplete?api_key=${ORS_API_KEY}&text=${encodeURIComponent(query)}`)
        .then(res => res.json())
        .then(data => {
            const list = document.getElementById(datalistId);
            list.innerHTML = '';
            data.features.forEach(feature => {
                const option = document.createElement('option');
                option.value = feature.properties.label;
                list.appendChild(option);
            });
        })
        .catch(err => console.error('Autocomplete error:', err));
}

// Initialize routing functionality
function initRouting() {
    // Set up autocomplete for inputs
    document.getElementById('origin').addEventListener('input', function() {
        fetchSuggestions(this.value, 'originSuggestions');
    });

    document.getElementById('destination').addEventListener('input', function() {
        fetchSuggestions(this.value, 'destinationSuggestions');
    });

    // Event listener for find route button
    document.getElementById('findRouteBtn').addEventListener('click', getRoute);
}

// Fetch autocomplete suggestions
function fetchSuggestions(query, datalistId) {
    if (!query || query.length < 3) return;

    fetch(`https://api.openrouteservice.org/geocode/autocomplete?api_key=${ORS_API_KEY}&text=${encodeURIComponent(query)}&boundary.country=AF`)
        .then(res => res.json())
        .then(data => {
            const list = document.getElementById(datalistId);
            list.innerHTML = '';
            if (data.features) {
                data.features.forEach(feature => {
                    const option = document.createElement('option');
                    option.value = feature.properties.label;
                    list.appendChild(option);
                });
            }
        })
        .catch(err => {
            console.error('Autocomplete error:', err);
            showAlert('danger', 'Failed to fetch location suggestions');
        });
}

// Get route between origin and destination
async function getRoute() {
    const origin = document.getElementById("origin").value;
    const destination = document.getElementById("destination").value;

    if (!origin || !destination) {
        showAlert("warning", "Please enter both origin and destination");
        return;
    }

    try {
        // Show loading state
        const routeInfo = document.getElementById('routeInfo');
        routeInfo.style.display = 'block';
        routeInfo.innerHTML = '<div class="text-center"><div class="spinner-border spinner-border-sm" role="status"></div> Calculating route...</div>';

        // Geocode origin and destination
        const [fromGeo, toGeo] = await Promise.all([
            fetch(`https://api.openrouteservice.org/geocode/search?api_key=${ORS_API_KEY}&text=${encodeURIComponent(origin)}`)
                .then(res => res.json()),
            fetch(`https://api.openrouteservice.org/geocode/search?api_key=${ORS_API_KEY}&text=${encodeURIComponent(destination)}`)
                .then(res => res.json())
        ]);

        if (!fromGeo.features || !fromGeo.features.length || !toGeo.features || !toGeo.features.length) {
            throw new Error('Could not find location for origin or destination');
        }

        const fromCoord = fromGeo.features[0].geometry.coordinates;
        const toCoord = toGeo.features[0].geometry.coordinates;

        // Get route between coordinates
        const body = {
            coordinates: [fromCoord, toCoord],
            instructions: false
        };

        const route = await fetch('https://api.openrouteservice.org/v2/directions/driving-car/geojson', {
            method: 'POST',
            headers: {
                'Authorization': ORS_API_KEY,
                'Content-Type': 'application/json',
                'Accept': 'application/json, application/geo+json'
            },
            body: JSON.stringify(body)
        }).then(res => {
            if (!res.ok) {
                throw new Error(`HTTP error! status: ${res.status}`);
            }
            return res.json();
        });

        if (!route.features || !route.features.length) {
            throw new Error('No route features returned');
        }

        const summary = route.features[0].properties.summary;
        const distanceKm = (summary.distance / 1000).toFixed(2);
        const durationMin = (summary.duration / 60).toFixed(1);

        // Format duration
        function formatDuration(mins) {
            const hours = Math.floor(mins / 60);
            const minutes = Math.round(mins % 60);
            return `${hours > 0 ? hours + ' hr ' : ''}${minutes} min`;
        }
        const formattedTime = formatDuration(durationMin);

        // Update route info box
        routeInfo.innerHTML = `
            <div class="route-info">
                <h6>Route Information</h6>
                <p><i class="fas fa-signpost"></i> <strong>From:</strong> ${origin}</p>
                <p><i class="fas fa-map-marker-alt"></i> <strong>To:</strong> ${destination}</p>
                <p><i class="fas fa-arrows-alt-h"></i> <strong>Distance:</strong> ${distanceKm} km</p>
                <p><i class="fas fa-clock"></i> <strong>Duration:</strong> ${formattedTime}</p>
            </div>
        `;

        // Remove old route if exists
        if (routeLine) {
            map.removeLayer(routeLine);
        }

        // Draw new polyline
        routeLine = L.geoJSON(route, {
            style: {
                color: '#0d6efd',
                weight: 5,
                opacity: 0.8,
                dashArray: '5, 5'
            }
        }).addTo(map);

        // Fit map to route
        map.fitBounds(routeLine.getBounds());

        // Add markers for start and end points
        const startIcon = L.divIcon({
            className: 'custom-icon',
            html: '<i class="fas fa-map-marker-alt text-primary fa-2x"></i>',
            iconSize: [30, 30],
            iconAnchor: [15, 30]
        });

        const endIcon = L.divIcon({
            className: 'custom-icon',
            html: '<i class="fas fa-map-marker-alt text-danger fa-2x"></i>',
            iconSize: [30, 30],
            iconAnchor: [15, 30]
        });

        // Remove any existing route markers
        if (window.routeStartMarker) map.removeLayer(window.routeStartMarker);
        if (window.routeEndMarker) map.removeLayer(window.routeEndMarker);

        window.routeStartMarker = L.marker([fromCoord[1], fromCoord[0]], { icon: startIcon })
            .addTo(map)
            .bindPopup(`<b>Origin</b><br>${origin}`);

        window.routeEndMarker = L.marker([toCoord[1], toCoord[0]], { icon: endIcon })
            .addTo(map)
            .bindPopup(`<b>Destination</b><br>${destination}`);

    } catch (error) {
        console.error('Error in route calculation:', error);
        const routeInfo = document.getElementById('routeInfo');
        routeInfo.innerHTML = `<div class="alert alert-danger">Error calculating route: ${error.message}</div>`;
        
        // Remove any existing route if there was an error
        if (routeLine) {
            map.removeLayer(routeLine);
            routeLine = null;
        }
    }
}

function clearRoute() {
    // Remove the route line if it exists
    if (routeLine) {
        map.removeLayer(routeLine);
        routeLine = null;
    }

    // Remove start and end markers if they exist
    if (window.routeStartMarker) {
        map.removeLayer(window.routeStartMarker);
        window.routeStartMarker = null;
    }
    if (window.routeEndMarker) {
        map.removeLayer(window.routeEndMarker);
        window.routeEndMarker = null;
    }

    // Clear any other route markers
    routeMarkers.forEach(marker => {
        if (marker && marker.remove) {
            marker.remove();
        }
    });
    routeMarkers = [];

    // Clear inputs and info
    document.getElementById('origin').value = '';
    document.getElementById('destination').value = '';
    document.getElementById('routeInfo').style.display = 'none';
    document.getElementById('routeInfo').innerHTML = '';

    // Clear stored latlng data
    $('#origin, #destination').removeData('latlng');
}



// Initialize SignalR connection
async function initSignalR() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/trackingHub")
        .build();

    connection.on("PositionUpdated", function (carId, latitude, longitude, timestamp) {
        updateCarPosition(carId, latitude, longitude, timestamp);
    });

    connection.onclose(function () {
        updateConnectionStatus(false);
        setTimeout(initSignalR, 5000); // Retry connection
    });

    try {
        await connection.start();
        updateConnectionStatus(true);
        console.log("SignalR Connected");
    } catch (err) {
        console.error("SignalR Connection Error:", err);
        updateConnectionStatus(false);
    }
}

// Update connection status
function updateConnectionStatus(connected) {
    const statusEl = document.getElementById('connectionStatus');
    const signalrStatusEl = document.getElementById('signalrStatus');

    if (connected) {
        statusEl.className = 'connection-status connected';
        statusEl.innerHTML = '<i class="fas fa-wifi"></i> Connected';
        signalrStatusEl.className = 'badge bg-success';
        signalrStatusEl.textContent = 'Connected';
    } else {
        statusEl.className = 'connection-status disconnected';
        statusEl.innerHTML = '<i class="fas fa-wifi"></i> Disconnected';
        signalrStatusEl.className = 'badge bg-danger';
        signalrStatusEl.textContent = 'Disconnected';
    }
}

// Load initial cars
function loadInitialCars() {
    const initialCars = JSON.parse(document.getElementById('initialCarsData').textContent);

    initialCars.forEach(car => {
        addCarToMap(car);
    });

    if (initialCars.length > 0) {
        fitMapToMarkers();
    }
}

// Add car to map
function addCarToMap(car) {
    if (car.LastPosition) {
        const marker = L.marker([car.LastPosition.Latitude, car.LastPosition.Longitude], {
            icon: L.divIcon({
                className: 'car-marker',
                html: '<div style="font-size:24px; color:blue">🚗</div>',
                iconSize: [30, 30],
                iconAnchor: [15, 15]
            })
        }).addTo(map)
        .bindPopup(`
            <div>
                <h6>${car.Make} ${car.Model}</h6>
                <p><strong>License:</strong> ${car.LicensePlate}</p>
                <p><strong>Status:</strong> ${car.IsActive ? 'Active' : 'Inactive'}</p>
                <p><strong>Last Update:</strong> ${new Date(car.LastPosition.Timestamp).toLocaleString()}</p>
                <button class="btn btn-sm btn-primary" onclick="showCarDetails(${car.Id})">Details</button>
            </div>
        `);

        markers[car.Id] = marker;

        // Initialize path for car
        carPaths[car.Id] = L.polyline([], {
            color: car.IsActive ? 'green' : 'red',
            weight: 3,
            opacity: 0.7
        }).addTo(map);
    }
}

// Update car position
function updateCarPosition(carId, latitude, longitude, timestamp) {
    if (markers[carId]) {
        const carData = markers[carId];
        const newLatLng = L.latLng(latitude, longitude);

        // Calculate rotation angle if we have previous position
        if (carData.prevLatLng) {
            const angle = Math.atan2(
                longitude - carData.prevLatLng.lng,
                latitude - carData.prevLatLng.lat
            ) * 180 / Math.PI;

            // Apply rotation to the icon
            const iconElement = carData.getElement();
            if (iconElement) {
                iconElement.style.transform = `rotate(${angle}deg)`;
                iconElement.style.transition = 'transform 0.5s ease';
            }
        }

        // Update marker position
        carData.setLatLng(newLatLng);
        carData.prevLatLng = newLatLng;

        // Add to path
        if (carPaths[carId]) {
            carPaths[carId].addLatLng(newLatLng);
        }

        // Update popup
        carData.bindPopup(`
            <div>
                <h6>Vehicle ${carId}</h6>
                <p><strong>Position:</strong> ${latitude.toFixed(6)}, ${longitude.toFixed(6)}</p>
                <p><strong>Last Update:</strong> ${new Date(timestamp).toLocaleString()}</p>
                <button class="btn btn-sm btn-primary" onclick="showCarDetails(${carId})">Details</button>
            </div>
        `);
    }
}

// Show car details
async function showCarDetails(carId) {
    try {
        const response = await fetch(`/Map/GetCar/${carId}`);
        const car = await response.json();

        document.getElementById('carDetailsBody').innerHTML = `
            <div class="row">
                <div class="col-md-6">
                    <h6>Vehicle Information</h6>
                    <p><strong>License Plate:</strong> ${car.LicensePlate}</p>
                    <p><strong>Make:</strong> ${car.Make}</p>
                    <p><strong>Model:</strong> ${car.Model}</p>
                    <p><strong>Last Tracked:</strong> ${new Date(car.LastTracked).toLocaleString()}</p>
                </div>
                <div class="col-md-6">
                    <h6>Location Data</h6>
                    <p><strong>Total Locations:</strong> ${car.TotalLocations}</p>
                    <p><strong>First Tracked:</strong> ${car.FirstTracked ? new Date(car.FirstTracked).toLocaleString() : 'N/A'}</p>
                    ${car.LastPosition ? `
                        <p><strong>Current Position:</strong><br>
                        Lat: ${car.LastPosition.Latitude.toFixed(6)}<br>
                        Lng: ${car.LastPosition.Longitude.toFixed(6)}</p>
                    ` : '<p><strong>No position data available</strong></p>'}
                </div>
            </div>
        `;

        selectedCarId = carId;
        new bootstrap.Modal(document.getElementById('carDetailsModal')).show();
    } catch (error) {
        showAlert('danger', 'Failed to load vehicle details');
        console.error('Error:', error);
    }
}

// Center map on car
function centerOnCar(carId) {
    if (markers[carId]) {
        map.setView(markers[carId].getLatLng(), 15);
        markers[carId].openPopup();
    }
}

// Fit map to show all markers
function fitMapToMarkers() {
    if (Object.keys(markers).length > 0) {
        const group = new L.featureGroup(Object.values(markers));
        map.fitBounds(group.getBounds().pad(0.1));
    }
}

// Show alert
function showAlert(type, message) {
    const alertArea = document.getElementById('alertArea');
    alertArea.innerHTML = `
        <div class="alert alert-${type} alert-dismissible fade show" role="alert">
            ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        </div>
    `;

    setTimeout(() => {
        alertArea.innerHTML = '';
    }, 5000);
}

// Toggle tracking
function toggleTracking() {
    const toggleBtn = document.getElementById('toggleTracking');

    if (isTracking) {
        isTracking = false;
        toggleBtn.innerHTML = '<i class="fas fa-play"></i> Start Tracking';
        toggleBtn.className = 'btn btn-success btn-sm';

        if (autoRefreshInterval) {
            clearInterval(autoRefreshInterval);
        }
    } else {
        isTracking = true;
        toggleBtn.innerHTML = '<i class="fas fa-stop"></i> Stop Tracking';
        toggleBtn.className = 'btn btn-danger btn-sm';

        // Start auto-refresh if enabled
        if (document.getElementById('autoRefresh').checked) {
            autoRefreshInterval = setInterval(refreshGpsPositions, 30000);
        }
    }
}

// Refresh GPS positions
async function refreshGpsPositions() {
    const countryCode = document.getElementById('countryCode').value;
    const refreshBtn = document.getElementById('refreshBtn');
    const loadingSpinner = document.getElementById('loadingSpinner');

    refreshBtn.disabled = true;
    loadingSpinner.style.display = 'block';

    try {
        const response = await fetch('/Map/RefreshGpsPositions', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(countryCode)
        });

        const result = await response.json();

        if (result.success) {
            showAlert('success', `Refreshed ${result.data.length} vehicle positions`);
            loadActiveCars();
        } else {
            showAlert('warning', result.message);
        }
    } catch (error) {
        showAlert('danger', 'Failed to refresh GPS positions');
        console.error('Error:', error);
    } finally {
        refreshBtn.disabled = false;
        loadingSpinner.style.display = 'none';
    }
}

// Load active cars
async function loadActiveCars() {
    try {
        const response = await fetch('/Map/GetActiveCars');
        const cars = await response.json();

        // Clear existing cars
        Object.keys(markers).forEach(carId => {
            map.removeLayer(markers[carId]);
            if (carPaths[carId]) {
                map.removeLayer(carPaths[carId]);
            }
        });
        markers = {};
        carPaths = {};

        // Add new cars
        cars.forEach(car => {
            addCarToMap(car);
        });

        checkSystemHealth();
    } catch (error) {
        showAlert('danger', 'Failed to load vehicle data');
        console.error('Error:', error);
    }
}

// Check system health
async function checkSystemHealth() {
    try {
        const response = await fetch('/Map/GetSystemHealth');
        const health = await response.json();

        const dbStatusEl = document.getElementById('dbStatus');
        const gpsStatusEl = document.getElementById('gpsStatus');

        dbStatusEl.className = `badge ${health.databaseConnected ? 'bg-success' : 'bg-danger'}`;
        dbStatusEl.textContent = health.databaseConnected ? 'Connected' : 'Disconnected';

        // Check GPS API status
        const gpsResponse = await fetch('/Map/CheckGpsApiStatus');
        const gpsStatus = await gpsResponse.json();

        gpsStatusEl.className = `badge ${gpsStatus.success ? 'bg-success' : 'bg-danger'}`;
        gpsStatusEl.textContent = gpsStatus.success ? 'Connected' : 'Disconnected';

    } catch (error) {
        console.error('Health check failed:', error);
    }
}

// Initialize everything when DOM is loaded
document.addEventListener('DOMContentLoaded', function() {
    initMap();
    initSignalR();
    loadInitialCars();
    checkSystemHealth();
    initRouting();

    // Check health every 30 seconds
    setInterval(checkSystemHealth, 30000);
    
    // Event listeners
    document.getElementById('refreshBtn').addEventListener('click', refreshGpsPositions);
    document.getElementById('toggleTracking').addEventListener('click', toggleTracking);
    document.getElementById('centerMapBtn').addEventListener('click', () => map.setView([34.5553, 69.2075], 10));
    document.getElementById('showAllCarsBtn').addEventListener('click', fitMapToMarkers);
    document.getElementById('clearHistoryBtn').addEventListener('click', () => {
        Object.values(carPaths).forEach(path => path.setLatLngs([]));
        showAlert('info', 'Path history cleared');
    });

    document.getElementById('autoRefresh').addEventListener('change', function() {
        if (this.checked && isTracking) {
            autoRefreshInterval = setInterval(refreshGpsPositions, 30000);
        } else if (autoRefreshInterval) {
            clearInterval(autoRefreshInterval);
        }
    });
});