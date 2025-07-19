let map;

window.onload = () => {
    initMap();
    loadActiveCars();
};

function initMap() {
    map = L.map('map').setView([34.5, 69.2], 12); // default view

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; OpenStreetMap contributors'
    }).addTo(map);
}

function loadActiveCars() {
    fetch('/Map/GetActiveCars')
        .then(response => response.json())
        .then(cars => {
            console.log('fetched cars:', cars);
            const carsList = document.getElementById('carsList');
            carsList.innerHTML = '';

            cars.forEach(car => {
                // ---- List UI ----
                const item = document.createElement('div');
                item.className = 'list-group-item car-item d-flex justify-content-between align-items-start';
                item.id = `car-item-${car.id}`;
                item.innerHTML = `
                    <div class="d-flex">
                        <div>
                            <h6 class="fw-semibold mb-1">${car.make} ${car.model}</h6>
                            <p class="text-muted small mb-1">Plate: ${car.licensePlate}</p>
                            <p class="text-muted small mb-0">Last tracked: ${new Date(car.lastTracked).toLocaleString()}</p>
                        </div>
                    </div>
                    <div class="btn-group btn-group-md align-self-center">
                        <button class="btn btn-primary" onclick="loadCarDetail(${car.id})"> 
                        <i class="fas fa-car-side text-secondery me-1 mt-1"></i>Detail</button>
                        <button class="btn btn-success" onclick="trackCar(${car.id})">
                        <i class="fas fa-map text-secondry me-1 mt-1"></i>
                        Track</button>
                    </div>
                `;

                item.addEventListener('click', () => {
                    highlightCar(car.id);
                });

                carsList.appendChild(item);

                
                // ---- Marker ----
                const marker = L.circleMarker ([car.lastPosition.latitude, car.lastPosition.longitude],{
                     radius: 10,
    color: '#007bff',
    fillColor: '#007bff',
    fillOpacity: 0.8
                }).addTo(map);
                const popupContent = `
                    <div>
                        <strong>${car.licensePlate}</strong><br/>
                        ${car.make} ${car.model}<br/>
                        Last Tracked: ${new Date(car.lastTracked).toLocaleString()}<br/>
                        <button class="btn btn-sm btn-primary mt-2" onclick="loadCarDetail(${car.id})">View Detail</button>
                    </div>
                `;
                marker.bindPopup(popupContent);

                marker.on('click', () => {
                    highlightCar(car.id);
                });
            });
        })
        .catch(error => console.error('Error loading cars:', error));
}

// Optional: Highlight selected car in list
function highlightCar(carId) {
    document.querySelectorAll('.car-item').forEach(item => {
        item.classList.remove('active');
    });

    const selectedItem = document.getElementById(`car-item-${carId}`);
    if (selectedItem) {
        selectedItem.classList.add('active');
        selectedItem.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
}



function loadCarDetail(carId) {
    fetch(`/Map/GetCar?id=${carId}`)
        .then(response => response.json())
        .then(car => {
            const html = `
                <h5>${car.make} ${car.model}</h5>
                <p><strong>License Plate:</strong> ${car.licensePlate}</p>
                <p><strong>Last Tracked:</strong> ${new Date(car.lastTracked).toLocaleString()}</p>
                <p><strong>Total Locations:</strong> ${car.totalLocations}</p>
                <p><strong>Coordinates:</strong> (${car.lastPosition.latitude}, ${car.lastPosition.longitude})</p>
            `;
            document.getElementById('carDetailsContent').innerHTML = html;

            const modal = new bootstrap.Modal(document.getElementById('carDetailsModal'));
            modal.show();

            // Optional: zoom to car
            map.setView([car.lastPosition.latitude, car.lastPosition.longitude], 15);
        })
        .catch(error => console.error('Error loading car detail:', error));
}