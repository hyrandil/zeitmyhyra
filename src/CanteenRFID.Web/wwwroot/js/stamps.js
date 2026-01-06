(function(){
    const form = document.getElementById('filter-form');
    const tableBody = document.querySelector('#stamps-table tbody');
    const refreshLabel = document.getElementById('last-refresh');
    const clearBtn = document.getElementById('btn-clear');

    const queryFromForm = () => {
        const data = new FormData(form);
        const params = new URLSearchParams();
        for (const [key, value] of data.entries()) {
            if (value) params.append(key, value.toString());
        }
        return params.toString();
    };

    const renderRows = (items) => {
        tableBody.innerHTML = '';
        items.forEach(item => {
            const row = document.createElement('tr');
            if (!item.user) row.classList.add('table-warning');
            row.innerHTML = `
                <td>${item.timestampUtc}</td>
                <td>${item.timestampLocal}</td>
                <td>${item.uidRaw}</td>
                <td>${item.user ? item.user.fullName : 'Unbekannt'}</td>
                <td>${item.readerId}</td>
                <td>${item.mealType}</td>
                <td>${!item.user ? `<a class="btn btn-sm btn-outline-primary" href="/Users?search=${encodeURIComponent(item.uidRaw)}">Benutzer verknüpfen</a>` : ''}</td>
            `;
            tableBody.appendChild(row);
        });
        refreshLabel.textContent = new Date().toLocaleTimeString();
    };

    const load = async () => {
        const qs = queryFromForm();
        const response = await fetch(`/api/v1/stamps?${qs}`, { headers: { 'Accept': 'application/json' } });
        if (!response.ok) return;
        const data = await response.json();
        renderRows(data);
    };

    let debounceTimer;
    form.addEventListener('input', () => {
        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(load, 400);
    });

    form.addEventListener('submit', (e) => {
        e.preventDefault();
        load();
    });

    clearBtn?.addEventListener('click', () => {
        form.reset();
        load();
    });

    load();
    setInterval(load, 4000);
})();
