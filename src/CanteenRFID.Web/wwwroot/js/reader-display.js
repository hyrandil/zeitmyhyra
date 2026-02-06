const CONFIG_KEYS = {
    readerId: 'canteenReaderId'
};

const setupPanel = document.getElementById('setupPanel');
const statusPanel = document.getElementById('statusPanel');
const stampCard = document.getElementById('stampCard');
const mealLabel = document.getElementById('mealLabel');
const userName = document.getElementById('userName');
const setupForm = document.getElementById('setupForm');
const readerSelect = document.getElementById('readerSelect');
const hintText = document.querySelector('.status-hint');

let lastStampTimestamp = null;
let hideTimer = null;
let pollInterval = null;

const loadConfig = () => {
    const readerId = localStorage.getItem(CONFIG_KEYS.readerId);
    return { readerId };
};

const saveConfig = (readerId) => {
    localStorage.setItem(CONFIG_KEYS.readerId, readerId);
};

const showStamp = (stamp) => {
    if (hideTimer) {
        clearTimeout(hideTimer);
    }

    mealLabel.textContent = stamp.mealLabel || stamp.mealType || 'Unbekannt';
    userName.textContent = stamp.userName || '';
    stampCard.classList.remove('hidden');
    stampCard.classList.add('visible');
    if (hintText) {
        hintText.textContent = 'Buchung erfasst';
    }

    hideTimer = setTimeout(() => {
        stampCard.classList.remove('visible');
        stampCard.classList.add('hidden');
        if (hintText) {
            hintText.textContent = 'Bitte RFID vorhalten';
        }
    }, 5000);
};

const fetchLatestStamp = async (readerId) => {
    const params = lastStampTimestamp ? `?since=${encodeURIComponent(lastStampTimestamp)}` : '';
    const response = await fetch(`/api/v1/readers/${encodeURIComponent(readerId)}/latest-stamp-display${params}`);

    if (response.status === 204) {
        return null;
    }

    if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
    }

    return response.json();
};

const startPolling = (readerId) => {
    setupPanel.classList.add('hidden');
    statusPanel.classList.remove('hidden');
    if (hintText) {
        hintText.textContent = 'Bitte RFID vorhalten';
    }

    const poll = async () => {
        try {
            const stamp = await fetchLatestStamp(readerId);
            if (stamp) {
                lastStampTimestamp = stamp.timestampUtc || stamp.TimestampUtc;
                showStamp({
                    mealLabel: stamp.mealLabel || stamp.MealLabel,
                    mealType: stamp.mealType || stamp.MealType,
                    userName: stamp.userName || stamp.UserName
                });
            }
        } catch (error) {
            if (hintText) {
                hintText.textContent = 'Verbindung zum Server fehlgeschlagen';
            }
        }
    };

    poll();
    if (pollInterval) {
        clearInterval(pollInterval);
    }
    pollInterval = setInterval(poll, 1200);
};

const loadReaders = async () => {
    const response = await fetch('/api/v1/readers/active');
    if (!response.ok) {
        return [];
    }
    return response.json();
};

const renderReaderOptions = (readers, selectedId) => {
    readerSelect.innerHTML = '';
    const placeholder = document.createElement('option');
    placeholder.value = '';
    placeholder.textContent = 'Bitte wählen...';
    placeholder.disabled = true;
    placeholder.selected = !selectedId;
    readerSelect.appendChild(placeholder);

    readers.forEach((reader) => {
        const option = document.createElement('option');
        option.value = reader.readerId || reader.ReaderId;
        const name = reader.displayName || reader.DisplayName || reader.readerId || reader.ReaderId;
        const location = reader.location || reader.Location;
        option.textContent = location ? `${name} (${location})` : name;
        if (option.value === selectedId) {
            option.selected = true;
        }
        readerSelect.appendChild(option);
    });
};

setupForm.addEventListener('submit', (event) => {
    event.preventDefault();
    const readerId = readerSelect.value;
    if (!readerId) {
        return;
    }

    saveConfig(readerId);
    startPolling(readerId);
});

const bootstrap = async () => {
    const { readerId } = loadConfig();
    const readers = await loadReaders();
    renderReaderOptions(readers, readerId);
    setupPanel.classList.remove('hidden');
    statusPanel.classList.add('hidden');
    if (readerId) {
        startPolling(readerId);
    }
};

bootstrap();
