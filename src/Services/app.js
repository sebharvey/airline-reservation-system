let servicesData = null;
let healthResults = {};
let selectedId = 'retail-api';

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
  return `<div class="sidebar-item${active}" onclick="selectService('${s.id}')" title="${s.name}">
    <div class="status-icon">${getStatusIcon(s.id)}</div>
    <span class="service-name">${s.name}</span>
    <span class="response-time">${time}</span>
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
  const errorMsg = r && r.error ? `<div class="detail-field full-width"><span class="field-label">Error</span><span class="field-value" style="color:var(--error)">${r.error}</span></div>` : '';

  el.innerHTML = `
    <div class="detail-card">
      <div class="detail-header">
        <div class="status-icon-lg ${cls}">${getStatusIcon(s.id, 'lg')}</div>
        <div>
          <div class="detail-title">${s.name}</div>
          <span class="detail-status-label label-${cls}">${getStatusLabel(s.id)}</span>
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
            <span class="field-value">${time}</span>
          </div>
          <div class="detail-field">
            <span class="field-label">Last Checked</span>
            <span class="field-value">${checkedAt}</span>
          </div>
          <div class="detail-field">
            <span class="field-label">Group</span>
            <span class="field-value">${s.group === 'orchestration' ? 'Orchestration API' : s.group === 'microservice' ? 'Microservice' : 'Other'}</span>
          </div>
          ${errorMsg}
          <div class="detail-field full-width">
            <span class="field-label">Callers</span>
            <div class="caller-tags">
              ${s.callers.map(c => `<span class="caller-tag">${c}</span>`).join('')}
            </div>
          </div>
        </div>
      </div>
    </div>
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
    const bodySection = isGet ? '' : `
      <div class="body-label">Request Body</div>
      <textarea class="body-textarea" placeholder='{\n  \n}'></textarea>`;
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

  const path = pathInput ? pathInput.value : service.operations[index].path;
  const url = service.baseUrl + path;
  const method = service.operations[index].method;

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

  try {
    const fetchOptions = { method, headers, mode: 'cors', signal: AbortSignal.timeout(30000) };
    if (bodyTextarea && bodyTextarea.value.trim()) {
      fetchOptions.body = bodyTextarea.value;
    }

    const resp = await fetch(url, fetchOptions);
    const statusCode = resp.status;

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
  } catch (e) {
    statusBadge.textContent = 'ERROR';
    statusBadge.className = 'status-badge status-err';
    responseBox.style.color = 'var(--error)';
    responseBox.textContent = e.name === 'TimeoutError' ? 'Request timed out (30s)' : (e.message || 'Request failed');
  } finally {
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
  // Live update sidebar and main as each check completes
  updateCounts();
  renderSidebar();
  renderMain();
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

// ── Init ──
loadServices();
setInterval(runHealthChecks, 60000);
