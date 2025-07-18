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
    fetch('/Cars/GetActiveCars')
        .then(response => response.json())
        .then(cars => {
            document.getElementById('carList').innerHTML = '';
            cars.forEach(car => {
                // Add to side panel
                const li = document.createElement('li');
                li.className = 'list-group-item';
                li.textContent = car.licensePlate;
                li.style.cursor = 'pointer';
                li.onclick = () => loadCarDetail(car.id);
                document.getElementById('carList').appendChild(li);

                // Add marker to map
                const marker = L.marker([car.latitude, car.longitude]).addTo(map);
                marker.on('click', () => loadCarDetail(car.id));
            });
        })
        .catch(error => console.error('Error loading cars:', error));
}

function loadCarDetail(carId) {
    fetch(`/Cars/GetCar?id=${carId}`)
        .then(response => response.json())
        .then(car => {
            const popupHtml = `
                <strong>${car.make} ${car.model}</strong><br>
                License: ${car.licensePlate}<br>
                Last Seen: ${new Date(car.lastTracked).toLocaleString()}<br>
                Total Points: ${car.totalLocations}
            `;

            // Center map and show popup
            const { latitude, longitude } = car.lastPosition;
            const popup = L.popup()
                .setLatLng([latitude, longitude])
                .setContent(popupHtml)
                .openOn(map);
        })
        .catch(error => console.error('Error loading car detail:', error));
}
