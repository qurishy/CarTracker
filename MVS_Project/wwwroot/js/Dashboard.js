// Global variables - ensure unique naming to avoid conflicts
let dashboardConnection;  // Changed from 'connection' to avoid conflicts
let dashboardAutoRefreshInterval; // Changed name to avoid conflict with index.js
let isAutoRefreshEnabled = false;
let notificationsEnabled = false;
let selectedVehicleId = null;
let startTime = new Date();

// Utility function to safely get DOM element
function safeGetElement(id) {
    const element = document.getElementById(id);
    if (!element) {
        console.warn(`Element with ID '${id}' not found`);
        return null;
    }
    return element;
}

// Utility function to safely set element content
function safeSetContent(elementId, content, property = 'textContent') {
    const element = safeGetElement(elementId);
    if (element) {
        element[property] = content;
        return true;
    }
    return false;
}

// Initialize SignalR connection
async function initSignalR() {
    try {
        if (typeof signalR === 'undefined') {
            console.error('SignalR library not loaded');
            setTimeout(initSignalR, 2000); // Retry after delay
            return;
        }

        dashboardConnection = new signalR.HubConnectionBuilder()
            .withUrl("/trackingHub")
            .build();

        dashboardConnection.on("PositionUpdated", function (carId, latitude, longitude, timestamp) {
            handlePositionUpdate(carId, latitude, longitude, timestamp);
        });

        dashboardConnection.onclose(async () => {
            try {
                updateHealthStatus('signalrHealthStatus', false, 'Disconnected');
                await new Promise(resolve => setTimeout(resolve, 5000));
                await initSignalR();
            } catch (retryError) {
                console.error("Reconnection failed:", retryError);
                updateHealthStatus('signalrHealthStatus', false, 'Reconnect Failed');
            }
        });

        await dashboardConnection.start();
        updateHealthStatus('signalrHealthStatus', true, 'Connected');
        console.log("SignalR Connected");
    } catch (err) {
        console.error("SignalR Initial Connection Error:", err);
        updateHealthStatus('signalrHealthStatus', false, 'Connection Failed');
        setTimeout(initSignalR, 5000);
    }
}

// Handle position updates
function handlePositionUpdate(carId, latitude, longitude, timestamp) {
    addActivityItem(`Vehicle ${carId} position updated`, 'success', timestamp);

    if (notificationsEnabled) {
        showNotification(`Vehicle ${carId} moved to new location`, 'info');
    }

    updateVehicleInList(carId, latitude, longitude, timestamp);
}

// Load dashboard data
async function loadDashboardData() {
    try {
        await loadVehicleList();
        await loadRecentHistory();
        await checkSystemHealth();
    } catch (error) {
        console.error('Error loading dashboard data:', error);
        showNotification('Failed to load dashboard data', 'error');
    }
}

// Helper function to safely get property value (handles PascalCase and camelCase)
function safeGetProperty(obj, propName, defaultValue = null) {
    if (!obj) return defaultValue;

    // Try exact match first
    if (obj.hasOwnProperty(propName)) return obj[propName];

    // Try PascalCase version
    const pascalCase = propName.charAt(0).toUpperCase() + propName.slice(1);
    if (obj.hasOwnProperty(pascalCase)) return obj[pascalCase];

    // Try camelCase version  
    const camelCase = propName.charAt(0).toLowerCase() + propName.slice(1);
    if (obj.hasOwnProperty(camelCase)) return obj[camelCase];

    return defaultValue;
}

// Load vehicle list - FIXED VERSION with null checks
async function loadVehicleList() {
    try {
        console.log("Starting loadVehicleList...");
        const response = await fetch('/Map/GetActiveCars');
        console.log("Response status:", response.status);

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(`HTTP ${response.status}: ${errorText}`);
        }

        const vehicles = await response.json();
        console.log("Received vehicles data:", vehicles);

        const vehicleList = safeGetElement('vehicleList');
        if (!vehicleList) {
            console.error('vehicleList element not found in DOM');
            return;
        }

        vehicleList.innerHTML = '';

        if (!Array.isArray(vehicles)) {
            throw new Error(`Expected array but got ${typeof vehicles}`);
        }

        if (vehicles.length === 0) {
            vehicleList.innerHTML = '<p class="text-muted text-center">No vehicles found</p>';
            return;
        }

        vehicles.forEach(vehicle => {
            console.log("Processing vehicle:", vehicle);

            // Use helper function for property access
            const id = safeGetProperty(vehicle, 'id');
            const make = safeGetProperty(vehicle, 'make', 'N/A');
            const model = safeGetProperty(vehicle, 'model', 'N/A');
            const licensePlate = safeGetProperty(vehicle, 'licensePlate', 'N/A');
            const lastTracked = safeGetProperty(vehicle, 'lastTracked');
            const isActive = safeGetProperty(vehicle, 'isActive', false);

            if (!id) {
                console.error("Vehicle missing ID property:", vehicle);
                return;
            }

            const vehicleDiv = document.createElement('div');
            vehicleDiv.className = `vehicle-item ${isActive ? 'active' : 'inactive'}`;
            vehicleDiv.id = `vehicle-${id}`;

            vehicleDiv.innerHTML = `
                <div class="d-flex justify-content-between align-items-center">
                    <div>
                        <h6 class="mb-1">${make} ${model}</h6>
                        <small class="text-muted">${licensePlate}</small>
                    </div>
                    <div>
                        <span class="badge ${isActive ? 'bg-success' : 'bg-danger'}">
                            ${isActive ? 'Active' : 'Inactive'}
                        </span>
                    </div>
                </div>
                <div class="mt-2">
                    <small class="text-muted">
                        Last tracked: ${lastTracked ? new Date(lastTracked).toLocaleString() : 'N/A'}
                    </small>
                </div>
                <div class="mt-2">
                    <button class="btn btn-sm btn-outline-primary" onclick="showVehicleDetails(${id})">
                        <i class="fas fa-info"></i> Details
                    </button>
                    <button class="btn btn-sm btn-outline-success" onclick="trackVehicleOnMap(${id})">
                        <i class="fas fa-map"></i> Track
                    </button>
                </div>
            `;

            vehicleList.appendChild(vehicleDiv);
        });

        updateStatistics(vehicles);
    } catch (error) {
        console.error('Error loading vehicle list:', error);
        const vehicleList = safeGetElement('vehicleList');
        if (vehicleList) {
            vehicleList.innerHTML = `
                <div class="alert alert-danger">
                    <i class="fas fa-exclamation-triangle"></i> 
                    Failed to load vehicles: ${error.message}
                </div>
            `;
        }
    }
}

// Update statistics with null checks
function updateStatistics(vehicles) {
    try {
        const totalCars = vehicles.length;
        const activeCars = vehicles.filter(v => safeGetProperty(v, 'isActive', false)).length;
        const inactiveCars = totalCars - activeCars;

        safeSetContent('totalCars', totalCars);
        safeSetContent('activeCars', activeCars);
        safeSetContent('inactiveCars', inactiveCars);

        // Calculate most active vehicle
        const mostActive = vehicles.reduce((prev, current) => {
            const prevCount = safeGetProperty(prev, 'locationCount', 0);
            const currentCount = safeGetProperty(current, 'locationCount', 0);
            return prevCount > currentCount ? prev : current;
        });

        if (mostActive && safeGetProperty(mostActive, 'locationCount', 0) > 0) {
            safeSetContent('mostActiveVehicle', safeGetProperty(mostActive, 'licensePlate', 'N/A'));
        }
    } catch (error) {
        console.error('Error updating statistics:', error);
    }
}

// Load recent history with null checks
async function loadRecentHistory() {
    const tbody = safeGetElement('historyTableBody');
    if (!tbody) {
        console.warn('historyTableBody element not found');
        return;
    }

    try {
        // Show loading row if it exists
        const loadingRow = safeGetElement('loadingRow');
        if (loadingRow) loadingRow.style.display = 'table-row';

        const response = await fetch('/Map/GetAllCarsHistory');
        if (!response.ok) throw new Error(`HTTP ${response.status}`);

        const historyData = await response.json();
        console.log("History API response:", historyData);

        tbody.innerHTML = '';

        if (!historyData || !historyData.data || !Array.isArray(historyData.data)) {
            tbody.innerHTML = '<tr><td colspan="6" class="text-center text-muted">Invalid data format</td></tr>';
            return;
        }

        let hasData = false;

        historyData.data.forEach(car => {
            try {
                const locations = safeGetProperty(car, 'locations', []);

                if (!Array.isArray(locations) || locations.length === 0) return;

                hasData = true;
                const carId = safeGetProperty(car, 'carId');
                const make = safeGetProperty(car, 'make', 'N/A');
                const model = safeGetProperty(car, 'model', 'N/A');
                const licensePlate = safeGetProperty(car, 'licensePlate', 'N/A');

                locations.slice(0, 5).forEach(location => {
                    const row = document.createElement('tr');

                    const lat = safeGetProperty(location, 'latitude');
                    const lng = safeGetProperty(location, 'longitude');
                    const timestamp = safeGetProperty(location, 'timestamp');

                    row.innerHTML = `
                        <td>${make} ${model}</td>
                        <td>${licensePlate}</td>
                        <td>
                            ${lat ? lat.toFixed(6) : 'N/A'}, 
                            ${lng ? lng.toFixed(6) : 'N/A'}
                        </td>
                        <td>${timestamp ? new Date(timestamp).toLocaleString() : 'N/A'}</td>
                        <td><span class="badge bg-success">Active</span></td>
                        <td>
                            <button class="btn btn-sm btn-outline-primary" onclick="showVehicleDetails(${carId})">
                                <i class="fas fa-eye"></i>
                            </button>
                        </td>
                    `;
                    tbody.appendChild(row);
                });
            } catch (error) {
                console.error('Error processing car data:', error);
            }
        });

        if (!hasData) {
            tbody.innerHTML = '<tr><td colspan="6" class="text-center text-muted">No location data available</td></tr>';
        }
    } catch (error) {
        console.error('Error loading recent history:', error);
        tbody.innerHTML = '<tr><td colspan="6" class="text-center text-danger">Failed to load history</td></tr>';
    } finally {
        // Hide loading row
        const loadingRow = safeGetElement('loadingRow');
        if (loadingRow) loadingRow.style.display = 'none';
    }
}

// Check system health with null checks
async function checkSystemHealth() {
    try {
        const healthResponse = await fetch('/Map/GetSystemHealth');
        const health = await healthResponse.json();

        updateHealthStatus('dbHealthStatus', health.databaseConnected,
            health.databaseConnected ? 'Connected' : 'Disconnected');

        const gpsResponse = await fetch('/Map/CheckGpsApiStatus');
        const gpsStatus = await gpsResponse.json();

        updateHealthStatus('gpsHealthStatus', gpsStatus.success,
            gpsStatus.success ? 'Connected' : 'Disconnected');

        safeSetContent('lastUpdateTime', new Date().toLocaleString());
    } catch (error) {
        console.error('Health check failed:', error);
        updateHealthStatus('dbHealthStatus', false, 'Error');
        updateHealthStatus('gpsHealthStatus', false, 'Error');
    }
}

// Update health status with null checks
function updateHealthStatus(elementId, isHealthy, text) {
    try {
        const element = safeGetElement(elementId);
        if (element) {
            element.className = `badge ${isHealthy ? 'bg-success' : 'bg-danger'}`;
            element.textContent = text;
        }
    } catch (error) {
        console.error(`Error updating health status for ${elementId}:`, error);
    }
}

// Show vehicle details - FIXED VERSION with null checks
async function showVehicleDetails(vehicleId) {
    try {
        console.log(`Fetching details for vehicle ID: ${vehicleId}`);

        const response = await fetch(`/Map/GetCar?id=${vehicleId}`);
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${await response.text()}`);
        }

        const vehicle = await response.json();
        console.log('Vehicle details response:', vehicle);

        if (!vehicle) {
            throw new Error('No vehicle data returned');
        }

        // Use helper function for safe property access
        const licensePlate = safeGetProperty(vehicle, 'licensePlate', 'N/A');
        const make = safeGetProperty(vehicle, 'make', 'N/A');
        const model = safeGetProperty(vehicle, 'model', 'N/A');
        const lastTracked = safeGetProperty(vehicle, 'lastTracked');
        const totalLocations = safeGetProperty(vehicle, 'totalLocations', 0);
        const firstTracked = safeGetProperty(vehicle, 'firstTracked');
        const lastPosition = safeGetProperty(vehicle, 'lastPosition');

        // Build the modal content safely
        let positionText = 'No position data';
        if (lastPosition) {
            const lat = safeGetProperty(lastPosition, 'latitude');
            const lng = safeGetProperty(lastPosition, 'longitude');
            if (lat !== null && lng !== null) {
                positionText = `${lat.toFixed(6)}, ${lng.toFixed(6)}`;
            }
        }

        const modalBody = safeGetElement('vehicleModalBody');
        if (modalBody) {
            modalBody.innerHTML = `
                <div class="row">
                    <div class="col-md-6">
                        <h6>Vehicle Information</h6>
                        <table class="table table-sm">
                            <tr><td><strong>License Plate:</strong></td><td>${licensePlate}</td></tr>
                            <tr><td><strong>Make:</strong></td><td>${make}</td></tr>
                            <tr><td><strong>Model:</strong></td><td>${model}</td></tr>
                            <tr><td><strong>Last Tracked:</strong></td><td>${lastTracked ? new Date(lastTracked).toLocaleString() : 'N/A'}</td></tr>
                        </table>
                    </div>
                    <div class="col-md-6">
                        <h6>Location Statistics</h6>
                        <table class="table table-sm">
                            <tr><td><strong>Total Locations:</strong></td><td>${totalLocations}</td></tr>
                            <tr><td><strong>First Tracked:</strong></td><td>${firstTracked ? new Date(firstTracked).toLocaleString() : 'N/A'}</td></tr>
                            <tr><td><strong>Current Position:</strong></td><td>${positionText}</td></tr>
                        </table>
                    </div>
                </div>
            `;

            selectedVehicleId = vehicleId;

            // Safely show modal
            const modalElement = safeGetElement('vehicleModal');
            if (modalElement && typeof bootstrap !== 'undefined') {
                new bootstrap.Modal(modalElement).show();
            } else {
                console.warn('Modal element or Bootstrap not found');
            }
        }

    } catch (error) {
        console.error('Error loading vehicle details:', error);
        showNotification(`Failed to load vehicle details: ${error.message}`, 'error');
    }
}

// Track vehicle on map
function trackVehicleOnMap(vehicleId) {
      window.open(`/Map/Index?id=${vehicleId}`, '_blank');
  
}

// Refresh GPS data with null checks
async function refreshGpsData() {
    const countrySelect = safeGetElement('countrySelect');
    const btn = safeGetElement('refreshGpsBtn');

    if (!btn) {
        console.warn('refreshGpsBtn not found');
        return;
    }

    const countryCode = countrySelect ? countrySelect.value : 'AF';

    btn.disabled = true;
    btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Refreshing...';

    try {
        const response = await fetch('/Map/RefreshGpsPositions', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(countryCode)
        });

        const result = await response.json();

        if (result.success) {
            showNotification(`Refreshed ${result.data.length} vehicle positions`, 'success');
            addActivityItem(`GPS data refreshed for ${result.data.length} vehicles`, 'success');
            await loadVehicleList();
        } else {
            showNotification(result.message, 'warning');
        }
    } catch (error) {
        console.error('Error refreshing GPS data:', error);
        showNotification('Failed to refresh GPS data', 'error');
    } finally {
        btn.disabled = false;
        btn.innerHTML = '<i class="fas fa-sync"></i> Refresh GPS Data';
    }
}

// Toggle auto-refresh with null checks
function toggleAutoRefresh() {
    const btn = safeGetElement('toggleAutoRefresh');
    if (!btn) {
        console.warn('toggleAutoRefresh button not found');
        return;
    }

    if (isAutoRefreshEnabled) {
        clearInterval(dashboardAutoRefreshInterval);
        isAutoRefreshEnabled = false;
        btn.innerHTML = '<i class="fas fa-play"></i> Start Auto-Refresh';
        btn.className = 'btn btn-success w-100';
        addActivityItem('Auto-refresh disabled', 'warning');
    } else {
        dashboardAutoRefreshInterval = setInterval(refreshGpsData, 30000);
        isAutoRefreshEnabled = true;
        btn.innerHTML = '<i class="fas fa-stop"></i> Stop Auto-Refresh';
        btn.className = 'btn btn-danger w-100';
        addActivityItem('Auto-refresh enabled (30s interval)', 'success');
    }
}

// Show notification with null checks
function showNotification(message, type = 'info') {
    try {
        const notifications = safeGetElement('notifications');
        if (!notifications) {
            console.error('Notifications container not found');
            // Fallback to console log
            console.log(`NOTIFICATION (${type.toUpperCase()}): ${message}`);
            return;
        }

        const notification = document.createElement('div');

        const bgClass = {
            success: 'bg-success',
            error: 'bg-danger',
            warning: 'bg-warning',
            info: 'bg-info'
        }[type] || 'bg-info';

        notification.className = `alert ${bgClass} text-white notification`;
        notification.innerHTML = `
            <div class="d-flex justify-content-between align-items-center">
                <span>${message}</span>
                <button type="button" class="btn-close btn-close-white" onclick="this.parentElement.parentElement.remove()"></button>
            </div>
        `;

        notifications.appendChild(notification);

        setTimeout(() => {
            if (notification.parentNode) {
                notification.remove();
            }
        }, 5000);
    } catch (error) {
        console.error('Error showing notification:', error);
        console.log(`NOTIFICATION (${type.toUpperCase()}): ${message}`);
    }
}

// Add activity item with null checks
function addActivityItem(message, type = 'info', timestamp = new Date()) {
    try {
        const activityFeed = safeGetElement('activityFeed');
        if (!activityFeed) {
            console.log(`ACTIVITY (${type.toUpperCase()}): ${message}`);
            return;
        }

        const item = document.createElement('div');
        item.className = `activity-item ${type}`;
        item.innerHTML = `
            <div class="d-flex justify-content-between align-items-center">
                <span>${message}</span>
                <small class="text-muted">${new Date(timestamp).toLocaleTimeString()}</small>
            </div>
        `;

        activityFeed.insertBefore(item, activityFeed.firstChild);

        // Keep only last 20 items
        while (activityFeed.children.length > 20) {
            activityFeed.removeChild(activityFeed.lastChild);
        }
    } catch (error) {
        console.error('Error adding activity item:', error);
    }
}

// Update vehicle in list with null checks
function updateVehicleInList(carId, latitude, longitude, timestamp) {
    try {
        const vehicleElement = safeGetElement(`vehicle-${carId}`);
        if (vehicleElement) {
            const timestampElement = vehicleElement.querySelector('.text-muted');
            if (timestampElement) {
                timestampElement.textContent = `Last tracked: ${new Date(timestamp).toLocaleString()}`;
            }

            vehicleElement.className = 'vehicle-item active';
            const badge = vehicleElement.querySelector('.badge');
            if (badge) {
                badge.className = 'badge bg-success';
                badge.textContent = 'Active';
            }
        }
    } catch (error) {
        console.error('Error updating vehicle in list:', error);
    }
}

// Update system uptime with null checks
function updateSystemUptime() {
    try {
        const uptimeElement = safeGetElement('systemUptimeValue');
        if (!uptimeElement) {
            console.log("systemUptime element not found - skipping update");
            return;
        }

        const uptime = new Date() - startTime;
        const hours = Math.floor(uptime / (1000 * 60 * 60));
        const minutes = Math.floor((uptime % (1000 * 60 * 60)) / (1000 * 60));
        const seconds = Math.floor((uptime % (1000 * 60)) / 1000);

        uptimeElement.textContent = `${hours}h ${minutes}m ${seconds}s`;
    } catch (error) {
        console.error('Error updating uptime:', error);
    }
}

// Export history with null checks
async function exportHistory() {
    try {
        showNotification('Preparing export...', 'info');

        const response = await fetch('/Map/GetAllCarsHistory?startDate=2000-01-01&endDate=2100-01-01');
        if (!response.ok) throw new Error(`HTTP ${response.status}`);

        const historyData = await response.json();

        if (!historyData?.data?.length) {
            showNotification('No data available to export', 'warning');
            return;
        }

        let csvContent = "Vehicle,License Plate,Make,Model,Latitude,Longitude,Timestamp,Status\n";
        let totalLocations = 0;

        historyData.data.forEach(car => {
            const locations = safeGetProperty(car, 'locations', []);
            const make = safeGetProperty(car, 'make', 'Unknown');
            const model = safeGetProperty(car, 'model', 'Unknown');
            const licensePlate = safeGetProperty(car, 'licensePlate', 'Unknown');

            locations.forEach(location => {
                const lat = safeGetProperty(location, 'latitude', '');
                const lng = safeGetProperty(location, 'longitude', '');
                const timestamp = safeGetProperty(location, 'timestamp', '');

                totalLocations++;
                csvContent += `"${make} ${model}",${licensePlate},${make},${model},${lat},${lng},"${timestamp}",Active\n`;
            });
        });

        if (totalLocations === 0) {
            showNotification('No location data available to export', 'warning');
            return;
        }

        const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `vehicle_locations_${new Date().toISOString().slice(0, 10)}.csv`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);

        showNotification(`Exported ${totalLocations} location records`, 'success');
    } catch (error) {
        console.error('Export failed:', error);
        showNotification('Export failed: ' + error.message, 'error');
    }
}

// Refresh functions
async function refreshDashboard() {
    await loadDashboardData();
    showNotification('Dashboard refreshed', 'success');
}

async function refreshHistory() {
    await loadRecentHistory();
    showNotification('History refreshed', 'success');
}

function trackVehicle() {
    if (selectedVehicleId) {
        trackVehicleOnMap(selectedVehicleId);
    }
}

// Event listeners and initialization with comprehensive null checks
document.addEventListener('DOMContentLoaded', function () {
    try {
        console.log('Dashboard initialization starting...');

        // Initialize SignalR
        initSignalR();

        // Load initial data
        loadDashboardData();

        // Set up intervals
        setInterval(updateSystemUptime, 1000);
        setInterval(checkSystemHealth, 30000);

        // Set up event listeners with null checks
        const refreshGpsBtn = safeGetElement('refreshGpsBtn');
        if (refreshGpsBtn) {
            refreshGpsBtn.addEventListener('click', refreshGpsData);
        }

        const toggleAutoRefreshBtn = safeGetElement('toggleAutoRefresh');
        if (toggleAutoRefreshBtn) {
            toggleAutoRefreshBtn.addEventListener('click', toggleAutoRefresh);
        }

        const enableNotifications = safeGetElement('enableNotifications');
        if (enableNotifications) {
            enableNotifications.addEventListener('change', function () {
                notificationsEnabled = this.checked;
                showNotification(
                    notificationsEnabled ? 'Notifications enabled' : 'Notifications disabled',
                    notificationsEnabled ? 'success' : 'warning'
                );
            });
        }

        addActivityItem('Dashboard initialized', 'success');
        console.log('Dashboard initialization completed successfully');
    } catch (error) {
        console.error('Error during initialization:', error);
        // Fallback notification
        console.log('NOTIFICATION (ERROR): Dashboard initialization failed: ' + error.message);
    }
});

// Cleanup on page unload
window.addEventListener('beforeunload', function () {
    if (dashboardAutoRefreshInterval) {
        clearInterval(dashboardAutoRefreshInterval);
    }
    if (dashboardConnection && dashboardConnection.state === signalR.HubConnectionState.Connected) {
        dashboardConnection.stop();
    }
});