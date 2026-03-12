(function(){
    const form = document.getElementById('filter-form');
    const tableBody = document.querySelector('#stamps-table tbody');
    const refreshLabel = document.getElementById('last-refresh');
    const clearBtn = document.getElementById('btn-clear');
    const canDelete = document.getElementById('stamps-table')?.dataset.canDelete === 'true';
    const alertBox = document.getElementById('stamps-alert');
    const antiForgeryToken = document.querySelector('#bulk-delete-form input[name="__RequestVerificationToken"]')?.value || '';
    const pageInfo = document.getElementById('page-info');
    const prevBtn = document.getElementById('btn-prev-page');
    const nextBtn = document.getElementById('btn-next-page');

    let currentPage = 1;
    let totalPages = 1;
    let lastItemsCount = 0;
    const selectedIds = new Set();

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

    const formatDateTime = (value) => {
        if (!value) return '';
        const date = new Date(value);
        return date.toLocaleString('de-DE', { timeZone: 'Europe/Berlin', hour12: false });
    };

    const mealLabel = (value) => {
        const map = {
            0: 'Unbekannt',
            1: 'Frühstück',
            2: 'Mittagessen',
            3: 'Abendessen',
            4: 'Unbekannt',
            Unknown: 'Unbekannt',
            Breakfast: 'Frühstück',
            Lunch: 'Mittagessen',
            Dinner: 'Abendessen',
            Snack: 'Unbekannt'
        };
        return map[value] ?? value;
    };


    const currentPageSize = () => {
        const raw = form?.elements?.namedItem('pageSize')?.value;
        const parsed = Number.parseInt(raw || '25', 10);
        return Number.isFinite(parsed) && parsed > 0 ? parsed : 25;
    };
    const queryFromForm = () => {
        const data = new FormData(form);
        const params = new URLSearchParams();
        for (const [key, value] of data.entries()) {
            if (value) params.append(key, value.toString());
        }
        params.set('page', String(currentPage));
        return params.toString();
    };

    const updatePaginationUi = () => {
        if (pageInfo) pageInfo.textContent = `Seite ${currentPage} von ${totalPages}`;
        if (prevBtn) prevBtn.disabled = currentPage <= 1;

        const optimisticHasNext = lastItemsCount >= currentPageSize();
        if (nextBtn) {
            nextBtn.disabled = !(currentPage < totalPages || optimisticHasNext);
        }
    };

    const renderRows = (items) => {
        tableBody.innerHTML = '';
        items.forEach(item => {
            const id = item.id ?? item.Id;
            const timestampLocal = item.timestampLocal ?? item.TimestampLocal;
            const uid = item.uidRaw ?? item.UidRaw ?? '';
            const user = item.user ?? item.User;
            const readerId = item.readerId ?? item.ReaderId ?? '';
            const mealType = item.mealType ?? item.MealType;
            const userDisplayName = item.userDisplayName ?? item.UserDisplayName ?? (user ? (user.fullName ?? user.FullName ?? '') : 'Unbekannt');
            const userPersonnelNo = item.userPersonnelNo ?? item.UserPersonnelNo ?? (user ? (user.personnelNo ?? user.PersonnelNo ?? '-') : '-');
            const row = document.createElement('tr');
            row.dataset.id = id;
            if (!user && !item.userDisplayName && !item.UserDisplayName) row.classList.add('table-warning');
            row.innerHTML = `
                <td>${canDelete && id ? `<input type="checkbox" class="stamp-select" name="ids" value="${id}" form="bulk-delete-form" ${selectedIds.has(id) ? 'checked' : ''} />` : ''}</td>
                <td>${formatDateTime(timestampLocal)}</td>
                <td>${uid}</td>
                <td>${userDisplayName || 'Unbekannt'}</td>
                <td>${userPersonnelNo || '-'}</td>
                <td>${readerId}</td>
                <td>${mealLabel(mealType)}</td>
                <td class="text-end">
                    ${(!user && !item.userDisplayName && !item.UserDisplayName) ? `<a class="btn btn-sm btn-outline-primary" href="/Users?search=${encodeURIComponent(uid)}">Benutzer verknüpfen</a>` : ''}
                    ${canDelete && id ? `<form method="post" action="/Stamps/Delete/${id}" class="d-inline" onsubmit="return confirm('Löschen?');"><input type="hidden" name="__RequestVerificationToken" value="${antiForgeryToken}" /><button class="btn btn-sm btn-danger" type="submit">Löschen</button></form>` : ''}
                </td>
            `;
            tableBody.appendChild(row);
        });

        const selectAll = document.getElementById('select-all-stamps');
        if (selectAll) {
            const checkboxes = Array.from(document.querySelectorAll('.stamp-select'));
            selectAll.checked = checkboxes.length > 0 && checkboxes.every((cb) => cb.checked);
        }

        refreshLabel.textContent = new Date().toLocaleTimeString('de-DE');
    };

    const load = async () => {
        clearAlert();
        try {
            const qs = queryFromForm();
            const response = await fetch(`/api/v1/stamps?${qs}`, { headers: { Accept: 'application/json' } });
            if (!response.ok) {
                showAlert('Stempelungen konnten nicht geladen werden.');
                return;
            }

            const data = await response.json();
            const items = data.items ?? data.Items ?? [];
            const serverPage = data.page ?? data.Page ?? currentPage;
            const serverTotalPages = Math.max(1, data.totalPages ?? data.TotalPages ?? 1);

            currentPage = serverPage;
            lastItemsCount = items.length;
            totalPages = Math.max(serverTotalPages, currentPage);
            if (lastItemsCount >= currentPageSize() && totalPages <= currentPage) {
                totalPages = currentPage + 1;
            }

            if (currentPage > 1 && lastItemsCount === 0) {
                currentPage -= 1;
                totalPages = Math.max(1, currentPage);
                await load();
                return;
            }

            updatePaginationUi();
            renderRows(items);
        } catch {
            showAlert('Fehler beim Laden der Stempelungen.');
        }
    };

    let debounceTimer;
    form.addEventListener('input', () => {
        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(() => {
            currentPage = 1;
            load();
        }, 400);
    });

    form.addEventListener('submit', (e) => {
        e.preventDefault();
        currentPage = 1;
        load();
    });

    clearBtn?.addEventListener('click', () => {
        form.reset();
        currentPage = 1;
        selectedIds.clear();
        load();
    });

    prevBtn?.addEventListener('click', () => {
        if (currentPage <= 1) return;
        currentPage -= 1;
        load();
    });

    nextBtn?.addEventListener('click', () => {
        if (currentPage >= totalPages) return;
        currentPage += 1;
        load();
    });

    const selectAll = document.getElementById('select-all-stamps');
    selectAll?.addEventListener('change', () => {
        document.querySelectorAll('.stamp-select').forEach((cb) => {
            cb.checked = selectAll.checked;
            if (cb.checked) selectedIds.add(cb.value); else selectedIds.delete(cb.value);
        });
    });

    tableBody.addEventListener('change', (e) => {
        const target = e.target;
        if (!(target instanceof HTMLInputElement) || !target.classList.contains('stamp-select')) return;
        if (target.checked) selectedIds.add(target.value); else selectedIds.delete(target.value);
        if (!selectAll) return;
        const checkboxes = Array.from(document.querySelectorAll('.stamp-select'));
        selectAll.checked = checkboxes.length > 0 && checkboxes.every((cb) => cb.checked);
    });

    updatePaginationUi();
    load();
    setInterval(load, 4000);
})();
