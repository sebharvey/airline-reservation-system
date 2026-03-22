let servicesData = null;
let healthResults = {};
let selectedId = 'retail-api';
let variablesOpen = false;

// ── Utility ──
function escapeHtml(str) {
  return (str || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

function escapeText(str) {
  return (str || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

// ── Variables ──
function loadVariables() {
  try { return JSON.parse(localStorage.getItem('apiVariables_' + selectedId) || '[]'); } catch (e) { return []; }
}

function saveVariables(vars) {
  localStorage.setItem('apiVariables_' + selectedId, JSON.stringify(vars));
}

function applyVariables(path) {
  const vars = loadVariables();
  return path.replace(/\{([^}]+)\}/g, (match, name) => {
    const v = vars.find(v => v.name === name);
    return v && v.value ? v.value : match;
  });
}

function toggleVariablesCard() {
  variablesOpen = !variablesOpen;
  const body = document.getElementById('variablesBody');
  const chevron = document.getElementById('variablesChevron');
  if (body) body.style.display = variablesOpen ? 'block' : 'none';
  if (chevron) chevron.style.transform = variablesOpen ? 'rotate(180deg)' : 'rotate(0deg)';
}

function renderVariablesCard() {
  const vars = loadVariables();
  return `<div class="variables-card">
    <div class="variables-header" onclick="toggleVariablesCard()">
      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M20.59 13.41l-7.17 7.17a2 2 0 0 1-2.83 0L2 12V2h10l8.59 8.59a2 2 0 0 1 0 2.82z"/><line x1="7" y1="7" x2="7.01" y2="7"/></svg>
      <span class="variables-title">Variables</span>
      <span class="variables-count" id="variablesCount">${vars.length}</span>
      <span class="variables-hint">Define values for <code>{placeholders}</code> in URL paths</span>
      <svg class="variables-chevron" id="variablesChevron" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="transition:transform 0.2s;transform:${variablesOpen ? 'rotate(180deg)' : 'rotate(0deg)'}"><polyline points="6 9 12 15 18 9"/></svg>
    </div>
    <div class="variables-body" id="variablesBody" style="display:${variablesOpen ? 'block' : 'none'}">
      <div class="variables-list" id="variablesList">${renderVariableRows(vars)}</div>
      <button class="add-variable-btn" onclick="addVariable()">
        <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
        Add Variable
      </button>
    </div>
  </div>`;
}

function renderVariableRows(vars) {
  if (vars.length === 0) {
    return `<div class="variables-empty">No variables defined yet. Add a variable to substitute URL placeholders like <code>{loyaltyNumber}</code> automatically when invoking operations.</div>`;
  }
  return vars.map((v, i) => `
    <div class="variable-row">
      <span class="variable-brace">{</span>
      <input class="variable-name-input" type="text" placeholder="name" value="${escapeHtml(v.name)}" oninput="updateVariableName(${i}, this.value)" spellcheck="false" />
      <span class="variable-brace">}</span>
      <span class="variable-equals">=</span>
      <input class="variable-value-input" type="text" placeholder="value" value="${escapeHtml(v.value)}" oninput="updateVariableValue(${i}, this.value)" spellcheck="false" />
      <button class="variable-remove-btn" onclick="removeVariable(${i})" title="Remove variable">&times;</button>
    </div>`).join('');
}

function addVariable() {
  const vars = loadVariables();
  vars.push({ name: '', value: '' });
  saveVariables(vars);
  const list = document.getElementById('variablesList');
  if (list) list.innerHTML = renderVariableRows(vars);
  const count = document.getElementById('variablesCount');
  if (count) count.textContent = vars.length;
}

function removeVariable(index) {
  const vars = loadVariables();
  vars.splice(index, 1);
  saveVariables(vars);
  const list = document.getElementById('variablesList');
  if (list) list.innerHTML = renderVariableRows(vars);
  const count = document.getElementById('variablesCount');
  if (count) count.textContent = vars.length;
}

function updateVariableName(index, value) {
  const vars = loadVariables();
  if (vars[index]) { vars[index].name = value; saveVariables(vars); }
}

function updateVariableValue(index, value) {
  const vars = loadVariables();
  if (vars[index]) { vars[index].value = value; saveVariables(vars); }
}

// ── Logs ──
function loadLogs() {
  try { return JSON.parse(localStorage.getItem('apiLogs') || '[]'); } catch (e) { return []; }
}

function saveLogs(logs) {
  localStorage.setItem('apiLogs', JSON.stringify(logs));
}

function addLog(entry) {
  const logs = loadLogs();
  logs.unshift(entry);
  saveLogs(logs);
  updateLogsCountBadge();
}

function updateLogsCountBadge() {
  const badge = document.getElementById('logsCountBadge');
  if (!badge) return;
  const count = loadLogs().length;
  badge.textContent = count;
  badge.style.display = count > 0 ? 'inline-flex' : 'none';
}

function openLogsModal() {
  renderLogsModal();
  document.getElementById('logsOverlay').style.display = 'flex';
}

function closeLogsModal() {
  document.getElementById('logsOverlay').style.display = 'none';
}

function onLogsOverlayClick(event) {
  if (event.target === document.getElementById('logsOverlay')) closeLogsModal();
}

function clearLogs() {
  if (!confirm('Clear all logs? This cannot be undone.')) return;
  saveLogs([]);
  renderLogsModal();
  updateLogsCountBadge();
}

function formatHeaders(headers) {
  if (!headers || typeof headers !== 'object') return '(none)';
  const entries = Object.entries(headers);
  if (entries.length === 0) return '(none)';
  return entries.map(([k, v]) => `${k}: ${v}`).join('\n');
}

function logToText(log) {
  const date = new Date(log.timestamp);
  const statusText = log.responseStatus ? `${log.responseStatus} ${log.responseStatusText || ''}`.trim() : 'ERROR';
  const lines = [
    `[${date.toLocaleString()}] ${log.method} ${log.url} — ${statusText}`,
    `Service: ${log.serviceName || ''} › ${log.operationName || ''}`,
    `--- Request Headers ---`,
    formatHeaders(log.requestHeaders)
  ];
  if (log.requestBody) { lines.push(`--- Request Body ---`); lines.push(log.requestBody); }
  lines.push(`--- Response Headers ---`);
  lines.push(formatHeaders(log.responseHeaders));
  if (log.responseBody) { lines.push(`--- Response Body ---`); lines.push(log.responseBody); }
  if (log.error) { lines.push(`--- Error ---`); lines.push(log.error); }
  return lines.join('\n');
}

function copyLogsToClipboard() {
  const logs = loadLogs();
  if (logs.length === 0) return;
  const text = logs.map(logToText).join('\n\n' + '='.repeat(60) + '\n\n');
  navigator.clipboard.writeText(text).then(() => {
    const btn = document.getElementById('copyLogsBtn');
    if (!btn) return;
    const original = btn.innerHTML;
    btn.innerHTML = `<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"/></svg> Copied!`;
    setTimeout(() => { btn.innerHTML = original; }, 2000);
  });
}

function renderLogsModal() {
  const logs = loadLogs();
  const container = document.getElementById('logsContent');
  if (!container) return;
  if (logs.length === 0) {
    container.innerHTML = `<div class="logs-empty">No API calls logged yet. Invoke an operation to see logs appear here.</div>`;
    return;
  }
  const copyBtn = `<div class="logs-toolbar">
    <button class="logs-copy-btn" id="copyLogsBtn" onclick="copyLogsToClipboard()">
      <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><rect x="9" y="9" width="13" height="13" rx="2" ry="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>
      Copy all to clipboard
    </button>
  </div>`;
  const entries = logs.map((log, i) => {
    const date = new Date(log.timestamp);
    let statusClass = 'status-err';
    if (log.responseStatus) {
      if (log.responseStatus >= 200 && log.responseStatus < 300) statusClass = 'status-2xx';
      else if (log.responseStatus >= 300 && log.responseStatus < 400) statusClass = 'status-3xx';
      else if (log.responseStatus >= 400 && log.responseStatus < 500) statusClass = 'status-4xx';
      else statusClass = 'status-5xx';
    }
    const statusText = log.responseStatus ? `${log.responseStatus} ${log.responseStatusText || ''}`.trim() : 'ERROR';
    let displayPath = log.url;
    try { displayPath = new URL(log.url).pathname; } catch (e) { /* keep full url if unparseable */ }
    return `<div class="log-entry" id="log-entry-${i}">
      <div class="log-summary" onclick="toggleLogEntry(${i})">
        <span class="method-badge method-${escapeHtml(log.method)}">${escapeHtml(log.method)}</span>
        <span class="log-url">${escapeHtml(displayPath)}</span>
        <span class="status-badge ${statusClass}" style="display:inline-block;flex-shrink:0">${statusText}</span>
        <span class="log-timestamp">${date.toLocaleString()}</span>
        <svg class="log-chevron" id="log-chevron-${i}" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="flex-shrink:0;transition:transform 0.2s"><polyline points="6 9 12 15 18 9"/></svg>
      </div>
      <div class="log-detail" id="log-detail-${i}" style="display:none">
        <div class="log-meta">${escapeHtml(log.serviceName || '')} &rsaquo; ${escapeHtml(log.operationName || '')}</div>
        <div class="log-section-label">Full URL</div>
        <pre class="log-pre">${escapeText(log.url)}</pre>
        <div class="log-section-label">Request Headers</div>
        <pre class="log-pre">${escapeText(formatHeaders(log.requestHeaders))}</pre>
        ${log.requestBody ? `<div class="log-section-label">Request Body</div><pre class="log-pre">${escapeText(log.requestBody)}</pre>` : ''}
        <div class="log-section-label">Response Headers</div>
        <pre class="log-pre">${escapeText(formatHeaders(log.responseHeaders))}</pre>
        ${log.responseBody ? `<div class="log-section-label">Response Body</div><pre class="log-pre">${escapeText(log.responseBody)}</pre>` : ''}
        ${log.error ? `<div class="log-section-label">Error</div><pre class="log-pre log-error">${escapeText(log.error)}</pre>` : ''}
      </div>
    </div>`;
  }).join('');
  container.innerHTML = copyBtn + entries;
}

function toggleLogEntry(index) {
  const detail = document.getElementById(`log-detail-${index}`);
  const chevron = document.getElementById(`log-chevron-${index}`);
  if (!detail) return;
  const isOpen = detail.style.display !== 'none';
  detail.style.display = isOpen ? 'none' : 'block';
  if (chevron) chevron.style.transform = isOpen ? 'rotate(0deg)' : 'rotate(180deg)';
}

// ── Secret Modal ──
function openSecretModal() {
  document.getElementById('secretInput').value = '';
  document.getElementById('secretSaveBtn').disabled = true;
  const hasSecret = !!localStorage.getItem('hostKey');
  const info = document.getElementById('secretStoredInfo');
  info.classList.toggle('visible', hasSecret);
  const overlay = document.getElementById('secretOverlay');
  overlay.style.display = 'flex';
  setTimeout(() => document.getElementById('secretInput').focus(), 50);
}

function closeSecretModal() {
  document.getElementById('secretOverlay').style.display = 'none';
}

function onOverlayClick(event) {
  if (event.target === document.getElementById('secretOverlay')) {
    closeSecretModal();
  }
}

function onSecretInput() {
  const val = document.getElementById('secretInput').value;
  document.getElementById('secretSaveBtn').disabled = val.trim().length === 0;
}

function onSecretKeyDown(event) {
  if (event.key === 'Enter') saveSecret();
  if (event.key === 'Escape') closeSecretModal();
}

function saveSecret() {
  const val = document.getElementById('secretInput').value.trim();
  if (!val) return;
  const encoded = btoa(val);
  localStorage.setItem('hostKey', encoded);
  closeSecretModal();
  updateSecretBtn();
}

function updateSecretBtn() {
  const hasSecret = !!localStorage.getItem('hostKey');
  const btn = document.getElementById('secretBtn');
  const label = document.getElementById('secretBtnLabel');
  btn.classList.toggle('has-secret', hasSecret);
  label.textContent = hasSecret ? 'Secret Set' : 'Add Secret';
}

// ── Theme ──
function toggleTheme() {
  const html = document.documentElement;
  const isDark = html.classList.toggle('dark');
  localStorage.setItem('theme', isDark ? 'dark' : 'light');
  document.getElementById('themeToggle').textContent = isDark ? '\u2600\uFE0F' : '\uD83C\uDF19';
}

(function initTheme() {
  const saved = localStorage.getItem('theme');
  if (saved === 'dark') {
    document.documentElement.classList.add('dark');
    document.getElementById('themeToggle').textContent = '\u2600\uFE0F';
  }
})();

updateSecretBtn();
updateLogsCountBadge();

// ── SVG icons ──
function tickIcon(cls) {
  return `<svg class="${cls}" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg>`;
}

function crossIcon(cls) {
  return `<svg class="${cls}" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg>`;
}

function pendingIcon(cls) {
  return `<svg class="${cls}" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>`;
}

function getStatusIcon(id, size) {
  const r = healthResults[id];
  if (!r) return pendingIcon(size === 'lg' ? 'icon-pending' : 'icon-pending');
  if (r.status === 'checking') return pendingIcon('icon-checking');
  if (r.status === 'healthy') return tickIcon('icon-healthy');
  return crossIcon('icon-unhealthy');
}

function getStatusClass(id) {
  const r = healthResults[id];
  if (!r) return 'pending';
  return r.status;
}

function getStatusLabel(id) {
  const r = healthResults[id];
  if (!r) return 'Pending';
  if (r.status === 'checking') return 'Checking...';
  if (r.status === 'healthy') return 'Healthy';
  return 'Unhealthy';
}

// ── Load services.json ──
async function loadServices() {
  try {
    const resp = await fetch('services.json');
    servicesData = await resp.json();
    renderSidebar();
    renderMain();
    runHealthChecks();
  } catch (e) {
    document.getElementById('main').innerHTML = `<div class="empty-state"><p>Failed to load services.json</p><p style="font-size:12px">${e.message}</p></div>`;
  }
}

// ── Get all services as flat list ──
function allServices() {
  if (!servicesData) return [];
  return [
    ...servicesData.orchestrationApis.map(s => ({ ...s, group: 'orchestration' })),
    ...servicesData.microservices.map(s => ({ ...s, group: 'microservice' })),
    ...(servicesData.other || []).map(s => ({ ...s, group: 'other' }))
  ];
}

function findService(id) {
  return allServices().find(s => s.id === id);
}

// ── Render sidebar ──
function renderSidebar() {
  const el = document.getElementById('sidebar');
  let html = '';

  html += `<div class="sidebar-group-title">Orchestration APIs</div>`;
  for (const s of servicesData.orchestrationApis) {
    html += sidebarItem(s);
  }

  html += `<div class="sidebar-group-title">Microservices</div>`;
  for (const s of servicesData.microservices) {
    html += sidebarItem(s);
  }

  if (servicesData.other && servicesData.other.length > 0) {
    html += `<div class="sidebar-group-title">Other</div>`;
    for (const s of servicesData.other) {
      html += sidebarItem(s);
    }
  }

  el.innerHTML = html;
}

function sidebarItem(s) {
  const active = selectedId === s.id ? ' active' : '';
  const r = healthResults[s.id];
  const time = r && r.responseTime ? `${r.responseTime}ms` : '';
  return `<div class="sidebar-item${active}" id="sidebar-item-${s.id}" onclick="selectService('${s.id}')" title="${s.name}">
    <div class="status-icon" id="sidebar-icon-${s.id}">${getStatusIcon(s.id)}</div>
    <span class="service-name">${s.name}</span>
    <span class="response-time" id="sidebar-time-${s.id}">${time}</span>
  </div>`;
}

// ── Render main ──
function renderMain() {
  if (selectedId) {
    renderDetail(selectedId);
  }
}

function renderDetail(id) {
  const s = findService(id);
  if (!s) return;
  const el = document.getElementById('main');
  const cls = getStatusClass(s.id);
  const r = healthResults[s.id];
  const time = r && r.responseTime ? `${r.responseTime}ms` : '-';
  const checkedAt = r && r.checkedAt ? new Date(r.checkedAt).toLocaleTimeString() : '-';
  const errorDisplay = r && r.error ? '' : 'display:none';
  const errorText = r && r.error ? r.error : '';

  el.innerHTML = `
    <div class="detail-card">
      <div class="detail-header">
        <div class="status-icon-lg ${cls}" id="detail-status-icon">${getStatusIcon(s.id, 'lg')}</div>
        <div>
          <div class="detail-title">${s.name}</div>
          <span class="detail-status-label label-${cls}" id="detail-status-label">${getStatusLabel(s.id)}</span>
        </div>
      </div>
      <div class="detail-body">
        <p class="detail-description">${s.description}</p>
        <div class="detail-grid">
          <div class="detail-field full-width">
            <span class="field-label">Base URL</span>
            <span class="field-value"><a href="${s.baseUrl}" target="_blank" rel="noopener">${s.baseUrl}</a></span>
          </div>
          <div class="detail-field">
            <span class="field-label">Health Endpoint</span>
            <span class="field-value"><a href="${s.baseUrl}${s.healthEndpoint}" target="_blank" rel="noopener">${s.healthEndpoint}</a></span>
          </div>
          <div class="detail-field">
            <span class="field-label">Swagger</span>
            <span class="field-value">
              <a class="btn-link" href="${s.baseUrl}${s.swaggerEndpoint}" target="_blank" rel="noopener">View JSON</a>
              <a class="btn-link" href="https://editor.swagger.io/?url=${encodeURIComponent(s.baseUrl + s.swaggerEndpoint)}" target="_blank" rel="noopener">Open in Swagger Editor</a>
            </span>
          </div>
          <div class="detail-field">
            <span class="field-label">Auth Type</span>
            <span class="field-value">${s.authType}</span>
          </div>
          <div class="detail-field">
            <span class="field-label">Response Time</span>
            <span class="field-value" id="detail-response-time">${time}</span>
          </div>
          <div class="detail-field">
            <span class="field-label">Last Checked</span>
            <span class="field-value" id="detail-last-checked">${checkedAt}</span>
          </div>
          <div class="detail-field">
            <span class="field-label">Group</span>
            <span class="field-value">${s.group === 'orchestration' ? 'Orchestration API' : s.group === 'microservice' ? 'Microservice' : 'Other'}</span>
          </div>
          <div class="detail-field full-width" id="detail-error-field" style="${errorDisplay}"><span class="field-label">Error</span><span class="field-value" id="detail-error-value" style="color:var(--error)">${errorText}</span></div>
          <div class="detail-field full-width">
            <span class="field-label">Callers</span>
            <div class="caller-tags">
              ${s.callers.map(c => `<span class="caller-tag">${c}</span>`).join('')}
            </div>
          </div>
        </div>
      </div>
    </div>
    ${renderVariablesCard()}
    <div class="operations-card">
      <div class="operations-header">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><polyline points="16 18 22 12 16 6"/><polyline points="8 6 2 12 8 18"/></svg>
        <span class="operations-title">Operations</span>
        <span class="operations-count">${(s.operations || []).length}</span>
      </div>
      ${renderOperations(s)}
    </div>`;
}

// ── Operations accordion ──
function renderOperations(s) {
  const ops = s.operations || [];
  if (ops.length === 0) {
    return `<div class="operations-empty">No operations defined for this service.</div>`;
  }
  return ops.map((op, i) => {
    const isGet = op.method === 'GET' || op.method === 'DELETE';
    const defaultPayload = op.defaultPayload || '';
    const resetBtn = defaultPayload
      ? `<button class="reset-payload-btn" onclick="resetPayload('${s.id}', ${i})" title="Reset to default payload">
          <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><polyline points="1 4 1 10 7 10"/><path d="M3.51 15a9 9 0 1 0 .49-3.8"/></svg>
          Reset
        </button>`
      : '';
    const bodySection = isGet ? '' : `
      <div class="body-header-row">
        <div class="body-label">Request Body</div>
        ${resetBtn}
      </div>
      <textarea class="body-textarea" id="body-${s.id}-${i}">${escapeText(defaultPayload)}</textarea>`;
    const playIcon = `<svg width="11" height="11" viewBox="0 0 24 24" fill="currentColor"><polygon points="5 3 19 12 5 21 5 3"/></svg>`;
    return `<div class="accordion-item" id="acc-${s.id}-${i}">
      <div class="accordion-trigger" onclick="toggleAccordion('${s.id}', ${i})">
        <span class="method-badge method-${op.method}">${op.method}</span>
        <span class="accordion-path">${op.path}</span>
        <span class="accordion-name">${op.name}</span>
        <svg class="accordion-chevron" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><polyline points="6 9 12 15 18 9"/></svg>
      </div>
      <div class="accordion-body">
        <div class="path-row">
          <span class="path-label">URL Path</span>
          <input class="path-input" type="text" value="${op.path}" spellcheck="false" />
        </div>
        ${bodySection}
        <div class="invoke-row">
          <button class="invoke-btn" id="invoke-btn-${s.id}-${i}" onclick="invokeOperation('${s.id}', ${i})">${playIcon} Invoke</button>
        </div>
        <div class="response-row">
          <span class="response-label">Response</span>
          <span class="status-badge" id="status-badge-${s.id}-${i}"></span>
        </div>
        <div class="response-box" id="response-box-${s.id}-${i}">— no response yet —</div>
      </div>
    </div>`;
  }).join('');
}

function resetPayload(serviceId, index) {
  const service = findService(serviceId);
  if (!service) return;
  const op = service.operations[index];
  if (!op || !op.defaultPayload) return;
  const textarea = document.getElementById(`body-${serviceId}-${index}`);
  if (textarea) textarea.value = op.defaultPayload;
}

function toggleAccordion(serviceId, index) {
  const item = document.getElementById(`acc-${serviceId}-${index}`);
  if (item) item.classList.toggle('open');
}

// ── Invoke operation ──
async function invokeOperation(serviceId, index) {
  const service = findService(serviceId);
  if (!service) return;

  const item = document.getElementById(`acc-${serviceId}-${index}`);
  const pathInput = item.querySelector('.path-input');
  const bodyTextarea = item.querySelector('.body-textarea');
  const responseBox = document.getElementById(`response-box-${serviceId}-${index}`);
  const statusBadge = document.getElementById(`status-badge-${serviceId}-${index}`);
  const invokeBtn = document.getElementById(`invoke-btn-${serviceId}-${index}`);

  const spinIcon = `<svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="animation:spin 0.8s linear infinite"><polyline points="23 4 23 10 17 10"/><path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10"/></svg>`;
  const playIcon = `<svg width="11" height="11" viewBox="0 0 24 24" fill="currentColor"><polygon points="5 3 19 12 5 21 5 3"/></svg>`;

  // Lock button
  invokeBtn.disabled = true;
  invokeBtn.innerHTML = `${spinIcon} Invoking…`;
  statusBadge.textContent = '';
  statusBadge.className = 'status-badge';
  responseBox.style.color = 'var(--text-muted)';
  responseBox.textContent = 'Loading…';

  const rawPath = pathInput ? pathInput.value : service.operations[index].path;
  const resolvedPath = applyVariables(rawPath);
  const url = service.baseUrl + resolvedPath;
  const method = service.operations[index].method;
  const op = service.operations[index];

  const headers = {};
  if (method !== 'GET' && method !== 'DELETE') {
    headers['Content-Type'] = 'application/json';
  }
  if (service.authType === 'host-key') {
    const encoded = localStorage.getItem('hostKey');
    if (encoded) headers['x-functions-key'] = encoded;
  } else if (service.authType === 'bearer') {
    const encoded = localStorage.getItem('hostKey');
    if (encoded) headers['Authorization'] = `Bearer ${encoded}`;
  }

  const requestBody = bodyTextarea && bodyTextarea.value.trim() ? bodyTextarea.value : null;

  const logEntry = {
    timestamp: new Date().toISOString(),
    serviceId,
    serviceName: service.name,
    operationName: op.name,
    method,
    url,
    requestHeaders: { ...headers },
    requestBody,
    responseStatus: null,
    responseStatusText: null,
    responseHeaders: {},
    responseBody: null,
    error: null
  };

  try {
    const fetchOptions = { method, headers, mode: 'cors', signal: AbortSignal.timeout(30000) };
    if (requestBody) fetchOptions.body = requestBody;

    const resp = await fetch(url, fetchOptions);
    const statusCode = resp.status;

    const respHeaders = {};
    resp.headers.forEach((val, key) => { respHeaders[key] = val; });

    let statusClass = 'status-5xx';
    if (statusCode >= 200 && statusCode < 300) statusClass = 'status-2xx';
    else if (statusCode >= 300 && statusCode < 400) statusClass = 'status-3xx';
    else if (statusCode >= 400 && statusCode < 500) statusClass = 'status-4xx';

    statusBadge.textContent = `${statusCode} ${resp.statusText || ''}`.trim();
    statusBadge.className = `status-badge ${statusClass}`;

    const contentType = resp.headers.get('content-type') || '';
    let bodyText;
    if (contentType.includes('application/json')) {
      const json = await resp.json();
      bodyText = JSON.stringify(json, null, 2);
    } else {
      bodyText = await resp.text();
    }

    responseBox.style.color = 'var(--text)';
    responseBox.textContent = bodyText || '(empty response)';

    logEntry.responseStatus = statusCode;
    logEntry.responseStatusText = resp.statusText;
    logEntry.responseHeaders = respHeaders;
    logEntry.responseBody = bodyText || '';
  } catch (e) {
    statusBadge.textContent = 'ERROR';
    statusBadge.className = 'status-badge status-err';
    responseBox.style.color = 'var(--error)';
    responseBox.textContent = e.name === 'TimeoutError' ? 'Request timed out (30s)' : (e.message || 'Request failed');
    logEntry.error = e.name === 'TimeoutError' ? 'Request timed out (30s)' : (e.message || 'Request failed');
  } finally {
    addLog(logEntry);
    invokeBtn.disabled = false;
    invokeBtn.innerHTML = `${playIcon} Invoke`;
  }
}

// ── Selection ──
function selectService(id) {
  selectedId = id;
  renderSidebar();
  renderMain();
}

// ── Health checks ──
async function runHealthChecks() {
  const btn = document.getElementById('refreshBtn');
  btn.classList.add('spinning');

  const services = allServices();

  // Run checks concurrently; keep showing last-known state while checks are in-flight
  const promises = services.map(s => checkHealth(s));
  await Promise.allSettled(promises);

  btn.classList.remove('spinning');
}

async function checkHealth(service) {
  const url = service.baseUrl + service.healthEndpoint;
  const start = performance.now();
  try {
    const resp = await fetch(url, {
      method: 'GET',
      mode: 'cors',
      signal: AbortSignal.timeout(10000)
    });
    const elapsed = Math.round(performance.now() - start);
    if (resp.ok) {
      healthResults[service.id] = { status: 'healthy', responseTime: elapsed, checkedAt: Date.now() };
    } else {
      healthResults[service.id] = { status: 'unhealthy', responseTime: elapsed, checkedAt: Date.now(), error: `HTTP ${resp.status}` };
    }
  } catch (e) {
    const elapsed = Math.round(performance.now() - start);
    healthResults[service.id] = { status: 'unhealthy', responseTime: elapsed, checkedAt: Date.now(), error: e.message || 'Network error' };
  }
  // Live update only the status elements — preserves open accordions and form data
  updateCounts();
  updateSidebarItemStatus(service.id);
  updateDetailStatus(service.id);
}

function updateCounts() {
  const all = allServices();
  let healthy = 0, unhealthy = 0;
  for (const s of all) {
    const r = healthResults[s.id];
    if (r && r.status === 'healthy') healthy++;
    else if (r && r.status === 'unhealthy') unhealthy++;
  }
  document.getElementById('healthyCount').textContent = healthy;
  document.getElementById('unhealthyCount').textContent = unhealthy;
}

// ── Targeted status updates (avoids full re-render on health check) ──
function updateSidebarItemStatus(serviceId) {
  const iconEl = document.getElementById(`sidebar-icon-${serviceId}`);
  const timeEl = document.getElementById(`sidebar-time-${serviceId}`);
  if (!iconEl || !timeEl) return;
  const r = healthResults[serviceId];
  iconEl.innerHTML = getStatusIcon(serviceId);
  timeEl.textContent = r && r.responseTime ? `${r.responseTime}ms` : '';
}

function updateDetailStatus(id) {
  if (id !== selectedId) return;
  const iconEl = document.getElementById('detail-status-icon');
  const labelEl = document.getElementById('detail-status-label');
  const timeEl = document.getElementById('detail-response-time');
  const checkedEl = document.getElementById('detail-last-checked');
  const errorField = document.getElementById('detail-error-field');
  const errorValue = document.getElementById('detail-error-value');
  if (!iconEl) return;
  const r = healthResults[id];
  const cls = getStatusClass(id);
  iconEl.className = `status-icon-lg ${cls}`;
  iconEl.innerHTML = getStatusIcon(id, 'lg');
  labelEl.className = `detail-status-label label-${cls}`;
  labelEl.textContent = getStatusLabel(id);
  timeEl.textContent = r && r.responseTime ? `${r.responseTime}ms` : '-';
  checkedEl.textContent = r && r.checkedAt ? new Date(r.checkedAt).toLocaleTimeString() : '-';
  if (r && r.error) {
    errorValue.textContent = r.error;
    errorField.style.display = '';
  } else {
    errorField.style.display = 'none';
  }
}

// ── Init ──
loadServices();
setInterval(runHealthChecks, 60000);
