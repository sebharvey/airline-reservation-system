(async function () {

    // =====================================================================
    // Available journey configs
    // =====================================================================

    const CONFIGS = [
        { value: 'multi-segment-oneway', label: 'Multi-Segment — One-Way Connecting (DEL → LHR → JFK)' },
        { value: 'multi-segment-return', label: 'Multi-Segment — Return Connecting (DEL → LHR → JFK ↔ DEL)' },
        { value: 'bookflow',          label: 'Bookflow (Retail API)' },
        { value: 'flight-order-ax001', label: 'Flight Order — AX001 Next Day' },
        { value: 'flight-order-ax001-no-seats', label: 'Flight Order — AX001 Next Day (No Seats)' },
        { value: 'oci-1-pax-outbound', label: 'OCI - 1 PAX Outbound' },
        { value: 'oci-1-pax-inbound', label: 'OCI - 1 PAX Inbound' },
        { value: 'oci-2-pax-outbound', label: 'OCI - 2 PAX Outbound' },
        { value: 'oci-2-pax-inbound', label: 'OCI - 2 PAX Inbound' },
        { value: 'payment',           label: 'Payment' },
        { value: 'user',              label: 'User' },
        { value: 'loyalty',           label: 'Loyalty' },
        { value: 'admin',             label: 'Admin' },
        { value: 'operations',        label: 'Operations', disabled: true },
        { value: 'terminal-customer', label: 'Terminal Customer' },
        { value: 'admin-auth-guard',  label: 'Admin Auth Guard (401 checks)' }
    ];

    // =====================================================================
    // Config dropdown — populate and select initial value
    // =====================================================================

    const params = new URLSearchParams(window.location.search);
    let config = params.get('config') || CONFIGS[0].value;

    // =====================================================================
    // View management
    // =====================================================================

    const mainPage   = document.getElementById('mainPage');
    const detailPage = document.getElementById('detailPage');
    const btnBack    = document.getElementById('btnBack');

    function showMainPage() {
        mainPage.style.display   = '';
        detailPage.style.display = 'none';
        document.title = 'Apex Air \u2014 Test Harness';
    }

    function showDetailPage() {
        mainPage.style.display   = 'none';
        detailPage.style.display = '';
    }

    btnBack.addEventListener('click', () => {
        // Unsubscribe detail view from any in-progress silent run
        if (config && journeyStates[config]) {
            journeyStates[config].onStepStart    = null;
            journeyStates[config].onStepComplete = null;
        }
        showMainPage();
    });

    // =====================================================================
    // Main page — test grid
    // =====================================================================

    function renderTestGrid() {
        const grid = document.getElementById('testGrid');
        grid.innerHTML = '';
        CONFIGS.forEach(c => {
            const card = document.createElement('div');
            card.id = 'card-' + c.value;

            if (c.disabled) {
                card.className = 'test-card disabled';
                card.innerHTML =
                    '<div class="test-card-indicator" id="card-indicator-' + c.value + '"></div>' +
                    '<div class="test-card-body">' +
                    '<div class="test-card-name">' + esc(c.label) + '</div>' +
                    '</div>' +
                    '<span class="test-card-badge">Disabled</span>';
            } else {
                card.className = 'test-card';
                card.innerHTML =
                    '<div class="test-card-indicator" id="card-indicator-' + c.value + '"></div>' +
                    '<div class="test-card-body">' +
                    '<div class="test-card-name">' + esc(c.label) + '</div>' +
                    '</div>' +
                    '<div class="test-card-arrow">\u203A</div>';
                card.addEventListener('click', async () => {
                    await loadJourney(c.value);
                    showDetailPage();
                });
            }

            grid.appendChild(card);
        });
    }

    // =====================================================================
    // Run All Journeys (main page)
    // =====================================================================

    document.getElementById('btnRunAllJourneys').addEventListener('click', runAllJourneys);

    async function runAllJourneys() {
        const btn = document.getElementById('btnRunAllJourneys');
        btn.disabled = true;
        btn.textContent = '\u23F3 Running\u2026';

        // Start all non-disabled journeys in parallel; each updates its own card
        const promises = CONFIGS.filter(c => !c.disabled).map(async (c) => {
            const card      = document.getElementById('card-' + c.value);
            const indicator = document.getElementById('card-indicator-' + c.value);
            card.classList.remove('result-pass', 'result-fail');
            card.classList.add('running');
            indicator.innerHTML = '<span class="spinner"></span>';

            try {
                const passed = await runJourneySilently(c.value);
                card.classList.remove('running');
                card.classList.add(passed ? 'result-pass' : 'result-fail');
                indicator.innerHTML = passed ? '\u2713' : '\u2717';
                indicator.style.color = passed ? 'var(--positive)' : 'var(--negative)';
            } catch {
                card.classList.remove('running');
                card.classList.add('result-fail');
                indicator.innerHTML = '\u2717';
                indicator.style.color = 'var(--negative)';
            }
        });

        await Promise.all(promises);

        btn.disabled = false;
        btn.textContent = '\u25B6 Run All Tests';
    }

    async function runJourneySilently(configName) {
        const res        = await fetch(configName + '-journey.json');
        const journeyRaw = await res.json();

        const hasRand = JSON.stringify(journeyRaw.steps).includes('__RAND_');
        let currentSteps;
        if (hasRand) {
            generateRuntimeVars();
            currentSteps = JSON.parse(JSON.stringify(journeyRaw.steps)).map(step => {
                if (step.request && step.request.body) {
                    step.request.body = applyRuntimeVars(step.request.body);
                }
                return step;
            });
        } else {
            currentSteps = JSON.parse(JSON.stringify(journeyRaw.steps));
        }

        // Initialise (or reset) this journey's shared execution state
        journeyStates[configName] = {
            status:           'running',
            stepCount:        currentSteps.length,
            currentStepIndex: 0,
            stepResults:      {},
            onStepStart:      null,
            onStepComplete:   null,
        };

        const localChain    = {};
        const localPaxCount = hasRand ? runtimeVars.paxCount : 1;
        let allPassed       = true;

        for (let idx = 0; idx < currentSteps.length; idx++) {
            journeyStates[configName].currentStepIndex = idx;
            if (journeyStates[configName].onStepStart) {
                journeyStates[configName].onStepStart(idx);
            }

            const result = await runStepSilent(
                currentSteps[idx], currentSteps, journeyRaw.journey.baseUrls, localChain, localPaxCount
            );
            journeyStates[configName].stepResults[idx] = result;

            if (journeyStates[configName].onStepComplete) {
                journeyStates[configName].onStepComplete(idx, result);
            }

            if (!result.passed) allPassed = false;
        }

        journeyStates[configName].status           = 'done';
        journeyStates[configName].currentStepIndex = -1;
        return allPassed;
    }

    async function runStepSilent(step, allSteps, baseUrlEntries, chain, paxCount) {
        const defaultId    = baseUrlEntries[0].id;
        const stepUrlRef   = step.apiCall.baseUrlRef || defaultId;
        const baseUrlEntry = baseUrlEntries.find(e => e.id === stepUrlRef) || baseUrlEntries[0];
        const baseUrl      = baseUrlEntry.url.replace(/\/+$/, '');
        const api          = step.apiCall;

        let endpoint = api.endpoint;
        if (api.pathParams) {
            for (const [k, v] of Object.entries(api.pathParams)) {
                let cv = chain[k];
                if (cv === undefined && step.request.dataChain) {
                    const dc = step.request.dataChain.find(c => c.field === k);
                    if (dc && chain[dc.from] !== undefined) cv = chain[dc.from];
                }
                if (cv === undefined && typeof v === 'string') {
                    const m = v.match(/^from-step-(\d+)$/);
                    if (m) {
                        const src = allSteps.find(s => s.step === parseInt(m[1]));
                        if (src && src.chainsTo) {
                            const mc = src.chainsTo.find(c => c.field === k || (typeof c.as === 'string' && c.as === k));
                            if (mc) cv = chain[mc.as || mc.field];
                        }
                    }
                }
                endpoint = endpoint.replace('{' + k + '}', cv !== undefined ? cv : v);
            }
        }
        const url = baseUrl + endpoint;

        const fetchOpts = { method: api.method, headers: {} };
        if (step.request.headers) Object.assign(fetchOpts.headers, step.request.headers);

        let requestBody = (step.request.body !== null && step.request.body !== undefined)
            ? JSON.parse(JSON.stringify(step.request.body)) : null;

        if (step.request.dataChain && requestBody) {
            step.request.dataChain.forEach(dc => {
                const fp  = dc.field.replace(/ \(path\)$/, '');
                const key = dc.from || fp;
                if (chain[key] === undefined) return;
                if (fp.includes('.') || fp.includes('[')) setPath(requestBody, fp, chain[key]);
                else if (fp in requestBody) requestBody[fp] = chain[key];
            });
        }
        if (step.request.dataChain) {
            step.request.dataChain.forEach(dc => {
                if (dc.field === 'Authorization' && chain['accessToken']) {
                    fetchOpts.headers['Authorization'] = 'Bearer ' + chain['accessToken'];
                }
            });
        }
        if (requestBody !== null && requestBody !== undefined) {
            fetchOpts.body = JSON.stringify(requestBody);
        }

        let liveStatus = 0, liveBody = null, liveError = null, durationMs = null;
        try {
            const t0   = performance.now();
            const r    = await fetch(url, fetchOpts);
            liveStatus = r.status;
            const ct   = r.headers.get('content-type') || '';
            if (ct.includes('application/json')) {
                liveBody = await r.json();
            } else {
                const t = await r.text();
                liveBody = t.length > 0 ? t : null;
            }
            durationMs = Math.round(performance.now() - t0);
        } catch (err) {
            liveError = err.message;
        }

        if (step.chainsTo && liveBody && typeof liveBody === 'object') {
            step.chainsTo.forEach(cd => {
                if (cd.randomArrayPath) {
                    const all = collectAllValues(liveBody, cd.randomArrayPath.split('.'));
                    if (all.length) chain[cd.as] = all[Math.floor(Math.random() * all.length)];
                } else if (cd.randomAvailableSeatFrom) {
                    const cabins = liveBody[cd.randomAvailableSeatFrom];
                    if (!Array.isArray(cabins)) return;
                    const avail   = cabins.flatMap(c => c.seats || []).filter(s => s.availability === 'available');
                    if (!avail.length) return;
                    const shuffled = [...avail].sort(() => Math.random() - 0.5);
                    const count    = Math.min(paxCount || 1, shuffled.length);
                    for (let i = 0; i < count; i++) {
                        for (const [sf, alias] of Object.entries(cd.as)) {
                            if (i === 0) chain[alias] = shuffled[i][sf];
                            chain[alias + '_' + (i + 1)] = shuffled[i][sf];
                        }
                    }
                } else if (cd.path) {
                    const val = getPath(liveBody, cd.path);
                    if (val !== undefined) chain[cd.as] = val;
                } else {
                    const am = cd.field.match(/^\[(\d+)\]\.(.+)$/);
                    if (am && Array.isArray(liveBody)) {
                        const ai = parseInt(am[1], 10);
                        if (liveBody[ai] && liveBody[ai][am[2]] !== undefined) {
                            chain[cd.as || am[2]] = liveBody[ai][am[2]];
                        }
                    } else if (liveBody[cd.field] !== undefined) {
                        chain[cd.as || cd.field] = liveBody[cd.field];
                    }
                }
            });
        }

        if (step.chainsTo && step.chainsTo.length) {
            step.chainsTo.forEach(cd => {
                const alias = cd.as || cd.field;
                if (typeof alias !== 'string') return;
                const value = chain[alias];
                if (value === undefined) return;
                const fromRef = 'from-step-' + step.step;
                allSteps.forEach(s => {
                    if (s.apiCall.pathParams) {
                        for (const [pn, pv] of Object.entries(s.apiCall.pathParams)) {
                            if (pv === fromRef && pn === alias) chain[pn] = value;
                        }
                    }
                });
            });
        }

        const statusMatch   = liveStatus === step.expected.statusCode;
        const assertResults = evaluateAssertions(step.expected.assertions, liveBody);
        const passed        = statusMatch && assertResults.every(r => r.pass);
        return { passed, liveStatus, liveError, durationMs };
    }

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

        // Second passenger (PAX-2) — guaranteed different from PAX-1
        let pax2GivenName = pick(FIRST_NAMES);
        while (pax2GivenName === givenName) pax2GivenName = pick(FIRST_NAMES);
        let pax2Surname = pick(SURNAMES);
        while (pax2Surname === surname) pax2Surname = pick(SURNAMES);
        const pax2Gender  = pick(genders);
        const pax2DobYear  = 1950 + Math.floor(Math.random() * 56);
        const pax2DobMonth = (1 + Math.floor(Math.random() * 12)).toString().padStart(2, '0');
        const pax2DobDay   = (1 + Math.floor(Math.random() * 28)).toString().padStart(2, '0');
        const pax2DateOfBirth = `${pax2DobYear}-${pax2DobMonth}-${pax2DobDay}`;
        const pax2Email = pax2GivenName.toLowerCase() + '.' +
                          pax2Surname.toLowerCase() + '.' +
                          randDigits(6) + '@testmail.example.com';
        const pax2Phone = '07' + randDigits(9);

        // PAX count (1–6) and supplementary passengers (PAX-3 through PAX-6)
        const paxCount = 1 + Math.floor(Math.random() * 6);
        function makePax() {
            let gn = pick(FIRST_NAMES); while (gn === givenName) gn = pick(FIRST_NAMES);
            let sn = pick(SURNAMES);    while (sn === surname)    sn = pick(SURNAMES);
            const g  = pick(genders);
            const dy = 1950 + Math.floor(Math.random() * 56);
            const dm = (1 + Math.floor(Math.random() * 12)).toString().padStart(2, '0');
            const dd = (1 + Math.floor(Math.random() * 28)).toString().padStart(2, '0');
            return { givenName: gn, surname: sn, gender: g,
                     dateOfBirth: `${dy}-${dm}-${dd}`,
                     email: `${gn.toLowerCase()}.${sn.toLowerCase()}.${randDigits(6)}@testmail.example.com`,
                     phone: '07' + randDigits(9) };
        }
        const pax3 = makePax(); const pax4 = makePax();
        const pax5 = makePax(); const pax6 = makePax();

        const route = pick(ROUTES);
        const today = new Date();
        const outboundOffset = 1;        // tomorrow
        const returnOffset   = outboundOffset + 7; // exactly 1 week after departure
        const outboundDateObj = new Date(today); outboundDateObj.setDate(today.getDate() + outboundOffset);
        const returnDateObj   = new Date(today); returnDateObj.setDate(today.getDate() + returnOffset);

        const outboundOrigin = route.origin;
        const outboundDest   = route.destination;
        const returnOrigin   = route.destination;
        const returnDest     = route.origin;
        const departDate     = isoDate(outboundDateObj);
        const returnDate     = isoDate(returnDateObj);

        const tomorrowDateObj = new Date(today); tomorrowDateObj.setDate(today.getDate() + 1);
        const tomorrowDate    = isoDate(tomorrowDateObj);

        runtimeVars = {
            givenName, surname, password, email,
            recipientGivenName, recipientSurname, recipientPassword, recipientEmail,
            gender, dateOfBirth, phone, loyaltyNumber,
            pax2GivenName, pax2Surname, pax2Gender, pax2DateOfBirth, pax2Email, pax2Phone,
            paxCount,
            pax3GivenName: pax3.givenName, pax3Surname: pax3.surname, pax3Gender: pax3.gender,
            pax3DateOfBirth: pax3.dateOfBirth, pax3Email: pax3.email, pax3Phone: pax3.phone,
            pax4GivenName: pax4.givenName, pax4Surname: pax4.surname, pax4Gender: pax4.gender,
            pax4DateOfBirth: pax4.dateOfBirth, pax4Email: pax4.email, pax4Phone: pax4.phone,
            pax5GivenName: pax5.givenName, pax5Surname: pax5.surname, pax5Gender: pax5.gender,
            pax5DateOfBirth: pax5.dateOfBirth, pax5Email: pax5.email, pax5Phone: pax5.phone,
            pax6GivenName: pax6.givenName, pax6Surname: pax6.surname, pax6Gender: pax6.gender,
            pax6DateOfBirth: pax6.dateOfBirth, pax6Email: pax6.email, pax6Phone: pax6.phone,
            outboundOrigin, outboundDest, returnOrigin, returnDest, departDate, returnDate,
            tomorrowDate
        };
    }

    function applyRuntimeVars(obj) {
        if (obj === null || obj === undefined) return obj;
        // Exact-match numeric placeholders — return the raw number, not a string
        if (obj === '__RAND_PAX_COUNT__') return runtimeVars.paxCount;
        if (typeof obj === 'string') {
            return obj
                .replace(/__RAND_RECIPIENT_GIVEN_NAME__/g, runtimeVars.recipientGivenName)
                .replace(/__RAND_RECIPIENT_SURNAME__/g,    runtimeVars.recipientSurname)
                .replace(/__RAND_RECIPIENT_EMAIL__/g,      runtimeVars.recipientEmail)
                .replace(/__RAND_RECIPIENT_PASSWORD__/g,   runtimeVars.recipientPassword)
                // PAX 6–3 before PAX 2/1 to avoid partial-match collisions
                .replace(/__RAND_GIVEN_NAME_6__/g,   runtimeVars.pax6GivenName)
                .replace(/__RAND_SURNAME_6__/g,      runtimeVars.pax6Surname)
                .replace(/__RAND_GENDER_6__/g,       runtimeVars.pax6Gender)
                .replace(/__RAND_DOB_6__/g,          runtimeVars.pax6DateOfBirth)
                .replace(/__RAND_EMAIL_6__/g,        runtimeVars.pax6Email)
                .replace(/__RAND_PHONE_6__/g,        runtimeVars.pax6Phone)
                .replace(/__RAND_GIVEN_NAME_5__/g,   runtimeVars.pax5GivenName)
                .replace(/__RAND_SURNAME_5__/g,      runtimeVars.pax5Surname)
                .replace(/__RAND_GENDER_5__/g,       runtimeVars.pax5Gender)
                .replace(/__RAND_DOB_5__/g,          runtimeVars.pax5DateOfBirth)
                .replace(/__RAND_EMAIL_5__/g,        runtimeVars.pax5Email)
                .replace(/__RAND_PHONE_5__/g,        runtimeVars.pax5Phone)
                .replace(/__RAND_GIVEN_NAME_4__/g,   runtimeVars.pax4GivenName)
                .replace(/__RAND_SURNAME_4__/g,      runtimeVars.pax4Surname)
                .replace(/__RAND_GENDER_4__/g,       runtimeVars.pax4Gender)
                .replace(/__RAND_DOB_4__/g,          runtimeVars.pax4DateOfBirth)
                .replace(/__RAND_EMAIL_4__/g,        runtimeVars.pax4Email)
                .replace(/__RAND_PHONE_4__/g,        runtimeVars.pax4Phone)
                .replace(/__RAND_GIVEN_NAME_3__/g,   runtimeVars.pax3GivenName)
                .replace(/__RAND_SURNAME_3__/g,      runtimeVars.pax3Surname)
                .replace(/__RAND_GENDER_3__/g,       runtimeVars.pax3Gender)
                .replace(/__RAND_DOB_3__/g,          runtimeVars.pax3DateOfBirth)
                .replace(/__RAND_EMAIL_3__/g,        runtimeVars.pax3Email)
                .replace(/__RAND_PHONE_3__/g,        runtimeVars.pax3Phone)
                .replace(/__RAND_GIVEN_NAME_2__/g,   runtimeVars.pax2GivenName)
                .replace(/__RAND_SURNAME_2__/g,      runtimeVars.pax2Surname)
                .replace(/__RAND_GENDER_2__/g,       runtimeVars.pax2Gender)
                .replace(/__RAND_DOB_2__/g,          runtimeVars.pax2DateOfBirth)
                .replace(/__RAND_EMAIL_2__/g,        runtimeVars.pax2Email)
                .replace(/__RAND_PHONE_2__/g,        runtimeVars.pax2Phone)
                .replace(/__RAND_GIVEN_NAME__/g,     runtimeVars.givenName)
                .replace(/__RAND_SURNAME__/g,        runtimeVars.surname)
                .replace(/__RAND_EMAIL__/g,          runtimeVars.email)
                .replace(/__RAND_PASSWORD__/g,       runtimeVars.password)
                .replace(/__RAND_GENDER__/g,         runtimeVars.gender)
                .replace(/__RAND_DOB__/g,            runtimeVars.dateOfBirth)
                .replace(/__RAND_PHONE__/g,          runtimeVars.phone)
                .replace(/__RAND_LOYALTY_NUMBER__/g,  runtimeVars.loyaltyNumber)
                .replace(/__RAND_OUTBOUND_ORIGIN__/g, runtimeVars.outboundOrigin)
                .replace(/__RAND_OUTBOUND_DEST__/g,   runtimeVars.outboundDest)
                .replace(/__RAND_RETURN_ORIGIN__/g,   runtimeVars.returnOrigin)
                .replace(/__RAND_RETURN_DEST__/g,     runtimeVars.returnDest)
                .replace(/__RAND_DEPART_DATE__/g,     runtimeVars.departDate)
                .replace(/__RAND_RETURN_DATE__/g,     runtimeVars.returnDate)
                .replace(/__TOMORROW_DATE__/g,         runtimeVars.tomorrowDate);
        }
        if (Array.isArray(obj)) {
            // Filter out items whose __PAX_MIN__ exceeds the current paxCount, then strip the marker
            return obj
                .filter(item => !item || typeof item !== 'object' || !('__PAX_MIN__' in item) || item.__PAX_MIN__ <= runtimeVars.paxCount)
                .map(item => {
                    if (item && typeof item === 'object' && '__PAX_MIN__' in item) {
                        const { __PAX_MIN__, ...rest } = item;
                        return applyRuntimeVars(rest);
                    }
                    return applyRuntimeVars(item);
                });
        }
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
    const btnViewLogs = document.getElementById('btnViewLogs');
    const logsModal = document.getElementById('logsModal');
    const logsModalContent = document.getElementById('logsModalContent');
    const logsModalClose = document.getElementById('logsModalClose');

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

    btnViewLogs.addEventListener('click', () => {
        logsModalContent.textContent = formatLogForCopy();
        logsModal.style.display = 'flex';
    });

    logsModalClose.addEventListener('click', () => {
        logsModal.style.display = 'none';
    });

    logsModal.addEventListener('click', (e) => {
        if (e.target === logsModal) logsModal.style.display = 'none';
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

    // Per-journey execution state — populated by runJourneySilently, read by detail view
    const journeyStates = {};

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
        btnViewLogs.disabled = true;

        // Initial render
        if (hasRuntimeVars) {
            generateRuntimeVars();
            buildTableRows(buildStepsWithVars());
            updateRuntimeBanner();
        } else {
            document.getElementById('runtimeDataBanner').style.display = 'none';
            buildTableRows(JSON.parse(JSON.stringify(raw.steps)));
        }

        // Overlay any in-progress or completed silent-run results
        applyJourneyState(config);

        btnRunAll.disabled = false;
        nextStepCursor = 0;
        nextCurrentSteps = null;
        btnNextStep.disabled = false;
        btnNextStep.textContent = '\u23ED Next';
    }

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
        const previewResult = liveResults[idx];
        if (previewResult && previewResult.url) {
            html += `<div class="live-url" style="margin-top:0.4rem">\u2192 ${esc(previewResult.url)}</div>`;
        }
        html += '</div>';

        html += '<div class="modal-section"><div class="modal-section-title">Request</div>';
        html += buildRequestCell(step).innerHTML;
        html += '</div>';

        html += '<div class="modal-section"><details class="expected-collapsible"><summary class="modal-section-title">Expected Response</summary>';
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
        html += '</details></div>';

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
            ['paxCount',  String(runtimeVars.paxCount)],
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
            btnViewLogs.disabled = true;
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

            // Detach from silent-run callbacks
            if (journeyStates[config]) {
                journeyStates[config].onStepStart    = null;
                journeyStates[config].onStepComplete = null;
            }

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
            btnViewLogs.disabled = false;
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
        btnViewLogs.disabled = true;
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

        // Detach from silent-run callbacks — this run owns the table now
        if (journeyStates[config]) {
            journeyStates[config].onStepStart    = null;
            journeyStates[config].onStepComplete = null;
        }

        for (const stepIdx of liveStepIndices) {
            await runStep(rowRefs[stepIdx], currentSteps);
        }

        btnRunAll.disabled = false;
        btnRunAll.textContent = '\u25B6 Run';
        btnCopyLogs.disabled = false;
        btnViewLogs.disabled = false;
        btnNextStep.disabled = false;
        btnNextStep.textContent = '\u23ED Next';
    }

    // Walk a dot-separated path where segments ending with [*] expand arrays,
    // or [?key=value] filter arrays to matching items.
    // e.g. 'flights[*].cabins[*].fareFamilies[*].offer.offerId' collects all offerIds.
    // e.g. 'flights[?flightNumber=AX001].cabins[*].fareFamilies[*].offer.offerId' collects only from AX001.
    function collectAllValues(obj, pathParts) {
        if (!pathParts.length || obj == null) return [];
        const [head, ...tail] = pathParts;
        const filterMatch = head.match(/^(\w+)\[\?(\w+)=(.+)\]$/);
        if (filterMatch) {
            const [, key, prop, value] = filterMatch;
            const arr = obj[key];
            if (!Array.isArray(arr)) return [];
            const filtered = arr.filter(item => String(item[prop]) === value);
            if (!tail.length) return filtered;
            return filtered.flatMap(item => collectAllValues(item, tail));
        }
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
                // Check request.dataChain for an explicit field→alias mapping for this path param
                if (chainedValue === undefined && step.request.dataChain) {
                    const dcEntry = step.request.dataChain.find(c => c.field === k);
                    if (dcEntry && liveChain[dcEntry.from] !== undefined) {
                        chainedValue = liveChain[dcEntry.from];
                    }
                }
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
                    // Collect all available seats from cabins, shuffle, and pick up to paxCount distinct seats.
                    // Stores un-indexed alias (first seat, for backward compat) and indexed alias_1…alias_N.
                    const cabins = liveBody[chain.randomAvailableSeatFrom];
                    if (!Array.isArray(cabins)) return;
                    const available = cabins.flatMap(c => c.seats || []).filter(s => s.availability === 'available');
                    if (!available.length) return;
                    const shuffled = [...available].sort(() => Math.random() - 0.5);
                    const count = Math.min(runtimeVars.paxCount || 1, shuffled.length);
                    for (let i = 0; i < count; i++) {
                        const seat = shuffled[i];
                        for (const [srcField, alias] of Object.entries(chain.as)) {
                            if (i === 0) liveChain[alias] = seat[srcField]; // un-indexed fallback
                            liveChain[`${alias}_${i + 1}`] = seat[srcField];
                        }
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
    // Journey state helpers — live progress in detail view
    // =====================================================================

    function applyJourneyState(configName) {
        const state = journeyStates[configName];
        if (!state) return;

        // Render results for all steps that have already finished
        for (const [idxStr, result] of Object.entries(state.stepResults)) {
            const ref = rowRefs[parseInt(idxStr)];
            if (ref) applyResultToRow(ref, result);
        }

        // Show spinner on the step that is currently executing (if not yet in results)
        if (state.status === 'running' && state.currentStepIndex >= 0 &&
            !(state.currentStepIndex in state.stepResults)) {
            const ref = rowRefs[state.currentStepIndex];
            if (ref) showStepRunning(ref);
        }

        // Subscribe to future step updates while the journey is still running
        if (state.status === 'running') {
            state.onStepStart = (idx) => {
                const ref = rowRefs[idx];
                if (ref) showStepRunning(ref);
            };
            state.onStepComplete = (idx, result) => {
                const ref = rowRefs[idx];
                if (ref) applyResultToRow(ref, result);
            };
        }
    }

    function showStepRunning(ref) {
        ref.row.classList.remove('result-pass', 'result-fail');
        ref.row.classList.add('step-running');
        if (ref.tdTime) ref.tdTime.innerHTML = '<span class="spinner step-spinner"></span>';
    }

    function applyResultToRow(ref, result) {
        ref.row.classList.remove('step-running', 'result-pass', 'result-fail');
        ref.row.classList.add(result.passed ? 'result-pass' : 'result-fail');
        if (ref.tdTime) {
            ref.tdTime.textContent = result.durationMs !== null && result.durationMs !== undefined
                ? result.durationMs + ' ms' : '\u2014';
        }
    }

    // =====================================================================
    // Boot
    // =====================================================================

    renderTestGrid();

    const initialConfig = params.get('config');
    if (initialConfig && CONFIGS.some(c => c.value === initialConfig)) {
        await loadJourney(initialConfig);
        showDetailPage();
    } else {
        showMainPage();
    }

})();
