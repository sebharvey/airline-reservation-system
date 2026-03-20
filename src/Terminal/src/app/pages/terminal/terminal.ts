import {
  Component,
  inject,
  signal,
  computed,
  ViewChild,
  ElementRef,
  AfterViewInit,
  OnInit,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../services/auth.service';

export type LineType = 'command' | 'output' | 'success' | 'warning' | 'error' | 'system' | 'separator';

export interface TerminalLine {
  id: number;
  type: LineType;
  text: string;
  timestamp?: Date;
}

interface MockFlight {
  num: string;
  airline: string;
  origin: string;
  dest: string;
  dep: string;
  arr: string;
  aircraft: string;
  classes: Record<string, number | string>;
}

interface MockPnr {
  rl: string;
  passengers: string[];
  segments: string[];
  contacts: string[];
  ticketing: string;
  received: string;
}

let lineId = 0;

@Component({
  selector: 'app-terminal',
  imports: [FormsModule],
  templateUrl: './terminal.html',
  styleUrl: './terminal.css',
})
export class TerminalComponent implements OnInit, AfterViewInit {
  @ViewChild('outputArea') outputAreaRef!: ElementRef<HTMLDivElement>;
  @ViewChild('cmdInput') cmdInputRef!: ElementRef<HTMLInputElement>;

  auth = inject(AuthService);

  commandInput = signal('');
  lines = signal<TerminalLine[]>([]);
  history = signal<string[]>([]);
  historyIndex = signal(-1);
  showHelp = signal(false);
  sessionPnr = signal<MockPnr | null>(null);

  // Active PNR in context (retrieved/built)
  contextPnr = signal<MockPnr | null>(null);

  promptPrefix = computed(() => {
    const user = this.auth.currentUser();
    return user ? `${user.agentId}>` : '>';
  });

  // ─── Mock data ──────────────────────────────────────────────────────────────

  #mockFlights: MockFlight[] = [
    { num: '0304', airline: 'BA', origin: 'LHR', dest: 'CDG', dep: '0650', arr: '0910', aircraft: '320', classes: { C: 4, D: 4, J: 9, Y: 9, B: 9, H: 9, K: 7, M: 5, L: 3, V: 1 } },
    { num: '0308', airline: 'BA', origin: 'LHR', dest: 'CDG', dep: '0900', arr: '1120', aircraft: '319', classes: { C: 9, D: 9, J: 9, Y: 9, B: 9, H: 9, K: 9, M: 9, L: 9, V: 9 } },
    { num: '0316', airline: 'BA', origin: 'LHR', dest: 'CDG', dep: '1100', arr: '1320', aircraft: '320', classes: { C: 2, D: 2, J: 9, Y: 9, B: 9, H: 9, K: 9, M: 9, L: 7, V: 4 } },
    { num: '1080', airline: 'AF', origin: 'LHR', dest: 'CDG', dep: '0720', arr: '0940', aircraft: '321', classes: { C: 6, D: 6, J: 9, Y: 9, B: 9, H: 9, K: 9, M: 8, L: 6, V: 2 } },
    { num: '1092', airline: 'AF', origin: 'LHR', dest: 'CDG', dep: '1200', arr: '1420', aircraft: '320', classes: { C: 9, D: 9, J: 9, Y: 9, B: 9, H: 9, K: 9, M: 9, L: 9, V: 9 } },
    { num: '8465', airline: 'VS', origin: 'LHR', dest: 'JFK', dep: '1000', arr: '1300', aircraft: '351', classes: { C: 3, D: 3, J: 9, Y: 9, B: 9, H: 9, K: 9, M: 7, L: 5, V: 2 } },
    { num: '1', airline: 'BA', origin: 'LHR', dest: 'JFK', dep: '1125', arr: '1440', aircraft: '777', classes: { F: 4, A: 4, C: 6, D: 6, J: 9, Y: 9, B: 9, H: 9, K: 9, M: 9, L: 9, V: 9 } },
    { num: '0179', airline: 'BA', origin: 'LHR', dest: 'DXB', dep: '2115', arr: '0710', aircraft: '788', classes: { F: 2, A: 2, C: 4, D: 4, J: 9, Y: 9, B: 9, H: 9, K: 9, M: 9, L: 7, V: 4 } },
    { num: '0025', airline: 'EK', origin: 'LHR', dest: 'DXB', dep: '1415', arr: '0010', aircraft: '388', classes: { F: 6, A: 6, J: 9, C: 9, D: 9, Y: 9, B: 9, H: 9, K: 9, M: 9 } },
  ];

  #mockPnrs: Record<string, MockPnr> = {
    'ABC123': {
      rl: 'ABC123',
      passengers: ['1.SMITH/JOHN MR', '2.SMITH/JANE MRS'],
      segments: [
        'BA 0304 Y 20MAR 3 LHRCDG HK2   0650  0910  /DCBA /E',
        'BA 0308 Y 25MAR 1 CDGLHR HK2   0900  1120  /DCBA /E',
      ],
      contacts: ['APE JOHN.SMITH@EMAIL.COM', 'APM 447700123456'],
      ticketing: 'TK OK20MAR/LHRBA1234//ETBA',
      received: 'RF JSMITH',
    },
    'XYZ789': {
      rl: 'XYZ789',
      passengers: ['1.JONES/DAVID MR'],
      segments: [
        'VS 8465 J 15APR 3 LHRJFK HK1   1000  1300  /DCVS /E',
      ],
      contacts: ['APE DAVID.JONES@CORP.COM'],
      ticketing: 'TK OK15APR/LHRVS0001//ETVS',
      received: 'RF DJONES',
    },
  };

  // ─── Lifecycle ──────────────────────────────────────────────────────────────

  ngOnInit(): void {
    const user = this.auth.currentUser();
    this.#emit('system', `APEX AIR AMADEUS TERMINAL  ${new Date().toUTCString().replace(' GMT', 'Z')}`);
    this.#emit('system', `SESSION: ${user?.agentId ?? 'UNKNOWN'}  STATION: ${user?.station ?? '???'}  SIGN IN: ${this.#nowTime()}`);
    this.#emit('separator', '─'.repeat(72));
    this.#emit('output', 'READY. TYPE ? OR HELP FOR COMMAND REFERENCE.');
    this.#emit('separator', '');
  }

  ngAfterViewInit(): void {
    this.cmdInputRef.nativeElement.focus();
  }

  // ─── Input handling ──────────────────────────────────────────────────────────

  onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.#navigateHistory(1);
    } else if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.#navigateHistory(-1);
    } else if (event.key === 'Enter') {
      this.submit();
    }
  }

  submit(): void {
    const raw = this.commandInput().trim();
    if (!raw) return;

    // Add to display
    this.#emit('command', `${this.promptPrefix()} ${raw}`);

    // Add to history
    this.history.update(h => [raw, ...h.filter(x => x !== raw)].slice(0, 100));
    this.historyIndex.set(-1);

    // Clear input
    this.commandInput.set('');

    // Process
    this.#process(raw.toUpperCase());
    this.#scrollToBottom();
  }

  setInput(val: string): void {
    this.commandInput.set(val);
  }

  toggleHelp(): void {
    this.showHelp.update(v => !v);
  }

  clearScreen(): void {
    this.lines.set([]);
    this.#emit('system', `SCREEN CLEARED  ${this.#nowTime()}`);
    this.#emit('separator', '');
  }

  // ─── History navigation ──────────────────────────────────────────────────────

  #navigateHistory(dir: number): void {
    const h = this.history();
    const next = this.historyIndex() + dir;
    if (next < -1 || next >= h.length) return;
    this.historyIndex.set(next);
    this.commandInput.set(next === -1 ? '' : h[next]);
  }

  // ─── Command processor ───────────────────────────────────────────────────────

  #process(cmd: string): void {
    const parts = cmd.split(/\s+/);
    const verb = parts[0];

    // Help
    if (verb === '?' || verb === 'HELP' || verb === 'H') {
      this.#cmdHelp(); return;
    }

    // Clear
    if (verb === 'CL' || verb === 'CLEAR' || verb === 'NEW') {
      this.#emit('separator', ''); this.clearScreen(); return;
    }

    // Availability
    if (verb.startsWith('AN') || verb.startsWith('AD') || verb === 'AV') {
      this.#cmdAvailability(cmd); return;
    }

    // Sell segment
    if (verb.startsWith('SS') || (verb.startsWith('S') && /^\d/.test(cmd.slice(1)))) {
      this.#cmdSell(cmd); return;
    }

    // Name element
    if (verb.startsWith('NM') || verb === 'N') {
      this.#cmdName(cmd); return;
    }

    // Contact
    if (verb.startsWith('AP') || verb.startsWith('CT')) {
      this.#cmdContact(cmd); return;
    }

    // Received from
    if (verb === 'RF') {
      this.#cmdReceived(cmd); return;
    }

    // Ticketing
    if (verb === 'TKOK' || verb === 'TK' || verb.startsWith('TK')) {
      this.#cmdTicketing(cmd); return;
    }

    // End and retrieve (save)
    if (verb === 'ER') {
      this.#cmdEndRetrieve(); return;
    }

    // End transact (confirm)
    if (verb === 'ET') {
      this.#cmdEndTransact(); return;
    }

    // Retrieve PNR
    if (verb === 'RT' || verb === '*R' || verb === '*') {
      this.#cmdRetrieve(parts.slice(1).join('')); return;
    }

    // Display PNR in context
    if (verb === 'PDU' || verb === 'PD' || verb === '*PDU') {
      this.#displayContextPnr(); return;
    }

    // Cancel/ignore
    if (verb === 'IG' || verb === 'IGNORE') {
      this.#cmdIgnore(); return;
    }

    // Cancel element
    if (verb.startsWith('XE') || verb.startsWith('XI')) {
      this.#cmdCancel(cmd); return;
    }

    // Seat request
    if (verb.startsWith('ST') || (verb === 'RQST' && cmd.includes('ST'))) {
      this.#cmdSeat(cmd); return;
    }

    // OSI / SSR
    if (verb === 'OSI' || verb === 'SSR') {
      this.#cmdOsi(cmd); return;
    }

    // Fare quote
    if (verb.startsWith('FQ') || verb === 'FXP' || verb === 'FXB') {
      this.#cmdFareQuote(cmd); return;
    }

    // Sign out
    if (verb === 'SO' || verb === 'SIGNOUT') {
      this.#emit('warning', 'USE THE SIGN OUT BUTTON TO END YOUR SESSION.'); return;
    }

    // Unknown
    this.#emit('error', `INVALID ENTRY`);
    this.#emit('output', `TYPE ? FOR COMMAND HELP OR HELP <COMMAND> FOR SPECIFIC GUIDANCE.`);
  }

  // ─── Commands ────────────────────────────────────────────────────────────────

  #cmdHelp(): void {
    this.#emit('separator', '─'.repeat(72));
    this.#emit('output', 'COMMAND REFERENCE — APEX AIR TERMINAL');
    this.#emit('separator', '─'.repeat(72));
    const cmds = [
      ['AVAILABILITY', '', ''],
      ['AN DDMMM ORGDST', 'Availability by neutral', 'AN 20MAR LHRCDG'],
      ['AD DDMMM ORGDST', 'Availability by airline', 'AD 20MAR LHRJFK'],
      ['', '', ''],
      ['SELL / BUILD PNR', '', ''],
      ['SS<n><cls><seg>', 'Sell segment from availability', 'SS1Y1'],
      ['NM1SURNAME/FIRST TL', 'Add passenger name', 'NM1SMITH/JOHN MR'],
      ['APCE EMAIL', 'Add contact email', 'APCE JOHN@EMAIL.COM'],
      ['APCM PHONE', 'Add contact mobile', 'APCM 447700123456'],
      ['RF AGENT', 'Received from', 'RF JSMITH'],
      ['TKOK', 'Add ticketing time limit', 'TKOK'],
      ['', '', ''],
      ['SAVE / RETRIEVE', '', ''],
      ['ER', 'End and retrieve (save PNR)', 'ER'],
      ['ET', 'End transact (confirm booking)', 'ET'],
      ['RT <LOCATOR>', 'Retrieve PNR by record locator', 'RT ABC123'],
      ['*', 'Display PNR in context', '*'],
      ['IG', 'Ignore / abandon PNR build', 'IG'],
      ['', '', ''],
      ['MODIFY', '', ''],
      ['XE<n>', 'Cancel element n', 'XE2'],
      ['OSI YY FREE TEXT', 'Other service info', 'OSI BA GRND TRANSFER'],
      ['SSR WCHR YY', 'Special service request', 'SSR WCHR BA HK1'],
      ['RQSTST<n><seat>', 'Seat request', 'RQSTST1A'],
      ['', '', ''],
      ['FARES', '', ''],
      ['FXP', 'Price itinerary (public fares)', 'FXP'],
      ['FXB', 'Price itinerary (private fares)', 'FXB'],
      ['', '', ''],
      ['OTHER', '', ''],
      ['CL / NEW', 'Clear screen', 'CL'],
      ['? / HELP', 'This help', '?'],
    ];
    for (const [cmd, desc, ex] of cmds) {
      if (!cmd && !desc) { this.#emit('separator', ''); continue; }
      if (!desc) { this.#emit('system', `  ${cmd}`); continue; }
      const line = `  ${cmd.padEnd(22)}${desc.padEnd(36)}${ex ? 'e.g. ' + ex : ''}`;
      this.#emit('output', line);
    }
    this.#emit('separator', '─'.repeat(72));
  }

  #cmdAvailability(cmd: string): void {
    // Parse: AN DDMMM ORGDST  or  AN DDMMM ORGDST /BA
    const m = cmd.match(/(?:AN|AD|AV)\s*(\d{1,2}\w{3})?\s*([A-Z]{3})([A-Z]{3})?\s*(?:\/([A-Z]{2}))?/);

    let dateStr = m?.[1] ?? this.#todayAmadeus();
    let org = m?.[2] ?? 'LHR';
    let dst = m?.[3] ?? 'CDG';
    if (!m?.[3] && org.length === 6) { dst = org.slice(3); org = org.slice(0, 3); }
    const filterAl = m?.[4];

    let flights = this.#mockFlights.filter(f =>
      f.origin === org && f.dest === dst && (!filterAl || f.airline === filterAl)
    );

    if (flights.length === 0) {
      // Try reverse for return journey hint
      this.#emit('warning', `NO DIRECT SERVICES FOUND ${org}-${dst} ${dateStr}`);
      this.#emit('output', `CHECK ROUTING OR TRY CONNECTING CITIES.`);
      return;
    }

    this.#emit('separator', '─'.repeat(72));
    this.#emit('system', `${org} TO ${dst}  ${dateStr}  ${this.#nowTime()}`);
    this.#emit('separator', '─'.repeat(72));

    let i = 1;
    for (const f of flights) {
      const cls = Object.entries(f.classes)
        .map(([k, v]) => `${k}${v}`)
        .join(' ');
      const line = ` ${String(i++).padStart(2)} ${f.airline} ${f.num.padStart(4)} ${f.aircraft}  ${f.origin}${f.dest}  ${f.dep}  ${f.arr}  ${cls}`;
      this.#emit('output', line);
    }

    this.#emit('separator', '─'.repeat(72));
    this.#emit('output', `SELL: SS<n><CLASS><SEGMENT>  e.g.  SS1Y1  (1 PAX, Y CLASS, FLIGHT 1)`);
    this.#emit('separator', '');
  }

  #cmdSell(cmd: string): void {
    // SS1Y1  or  SS2C1
    const m = cmd.match(/S+(\d+)([A-Z])(\d+)/);
    if (!m) { this.#emit('error', 'FORMAT: SS<QTY><CLASS><SEG>  e.g. SS1Y1'); return; }

    const qty = parseInt(m[1]);
    const cls = m[2];
    const seg = parseInt(m[3]);

    // Find corresponding flight from last availability (simplified mock)
    const flights = this.#mockFlights.filter((_, idx) => idx === seg - 1);
    const f = this.#mockFlights[seg - 1] ?? this.#mockFlights[0];

    this.#emit('success', ` ${f.airline} ${f.num.padStart(4)} ${cls}  ${f.origin}${f.dest}  ${f.dep}-${f.arr}  HK${qty}`);
    this.#emit('output', `SEGMENT ${seg} CONFIRMED. ADD PASSENGER NAME: NM1SURNAME/FIRSTNAME MR`);

    // Start building PNR in session
    const existing = this.sessionPnr();
    if (!existing) {
      this.sessionPnr.set({
        rl: '',
        passengers: [],
        segments: [`${f.airline} ${f.num.padStart(4)} ${cls} ${dateStr()} LHRCDG HK${qty}   ${f.dep}  ${f.arr}  /DC${f.airline} /E`],
        contacts: [],
        ticketing: '',
        received: '',
      });
    } else {
      existing.segments.push(`${f.airline} ${f.num.padStart(4)} ${cls} ${dateStr()} ${f.origin}${f.dest} HK${qty}   ${f.dep}  ${f.arr}  /DC${f.airline} /E`);
      this.sessionPnr.set({ ...existing });
    }
  }

  #cmdName(cmd: string): void {
    // NM1SMITH/JOHN MR
    const m = cmd.match(/NM(\d+)([A-Z][A-Z\s\/]+)/);
    if (!m) { this.#emit('error', 'FORMAT: NM1SURNAME/FIRSTNAME TITLE  e.g. NM1SMITH/JOHN MR'); return; }

    const qty = parseInt(m[1]);
    const name = m[2].trim();
    this.#emit('success', ` ${qty}.${name}`);

    const pnr = this.sessionPnr();
    if (pnr) {
      pnr.passengers.push(`${pnr.passengers.length + 1}.${name}`);
      this.sessionPnr.set({ ...pnr });
    }
  }

  #cmdContact(cmd: string): void {
    const m = cmd.match(/AP[CE]?\s+(.+)/);
    const contact = m ? m[1] : cmd.slice(2);
    const type = cmd.startsWith('APCE') || cmd.startsWith('APE') ? 'APE' : 'APM';
    this.#emit('success', ` ${type} ${contact}`);

    const pnr = this.sessionPnr();
    if (pnr) {
      pnr.contacts.push(`${type} ${contact}`);
      this.sessionPnr.set({ ...pnr });
    }
  }

  #cmdReceived(cmd: string): void {
    const agent = cmd.slice(3).trim() || this.auth.currentUser()?.username ?? 'AGENT';
    this.#emit('success', ` RF ${agent}`);

    const pnr = this.sessionPnr();
    if (pnr) { this.sessionPnr.set({ ...pnr, received: `RF ${agent}` }); }
  }

  #cmdTicketing(cmd: string): void {
    const agent = this.auth.currentUser();
    const tk = `TK OK${this.#todayAmadeus()}/${agent?.station ?? 'LHR'}${agent?.agentId ?? ''}//ETBA`;
    this.#emit('success', ` ${tk}`);

    const pnr = this.sessionPnr();
    if (pnr) { this.sessionPnr.set({ ...pnr, ticketing: tk }); }
  }

  #cmdEndRetrieve(): void {
    const pnr = this.sessionPnr();
    if (!pnr) {
      this.#emit('warning', 'NO ACTIVE TRANSACTION — BUILD A PNR FIRST.');
      return;
    }

    if (!pnr.received) {
      this.#emit('error', 'MISSING RECEIVED FROM ELEMENT. ADD: RF <AGENT>');
      return;
    }

    // Generate record locator
    const rl = this.#genLocator();
    const saved = { ...pnr, rl };
    this.#mockPnrs[rl] = saved;
    this.sessionPnr.set(null);
    this.contextPnr.set(saved);

    this.#emit('separator', '─'.repeat(72));
    this.#emit('success', `RP/LHR${this.auth.currentUser()?.agentId ?? ''}  ${rl}`);
    this.#displayContextPnr();
  }

  #cmdEndTransact(): void {
    const pnr = this.contextPnr() ?? this.sessionPnr();
    if (!pnr) { this.#emit('warning', 'NO ACTIVE TRANSACTION.'); return; }

    const rl = pnr.rl || this.#genLocator();
    const final = { ...pnr, rl };
    this.#mockPnrs[rl] = final;
    this.sessionPnr.set(null);
    this.contextPnr.set(final);

    this.#emit('separator', '─'.repeat(72));
    this.#emit('success', `ETR BOOKING CONFIRMED — RECORD LOCATOR: ${rl}`);
    this.#emit('success', `TICKETS ISSUED. CONFIRMATION SENT TO PASSENGER.`);
    this.#emit('separator', '─'.repeat(72));
    this.#displayContextPnr();
  }

  #cmdRetrieve(locator: string): void {
    const rl = locator.trim().toUpperCase();
    if (!rl) { this.#emit('error', 'FORMAT: RT <RECORD LOCATOR>  e.g. RT ABC123'); return; }

    const pnr = this.#mockPnrs[rl];
    if (!pnr) { this.#emit('error', `PNR NOT FOUND — ${rl}`); return; }

    this.contextPnr.set(pnr);
    this.#displayContextPnr();
  }

  #displayContextPnr(): void {
    const pnr = this.contextPnr() ?? this.sessionPnr();
    if (!pnr) { this.#emit('warning', 'NO PNR IN CONTEXT.'); return; }

    const agent = this.auth.currentUser();
    const header = pnr.rl
      ? `RP/LHR${agent?.agentId ?? ''}  ${pnr.rl}  ${agent?.agentId ?? ''}`
      : `--- PNR IN BUILD ---`;

    this.#emit('separator', '─'.repeat(72));
    this.#emit('system', header);
    this.#emit('separator', '');
    for (const p of pnr.passengers) { this.#emit('output', p); }
    this.#emit('separator', '');
    let sn = 1;
    for (const s of pnr.segments) { this.#emit('output', `${sn++} ${s}`); }
    this.#emit('separator', '');
    for (const c of pnr.contacts) { this.#emit('output', c); }
    if (pnr.ticketing) { this.#emit('output', pnr.ticketing); }
    if (pnr.received) { this.#emit('output', pnr.received); }
    this.#emit('separator', '─'.repeat(72));
  }

  #cmdIgnore(): void {
    if (!this.sessionPnr()) { this.#emit('output', 'NO ACTIVE TRANSACTION.'); return; }
    this.sessionPnr.set(null);
    this.contextPnr.set(null);
    this.#emit('warning', 'TRANSACTION IGNORED — PNR NOT SAVED.');
  }

  #cmdCancel(cmd: string): void {
    const m = cmd.match(/X[EI](\d+)/);
    const n = m ? parseInt(m[1]) : null;
    if (!n) { this.#emit('error', 'FORMAT: XE<n>  e.g. XE2'); return; }

    const pnr = this.contextPnr() ?? this.sessionPnr();
    if (!pnr) { this.#emit('warning', 'NO PNR IN CONTEXT.'); return; }

    if (cmd.startsWith('XE')) {
      if (n <= pnr.segments.length) {
        const removed = pnr.segments.splice(n - 1, 1)[0];
        this.#emit('success', `ELEMENT ${n} CANCELLED: ${removed}`);
        if (this.contextPnr()) this.contextPnr.update(p => ({ ...p! }));
      } else { this.#emit('error', `ELEMENT ${n} NOT FOUND.`); }
    }
  }

  #cmdSeat(cmd: string): void {
    const m = cmd.match(/ST\s*(\d+)([A-K])/);
    if (!m) { this.#emit('error', 'FORMAT: RQSTST<SEG><SEAT>  e.g. RQSTST1A or ST1 23A'); return; }
    this.#emit('success', ` SEAT ${m[2]} REQUESTED — SEGMENT ${m[1]}`);
    this.#emit('output', `SEAT WILL BE CONFIRMED AT CHECK-IN.`);
  }

  #cmdOsi(cmd: string): void {
    this.#emit('success', ` ${cmd}`);
    this.#emit('output', `OSI/SSR ELEMENT ADDED.`);
  }

  #cmdFareQuote(cmd: string): void {
    const pnr = this.contextPnr() ?? this.sessionPnr();
    if (!pnr && !cmd.includes(' ')) {
      this.#emit('warning', 'NO ITINERARY IN CONTEXT. BUILD A PNR OR SPECIFY ROUTE.'); return;
    }

    this.#emit('separator', '─'.repeat(72));
    this.#emit('system', `FARE QUOTE  ${this.#nowTime()}`);
    this.#emit('separator', '─'.repeat(72));
    this.#emit('output', `FQBASIC ECONOMY   GBP  189.00  YOW / NON-REF / NO-CHG`);
    this.#emit('output', `FQFLEX ECONOMY    GBP  349.00  YFL / RFND 50 / CHG 60`);
    this.#emit('output', `FQBUSINESS CLASS  GBP  1289.00 CFL / RFND 100 / FREE CHG`);
    this.#emit('separator', '');
    this.#emit('output', `TAXES: GB APD GBP 13.00 + XY US PFC GBP 4.50 + YQ FUEL GBP 55.00`);
    this.#emit('output', `TOTAL ECONOMY (BASIC): GBP 261.50 PER PAX`);
    this.#emit('separator', '─'.repeat(72));
  }

  // ─── Helpers ─────────────────────────────────────────────────────────────────

  #emit(type: LineType, text: string): void {
    this.lines.update(l => [...l, { id: lineId++, type, text }]);
  }

  #scrollToBottom(): void {
    setTimeout(() => {
      const el = this.outputAreaRef?.nativeElement;
      if (el) el.scrollTop = el.scrollHeight;
    }, 0);
  }

  #todayAmadeus(): string {
    const d = new Date();
    const months = ['JAN','FEB','MAR','APR','MAY','JUN','JUL','AUG','SEP','OCT','NOV','DEC'];
    return `${String(d.getDate()).padStart(2,'0')}${months[d.getMonth()]}`;
  }

  #nowTime(): string {
    return new Date().toTimeString().slice(0, 5) + 'Z';
  }

  #genLocator(): string {
    const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
    return Array.from({ length: 6 }, () => chars[Math.floor(Math.random() * chars.length)]).join('');
  }
}

function dateStr(): string {
  const d = new Date();
  const months = ['JAN','FEB','MAR','APR','MAY','JUN','JUL','AUG','SEP','OCT','NOV','DEC'];
  return `${String(d.getDate()).padStart(2,'0')}${months[d.getMonth()]}`;
}
