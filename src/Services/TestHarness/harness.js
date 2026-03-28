(async function () {

    // =====================================================================
    // Available journey configs
    // =====================================================================

    const CONFIGS = [
        { value: 'payment',  label: 'Payment' },
        { value: 'user',     label: 'User' },
        { value: 'loyalty',  label: 'Loyalty' },
        { value: 'admin',    label: 'Admin' }
    ];

    // =====================================================================
    // Config dropdown — populate and select initial value
    // =====================================================================

    const configSelect = document.getElementById('configSelect');
    const params = new URLSearchParams(window.location.search);
    let config = params.get('config') || 'payment';

    CONFIGS.forEach(c => {
        const opt = document.createElement('option');
        opt.value = c.value;
        opt.textContent = c.label;
        if (c.value === config) opt.selected = true;
        configSelect.appendChild(opt);
    });

    // =====================================================================
    // Name pools for random test data generation
    // =====================================================================

    const FIRST_NAMES = [
        'Amara', 'James', 'Priya', 'Liam', 'Fatima', 'Oliver', 'Sophie',
        'Mohammed', 'Emily', 'Carlos', 'Aisha', 'Daniel', 'Charlotte',
        'Ravi', 'Emma', 'Noah', 'Mia', 'David', 'Yuki', 'Thomas',
        'Aaliya', 'Adrian', 'Akiko', 'Alejandro', 'Amira', 'Andrei',
        'Annika', 'Antonio', 'Arjun', 'Astrid', 'Beatriz', 'Benjamin',
        'Bianca', 'Bjorn', 'Camille', 'Chen', 'Chiara', 'Chloe',
        'Cristina', 'Daisuke', 'Daria', 'Declan', 'Dmitri', 'Elena',
        'Elias', 'Emeka', 'Erika', 'Ethan', 'Farah', 'Felix',
        'Freya', 'Gabriel', 'Gabriella', 'Hamza', 'Hannah', 'Haruki',
        'Hassan', 'Helena', 'Hugo', 'Ibrahim', 'Ingrid', 'Isla',
        'Ivan', 'Jack', 'Jasmine', 'Javier', 'Julia', 'Jun',
        'Kai', 'Kamila', 'Karim', 'Katarina', 'Kenji', 'Khadija',
        'Kwame', 'Layla', 'Leo', 'Lina', 'Lucas', 'Luna',
        'Malik', 'Mariam', 'Mateo', 'Maya', 'Mei', 'Miguel',
        'Nadia', 'Nathan', 'Nia', 'Nikolai', 'Nina', 'Noor',
        'Oscar', 'Petra', 'Rafael', 'Rana', 'Rosa', 'Rowan',
        'Sahra', 'Samuel', 'Sara', 'Sebastian', 'Seo-Yeon', 'Sienna',
        'Soren', 'Tariq', 'Valentina', 'Victor', 'William', 'Xin',
        'Yasmin', 'Zahra', 'Zara', 'Zoe'
    ];
    const SURNAMES = [
        'Okafor', 'Smith', 'Patel', 'Johnson', 'Ahmed', 'Garcia', 'Williams',
        'Taylor', 'Kumar', 'Martinez', 'Anderson', 'Robinson', 'Harris',
        'Lee', 'Wilson', 'Clark', 'Lewis', 'Walker', 'Hall', 'Brown',
        'Andersson', 'Bauer', 'Beltrán', 'Berg', 'Bergström', 'Bianchi',
        'Bogdanov', 'Cai', 'Castillo', 'Chandra', 'Chang', 'Cheung',
        'Costa', 'Cruz', 'Da Silva', 'De Jong', 'Delgado', 'Deng',
        'Diallo', 'Dubois', 'Eriksen', 'Fernandez', 'Fischer', 'Flores',
        'Fontaine', 'Fujimoto', 'Gao', 'Gomez', 'Gonzalez', 'Gruber',
        'Gupta', 'Hansen', 'Hasan', 'Hernandez', 'Ho', 'Hoffmann',
        'Hussain', 'Ibrahim', 'Ishikawa', 'Ivanov', 'Jensen', 'Johansson',
        'Kato', 'Khan', 'Kim', 'Kowalski', 'Larsen', 'Laurent',
        'Li', 'Lindberg', 'Lopez', 'Lund', 'Mäkinen', 'Mancini',
        'Mendoza', 'Meyer', 'Moreau', 'Moreno', 'Morita', 'Muller',
        'Murphy', 'Nakamura', 'Ng', 'Nielsen', 'Novak', 'Oliveira',
        'Omar', 'Park', 'Petrov', 'Ramirez', 'Reyes', 'Rossi',
        'Santos', 'Sato', 'Schmidt', 'Schneider', 'Singh', 'Svensson',
        'Takahashi', 'Tanaka', 'Torres', 'Tran', 'Virtanen', 'Wagner',
        'Wang', 'Weber', 'Wong', 'Wu', 'Yamamoto', 'Zhang'
    ];

    function pick(arr) { return arr[Math.floor(Math.random() * arr.length)]; }
    function randDigits(n) { return Math.floor(Math.random() * Math.pow(10, n)).toString().padStart(n, '0'); }

    let runtimeVars = {};

    function generateRuntimeVars() {
        const givenName = pick(FIRST_NAMES);
        const surname   = pick(SURNAMES);
        const password  = 'Apex@ir2026!';
        const email     = givenName.toLowerCase() + '.' +
                          surname.toLowerCase() + '.' +
                          randDigits(6) + '@testmail.example.com';

        const recipientGivenName = pick(FIRST_NAMES);
        const recipientSurname   = pick(SURNAMES);
        const recipientPassword  = 'Apex@ir2026!';
        const recipientEmail     = recipientGivenName.toLowerCase() + '.' +
                                   recipientSurname.toLowerCase() + '.' +
                                   randDigits(6) + '@testmail.example.com';

        runtimeVars = {
            givenName, surname, password, email,
            recipientGivenName, recipientSurname, recipientPassword, recipientEmail
        };
    }

    function applyRuntimeVars(obj) {
        if (obj === null || obj === undefined) return obj;
        if (typeof obj === 'string') {
            return obj
                .replace(/__RAND_RECIPIENT_GIVEN_NAME__/g, runtimeVars.recipientGivenName)
                .replace(/__RAND_RECIPIENT_SURNAME__/g,    runtimeVars.recipientSurname)
                .replace(/__RAND_RECIPIENT_EMAIL__/g,      runtimeVars.recipientEmail)
                .replace(/__RAND_RECIPIENT_PASSWORD__/g,   runtimeVars.recipientPassword)
                .replace(/__RAND_GIVEN_NAME__/g, runtimeVars.givenName)
                .replace(/__RAND_SURNAME__/g,    runtimeVars.surname)
                .replace(/__RAND_EMAIL__/g,      runtimeVars.email)
                .replace(/__RAND_PASSWORD__/g,   runtimeVars.password);
        }
        if (Array.isArray(obj)) return obj.map(applyRuntimeVars);
        if (typeof obj === 'object') {
            const out = {};
            for (const [k, v] of Object.entries(obj)) out[k] = applyRuntimeVars(v);
            return out;
        }
        return obj;
    }

    // =====================================================================
    // API interaction log
    // =====================================================================

    let apiLog = [];

    function logInteraction(entry) {
        apiLog.push(entry);
    }

    function formatLogForCopy() {
        if (apiLog.length === 0) return 'No API interactions recorded.';

        const lines = [];
        lines.push('='.repeat(80));
        lines.push('Apex Air Test Harness — API Log');
        lines.push('Journey: ' + (raw ? (raw.journey.domain || config) : config));
        lines.push('Timestamp: ' + new Date().toISOString());
        lines.push('='.repeat(80));

        apiLog.forEach((entry, i) => {
            lines.push('');
            lines.push('-'.repeat(80));
            lines.push('Step ' + entry.step + ': ' + entry.name);
            lines.push('-'.repeat(80));
            lines.push('');
            lines.push('REQUEST');
            lines.push('  ' + entry.method + ' ' + entry.url);
            lines.push('');
            if (entry.requestHeaders && Object.keys(entry.requestHeaders).length) {
                lines.push('  Headers:');
                for (const [k, v] of Object.entries(entry.requestHeaders)) {
                    lines.push('    ' + k + ': ' + v);
                }
                lines.push('');
            }
            if (entry.requestBody !== null && entry.requestBody !== undefined) {
                lines.push('  Body:');
                lines.push(indent(formatBody(entry.requestBody), '    '));
            } else {
                lines.push('  Body: (none)');
            }
            lines.push('');
            lines.push('RESPONSE');
            if (entry.error) {
                lines.push('  Error: ' + entry.error);
            } else {
                lines.push('  Status: ' + entry.status + ' ' + (statusLabel(entry.status) || ''));
                lines.push('');
                if (entry.responseBody !== null && entry.responseBody !== undefined) {
                    lines.push('  Body:');
                    lines.push(indent(formatBody(entry.responseBody), '    '));
                } else {
                    lines.push('  Body: (none)');
                }
            }
        });

        lines.push('');
        lines.push('='.repeat(80));
        lines.push('End of log (' + apiLog.length + ' interactions)');
        lines.push('='.repeat(80));

        return lines.join('\n');
    }

    function formatBody(body) {
        if (typeof body === 'string') return body;
        return JSON.stringify(body, null, 2);
    }

    function indent(text, prefix) {
        return text.split('\n').map(line => prefix + line).join('\n');
    }

    // =====================================================================
    // Copy Logs button
    // =====================================================================

    const btnCopyLogs = document.getElementById('btnCopyLogs');

    btnCopyLogs.addEventListener('click', async () => {
        const text = formatLogForCopy();
        try {
            await navigator.clipboard.writeText(text);
            btnCopyLogs.textContent = '\u2713 Copied';
            btnCopyLogs.classList.add('copied');
            setTimeout(() => {
                btnCopyLogs.textContent = '\uD83D\uDCCB Copy Logs';
                btnCopyLogs.classList.remove('copied');
            }, 2000);
        } catch {
            // Fallback for non-secure contexts
            const ta = document.createElement('textarea');
            ta.value = text;
            ta.style.position = 'fixed';
            ta.style.opacity = '0';
            document.body.appendChild(ta);
            ta.select();
            document.execCommand('copy');
            document.body.removeChild(ta);
            btnCopyLogs.textContent = '\u2713 Copied';
            btnCopyLogs.classList.add('copied');
            setTimeout(() => {
                btnCopyLogs.textContent = '\uD83D\uDCCB Copy Logs';
                btnCopyLogs.classList.remove('copied');
            }, 2000);
        }
    });

    // =====================================================================
    // Load journey definition
    // =====================================================================

    let raw;

    async function loadJourney(configName) {
        const res = await fetch(configName + '-journey.json');
        raw = await res.json();
        config = configName;
        initJourney();
    }

    // =====================================================================
    // State
    // =====================================================================

    let liveStepIndices, liveChain, liveResults, rowRefs, hasRuntimeVars, defaultBaseUrlId;

    const tbody = document.getElementById('journeyBody');
    const baseUrlListEl = document.getElementById('baseUrlList');
    const btnRunAll = document.getElementById('btnRunAll');

    // =====================================================================
    // Initialise journey after load or switch
    // =====================================================================

    function initJourney() {
        // Page title
        const journeyTitle = raw.journey.domain || config;
        document.title = 'Apex Air \u2014 ' + journeyTitle + ' API Test Harness';
        document.getElementById('journeyTitle').textContent = journeyTitle + ' API';

        // Detect runtime variable substitution
        hasRuntimeVars = JSON.stringify(raw.steps).includes('__RAND_');

        // Base URLs
        defaultBaseUrlId = raw.journey.baseUrls[0].id;
        renderBaseUrlList();
        runAllHealthChecks();

        // Table setup
        liveStepIndices = raw.steps.map((_, i) => i);
        liveChain = {};
        liveResults = {};
        rowRefs = [];

        // Clear logs on journey switch
        apiLog = [];
        btnCopyLogs.disabled = true;

        // Initial render
        if (hasRuntimeVars) {
            generateRuntimeVars();
            buildTableRows(buildStepsWithVars());
            updateRuntimeBanner();
        } else {
            document.getElementById('runtimeDataBanner').style.display = 'none';
            buildTableRows(JSON.parse(JSON.stringify(raw.steps)));
        }

        btnRunAll.disabled = false;
    }

    // =====================================================================
    // Config dropdown change handler
    // =====================================================================

    configSelect.addEventListener('change', () => {
        loadJourney(configSelect.value);
    });

    // =====================================================================
    // Base URL inputs & health checks
    // =====================================================================

    function renderBaseUrlList() {
        baseUrlListEl.innerHTML = '';
        raw.journey.baseUrls.forEach(entry => {
            const row = document.createElement('div');
            row.className = 'base-url-row';
            row.innerHTML =
                '<label class="base-url-label">' + entry.label + '</label>' +
                '<input type="text" id="baseUrl-' + entry.id + '" class="base-url-input" value="' + entry.url + '" spellcheck="false">' +
                '<span id="health-' + entry.id + '" class="health-indicator checking" title="Health check: ' + entry.url + '/api/v1/health">\u2026</span>';
            baseUrlListEl.appendChild(row);
        });
    }

    function getBaseUrl(ref) {
        const input = document.getElementById('baseUrl-' + ref);
        return input ? input.value.replace(/\/+$/, '') : '';
    }

    async function checkHealthForEntry(entry) {
        const indicator = document.getElementById('health-' + entry.id);
        if (!indicator) return;
        const url = getBaseUrl(entry.id);
        indicator.textContent = '\u2026';
        indicator.className = 'health-indicator checking';
        indicator.title = 'Health check: ' + url + '/api/v1/health';
        try {
            const r = await fetch(url + '/api/v1/health');
            if (r.ok) {
                indicator.textContent = '\u2713';
                indicator.className = 'health-indicator healthy';
            } else {
                indicator.textContent = '\u2717';
                indicator.className = 'health-indicator unhealthy';
            }
        } catch {
            indicator.textContent = '\u2717';
            indicator.className = 'health-indicator unhealthy';
        }
    }

    function runAllHealthChecks() {
        raw.journey.baseUrls.forEach(entry => checkHealthForEntry(entry));
    }

    let healthDebounce;
    baseUrlListEl.addEventListener('input', () => {
        clearTimeout(healthDebounce);
        healthDebounce = setTimeout(runAllHealthChecks, 600);
    });

    // =====================================================================
    // Table setup
    // =====================================================================

    function buildTableRows(steps) {
        tbody.innerHTML = '';
        rowRefs = [];

        steps.forEach((step, idx) => {
            const row = document.createElement('tr');

            const tdStep = document.createElement('td');
            tdStep.innerHTML = `<div class="step-number">Step ${step.step}</div>`;

            const tdName = document.createElement('td');
            tdName.innerHTML = `<div class="step-name">${esc(step.name)}</div>`;

            const tdApi = document.createElement('td');
            const api = step.apiCall;
            let endpointHtml = esc(api.endpoint).replace(/\{(\w+)\}/g, '<span class="path-param">{$1}</span>');
            tdApi.innerHTML = `<span class="method-badge method-${api.method}">${api.method}</span> <span class="endpoint-url">${endpointHtml}</span>`;

            const tdExpected = document.createElement('td');
            const ex = step.expected;
            const scClass = ex.statusCode >= 200 && ex.statusCode < 300 ? 'ok' : 'error';
            tdExpected.innerHTML = `<div class="expected-status ${scClass}">${ex.statusCode} ${statusLabel(ex.statusCode)}</div>`;

            row.appendChild(tdStep);
            row.appendChild(tdName);
            row.appendChild(tdApi);
            row.appendChild(tdExpected);

            const ref = { row, step, idx };
            rowRefs.push(ref);

            row.addEventListener('click', () => openStepModal(ref.currentStep || ref.step, ref.idx));
            tbody.appendChild(row);
        });
    }

    // =====================================================================
    // Modal
    // =====================================================================

    const modalOverlay = document.getElementById('stepModal');
    document.getElementById('modalClose').addEventListener('click', () => { modalOverlay.style.display = 'none'; });
    modalOverlay.addEventListener('click', e => { if (e.target === modalOverlay) modalOverlay.style.display = 'none'; });

    function openStepModal(step, idx) {
        const modalTitle = document.getElementById('modalTitle');
        const modalBody  = document.getElementById('modalBody');

        const isLive = liveStepIndices.includes(idx);
        const livePill = isLive
            ? '<span style="font-size:0.65rem;font-family:var(--font-mono);color:var(--accent);border:1px solid var(--accent);border-radius:3px;padding:0.1rem 0.4rem;margin-left:0.5rem;vertical-align:middle">LIVE</span>'
            : '';
        modalTitle.innerHTML = `<span class="step-number" style="display:inline;font-size:1rem">Step ${step.step}</span> \u2014 ${esc(step.name)}${livePill}`;

        let html = '';

        if (step.description) {
            html += `<div class="modal-section-desc">${esc(step.description)}</div>`;
        }

        html += '<div class="modal-section"><div class="modal-section-title">API Call</div>';
        html += buildApiCallCell(step).innerHTML;
        html += '</div>';

        html += '<div class="modal-section"><div class="modal-section-title">Request</div>';
        html += buildRequestCell(step).innerHTML;
        html += '</div>';

        html += '<div class="modal-section"><div class="modal-section-title">Expected Response</div>';
        html += buildExpectedCell(step).innerHTML;
        const respTd = buildResponseCell(step);
        if (step.response.body !== null && step.response.body !== undefined) {
            html += '<div style="margin-top:0.6rem">' + respTd.querySelector('.json-block').outerHTML + '</div>';
        }
        if (step.chainsTo && step.chainsTo.length) {
            html += respTd.querySelector('.chain-section') ? respTd.querySelector('.chain-section').outerHTML : '';
        }
        html += '</div>';

        const result = liveResults[idx];
        if (result) {
            const scClass = result.statusMatch ? 'pass' : 'fail';
            const scLabel = statusLabel(result.liveStatus) || (result.liveError ? 'Network Error' : '');
            const resBadge = result.statusMatch
                ? '<span class="result-badge pass">Pass</span>'
                : '<span class="result-badge fail">Fail</span>';

            html += '<div class="modal-section"><div class="modal-section-title">Live Result</div>';
            html += `<div class="live-result-label">Live Response ${resBadge}</div>`;
            if (result.url) html += `<div class="live-url">\u2192 ${esc(result.url)}</div>`;
            if (result.liveError) {
                html += `<div class="status-code fail">Error: ${esc(result.liveError)}</div>`;
            } else {
                html += `<div class="status-code ${scClass}">${result.liveStatus} ${esc(scLabel)}</div>`;
            }
            if (result.liveBody !== null && result.liveBody !== undefined) {
                if (typeof result.liveBody === 'object') {
                    html += '<div class="json-block">' + syntaxHighlight(result.liveBody, null, step.chainsTo) + '</div>';
                } else {
                    html += '<div class="json-block">' + esc(String(result.liveBody)) + '</div>';
                }
            } else if (!result.liveError) {
                html += '<div class="no-body">No response body</div>';
            }
            html += '</div>';
        }

        modalBody.innerHTML = html;
        modalOverlay.style.display = 'flex';
    }

    // =====================================================================
    // Runtime data banner
    // =====================================================================

    function updateRuntimeBanner() {
        const banner   = document.getElementById('runtimeDataBanner');
        const valuesEl = document.getElementById('runtimeDataValues');
        valuesEl.innerHTML = '';
        banner.style.display = '';
        [
            ['givenName', runtimeVars.givenName],
            ['surname',   runtimeVars.surname],
            ['email',     runtimeVars.email],
            ['password',  runtimeVars.password]
        ].forEach(([label, value]) => {
            const chip = document.createElement('span');
            chip.style.cssText = 'font-family:var(--font-mono);font-size:0.72rem;background:var(--surface-raised);border:1px solid var(--border);border-radius:4px;padding:0.2rem 0.6rem';
            chip.innerHTML = `<span style="color:var(--text-muted)">${esc(label)}:</span> <span style="color:var(--accent)">${esc(value)}</span>`;
            valuesEl.appendChild(chip);
        });
    }

    // =====================================================================
    // Step data helpers
    // =====================================================================

    function buildStepsWithVars() {
        return JSON.parse(JSON.stringify(raw.steps)).map(step => {
            if (step.request && step.request.body) {
                step.request.body = applyRuntimeVars(step.request.body);
            }
            return step;
        });
    }

    // =====================================================================
    // Run All button
    // =====================================================================

    btnRunAll.addEventListener('click', runLiveSteps);

    // =====================================================================
    // Live API invocation
    // =====================================================================

    async function runLiveSteps() {
        btnRunAll.disabled = true;
        btnRunAll.textContent = '\u23F3 Running\u2026';
        btnCopyLogs.disabled = true;
        configSelect.disabled = true;

        // Clear previous logs
        apiLog = [];

        let currentSteps;
        if (hasRuntimeVars) {
            generateRuntimeVars();
            currentSteps = buildStepsWithVars();
            updateRuntimeBanner();
        } else {
            currentSteps = JSON.parse(JSON.stringify(raw.steps));
        }

        buildTableRows(currentSteps);
        liveChain = {};
        liveResults = {};

        for (const stepIdx of liveStepIndices) {
            await runStep(rowRefs[stepIdx], currentSteps);
        }

        btnRunAll.disabled = false;
        btnRunAll.textContent = '\u25B6 Run';
        btnCopyLogs.disabled = false;
        configSelect.disabled = false;
    }

    async function runStep(ref, currentSteps) {
        const { row, idx } = ref;
        const step = currentSteps[idx];
        ref.currentStep = step;

        const stepBaseUrlRef = step.apiCall.baseUrlRef || defaultBaseUrlId;
        const baseUrl = getBaseUrl(stepBaseUrlRef) || getBaseUrl(defaultBaseUrlId);
        const api = step.apiCall;

        // Build the full URL, substituting path params from liveChain
        let endpoint = api.endpoint;
        if (api.pathParams) {
            for (const [k, v] of Object.entries(api.pathParams)) {
                const chainedValue = liveChain[k];
                endpoint = endpoint.replace(`{${k}}`, chainedValue !== undefined ? chainedValue : v);
            }
        }
        const url = baseUrl + endpoint;

        // Build request options
        const fetchOpts = {
            method: api.method,
            headers: {}
        };

        if (step.request.headers) {
            Object.assign(fetchOpts.headers, step.request.headers);
        }

        // Deep-copy the body
        let requestBody = (step.request.body !== null && step.request.body !== undefined)
            ? JSON.parse(JSON.stringify(step.request.body))
            : null;

        // Substitute chained values into request body fields
        if (step.request.dataChain && requestBody) {
            step.request.dataChain.forEach(chain => {
                const fieldName = chain.field.replace(/ \(path\)$/, '');
                const chainKey = chain.from || fieldName;
                if (fieldName in requestBody && liveChain[chainKey] !== undefined) {
                    requestBody[fieldName] = liveChain[chainKey];
                }
            });
        }

        // Substitute chained Authorization header
        if (step.request.dataChain) {
            step.request.dataChain.forEach(chain => {
                if (chain.field === 'Authorization' && liveChain['accessToken']) {
                    fetchOpts.headers['Authorization'] = 'Bearer ' + liveChain['accessToken'];
                }
            });
        }

        if (requestBody !== null && requestBody !== undefined) {
            fetchOpts.body = JSON.stringify(requestBody);
        }

        let liveStatus, liveBody, liveError;

        try {
            const response = await fetch(url, fetchOpts);
            liveStatus = response.status;

            const contentType = response.headers.get('content-type') || '';
            if (contentType.includes('application/json')) {
                liveBody = await response.json();
            } else {
                const text = await response.text();
                liveBody = text.length > 0 ? text : null;
            }
        } catch (err) {
            liveError = err.message;
            liveStatus = 0;
            liveBody = null;
        }

        // Log this interaction
        logInteraction({
            step: step.step,
            name: step.name,
            method: api.method,
            url: url,
            requestHeaders: { ...fetchOpts.headers },
            requestBody: requestBody,
            status: liveStatus,
            responseBody: liveBody,
            error: liveError || null
        });

        // Store chained values from response (supports array indexing e.g. [0].identityId)
        if (step.chainsTo && liveBody && typeof liveBody === 'object') {
            step.chainsTo.forEach(chain => {
                const field = chain.field;
                const arrayMatch = field.match(/^\[(\d+)\]\.(.+)$/);
                if (arrayMatch && Array.isArray(liveBody)) {
                    const arrIdx = parseInt(arrayMatch[1], 10);
                    const prop = arrayMatch[2];
                    if (liveBody[arrIdx] && liveBody[arrIdx][prop] !== undefined) {
                        liveChain[chain.as || prop] = liveBody[arrIdx][prop];
                    }
                } else if (liveBody[field] !== undefined) {
                    liveChain[chain.as || field] = liveBody[field];
                }
            });
        }

        // Resolve path parameter chains for subsequent steps
        resolvePathParamChains(step, currentSteps);

        // Determine pass/fail
        const statusMatch = liveStatus === step.expected.statusCode;
        row.classList.remove('step-positive', 'step-negative', 'result-pass', 'result-fail');
        row.classList.add(statusMatch ? 'result-pass' : 'result-fail');

        liveResults[idx] = { liveStatus, liveBody, liveError, statusMatch, url };
    }

    function resolvePathParamChains(completedStep, allSteps) {
        if (!completedStep.chainsTo || !completedStep.chainsTo.length) return;

        completedStep.chainsTo.forEach(chain => {
            const alias = chain.as || chain.field;
            const value = liveChain[alias];
            if (value === undefined) return;

            const fromRef = 'from-step-' + completedStep.step;
            allSteps.forEach(s => {
                if (s.apiCall.pathParams) {
                    for (const [paramName, paramVal] of Object.entries(s.apiCall.pathParams)) {
                        if (paramVal === fromRef) {
                            liveChain[paramName] = value;
                        }
                    }
                }
            });
        });
    }

    // =====================================================================
    // Cell builders
    // =====================================================================

    function buildApiCallCell(step) {
        const td = document.createElement('td');
        const api = step.apiCall;
        const methodClass = 'method-' + api.method;

        let endpointHtml = esc(api.endpoint);
        endpointHtml = endpointHtml.replace(/\{(\w+)\}/g, '<span class="path-param">{$1}</span>');

        let html = `<span class="method-badge ${methodClass}">${api.method}</span><br>`;
        html += `<span class="endpoint-url">${endpointHtml}</span>`;

        if (api.note) {
            html += `<div class="api-note">${esc(api.note)}</div>`;
        }

        if (api.pathParams) {
            html += '<div style="margin-top:0.35rem">';
            for (const [k, v] of Object.entries(api.pathParams)) {
                html += `<span class="chain-tag">${esc(k)} = ${esc(String(v))}</span> `;
            }
            html += '</div>';
        }

        td.innerHTML = html;
        return td;
    }

    function buildRequestCell(step) {
        const td = document.createElement('td');
        let html = '';

        if (step.request.headers && Object.keys(step.request.headers).length) {
            html += '<div class="headers-block">';
            for (const [k, v] of Object.entries(step.request.headers)) {
                const isChained = step.request.dataChain &&
                    step.request.dataChain.some(c => c.field === k || c.field === 'Authorization');
                const val = isChained && k === 'Authorization'
                    ? `<span style="color:var(--chain-highlight)">${esc(v)}</span>`
                    : `<span class="header-value">${esc(v)}</span>`;
                html += `<span class="header-name">${esc(k)}:</span> ${val}<br>`;
            }
            html += '</div>';
        }

        if (step.request.body !== null && step.request.body !== undefined) {
            html += '<div class="json-block">' + syntaxHighlight(step.request.body, step.request.dataChain) + '</div>';
        } else {
            html += '<div class="no-body">No request body</div>';
        }

        if (step.request.dataChain && step.request.dataChain.length) {
            html += '<div class="chain-section">';
            html += '<div class="chain-label">Data chained from:</div>';
            step.request.dataChain.forEach(c => {
                html += `<span class="chain-tag">${esc(c.field)}</span> \u2190 <span style="font-size:0.7rem;color:var(--text-muted)">${esc(c.source)}</span><br>`;
            });
            html += '</div>';
        }

        if (step.request.diff) {
            html += '<div class="diff-block">';
            html += '<div class="diff-label">Field changes:</div>';
            for (const [field, change] of Object.entries(step.request.diff)) {
                html += `<div class="diff-line diff-from">- ${esc(field)}: ${esc(String(change.from))}</div>`;
                html += `<div class="diff-line diff-to">+ ${esc(field)}: ${esc(String(change.to))}</div>`;
            }
            html += '</div>';
        }

        td.innerHTML = html;
        return td;
    }

    function buildResponseCell(step) {
        const td = document.createElement('td');
        const sc = step.response.statusCode;
        const scClass = sc >= 200 && sc < 300 ? 's2xx' : 's4xx';
        const scLabel = statusLabel(sc);

        let html = `<div class="status-code ${scClass}">${sc} ${scLabel}</div>`;

        if (step.response.body !== null && step.response.body !== undefined) {
            html += '<div class="json-block">' + syntaxHighlight(step.response.body, null, step.chainsTo) + '</div>';
        } else {
            html += '<div class="no-body">No response body</div>';
        }

        if (step.chainsTo && step.chainsTo.length) {
            html += '<div class="chain-section">';
            html += '<div class="chain-label">Chains forward to:</div>';
            step.chainsTo.forEach(c => {
                const steps = Array.isArray(c.usedInSteps) ? c.usedInSteps.join(', ') : c.usedInSteps;
                html += `<span class="chain-tag">${esc(c.field)}</span> \u2192 <span style="font-size:0.7rem;color:var(--text-muted)">Step ${steps}</span><br>`;
            });
            html += '</div>';
        }

        td.innerHTML = html;
        return td;
    }

    function buildExpectedCell(step) {
        const td = document.createElement('td');
        const ex = step.expected;
        const scClass = ex.statusCode >= 200 && ex.statusCode < 300 ? 'ok' : 'error';

        let html = `<div class="expected-status ${scClass}">${ex.statusCode} ${statusLabel(ex.statusCode)}</div>`;
        html += `<div class="expected-desc">${esc(ex.description)}</div>`;

        if (ex.keyFields && ex.keyFields.length) {
            html += '<div class="expected-fields">';
            ex.keyFields.forEach(f => {
                html += `<span class="field-tag">${esc(f)}</span>`;
            });
            html += '</div>';
        }

        if (ex.validationNotes) {
            html += `<div class="validation-notes">${esc(ex.validationNotes)}</div>`;
        }

        td.innerHTML = html;
        return td;
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    function esc(str) {
        const el = document.createElement('span');
        el.textContent = str;
        return el.innerHTML;
    }

    function statusLabel(code) {
        const labels = {
            200: 'OK', 201: 'Created', 202: 'Accepted', 204: 'No Content',
            400: 'Bad Request', 401: 'Unauthorized', 403: 'Forbidden',
            404: 'Not Found', 409: 'Conflict'
        };
        return labels[code] || '';
    }

    function syntaxHighlight(obj, chainAnnotations, chainsTo) {
        const raw = JSON.stringify(obj, null, 2);

        const chainFields = new Set();
        if (chainAnnotations) chainAnnotations.forEach(c => {
            const f = c.field.replace(/ \(path\)$/, '');
            chainFields.add(f);
        });
        if (chainsTo) chainsTo.forEach(c => {
            const parts = c.field.split('.');
            chainFields.add(parts[parts.length - 1]);
        });

        return raw.replace(
            /("(?:[^"\\]|\\.)*")\s*(:)?|(\b\d+\.?\d*\b)|(\btrue\b|\bfalse\b)|(\bnull\b)/g,
            (match, str, colon, num, bool, nul) => {
                if (str) {
                    if (colon) {
                        const keyName = str.slice(1, -1);
                        const cls = chainFields.has(keyName)
                            ? 'json-key" style="color:var(--chain-highlight);font-weight:600'
                            : 'json-key';
                        return `<span class="${cls}">${esc(str)}</span>:`;
                    }
                    return `<span class="json-str">${esc(str)}</span>`;
                }
                if (num)  return `<span class="json-num">${esc(num)}</span>`;
                if (bool) return `<span class="json-bool">${esc(bool)}</span>`;
                if (nul)  return `<span class="json-null">${esc(nul)}</span>`;
                return match;
            }
        );
    }

    // =====================================================================
    // Boot — load initial journey
    // =====================================================================

    await loadJourney(config);

})();
