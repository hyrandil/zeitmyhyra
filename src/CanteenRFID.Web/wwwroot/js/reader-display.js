const CONFIG_KEYS = {
    readerId: 'canteenReaderId',
    apiKey: 'canteenApiKey'
};

const configPanel = document.getElementById('configPanel');
const statusPanel = document.getElementById('statusPanel');
const statusMessage = document.getElementById('statusMessage');
const stampCard = document.getElementById('stampCard');
const mealLabel = document.getElementById('mealLabel');
const userName = document.getElementById('userName');
const configForm = document.getElementById('configForm');
const readerIdInput = document.getElementById('readerIdInput');
const apiKeyInput = document.getElementById('apiKeyInput');

let lastStampTimestamp = null;
let hideTimer = null;

const loadConfig = () => {
    const readerId = localStorage.getItem(CONFIG_KEYS.readerId);
    const apiKey = localStorage.getItem(CONFIG_KEYS.apiKey);
    return { readerId, apiKey };
};

const saveConfig = (readerId, apiKey) => {
    localStorage.setItem(CONFIG_KEYS.readerId, readerId);
    localStorage.setItem(CONFIG_KEYS.apiKey, apiKey);
};

const showStamp = (stamp) => {
    if (hideTimer) {
        clearTimeout(hideTimer);
    }

    mealLabel.textContent = stamp.mealLabel || stamp.mealType || 'Unbekannt';
    userName.textContent = stamp.userName || '';
    stampCard.classList.remove('hidden');
    statusMessage.textContent = 'Stempelung erfasst';

    hideTimer = setTimeout(() => {
        stampCard.classList.add('hidden');
        statusMessage.textContent = 'Warte auf Stempelung…';
    }, 5000);
};

const fetchLatestStamp = async (readerId, apiKey) => {
    const params = lastStampTimestamp ? `?since=${encodeURIComponent(lastStampTimestamp)}` : '';
    const response = await fetch(`/api/v1/readers/${encodeURIComponent(readerId)}/latest-stamp${params}`, {
        headers: {
            'X-API-KEY': apiKey
        }
    });

    if (response.status === 204) {
        return null;
    }

    if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
    }

    return response.json();
};

const startPolling = (readerId, apiKey) => {
    configPanel.classList.add('hidden');
    statusPanel.classList.remove('hidden');
    statusMessage.textContent = 'Warte auf Stempelung…';

    const poll = async () => {
        try {
            const stamp = await fetchLatestStamp(readerId, apiKey);
            if (stamp) {
                lastStampTimestamp = stamp.timestampUtc || stamp.TimestampUtc;
                showStamp({
                    mealLabel: stamp.mealLabel || stamp.MealLabel,
                    mealType: stamp.mealType || stamp.MealType,
                    userName: stamp.userName || stamp.UserName
                });
            }
        } catch (error) {
            statusMessage.textContent = 'Verbindung zum Server fehlgeschlagen.';
        }
    };

    poll();
    setInterval(poll, 1500);
};

configForm.addEventListener('submit', (event) => {
    event.preventDefault();
    const readerId = readerIdInput.value.trim();
    const apiKey = apiKeyInput.value.trim();
    if (!readerId || !apiKey) {
        return;
    }

    saveConfig(readerId, apiKey);
    startPolling(readerId, apiKey);
});

const bootstrap = () => {
    const { readerId, apiKey } = loadConfig();
    if (readerId && apiKey) {
        startPolling(readerId, apiKey);
    } else {
        configPanel.classList.remove('hidden');
        statusPanel.classList.add('hidden');
    }
};

bootstrap();
