//// Global variables
//let map;
//let markers = {};
//let routes = {};
//let activeRoute = null;
//const connection = new signalR.HubConnectionBuilder()
//    .withUrl("/trackingHub")
//    .configureLogging(signalR.LogLevel.Information)
//    .build();

//// Initialize map
//function initMap() {
//    map = L.map('map').setView([34.5, 69.2], 13);

//    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
//        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
//    }).addTo(map);

//    // Add legend
//    const legend = L.control({ position: 'bottomright' });
//    legend.onAdd = function () {
//        const div = L.DomUtil.create('div', 'legend');
//        div.innerHTML = `
//                    <h5>Vehicle Status</h5>
//                    <div class="legend-item">
//                        <div class="legend-color" style="background-color: #28a745"></div>
//                        <span>Moving</span>
//                    </div>
//                    <div class="legend-item">
//                        <div class="legend-color" style="background-color: #ffc107"></div>
//                        <span>Idle</span>
//                    </div>
//                    <div class="legend-item">
//                        <div class="legend-color" style="background-color: #dc3545"></div>
//                        <span>Offline</span>
//                    </div>
//                    <div class="legend-item">
//                        <div class="legend-color" style="background-color: #0d6efd"></div>
//                        <span>Selected</span>
//                    </div>
//                `;
//        return div;
//    };
//    legend.addTo(map);
//}

//// Simulate real-time progress bar
//function simulateRealTimeProgress() {
//    const progressBar = document.getElementById('realtimeProgress');
//    let width = 0;

//    setInterval(() => {
//        width = (width + 5) % 100;
//        progressBar.style.width = width + '%';
//    }, 200);
//}

//// Load active cars
//function loadActiveCars() {
//    // Simulate API call to get active cars
//    setTimeout(() => {
//        const carsList = document.getElementById('carsList');
//        carsList.innerHTML = '';

//        const cars = [
//            { id: 1, make: "Toyota", model: "Camry", licensePlate: "ABC123", status: "moving", lastTracked: new Date(), lastPosition: { latitude: 34.52, longitude: 69.18 } },
//            { id: 2, make: "Honda", model: "Civic", licensePlate: "XYZ789", status: "idle", lastTracked: new Date(Date.now() - 300000), lastPosition: { latitude: 34.53, longitude: 69.20 } },
//            { id: 3, make: "Ford", model: "F-150", licensePlate: "TRK456", status: "moving", lastTracked: new Date(), lastPosition: { latitude: 34.51, longitude: 69.22 } },
//            { id: 4, make: "Chevrolet", model: "Malibu", licensePlate: "MAL789", status: "offline", lastTracked: new Date(Date.now() - 900000), lastPosition: { latitude: 34.49, longitude: 69.15 } },
//            { id: 5, make: "Tesla", model: "Model S", licensePlate: "TES001", status: "moving", lastTracked: new Date(), lastPosition: { latitude: 34.50, longitude: 69.25 } },
//        ];

//        cars.forEach(car => {
//            const item = createCarListItem(car);
//            carsList.appendChild(item);

//            // Add marker to map
//            addCarMarker(car);
//        });

//        // Start SignalR connection after cars are loaded
//        startSignalRConnection();
//    }, 1000);
//}

//// Create car list item
//function createCarListItem(car) {
//    const item = document.createElement('div');
//    item.className = 'list-group-item car-item d-flex justify-content-between align-items-start';
//    item.id = `car-item-${car.id}`;

//    const statusClass = car.status === 'moving' ? 'status-moving' :
//        car.status === 'idle' ? 'status-idle' : 'status-offline';

//    const statusText = car.status === 'moving' ? 'Moving' :
//        car.status === 'idle' ? 'Idle' : 'Offline';

//    item.innerHTML = `
//                <div class="d-flex w-100">
//                    <div class="car-icon">
//                        <i class="fas fa-car"></i>
//                    </div>
//                    <div class="w-100">
//                        <div class="d-flex justify-content-between">
//                            <h6 class="fw-semibold mb-1">${car.make} ${car.model}</h6>
//                            <span class="badge bg-light text-dark">
//                                <span class="${statusClass}"></span>${statusText}
//                            </span>
//                        </div>
//                        <div class="d-flex justify-content-between">
//                            <p class="text-muted small mb-1">Plate: ${car.licensePlate}</p>
//                            <p class="text-muted small mb-0">ID: ${car.id}</p>
//                        </div>
//                        <p class="text-muted small mb-0">Last tracked: ${formatTime(car.lastTracked)}</p>
//                    </div>
//                </div>
//                <div class="btn-group btn-group-sm align-self-center">
//                    <button class="btn btn-track" onclick="showRoute(${car.id})">
//                        <i class="fas fa-route me-1"></i>Route
//                    </button>
//                    <button class="btn btn-route" onclick="loadCarDetail(${car.id})">
//                        <i class="fas fa-info-circle me-1"></i>Details
//                    </button>
//                </div>
//            `;

//    item.addEventListener('click', () => {
//        highlightCar(car.id);
//        centerOnCar(car.id);
//    });

//    return item;
//}

//// Add car marker to map
//function addCarMarker(car) {
//    if (markers[car.id]) {
//        map.removeLayer(markers[car.id]);
//    }

//    const color = car.status === 'moving' ? '#28a745' :
//        car.status === 'idle' ? '#ffc107' : '#dc3545';

//    const marker = L.circleMarker([car.lastPosition.latitude, car.lastPosition.longitude], {
//        radius: 10,
//        color: color,
//        fillColor: color,
//        fillOpacity: 0.8,
//        className: `car-marker car-marker-${car.id}`
//    }).addTo(map);

//    const popupContent = `
//                <div class="text-center">
//                    <strong>${car.licensePlate}</strong><br/>
//                    ${car.make} ${car.model}<br/>
//                    <span class="badge ${car.status === 'moving' ? 'bg-success' :
//            car.status === 'idle' ? 'bg-warning' : 'bg-danger'}">
//                        ${car.status}
//                    </span><br/>
//                    <button class="btn btn-sm btn-primary mt-2" onclick="loadCarDetail(${car.id})">View Detail</button>
//                </div>
//            `;

//    marker.bindPopup(popupContent);
//    marker.on('click', () => {
//        highlightCar(car.id);
//    });

//    markers[car.id] = marker;
//}

//// Format time for display
//function formatTime(date) {
//    const now = new Date();
//    const diff = (now - date) / 1000; // in seconds

//    if (diff < 60) {
//        return 'Just now';
//    } else if (diff < 3600) {
//        return `${Math.floor(diff / 60)} min ago`;
//    } else {
//        return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
//    }
//}

//// Highlight car in list
//function highlightCar(carId) {
//    document.querySelectorAll('.car-item').forEach(item => {
//        item.classList.remove('active');
//    });

//    document.querySelectorAll('.car-marker').forEach(marker => {
//        marker.setStyle({ color: '#6c757d', fillColor: '#6c757d' });
//    });

//    const selectedItem = document.getElementById(`car-item-${carId}`);
//    if (selectedItem) {
//        selectedItem.classList.add('active');
//        selectedItem.scrollIntoView({ behavior: 'smooth', block: 'center' });
//    }

//    if (markers[carId]) {
//        markers[carId].setStyle({ color: '#0d6efd', fillColor: '#0d6efd' });
//        markers[carId].openPopup();
//    }
//}

//// Center map on car
//function centerOnCar(carId) {
//    if (markers[carId]) {
//        map.setView(markers[carId].getLatLng(), 15);
//    }
//}

//// Load car details
//function loadCarDetail(carId) {
//    // Simulate API call to get car details
//    const car = {
//        id: carId,
//        make: "Toyota",
//        model: "Camry",
//        licensePlate: "ABC123",
//        lastTracked: new Date(),
//        totalLocations: 142,
//        lastPosition: { latitude: 34.52, longitude: 69.18 },
//        status: "moving",
//        driver: "John Smith",
//        speed: "65 km/h"
//    };

//    const html = `
//                <div class="row">
//                    <div class="col-md-4 text-center">
//                        <i class="fas fa-car fa-4x text-primary mb-3"></i>
//                        <h3>${car.make} ${car.model}</h3>
//                        <h5 class="text-muted">${car.licensePlate}</h5>
//                        <div class="mt-3">
//                            <span class="badge ${car.status === 'moving' ? 'bg-success' :
//            car.status === 'idle' ? 'bg-warning' : 'bg-danger'} fs-6">
//                                ${car.status.charAt(0).toUpperCase() + car.status.slice(1)}
//                            </span>
//                        </div>
//                    </div>
//                    <div class="col-md-8">
//                        <h5>Vehicle Information</h5>
//                        <table class="table table-sm">
//                            <tr>
//                                <th>Driver:</th>
//                                <td>${car.driver}</td>
//                            </tr>
//                            <tr>
//                                <th>Current Speed:</th>
//                                <td>${car.speed}</td>
//                            </tr>
//                            <tr>
//                                <th>Last Tracked:</th>
//                                <td>${car.lastTracked.toLocaleString()}</td>
//                            </tr>
//                            <tr>
//                                <th>Total Locations:</th>
//                                <td>${car.totalLocations}</td>
//                            </tr>
//                            <tr>
//                                <th>Coordinates:</th>
//                                <td>${car.lastPosition.latitude.toFixed(6)}, ${car.lastPosition.longitude.toFixed(6)}</td>
//                            </tr>
//                        </table>

//                        <div class="mt-4">
//                            <h5>Recent Activity</h5>
//                            <div class="list-group">
//                                <div class="list-group-item">
//                                    <div class="d-flex justify-content-between">
//                                        <span>Started trip at 8:30 AM</span>
//                                        <small>2 hours ago</small>
//                                    </div>
//                                </div>
//                                <div class="list-group-item">
//                                    <div class="d-flex justify-content-between">
//                                        <span>Stopped at gas station</span>
//                                        <small>1 hour ago</small>
//                                    </div>
//                                </div>
//                                <div class="list-group-item">
//                                    <div class="d-flex justify-content-between">
//                                        <span>Arrived at customer location</span>
//                                        <small>30 minutes ago</small>
//                                    </div>
//                                </div>
//                            </div>
//                        </div>
//                    </div>
//                </div>
//            `;

//    document.getElementById('carDetailsContent').innerHTML = html;

//    const modal = new bootstrap.Modal(document.getElementById('carDetailsModal'));
//    modal.show();

//    centerOnCar(carId);
//}

//// Show route for a car
//function showRoute(carId) {
//    // Clear existing route if any
//    if (activeRoute) {
//        map.removeLayer(activeRoute);
//        activeRoute = null;
//    }

//    // Simulate fetching route data
//    const routePoints = [
//        [34.50, 69.10],
//        [34.51, 69.12],
//        [34.52, 69.15],
//        [34.53, 69.18],
//        [34.52, 69.20],
//        [34.51, 69.22],
//        [34.50, 69.25]
//    ];

//    // Create route polyline
//    activeRoute = L.polyline(routePoints, {
//        color: '#0d6efd',
//        weight: 4,
//        opacity: 0.7,
//        dashArray: '10',
//        className: 'route-path'
//    }).addTo(map);

//    // Add start and end markers
//    const startMarker = L.marker(routePoints[0], {
//        icon: L.divIcon({ className: 'fa fa-play-circle fa-2x text-success', iconSize: [30, 30] })
//    }).addTo(map).bindPopup('Start Point');

//    const endMarker = L.marker(routePoints[routePoints.length - 1], {
//        icon: L.divIcon({ className: 'fa fa-flag-checkered fa-2x text-danger', iconSize: [30, 30] })
//    }).addTo(map).bindPopup('Destination');

//    // Fit map to route bounds
//    map.fitBounds(activeRoute.getBounds());

//    // Highlight the car
//    highlightCar(carId);

//    // Show notification
//    showNotification(`Route displayed for Vehicle #${carId}`);
//}

//// Show notification
//function showNotification(message) {
//    const notification = document.createElement('div');
//    notification.className = 'position-fixed top-0 end-0 p-3';
//    notification.style = 'z-index: 1050; margin-top: 70px;';
//    notification.innerHTML = `
//                <div class="toast show" role="alert" aria-live="assertive" aria-atomic="true">
//                    <div class="toast-header bg-primary text-white">
//                        <strong class="me-auto">Tracking System</strong>
//                        <button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast" aria-label="Close"></button>
//                    </div>
//                    <div class="toast-body">
//                        ${message}
//                    </div>
//                </div>
//            `;

//    document.body.appendChild(notification);

//    // Auto remove after 3 seconds
//    setTimeout(() => {
//        notification.remove();
//    }, 3000);
//}

//// Start SignalR connection
//function startSignalRConnection() {
//    connection.start().then(() => {
//        console.log("SignalR Connected");
//        connection.invoke("SubscribeToAllCars").catch(err => console.error(err));
//    }).catch(err => console.error('SignalR Connection Error: ', err));

//    // Handle car position updates
//    connection.on("CarPositionUpdate", (position) => {
//        console.log("Received position update for car:", position.carId);

//        // Update car position in UI
//        if (markers[position.carId]) {
//            markers[position.carId].setLatLng([position.latitude, position.longitude]);
//        }

//        // Update last tracked time in list
//        const carItem = document.getElementById(`car-item-${position.carId}`);
//        if (carItem) {
//            const timeElement = carItem.querySelector('.text-muted.small.mb-0');
//            if (timeElement) {
//                timeElement.textContent = `Last tracked: Just now`;
//            }
//        }
//    });

//    // Handle multiple car updates
//    connection.on("MultipleCarPositions", (positions) => {
//        positions.forEach(position => {
//            if (markers[position.carId]) {
//                markers[position.carId].setLatLng([position.latitude, position.longitude]);
//            }
//        });
//    });

//    // Handle initial positions
//    connection.on("InitialPositions", (positions) => {
//        positions.forEach(position => {
//            if (markers[position.carId]) {
//                markers[position.carId].setLatLng([position.latitude, position.longitude]);
//            }
//        });
//    });
//}

//// Initialize on page load
//window.onload = () => {
//    initMap();
//    loadActiveCars();
//    simulateRealTimeProgress();

//    // Add search functionality
//    document.getElementById('searchInput').addEventListener('input', function () {
//        const searchTerm = this.value.toLowerCase();
//        const carItems = document.querySelectorAll('.car-item');

//        carItems.forEach(item => {
//            const text = item.textContent.toLowerCase();
//            if (text.includes(searchTerm)) {
//                item.style.display = 'flex';
//            } else {
//                item.style.display = 'none';
//            }
//        });
//    });
//};