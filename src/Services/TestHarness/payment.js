(async function () {

    // =====================================================================
    // Load journey definition
    // =====================================================================

    const res = await fetch('payment-journey.json');
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

    // All steps are live (steps 1–18)
    const liveStepIndices = raw.steps.map((_, i) => i);
    let liveChain = {};
    let liveResults = {};
    let rowRefs = [];

    const tbody = document.getElementById('journeyBody');

    function buildTableRows(steps) {
        tbody.innerHTML = '';
        rowRefs = [];

        steps.forEach((step, idx) => {
            const row = document.createElement('tr');

            // Step cell
            const tdStep = document.createElement('td');
            tdStep.innerHTML = `<div class="step-number">Step ${step.step}</div>`;

            // Name cell
            const tdName = document.createElement('td');
            tdName.innerHTML = `<div class="step-name">${esc(step.name)}</div>`;

            // API call cell
            const tdApi = document.createElement('td');
            const api = step.apiCall;
            let endpointHtml = esc(api.endpoint).replace(/\{(\w+)\}/g, '<span class="path-param">{$1}</span>');
            tdApi.innerHTML = `<span class="method-badge method-${api.method}">${api.method}</span> <span class="endpoint-url">${endpointHtml}</span>`;

            // Expected status cell
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
    // Initial render
    // =====================================================================

    const initialSteps = JSON.parse(JSON.stringify(raw.steps));
    buildTableRows(initialSteps);

    // =====================================================================
    // Run All button
    // =====================================================================

    const btnRunAll = document.getElementById('btnRunAll');
    btnRunAll.disabled = false;
    btnRunAll.addEventListener('click', runLiveSteps);

    // =====================================================================
    // Live API invocation
    // =====================================================================

    async function runLiveSteps() {
        btnRunAll.disabled = true;
        btnRunAll.textContent = '\u23F3 Running\u2026';

        const currentSteps = JSON.parse(JSON.stringify(raw.steps));

        // Rebuild table
        buildTableRows(currentSteps);

        // Reset chain and results
        liveChain = {};
        liveResults = {};

        for (const stepIdx of liveStepIndices) {
            await runStep(rowRefs[stepIdx], currentSteps);
        }

        btnRunAll.disabled = false;
        btnRunAll.textContent = '\u25B6 Run';
    }

    async function runStep(ref, currentSteps) {
        const { row, idx } = ref;
        const step = currentSteps[idx];
        ref.currentStep = step;

        const stepBaseUrlRef = step.apiCall.baseUrlRef || 'payment';
        const baseUrl = getBaseUrl(stepBaseUrlRef) || getBaseUrl('payment');
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
                if (liveBody[field] !== undefined) {
                    liveChain[chain.as || field] = liveBody[field];
                }
            });
        }

        // Map chained aliases to the pathParam name used in subsequent steps
        // e.g. farePaymentId → paymentId, seatPaymentId → paymentId
        // Subsequent steps use pathParams: { "paymentId": "from-step-X" }
        // We need to resolve which alias maps to which step
        resolvePathParamChains(step, currentSteps);

        // Determine pass/fail
        const statusMatch = liveStatus === step.expected.statusCode;
        row.classList.remove('step-positive', 'step-negative', 'result-pass', 'result-fail');
        row.classList.add(statusMatch ? 'result-pass' : 'result-fail');

        // Store result for modal display
        liveResults[idx] = { liveStatus, liveBody, liveError, statusMatch, url };
    }

    // Resolve path parameter chains for subsequent steps
    // The journey JSON uses "from-step-X" convention in pathParams values
    // and chainsTo with aliases like farePaymentId, seatPaymentId, refundPaymentId
    function resolvePathParamChains(completedStep, allSteps) {
        if (!completedStep.chainsTo || !completedStep.chainsTo.length) return;

        completedStep.chainsTo.forEach(chain => {
            const alias = chain.as || chain.field;
            const value = liveChain[alias];
            if (value === undefined) return;

            // Find steps that reference "from-step-{completedStep.step}"
            const fromRef = 'from-step-' + completedStep.step;
            allSteps.forEach(s => {
                if (s.apiCall.pathParams) {
                    for (const [paramName, paramVal] of Object.entries(s.apiCall.pathParams)) {
                        if (paramVal === fromRef) {
                            // Set the chained value so path substitution works
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
                html += `<span class="header-name">${esc(k)}:</span> <span class="header-value">${esc(v)}</span><br>`;
            }
            html += '</div>';
        }

        if (step.request.body !== null && step.request.body !== undefined) {
            html += '<div class="json-block">' + syntaxHighlight(step.request.body, null) + '</div>';
        } else {
            html += '<div class="no-body">No request body</div>';
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

})();
