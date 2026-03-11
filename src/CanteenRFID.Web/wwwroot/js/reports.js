(function(){
    const form = document.getElementById('report-filter-form');
    const resetBtn = document.getElementById('report-reset');
    const tableBody = document.querySelector('#consumption-table tbody');

    const queryFromForm = () => {
        const data = new FormData(form);
        const params = new URLSearchParams();
        for (const [key, value] of data.entries()) {
            if (value) params.append(key, value.toString());
        }
        return params.toString();
    };

    const renderRows = (rows) => {
        tableBody.innerHTML = '';
        rows.forEach(row => {
            const tr = document.createElement('tr');
            tr.innerHTML = `
                <td>${row.personnelNo ?? row.PersonnelNo ?? ''}</td>
                <td>${row.name ?? row.Name ?? ''}</td>
                <td>${row.breakfast ?? row.Breakfast ?? 0}</td>
                <td>${row.lunch ?? row.Lunch ?? 0}</td>
                <td>${row.dinner ?? row.Dinner ?? 0}</td>
            `;
            tableBody.appendChild(tr);
        });
    };

    const load = async () => {
        const qs = queryFromForm();
        const response = await fetch(`/Reports/ConsumptionData?${qs}`, { headers: { 'Accept': 'application/json' } });
        if (!response.ok) return;
        const data = await response.json();
        renderRows(data);
    };

    let debounceTimer;
    form.addEventListener('input', () => {
        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(load, 300);
    });

    form.addEventListener('submit', (e) => {
        e.preventDefault();
        load();
    });

    resetBtn?.addEventListener('click', () => {
        form.reset();
        load();
    });

    load();
})();
