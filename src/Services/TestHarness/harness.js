(async function () {

    // =====================================================================
    // Available journey configs
    // =====================================================================

    const CONFIGS = [
        { value: 'bookflow',          label: 'Bookflow (Retail API)' },
        { value: 'oci-1-pax-outbound', label: 'OCI - 1 PAX Outbound' },
        { value: 'payment',           label: 'Payment' },
        { value: 'user',              label: 'User' },
        { value: 'loyalty',           label: 'Loyalty' },
        { value: 'admin',             label: 'Admin' },
        { value: 'operations',        label: 'Operations' },
        { value: 'terminal-customer', label: 'Terminal Customer' }
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

    const ROUTES = [
        { origin: 'LHR', destination: 'JFK' }
    ];

    function pick(arr) { return arr[Math.floor(Math.random() * arr.length)]; }
    function randDigits(n) { return Math.floor(Math.random() * Math.pow(10, n)).toString().padStart(n, '0'); }
    function isoDate(d) { return d.toISOString().split('T')[0]; }

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

        const genders = ['Male', 'Female'];
        const gender  = pick(genders);

        const dobYear  = 1950 + Math.floor(Math.random() * 56);  // 1950–2005
        const dobMonth = (1 + Math.floor(Math.random() * 12)).toString().padStart(2, '0');
        const dobDay   = (1 + Math.floor(Math.random() * 28)).toString().padStart(2, '0');
        const dateOfBirth = `${dobYear}-${dobMonth}-${dobDay}`;

        const phone         = '07' + randDigits(9);
        const loyaltyNumber = 'AX' + randDigits(7);

        const route = pick(ROUTES);
        const today = new Date();
        const outboundOffset = 7 + Math.floor(Math.random() * 28);   // 7–34 days out
        const returnOffset   = outboundOffset + 7 + Math.floor(Math.random() * 8); // +7–14 days
        const outboundDateObj = new Date(today); outboundDateObj.setDate(today.getDate() + outboundOffset);
        const returnDateObj   = new Date(today); returnDateObj.setDate(today.getDate() + returnOffset);

        const outboundOrigin = route.origin;
        const outboundDest   = route.destination;
        const returnOrigin   = route.destination;
        const returnDest     = route.origin;
        const departDate     = isoDate(outboundDateObj);
        const returnDate     = isoDate(returnDateObj);

        runtimeVars = {
            givenName, surname, password, email,
            recipientGivenName, recipientSurname, recipientPassword, recipientEmail,
            gender, dateOfBirth, phone, loyaltyNumber,
            outboundOrigin, outboundDest, returnOrigin, returnDest, departDate, returnDate
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
                .replace(/__RAND_GIVEN_NAME__/g,     runtimeVars.givenName)
                .replace(/__RAND_SURNAME__/g,        runtimeVars.surname)
                .replace(/__RAND_EMAIL__/g,          runtimeVars.email)
                .replace(/__RAND_PASSWORD__/g,       runtimeVars.password)
                .replace(/__RAND_GENDER__/g,            runtimeVars.gender)
                .replace(/__RAND_DOB__/g,             runtimeVars.dateOfBirth)
                .replace(/__RAND_PHONE__/g,           runtimeVars.phone)
                .replace(/__RAND_LOYALTY_NUMBER__/g,  runtimeVars.loyaltyNumber)
                .replace(/__RAND_OUTBOUND_ORIGIN__/g, runtimeVars.outboundOrigin)
                .replace(/__RAND_OUTBOUND_DEST__/g,   runtimeVars.outboundDest)
                .replace(/__RAND_RETURN_ORIGIN__/g,   runtimeVars.returnOrigin)
                .replace(/__RAND_RETURN_DEST__/g,     runtimeVars.returnDest)
                .replace(/__RAND_DEPART_DATE__/g,     runtimeVars.departDate)
                .replace(/__RAND_RETURN_DATE__/g,     runtimeVars.returnDate);
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

    function formatStepLogForCopy(stepNumber) {
        const entry = apiLog.find(e => e.step === stepNumber);
        if (!entry) return 'No log entry for step ' + stepNumber + '.';

        const lines = [];
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
            if (entry.durationMs !== null && entry.durationMs !== undefined) {
                lines.push('  Time:   ' + entry.durationMs + ' ms');
            }
            lines.push('');
            if (entry.responseBody !== null && entry.responseBody !== undefined) {
                lines.push('  Body:');
                lines.push(indent(formatBody(entry.responseBody), '    '));
            } else {
                lines.push('  Body: (none)');
            }
        }
        return lines.join('\n');
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
                if (entry.durationMs !== null && entry.durationMs !== undefined) {
                    lines.push('  Time:   ' + entry.durationMs + ' ms');
                }
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
    let nextStepCursor = 0;
    let nextCurrentSteps = null;

    const tbody = document.getElementById('journeyBody');
    const baseUrlListEl = document.getElementById('baseUrlList');
    const btnRunAll = document.getElementById('btnRunAll');
    const btnNextStep = document.getElementById('btnNextStep');

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
        nextStepCursor = 0;
        nextCurrentSteps = null;
        btnNextStep.disabled = false;
        btnNextStep.textContent = '\u23ED Next';
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
            tdStep.innerHTML = `<div class="step-number"><span class="step-label">Step </span>${step.step}</div>`;

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

            const tdTime = document.createElement('td');
            tdTime.style.cssText = 'font-family:var(--font-mono);font-size:0.78rem;color:var(--text-muted);white-space:nowrap';

            row.appendChild(tdStep);
            row.appendChild(tdName);
            row.appendChild(tdApi);
            row.appendChild(tdExpected);
            row.appendChild(tdTime);

            const ref = { row, tdTime, step, idx };
            rowRefs.push(ref);

            row.addEventListener('click', () => openStepModal(ref.currentStep || ref.step, ref.idx));
            tbody.appendChild(row);
        });
    }

    // =====================================================================
    // Modal
    // =====================================================================

    const modalOverlay = document.getElementById('stepModal');
    const btnCopyStepLog = document.getElementById('btnCopyStepLog');
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
        if (step.response) {
            const respTd = buildResponseCell(step);
            if (step.response.body !== null && step.response.body !== undefined) {
                html += '<div style="margin-top:0.6rem">' + respTd.querySelector('.json-block').outerHTML + '</div>';
            }
            if (step.chainsTo && step.chainsTo.length) {
                html += respTd.querySelector('.chain-section') ? respTd.querySelector('.chain-section').outerHTML : '';
            }
        }
        html += '</div>';

        const result = liveResults[idx];
        if (result) {
            const assertionsPass = !result.assertionResults || result.assertionResults.every(r => r.pass);
            const overallPass = result.statusMatch && assertionsPass;
            const scClass = result.statusMatch ? 'pass' : 'fail';
            const scLabel = statusLabel(result.liveStatus) || (result.liveError ? 'Network Error' : '');
            const resBadge = overallPass
                ? '<span class="result-badge pass">Pass</span>'
                : '<span class="result-badge fail">Fail</span>';

            html += '<div class="modal-section"><div class="modal-section-title">Live Result</div>';
            const durationLabel = result.durationMs !== null && result.durationMs !== undefined
                ? `<span style="font-family:var(--font-mono);font-size:0.72rem;color:var(--text-muted);margin-left:0.6rem">${result.durationMs} ms</span>`
                : '';
            html += `<div class="live-result-label">Live Response ${resBadge}${durationLabel}</div>`;
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
            if (result.assertionResults && result.assertionResults.length) {
                html += '<div class="assertion-results">';
                result.assertionResults.forEach(a => {
                    const cls = a.pass ? 'pass' : 'fail';
                    const icon = a.pass ? '✓' : '✗';
                    const detail = a.pass ? '' : ` (expected ${a.expected}, got ${a.actual})`;
                    html += `<div class="assertion-row ${cls}">${icon} ${esc(a.description)}${esc(detail)}</div>`;
                });
                html += '</div>';
            }
            html += '</div>';
        }

        modalBody.innerHTML = html;

        const hasLog = apiLog.some(e => e.step === step.step);
        btnCopyStepLog.style.display = hasLog ? '' : 'none';
        btnCopyStepLog.textContent = '\uD83D\uDCCB Copy Step Log';
        btnCopyStepLog.classList.remove('copied');
        btnCopyStepLog.onclick = async () => {
            const text = formatStepLogForCopy(step.step);
            try {
                await navigator.clipboard.writeText(text);
            } catch {
                const ta = document.createElement('textarea');
                ta.value = text;
                ta.style.position = 'fixed';
                ta.style.opacity = '0';
                document.body.appendChild(ta);
                ta.select();
                document.execCommand('copy');
                document.body.removeChild(ta);
            }
            btnCopyStepLog.textContent = '\u2713 Copied';
            btnCopyStepLog.classList.add('copied');
            setTimeout(() => {
                btnCopyStepLog.textContent = '\uD83D\uDCCB Copy Step Log';
                btnCopyStepLog.classList.remove('copied');
            }, 2000);
        };

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
            ['route',     runtimeVars.outboundOrigin + ' \u2192 ' + runtimeVars.outboundDest + ' / ' + runtimeVars.returnDate],
            ['departs',   runtimeVars.departDate],
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
    // Next Step button
    // =====================================================================

    btnNextStep.addEventListener('click', runNextStep);

    async function runNextStep() {
        // First click — initialise the run without executing anything yet
        if (nextCurrentSteps === null) {
            apiLog = [];
            btnCopyLogs.disabled = true;
            configSelect.disabled = true;
            btnRunAll.disabled = true;

            if (hasRuntimeVars) {
                generateRuntimeVars();
                nextCurrentSteps = buildStepsWithVars();
                updateRuntimeBanner();
            } else {
                nextCurrentSteps = JSON.parse(JSON.stringify(raw.steps));
            }

            buildTableRows(nextCurrentSteps);
            liveChain = {};
            liveResults = {};
            nextStepCursor = 0;
        }

        if (nextStepCursor >= liveStepIndices.length) return;

        btnNextStep.disabled = true;
        btnNextStep.textContent = '\u23F3 Running\u2026';

        const stepIdx = liveStepIndices[nextStepCursor];
        nextStepCursor++;

        await runStep(rowRefs[stepIdx], nextCurrentSteps);

        if (nextStepCursor >= liveStepIndices.length) {
            // All steps done
            btnNextStep.disabled = true;
            btnNextStep.textContent = '\u23ED Next';
            btnRunAll.disabled = false;
            btnCopyLogs.disabled = false;
            configSelect.disabled = false;
            nextCurrentSteps = null;
            nextStepCursor = 0;
        } else {
            btnNextStep.disabled = false;
            btnNextStep.textContent = '\u23ED Next';
        }
    }

    // =====================================================================
    // Live API invocation
    // =====================================================================

    async function runLiveSteps() {
        btnRunAll.disabled = true;
        btnRunAll.textContent = '\u23F3 Running\u2026';
        btnCopyLogs.disabled = true;
        configSelect.disabled = true;
        btnNextStep.disabled = true;
        nextStepCursor = 0;
        nextCurrentSteps = null;

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
        btnNextStep.disabled = false;
        btnNextStep.textContent = '\u23ED Next';
    }

    // Walk a dot-separated path where segments ending with [*] expand arrays.
    // e.g. 'flights[*].cabins[*].fareFamilies[*].offer.offerId' collects all offerIds.
    function collectAllValues(obj, pathParts) {
        if (!pathParts.length || obj == null) return [];
        const [head, ...tail] = pathParts;
        const isWildcard = head.endsWith('[*]');
        const key = isWildcard ? head.slice(0, -3) : head;
        const val = key ? obj[key] : obj;
        if (val === undefined || val === null) return [];
        if (isWildcard) {
            if (!Array.isArray(val)) return [];
            if (!tail.length) return val;
            return val.flatMap(item => collectAllValues(item, tail));
        }
        if (!tail.length) return [val];
        return collectAllValues(val, tail);
    }

    // Navigate a dot-separated path (supports key[N] and standalone [N] segments).
    function getPath(obj, path) {
        const parts = path.split('.');
        let cur = obj;
        for (const part of parts) {
            if (cur == null) return undefined;
            const solo = part.match(/^\[(\d+)\]$/);
            const embedded = part.match(/^(\w+)\[(\d+)\]$/);
            if (solo)     cur = Array.isArray(cur) ? cur[parseInt(solo[1])] : undefined;
            else if (embedded) cur = cur[embedded[1]]?.[parseInt(embedded[2])];
            else          cur = cur[part];
        }
        return cur;
    }

    // Set a value at a dot-separated path (supports key[N] and standalone [N] segments).
    function setPath(obj, path, value) {
        const parts = path.split('.');
        let cur = obj;
        for (let i = 0; i < parts.length - 1; i++) {
            const solo = parts[i].match(/^\[(\d+)\]$/);
            const embedded = parts[i].match(/^(\w+)\[(\d+)\]$/);
            if (solo)     cur = Array.isArray(cur) ? cur[parseInt(solo[1])] : null;
            else if (embedded) cur = cur[embedded[1]]?.[parseInt(embedded[2])];
            else          cur = cur[parts[i]];
            if (cur == null) return;
        }
        const last = parts[parts.length - 1];
        const solo = last.match(/^\[(\d+)\]$/);
        const embedded = last.match(/^(\w+)\[(\d+)\]$/);
        if (solo && Array.isArray(cur)) cur[parseInt(solo[1])] = value;
        else if (embedded) { if (cur[embedded[1]]) cur[embedded[1]][parseInt(embedded[2])] = value; }
        else cur[last] = value;
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
                let chainedValue = liveChain[k];
                // If not found directly, resolve "from-step-N" references by looking up
                // the source step's chainsTo entry that matches on field name
                if (chainedValue === undefined && typeof v === 'string') {
                    const fromStepMatch = v.match(/^from-step-(\d+)$/);
                    if (fromStepMatch) {
                        const srcStepNum = parseInt(fromStepMatch[1]);
                        const srcStep = currentSteps.find(s => s.step === srcStepNum);
                        if (srcStep && srcStep.chainsTo) {
                            const matchingChain = srcStep.chainsTo.find(c =>
                                c.field === k || (typeof c.as === 'string' && c.as === k)
                            );
                            if (matchingChain) {
                                chainedValue = liveChain[matchingChain.as || matchingChain.field];
                            }
                        }
                    }
                }
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

        // Substitute chained values into request body fields (supports dot/index paths)
        if (step.request.dataChain && requestBody) {
            step.request.dataChain.forEach(chain => {
                const fieldPath = chain.field.replace(/ \(path\)$/, '');
                const chainKey  = chain.from || fieldPath;
                if (liveChain[chainKey] === undefined) return;
                if (fieldPath.includes('.') || fieldPath.includes('[')) {
                    setPath(requestBody, fieldPath, liveChain[chainKey]);
                } else if (fieldPath in requestBody) {
                    requestBody[fieldPath] = liveChain[chainKey];
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

        let liveStatus, liveBody, liveError, durationMs;

        try {
            const t0 = performance.now();
            const response = await fetch(url, fetchOpts);
            liveStatus = response.status;

            const contentType = response.headers.get('content-type') || '';
            if (contentType.includes('application/json')) {
                liveBody = await response.json();
            } else {
                const text = await response.text();
                liveBody = text.length > 0 ? text : null;
            }
            durationMs = Math.round(performance.now() - t0);
        } catch (err) {
            liveError = err.message;
            liveStatus = 0;
            liveBody = null;
            durationMs = null;
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
            error: liveError || null,
            durationMs: durationMs
        });

        // Store chained values from response
        if (step.chainsTo && liveBody && typeof liveBody === 'object') {
            step.chainsTo.forEach(chain => {
                if (chain.randomArrayPath) {
                    // Collect all values at the wildcard path and pick one at random
                    const parts = chain.randomArrayPath.split('.');
                    const all = collectAllValues(liveBody, parts);
                    if (all.length > 0) {
                        liveChain[chain.as] = all[Math.floor(Math.random() * all.length)];
                    }
                } else if (chain.randomAvailableSeatFrom) {
                    // Collect all available seats from cabins and pick one at random,
                    // mapping each seat field to a named chain var via chain.as object
                    const cabins = liveBody[chain.randomAvailableSeatFrom];
                    if (!Array.isArray(cabins)) return;
                    const available = cabins.flatMap(c => c.seats || []).filter(s => s.availability === 'available');
                    if (!available.length) return;
                    const seat = available[Math.floor(Math.random() * available.length)];
                    for (const [srcField, alias] of Object.entries(chain.as)) {
                        liveChain[alias] = seat[srcField];
                    }
                } else if (chain.path) {
                    // Navigate a nested path to extract a single value
                    const val = getPath(liveBody, chain.path);
                    if (val !== undefined) liveChain[chain.as] = val;
                } else {
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
                }
            });
        }

        // Resolve path parameter chains for subsequent steps
        resolvePathParamChains(step, currentSteps);

        // Determine pass/fail
        const statusMatch = liveStatus === step.expected.statusCode;
        const assertionResults = evaluateAssertions(step.expected.assertions, liveBody);
        const passed = statusMatch && assertionResults.every(r => r.pass);
        row.classList.remove('step-positive', 'step-negative', 'result-pass', 'result-fail');
        row.classList.add(passed ? 'result-pass' : 'result-fail');

        // Update timing cell
        if (ref.tdTime) {
            ref.tdTime.textContent = durationMs !== null ? durationMs + ' ms' : '—';
        }

        liveResults[idx] = { liveStatus, liveBody, liveError, statusMatch, assertionResults, url, durationMs };
    }

    function evaluateAssertions(assertions, body) {
        if (!assertions || !assertions.length) return [];
        return assertions.map(a => {
            const value = (a.field || '').split('.').reduce((o, k) => (o != null ? o[k] : undefined), body);
            if (a.assertion === 'count') {
                const actual = Array.isArray(value) ? value.length : null;
                const pass = actual === a.expected;
                return { pass, description: a.description, expected: a.expected, actual };
            }
            return { pass: false, description: a.description, expected: a.expected, actual: undefined };
        });
    }

    function resolvePathParamChains(completedStep, allSteps) {
        if (!completedStep.chainsTo || !completedStep.chainsTo.length) return;

        completedStep.chainsTo.forEach(chain => {
            const alias = chain.as || chain.field;
            // randomAvailableSeatFrom uses an object for 'as' — skip, nothing to resolve here
            if (typeof alias !== 'string') return;
            const value = liveChain[alias];
            if (value === undefined) return;

            const fromRef = 'from-step-' + completedStep.step;
            allSteps.forEach(s => {
                if (s.apiCall.pathParams) {
                    for (const [paramName, paramVal] of Object.entries(s.apiCall.pathParams)) {
                        // Only update the param whose name matches this chain's alias,
                        // preventing one chain from overwriting unrelated params.
                        if (paramVal === fromRef && paramName === alias) {
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
            if (!c.field) return;
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
