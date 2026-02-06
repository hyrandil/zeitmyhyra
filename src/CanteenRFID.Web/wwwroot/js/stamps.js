(function(){
    const form = document.getElementById('filter-form');
    const tableBody = document.querySelector('#stamps-table tbody');
    const refreshLabel = document.getElementById('last-refresh');
    const clearBtn = document.getElementById('btn-clear');
    const canDelete = document.getElementById('stamps-table')?.dataset.canDelete === 'true';
    const alertBox = document.getElementById('stamps-alert');
    const deleteModalElement = document.getElementById('stamps-delete-modal');
    const deleteConfirmBtn = document.getElementById('stamps-delete-confirm');
    const deleteCancelBtn = document.getElementById('stamps-delete-cancel');
    const deleteCancelXBtn = document.getElementById('stamps-delete-cancel-x');

    const deleteModal = deleteModalElement && window.bootstrap?.Modal
        ? new window.bootstrap.Modal(deleteModalElement, { backdrop: 'static' })
        : null;

    let pendingDeleteId = null;

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
                <td>${item.userDisplayName ?? item.UserDisplayName ?? (user ? (user.fullName ?? user.FullName ?? '') : 'Unbekannt')}</td>
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

    const performDelete = async () => {
        if (!pendingDeleteId) return;
        const id = pendingDeleteId;
        pendingDeleteId = null;

        const resp = await fetch(`/api/v1/stamps/${id}`, { method: 'DELETE' });
        if (resp.ok) {
            load();
        } else {
            const text = await resp.text();
            showAlert(text || 'Löschen fehlgeschlagen. Prüfe Berechtigungen.');
        }
    };

    const closeDeleteModal = () => {
        pendingDeleteId = null;
        if (deleteModal) {
            deleteModal.hide();
        } else if (deleteModalElement) {
            deleteModalElement.classList.remove('show');
            deleteModalElement.style.display = 'none';
            deleteModalElement.setAttribute('aria-hidden', 'true');
        }
    };

    const openDeleteModal = (id) => {
        pendingDeleteId = id;
        if (deleteModal) {
            deleteModal.show();
        } else if (deleteModalElement) {
            deleteModalElement.classList.add('show');
            deleteModalElement.style.display = 'block';
            deleteModalElement.setAttribute('aria-hidden', 'false');
        }
    };

    deleteConfirmBtn?.addEventListener('click', async () => {
        const idBeforeDelete = pendingDeleteId;
        closeDeleteModal();
        pendingDeleteId = idBeforeDelete;
        await performDelete();
    });

    deleteCancelBtn?.addEventListener('click', closeDeleteModal);
    deleteCancelXBtn?.addEventListener('click', closeDeleteModal);

    tableBody.addEventListener('click', (e) => {
        if (!canDelete) return;

        const rawTarget = e.target;
        const targetElement = rawTarget instanceof Element ? rawTarget : rawTarget?.parentElement;
        if (!targetElement) return;

        const deleteButton = targetElement.closest('.btn-delete');
        if (!deleteButton) return;

        const id = deleteButton.getAttribute('data-id');
        if (!id) return;
        openDeleteModal(id);
    });

    load();
    setInterval(load, 4000);
})();
