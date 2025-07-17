// Global variables
    let connection;
    let autoRefreshInterval;
    let isAutoRefreshEnabled = false;
    let notificationsEnabled = false;
    let selectedVehicleId = null;
    let startTime = new Date();

    // Initialize SignalR connection
    async function initSignalR() {
        connection = new signalR.HubConnectionBuilder()
            .withUrl("/trackingHub")
            .build();

        connection.on("PositionUpdated", function (carId, latitude, longitude, timestamp) {
            handlePositionUpdate(carId, latitude, longitude, timestamp);
        });

        connection.onclose(function () {
            updateHealthStatus('signalrHealthStatus', false, 'Disconnected');
            setTimeout(initSignalR, 5000); // Retry connection
        });

        try {
            await connection.start();
            updateHealthStatus('signalrHealthStatus', true, 'Connected');
            console.log("SignalR Connected");
        } catch (err) {
            console.error("SignalR Connection Error:", err);
            updateHealthStatus('signalrHealthStatus', false, 'Connection Failed');
        }
    }

    // Handle position updates
    function handlePositionUpdate(carId, latitude, longitude, timestamp) {
        addActivityItem(`Vehicle ${carId} position updated`, 'success', timestamp);

        if (notificationsEnabled) {
            showNotification(`Vehicle ${carId} moved to new location`, 'info');
        }

        // Update vehicle list if visible
        updateVehicleInList(carId, latitude, longitude, timestamp);
    }

    // Load dashboard data
    async function loadDashboardData() {
        try {
            const response = await fetch('/Map/Dashboard');
            if (!response.ok) throw new Error('Failed to load dashboard data');

            // Dashboard data is already loaded server-side
            await loadVehicleList();
            await loadRecentHistory();
            await checkSystemHealth();
        } catch (error) {
            console.error('Error loading dashboard data:', error);
            showNotification('Failed to load dashboard data', 'error');
        }
    }

// Load vehicle list - FIXED VERSION
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

        const vehicleList = document.getElementById('vehicleList');
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

            // Handle different property naming cases
            const id = vehicle.id || vehicle.Id;
            const make = vehicle.make || vehicle.Make;
            const model = vehicle.model || vehicle.Model;
            const licensePlate = vehicle.licensePlate || vehicle.LicensePlate;
            const lastTracked = vehicle.lastTracked || vehicle.LastTracked;
            const isActive = vehicle.isActive || vehicle.IsActive;

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
                        <h6 class="mb-1">${make || 'N/A'} ${model || 'N/A'}</h6>
                        <small class="text-muted">${licensePlate || 'N/A'}</small>
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
        document.getElementById('vehicleList').innerHTML = `
            <div class="alert alert-danger">
                <i class="fas fa-exclamation-triangle"></i> 
                Failed to load vehicles: ${error.message}
            </div>
        `;
    }
}
    // Update statistics
    function updateStatistics(vehicles) {
        const totalCars = vehicles.length;
        const activeCars = vehicles.filter(v => v.IsActive).length;
        const inactiveCars = totalCars - activeCars;

        document.getElementById('totalCars').textContent = totalCars;
        document.getElementById('activeCars').textContent = activeCars;
        document.getElementById('inactiveCars').textContent = inactiveCars;

        // Calculate most active vehicle
        const mostActive = vehicles.reduce((prev, current) =>
            (prev.LocationCount > current.LocationCount) ? prev : current
        );

        if (mostActive && mostActive.LocationCount > 0) {
            document.getElementById('mostActiveVehicle').textContent = mostActive.LicensePlate;
        }
    }

    // Load recent history
async function loadRecentHistory() {
    try {
        const response = await fetch('/Map/GetAllCarsHistory');
        const historyData = await response.json();

        const tbody = document.getElementById('historyTableBody');
        tbody.innerHTML = '';

        // 1. Check if historyData.data exists and is an array
        if (!historyData.data || !Array.isArray(historyData.data) || historyData.data.length === 0) {
            tbody.innerHTML = '<tr><td colspan="6" class="text-center text-muted">No recent history found</td></tr>';
            return;
        }

        historyData.data.forEach(car => {
            // 2. Safely handle missing Locations array
            const locations = Array.isArray(car.Locations) ? car.Locations : [];

            // 3. Take only the first 5 locations
            locations.slice(0, 5).forEach(location => {
                const row = document.createElement('tr');
                row.innerHTML = `
                    <td>${car.Make || 'N/A'} ${car.Model || 'N/A'}</td>
                    <td>${car.LicensePlate || 'N/A'}</td>
                    <td>${location.Latitude?.toFixed(6) || 'N/A'}, ${location.Longitude?.toFixed(6) || 'N/A'}</td>
                    <td>${location.Timestamp ? new Date(location.Timestamp).toLocaleString() : 'N/A'}</td>
                    <td><span class="badge bg-success">Active</span></td>
                    <td>
                        <button class="btn btn-sm btn-outline-primary" onclick="showVehicleDetails(${car.CarId})">
                            <i class="fas fa-eye"></i>
                        </button>
                    </td>
                `;
                tbody.appendChild(row);
            });
        });
    } catch (error) {
        console.error('Error loading recent history:', error);
        document.getElementById('historyTableBody').innerHTML =
            '<tr><td colspan="6" class="text-center text-danger">Failed to load history</td></tr>';
    }
}

    // Check system health
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

            document.getElementById('lastUpdateTime').textContent = new Date().toLocaleString();
        } catch (error) {
            console.error('Health check failed:', error);
            updateHealthStatus('dbHealthStatus', false, 'Error');
            updateHealthStatus('gpsHealthStatus', false, 'Error');
        }
    }

    // Update health status
    function updateHealthStatus(elementId, isHealthy, text) {
        const element = document.getElementById(elementId);
        element.className = `badge ${isHealthy ? 'bg-success' : 'bg-danger'}`;
        element.textContent = text;
    }

    // Show vehicle details
    async function showVehicleDetails(vehicleId) {
        try {
            const response = await fetch(`/Map/GetCar/${vehicleId}`);
            const vehicle = await response.json();

            document.getElementById('vehicleModalBody').innerHTML = `
                <div class="row">
                    <div class="col-md-6">
                        <h6>Vehicle Information</h6>
                        <table class="table table-sm">
                            <tr><td><strong>License Plate:</strong></td><td>${vehicle.LicensePlate}</td></tr>
                            <tr><td><strong>Make:</strong></td><td>${vehicle.Make}</td></tr>
                            <tr><td><strong>Model:</strong></td><td>${vehicle.Model}</td></tr>
                            <tr><td><strong>Last Tracked:</strong></td><td>${new Date(vehicle.LastTracked).toLocaleString()}</td></tr>
                        </table>
                    </div>
                    <div class="col-md-6">
                        <h6>Location Statistics</h6>
                        <table class="table table-sm">
                            <tr><td><strong>Total Locations:</strong></td><td>${vehicle.TotalLocations}</td></tr>
                            <tr><td><strong>First Tracked:</strong></td><td>${vehicle.FirstTracked ? new Date(vehicle.FirstTracked).toLocaleString() : 'N/A'}</td></tr>
                            <tr><td><strong>Current Position:</strong></td><td>
                                ${vehicle.LastPosition ?
                                    `${vehicle.LastPosition.Latitude.toFixed(6)}, ${vehicle.LastPosition.Longitude.toFixed(6)}` :
                                    'No position data'
                                }
                            </td></tr>
                        </table>
                    </div>
                </div>
            `;

            selectedVehicleId = vehicleId;
            new bootstrap.Modal(document.getElementById('vehicleModal')).show();
        } catch (error) {
            console.error('Error loading vehicle details:', error);
            showNotification('Failed to load vehicle details', 'error');
        }
    }

    // Track vehicle on map
    function trackVehicleOnMap(vehicleId) {
        window.open(`/Map/Index?trackVehicle=${vehicleId}`, '_blank');
    }

    // Refresh GPS data
    async function refreshGpsData() {
        const countryCode = document.getElementById('countrySelect').value;
        const btn = document.getElementById('refreshGpsBtn');

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

    // Toggle auto-refresh
    function toggleAutoRefresh() {
        const btn = document.getElementById('toggleAutoRefresh');

        if (isAutoRefreshEnabled) {
            clearInterval(autoRefreshInterval);
            isAutoRefreshEnabled = false;
            btn.innerHTML = '<i class="fas fa-play"></i> Start Auto-Refresh';
            btn.className = 'btn btn-success w-100';
            addActivityItem('Auto-refresh disabled', 'warning');
        } else {
            autoRefreshInterval = setInterval(refreshGpsData, 30000);
            isAutoRefreshEnabled = true;
            btn.innerHTML = '<i class="fas fa-stop"></i> Stop Auto-Refresh';
            btn.className = 'btn btn-danger w-100';
            addActivityItem('Auto-refresh enabled (30s interval)', 'success');
        }
    }

    // Show notification
    function showNotification(message, type = 'info') {
        const notifications = document.getElementById('notifications');
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
            notification.remove();
        }, 5000);
    }

    // Add activity item
    function addActivityItem(message, type = 'info', timestamp = new Date()) {
        const activityFeed = document.getElementById('activityFeed');
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
    }

    // Update vehicle in list
    function updateVehicleInList(carId, latitude, longitude, timestamp) {
        const vehicleElement = document.getElementById(`vehicle-${carId}`);
        if (vehicleElement) {
            const timestampElement = vehicleElement.querySelector('.text-muted');
            if (timestampElement) {
                timestampElement.textContent = `Last tracked: ${new Date(timestamp).toLocaleString()}`;
            }

            // Update to active status
            vehicleElement.className = 'vehicle-item active';
            const badge = vehicleElement.querySelector('.badge');
            if (badge) {
                badge.className = 'badge bg-success';
                badge.textContent = 'Active';
            }
        }
    }

    // Update system uptime
    function updateSystemUptime() {
        const uptime = new Date() - startTime;
        const hours = Math.floor(uptime / (1000 * 60 * 60));
        const minutes = Math.floor((uptime % (1000 * 60 * 60)) / (1000 * 60));
        const seconds = Math.floor((uptime % (1000 * 60)) / 1000);

        document.getElementById('systemUptime').textContent = `${hours}h ${minutes}m ${seconds}s`;
    }

    // Export history
    function exportHistory() {
        // This would typically generate a CSV or Excel file
        showNotification('Export functionality would be implemented here', 'info');
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

    // Event listeners
    document.getElementById('refreshGpsBtn').addEventListener('click', refreshGpsData);
    document.getElementById('toggleAutoRefresh').addEventListener('click', toggleAutoRefresh);
    document.getElementById('enableNotifications').addEventListener('change', function() {
        notificationsEnabled = this.checked;
        showNotification(
            notificationsEnabled ? 'Notifications enabled' : 'Notifications disabled',
            notificationsEnabled ? 'success' : 'warning'
        );
    });

    // Initialize dashboard
    document.addEventListener('DOMContentLoaded', function() {
        initSignalR();
        loadDashboardData();

        // Update uptime every second
        setInterval(updateSystemUptime, 1000);

        // Check system health every 30 seconds
        setInterval(checkSystemHealth, 30000);

        // Initial activity message
        addActivityItem('Dashboard initialized', 'success');
    });