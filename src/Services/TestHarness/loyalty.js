(async function () {

    // =====================================================================
    // Name pools for random test data generation
    // =====================================================================

    const FIRST_NAMES = [
        'Amara', 'James', 'Priya', 'Liam', 'Fatima', 'Oliver', 'Sophie',
        'Mohammed', 'Emily', 'Carlos', 'Aisha', 'Daniel', 'Charlotte',
        'Ravi', 'Emma', 'Noah', 'Mia', 'David', 'Yuki', 'Thomas'
    ];
    const SURNAMES = [
        'Okafor', 'Smith', 'Patel', 'Johnson', 'Ahmed', 'Garcia', 'Williams',
        'Taylor', 'Kumar', 'Martinez', 'Anderson', 'Robinson', 'Harris',
        'Lee', 'Wilson', 'Clark', 'Lewis', 'Walker', 'Hall', 'Brown'
    ];

    function pick(arr) { return arr[Math.floor(Math.random() * arr.length)]; }
    function randDigits(n) { return Math.floor(Math.random() * Math.pow(10, n)).toString().padStart(n, '0'); }

    // =====================================================================
    // Runtime vars — regenerated fresh on every run
    // =====================================================================

    let runtimeVars = {};

    function generateRuntimeVars() {
        const givenName = pick(FIRST_NAMES);
        const surname   = pick(SURNAMES);
        const password  = 'Apex@ir2026!';
        const email     = givenName.toLowerCase() + '.' +
                          surname.toLowerCase() + '.' +
                          randDigits(6) + '@testmail.example.com';
        runtimeVars = { givenName, surname, password, email };
    }

    function applyRuntimeVars(obj) {
        if (obj === null || obj === undefined) return obj;
        if (typeof obj === 'string') {
            return obj
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
    // Load journey definition
    // =====================================================================

    const res = await fetch('loyalty-journey.json');
    const raw = await res.json();

    // =====================================================================
    // Base URL inputs & health checks — driven from journey.baseUrls
    // =====================================================================

    const baseUrlListEl = document.getElementById('baseUrlList');

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

    renderBaseUrlList();
    runAllHealthChecks();

    let healthDebounce;
    baseUrlListEl.addEventListener('input', () => {
        clearTimeout(healthDebounce);
        healthDebounce = setTimeout(runAllHealthChecks, 600);
    });

    // =====================================================================
    // Table setup
    // =====================================================================

    const liveStepIndices = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10]; // Steps 1–11
    let liveChain = {};
    let liveResults = {};
    let rowRefs = [];

    const tbody = document.getElementById('journeyBody');

    function buildTableRows(steps) {
        tbody.innerHTML = '';
        rowRefs = [];

        steps.forEach((step, idx) => {
            const row = document.createElement('tr');

            // Step cell — number only
            const tdStep = document.createElement('td');
            tdStep.innerHTML = `<div class="step-number">Step ${step.step}</div>`;

            // Name cell
            const tdName = document.createElement('td');
            tdName.innerHTML = `<div class="step-name">${esc(step.name)}</div>`;

            // API call cell — method badge + endpoint only
            const tdApi = document.createElement('td');
            const api = step.apiCall;
            let endpointHtml = esc(api.endpoint).replace(/\{(\w+)\}/g, '<span class="path-param">{$1}</span>');
            tdApi.innerHTML = `<span class="method-badge method-${api.method}">${api.method}</span> <span class="endpoint-url">${endpointHtml}</span>`;

            // Expected status cell
            const tdExpected = document.createElement('td');
            const ex = step.expected;
            const scClass = ex.statusCode >= 200 && ex.statusCode < 300 ? 'ok' : 'error';
            tdExpected.innerHTML = `<div class="expected-status ${scClass}">${ex.statusCode} ${statusLabel(ex.statusCode)}</div>`;

            // Result cell — filled after live run
            const tdResult = document.createElement('td');
            tdResult.className = 'result-cell';

            row.appendChild(tdStep);
            row.appendChild(tdName);
            row.appendChild(tdApi);
            row.appendChild(tdExpected);
            row.appendChild(tdResult);

            const ref = { row, step, resultCell: tdResult, idx };
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
        modalTitle.innerHTML = `<span class="step-number" style="display:inline;font-size:1rem">Step ${step.step}</span> — ${esc(step.name)}${livePill}`;

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
            if (result.url) html += `<div class="live-url">→ ${esc(result.url)}</div>`;
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

    // Initial render with first set of runtime vars
    generateRuntimeVars();
    const initialSteps = buildStepsWithVars();
    buildTableRows(initialSteps);
    updateRuntimeBanner();

    // =====================================================================
    // Run All button
    // =====================================================================

    const btnRunAll = document.getElementById('btnRunAll');
    btnRunAll.disabled = false;
    btnRunAll.addEventListener('click', runLiveSteps);

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
    // Live API invocation
    // =====================================================================

    async function runLiveSteps() {
        btnRunAll.disabled = true;
        btnRunAll.textContent = '⏳ Running…';

        // Fresh test data for this run
        generateRuntimeVars();
        const currentSteps = buildStepsWithVars();

        // Rebuild table with new runtime vars
        buildTableRows(currentSteps);
        updateRuntimeBanner();

        // Reset chain and results
        liveChain = {};
        liveResults = {};

        for (const stepIdx of liveStepIndices) {
            await runStep(rowRefs[stepIdx], currentSteps);
        }

        btnRunAll.disabled = false;
        btnRunAll.textContent = '▶ Run';
    }

    async function runStep(ref, currentSteps) {
        const { resultCell, row, idx } = ref;
        const step = currentSteps[idx];
        ref.currentStep = step;

        const stepBaseUrlRef = step.apiCall.baseUrlRef || 'loyalty';
        const baseUrl = getBaseUrl(stepBaseUrlRef) || getBaseUrl('loyalty');
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

        // Deep-copy the body so we don't mutate the step definition
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

        // Store chained values from response
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

        // Determine pass/fail
        const statusMatch = liveStatus === step.expected.statusCode;
        row.classList.remove('step-positive', 'step-negative', 'result-pass', 'result-fail');
        row.classList.add(statusMatch ? 'result-pass' : 'result-fail');

        // Store result for modal display
        liveResults[idx] = { liveStatus, liveBody, liveError, statusMatch, url };

        // Update slim result cell with badge
        resultCell.innerHTML = statusMatch
            ? '<span class="result-badge pass">Pass</span>'
            : '<span class="result-badge fail">Fail</span>';
    }

    function appendLiveResult(cell, step, liveStatus, liveBody, liveError, statusMatch, actualUrl) {
        const div = document.createElement('div');
        div.className = 'live-result';

        const scClass = statusMatch ? 'pass' : 'fail';
        const scLabel = statusLabel(liveStatus) || (liveError ? 'Network Error' : '');
        const badge = statusMatch
            ? '<span class="result-badge pass">Pass</span>'
            : '<span class="result-badge fail">Fail</span>';

        let html = '<div class="live-result-label">Live Response ' + badge + '</div>';

        if (actualUrl) {
            html += `<div class="live-url">→ ${esc(actualUrl)}</div>`;
        }

        if (liveError) {
            html += `<div class="status-code fail">Error: ${esc(liveError)}</div>`;
        } else {
            html += `<div class="status-code ${scClass}">${liveStatus} ${esc(scLabel)}</div>`;
        }

        if (liveBody !== null && liveBody !== undefined) {
            if (typeof liveBody === 'object') {
                html += '<div class="json-block">' + syntaxHighlight(liveBody, null, step.chainsTo) + '</div>';
            } else {
                html += '<div class="json-block">' + esc(String(liveBody)) + '</div>';
            }
        } else if (!liveError) {
            html += '<div class="no-body">No response body</div>';
        }

        div.innerHTML = html;
        cell.appendChild(div);
    }

    // =====================================================================
    // Cell builders
    // =====================================================================

    function buildStepCell(step, idx) {
        const td = document.createElement('td');
        const isLive = liveStepIndices.includes(idx);
        const liveIndicator = isLive
            ? '<div style="margin-top:0.4rem"><span style="font-size:0.65rem;font-family:var(--font-mono);color:var(--accent);border:1px solid var(--accent);border-radius:3px;padding:0.1rem 0.4rem">LIVE</span></div>'
            : '';
        td.innerHTML = `
            <div class="step-number">Step ${step.step}</div>
            <div class="step-name">${esc(step.name)}</div>
            <div class="step-desc">${esc(step.description)}</div>
            ${liveIndicator}
        `;
        return td;
    }

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
                    ? highlightChainValue(v)
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
                html += `<span class="chain-tag">${esc(c.field)}</span> ← <span style="font-size:0.7rem;color:var(--text-muted)">${esc(c.source)}</span><br>`;
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
                html += `<span class="chain-tag">${esc(c.field)}</span> → <span style="font-size:0.7rem;color:var(--text-muted)">Step ${steps}</span><br>`;
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

    function highlightChainValue(val) {
        return `<span style="color:var(--chain-highlight)">${esc(val)}</span>`;
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

})();
