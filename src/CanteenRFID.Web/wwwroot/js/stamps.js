(function(){
    const form = document.getElementById('filter-form');
    const tableBody = document.querySelector('#stamps-table tbody');
    const refreshLabel = document.getElementById('last-refresh');
    const clearBtn = document.getElementById('btn-clear');
    const canDelete = document.getElementById('stamps-table')?.dataset.canDelete === 'true';
    const alertBox = document.getElementById('stamps-alert');

    const showAlert = (message, type = 'danger') => {
        if (!alertBox) return;
        alertBox.className = `alert alert-${type}`;
        alertBox.textContent = message;
    };

    const clearAlert = () => {
        if (!alertBox) return;
        alertBox.className = 'd-none';
        alertBox.textContent = '';
    };

    const formatDateTime = (value, timeZone) => {
        if (!value) return '';
        const date = new Date(value);
        return date.toLocaleString('de-DE', {
            timeZone: timeZone || 'Europe/Berlin',
            hour12: false
        });
    };

    const mealLabel = (value) => {
        const map = {
            0: 'Unbekannt',
            1: 'Frühstück',
            2: 'Mittagessen',
            3: 'Abendessen',
            4: 'Unbekannt',
            'Unknown': 'Unbekannt',
            'Breakfast': 'Frühstück',
            'Lunch': 'Mittagessen',
            'Dinner': 'Abendessen',
            'Snack': 'Unbekannt'
        };
        return map[value] ?? value;
    };

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
            const id = item.id ?? item.Id;
            const timestampUtc = item.timestampUtc ?? item.TimestampUtc;
            const timestampLocal = item.timestampLocal ?? item.TimestampLocal;
            const uid = item.uidRaw ?? item.UidRaw ?? '';
            const user = item.user ?? item.User;
            const readerId = item.readerId ?? item.ReaderId ?? '';
            const mealType = item.mealType ?? item.MealType;
            const row = document.createElement('tr');
            row.dataset.id = id;
            if (!user) row.classList.add('table-warning');
            row.innerHTML = `
                <td>${formatDateTime(timestampUtc, 'UTC')}</td>
                <td>${formatDateTime(timestampLocal, 'Europe/Berlin')}</td>
                <td>${uid}</td>
                <td>${stamp.userDisplayName ?? stamp.UserDisplayName ?? (user ? (user.fullName ?? user.FullName ?? '') : 'Unbekannt')}</td>
                <td>${readerId}</td>
                <td>${mealLabel(mealType)}</td>
                <td class="text-end">
                    ${!user ? `<a class="btn btn-sm btn-outline-primary" href="/Users?search=${encodeURIComponent(uid)}">Benutzer verknüpfen</a>` : ''}
                    ${canDelete && id ? `<button type="button" class="btn btn-sm btn-outline-danger btn-delete" data-id="${id}">Löschen</button>` : ''}
                </td>
            `;
            tableBody.appendChild(row);
        });
        refreshLabel.textContent = new Date().toLocaleTimeString('de-DE');
    };

    const load = async () => {
        clearAlert();
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

    tableBody.addEventListener('click', async (e) => {
        const target = e.target;
        if (!canDelete) return;
        if (target instanceof HTMLElement && target.classList.contains('btn-delete')) {
            const id = target.dataset.id;
            if (!id) return;
            if (!confirm('Stempelung wirklich löschen?')) return;
            const resp = await fetch(`/api/v1/stamps/${id}`, { method: 'DELETE' });
            if (resp.ok) {
                load();
            } else {
                const text = await resp.text();
                showAlert(text || 'Löschen fehlgeschlagen. Prüfe Berechtigungen.');
            }
        }
    });

    load();
    setInterval(load, 4000);
})();
