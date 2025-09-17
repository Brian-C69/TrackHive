(() => {
    'use strict';

    const languageSelect = document.getElementById('onboardingLanguage');
    if (!languageSelect) {
        return;
    }

    const heroTitleEl = document.getElementById('heroTitle');
    const heroSubtitleEl = document.getElementById('heroSubtitle');
    const languageLabelTextEl = document.getElementById('languageLabelText');
    const valuesTitleEl = document.getElementById('valuesTitle');
    const valuesDescriptionEl = document.getElementById('valuesDescription');
    const valuesListEl = document.getElementById('valuesList');
    const conductTitleEl = document.getElementById('conductTitle');
    const conductDescriptionEl = document.getElementById('conductDescription');
    const conductListEl = document.getElementById('conductList');
    const safetyTitleEl = document.getElementById('safetyTitle');
    const safetyDescriptionEl = document.getElementById('safetyDescription');
    const safetyListEl = document.getElementById('safetyList');
    const nextStepsTitleEl = document.getElementById('nextStepsTitle');
    const nextStepsListEl = document.getElementById('nextStepsList');
    const errorEl = document.getElementById('onboardingError');

    const fallbackLanguage = 'en';
    const languageCache = new Map();

    let currentLanguage =
        languageSelect.value ||
        document.documentElement?.dataset?.language ||
        fallbackLanguage;

    if (!languageSelect.value) {
        languageSelect.value = currentLanguage;
    }

    const setLoading = (isLoading) => {
        languageSelect.disabled = Boolean(isLoading);
    };

    const setTextContent = (element, value) => {
        if (!element || typeof value !== 'string') {
            return;
        }
        element.textContent = value;
    };

    const renderCardList = (container, items) => {
        if (!container) {
            return;
        }

        container.innerHTML = '';

        if (!Array.isArray(items)) {
            return;
        }

        items.forEach((item) => {
            if (!item) {
                return;
            }

            const wrapper = document.createElement('div');
            wrapper.className = 'p-3 border rounded';

            if (typeof item.title === 'string') {
                const titleEl = document.createElement('h3');
                titleEl.className = 'h6 mb-1';
                titleEl.textContent = item.title;
                wrapper.appendChild(titleEl);
            }

            if (typeof item.description === 'string') {
                const descriptionEl = document.createElement('p');
                descriptionEl.className = 'mb-0 text-secondary small';
                descriptionEl.textContent = item.description;
                wrapper.appendChild(descriptionEl);
            }

            container.appendChild(wrapper);
        });
    };

    const renderOrderedList = (container, items) => {
        if (!container) {
            return;
        }

        container.innerHTML = '';

        if (!Array.isArray(items)) {
            return;
        }

        items.forEach((item) => {
            if (typeof item !== 'string') {
                return;
            }

            const listItem = document.createElement('li');
            listItem.className = 'mb-2';
            listItem.textContent = item;
            container.appendChild(listItem);
        });

        const lastItem = container.lastElementChild;
        if (lastItem) {
            lastItem.classList.remove('mb-2');
            lastItem.classList.add('mb-0');
        }
    };

    const showError = (message) => {
        if (!errorEl) {
            return;
        }
        errorEl.textContent = message;
        errorEl.classList.remove('d-none');
    };

    const hideError = () => {
        if (!errorEl) {
            return;
        }
        errorEl.classList.add('d-none');
    };

    const applyTranslations = (code, data) => {
        if (!data) {
            return;
        }

        setTextContent(heroTitleEl, data.heroTitle);
        setTextContent(heroSubtitleEl, data.heroSubtitle);
        setTextContent(languageLabelTextEl, data.languageLabel);
        setTextContent(valuesTitleEl, data.valuesTitle);
        setTextContent(valuesDescriptionEl, data.valuesDescription);
        renderCardList(valuesListEl, data.values);
        setTextContent(conductTitleEl, data.conductTitle);
        setTextContent(conductDescriptionEl, data.conductDescription);
        renderCardList(conductListEl, data.conduct);
        setTextContent(safetyTitleEl, data.safetyTitle);
        setTextContent(safetyDescriptionEl, data.safetyDescription);
        renderCardList(safetyListEl, data.safety);
        setTextContent(nextStepsTitleEl, data.nextStepsTitle);
        renderOrderedList(nextStepsListEl, data.nextSteps);

        languageSelect.value = code;
        currentLanguage = code;
    };

    const fetchLanguage = async (code) => {
        if (languageCache.has(code)) {
            return languageCache.get(code);
        }

        const response = await fetch(`/lang/${encodeURIComponent(code)}.json`, {
            headers: {
                Accept: 'application/json',
            },
        });

        if (!response.ok) {
            throw new Error(`Unexpected response: ${response.status}`);
        }

        const payload = await response.json();
        languageCache.set(code, payload);
        return payload;
    };

    const loadLanguage = async (code, allowFallback = true) => {
        const targetCode = code || fallbackLanguage;
        setLoading(true);

        try {
            const payload = await fetchLanguage(targetCode);
            const translations = payload?.translations?.onboarding;
            if (!translations) {
                throw new Error('Missing onboarding translations in language pack');
            }

            applyTranslations(targetCode, translations);
            hideError();
            return true;
        } catch (error) {
            console.error('Unable to load onboarding translations', error);

            if (allowFallback && targetCode !== fallbackLanguage) {
                const fallbackLoaded = await loadLanguage(fallbackLanguage, false);
                if (fallbackLoaded) {
                    showError('We could not load the selected language. Showing English instead.');
                }
                return false;
            }

            showError('Unable to load translations. Please try again.');
            return false;
        } finally {
            setLoading(false);
        }
    };

    languageSelect.addEventListener('change', (event) => {
        const code = event.target.value;
        if (!code || code === currentLanguage) {
            return;
        }

        loadLanguage(code);
    });

    loadLanguage(currentLanguage);
})();
