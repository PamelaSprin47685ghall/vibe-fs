import fs from 'node:fs';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';

const commonDirCache = new Map(); // workDir -> git common dir (immutable per checkout)

const gitCommonDir = (workDir) => {
  const cached = commonDirCache.get(workDir);
  if (cached !== undefined) return cached;
  const common = execFileSync('git', ['-C', workDir, 'rev-parse', '--git-common-dir'], {
    encoding: 'utf8',
  }).trim();
  const resolved = path.isAbsolute(common) ? common : path.resolve(workDir, common);
  commonDirCache.set(workDir, resolved);
  return resolved;
};

const eventsDir = (workDir) => path.join(gitCommonDir(workDir), 'wanxiang', 'events');
const payloadsDir = (workDir) => path.join(gitCommonDir(workDir), 'wanxiang', 'payloads');

/**
 * Read UTF-8 content for a journal BlobRef (`blobs/<PayloadRef>` or bare PayloadRef).
 * Runtime payload truth is local and content-addressed under `.git/wanxiang/payloads`;
 * Git OIDs only exist later at the independent remote-sync hook boundary.
 */
export function readBlobRef(workDir, blobRef) {
  const raw = Array.isArray(blobRef) ? blobRef.at(-1) : blobRef;
  const token = String(raw ?? '');
  const digest = /^(?:blobs\/)?([0-9a-f]{64})$/i.exec(token)?.[1];
  if (!digest) throw new Error(`invalid local BlobRef payload digest: ${token}`);
  return fs.readFileSync(path.join(payloadsDir(workDir), digest), 'utf8');
}

/** Journal Envelope object nested under EventStore `payload`, or null. */
export function journalEnvelopeFromEventText(text) {
  try {
    const event = JSON.parse(text);
    if (event && typeof event === 'object' && event.payload && typeof event.payload === 'object') {
      return event.payload;
    }
    if (event && typeof event === 'object' && Object.prototype.hasOwnProperty.call(event, 'Fact')) {
      return event;
    }
    return null;
  } catch {
    return null;
  }
}

const tallyFactNames = (fact, counts) => {
  if (Array.isArray(fact)) {
    if (typeof fact[0] === 'string') counts.set(fact[0], (counts.get(fact[0]) ?? 0) + 1);
    for (const child of fact) tallyFactNames(child, counts);
  } else if (fact && typeof fact === 'object') {
    for (const child of Object.values(fact)) tallyFactNames(child, counts);
  }
};

const readLocalSnapshot = (workDir) => {
  const directory = eventsDir(workDir);
  if (!fs.existsSync(directory)) return { lines: [], factCounts: new Map(), token: null };

  const byEventId = new Map();
  const files = fs.readdirSync(directory)
    .filter((name) => name.endsWith('.ndjson'))
    .sort();

  for (const name of files) {
    const file = path.join(directory, name);
    let text;
    try {
      text = fs.readFileSync(file, 'utf8');
    } catch {
      // A concurrently created writer may disappear only under test cleanup.
      // Observation is best-effort; the next directory wake retries.
      continue;
    }

    const completeLength = text.endsWith('\n') ? text.length : text.lastIndexOf('\n') + 1;
    const complete = completeLength > 0 ? text.slice(0, completeLength) : '';

    for (const line of complete.split('\n')) {
      if (line === '') continue;
      let parsed;
      try {
        parsed = JSON.parse(line);
      } catch {
        // A production durable writer only publishes canonical complete lines.
        // If an observer catches bytes during an OS write, retry on the next wake.
        continue;
      }
      const eventId = parsed?.event_id;
      if (typeof eventId !== 'string' || eventId.length === 0) continue;
      const existing = byEventId.get(eventId);
      if (existing !== undefined && existing !== line) {
        throw new Error(`journal observer saw EventId identity collision: ${eventId}`);
      }
      byEventId.set(eventId, line);
    }
  }

  const lines = [...byEventId.entries()]
    .sort(([left], [right]) => left.localeCompare(right))
    .map(([, text]) => text);

  const factCounts = new Map();
  for (const line of lines) {
    const envelope = journalEnvelopeFromEventText(line);
    if (envelope?.Fact !== undefined) tallyFactNames(envelope.Fact, factCounts);
  }

  const token = lines.length === 0
    ? null
    : createHash('sha256').update(lines.join('\n')).digest('hex');

  return { lines, factCounts, token };
};

/**
 * Local durable snapshot token used only by the E2E observer diagnostics/wake logic.
 * It is deliberately NOT a Git object/ref identity.
 */
export function storeTip(workDir) {
  return readLocalSnapshot(workDir).token;
}

/** Canonical event JSON texts from every local process writer file. */
export const journalEventLines = (workDir) => readLocalSnapshot(workDir).lines;
const journalEventTexts = journalEventLines;

/**
 * Payloads of the named fact case wherever it nests inside EventStore journal envelopes.
 * Accepts either workDir or an array of event JSON texts / Envelope-like objects.
 */
export function factPayloads(workDirOrLines, caseName) {
  const lines = typeof workDirOrLines === 'string' ? journalEventLines(workDirOrLines) : workDirOrLines;
  const found = [];
  const walk = (value) => {
    if (Array.isArray(value)) {
      if (typeof value[0] === 'string' && value[0] === caseName) found.push(value[1]);
      for (const item of value) walk(item);
    } else if (value && typeof value === 'object') {
      for (const child of Object.values(value)) walk(child);
    }
  };
  for (const line of lines) {
    const envelope =
      typeof line === 'string'
        ? journalEnvelopeFromEventText(line)
        : line && typeof line === 'object'
          ? line.payload && typeof line.payload === 'object'
            ? line.payload
            : line
          : null;
    if (envelope) walk(envelope.Fact);
  }
  return found;
}

export const countFactCase = (workDirOrLines, caseName) => {
  if (typeof workDirOrLines === 'string') {
    return readLocalSnapshot(workDirOrLines).factCounts.get(caseName) ?? 0;
  }
  return factPayloads(workDirOrLines, caseName).length;
};

const digFactLabel = (fact) => {
  if (typeof fact === 'string') return fact;
  if (!Array.isArray(fact) || typeof fact[0] !== 'string') return null;
  if (fact.length >= 2 && Array.isArray(fact[1]) && typeof fact[1][0] === 'string') {
    return digFactLabel(fact[1]) ?? fact[0];
  }
  return fact[0];
};

const factLabelFromEvent = (text) => {
  try {
    const envelope = journalEnvelopeFromEventText(text);
    const label = digFactLabel(envelope?.Fact);
    if (label) return label;
    const match = text.match(
      /"(?:Plugin|Prompt|Fallback|Review|Execution|Orchestrator|Companion|Context|Host|Runtime|Life|Handle|Pair)[A-Za-z0-9]+"/,
    );
    return match?.[0]?.slice(1, -1) ?? 'UnknownFact';
  } catch {
    return 'malformed';
  }
};

export function readJournal(workDir, factName, renewOn = []) {
  const snap = readLocalSnapshot(workDir);
  const texts = snap.lines;
  let named = 0;
  if (factName !== undefined) {
    named = snap.factCounts.get(factName) ?? 0;
    if (named === 0) named = texts.filter((text) => text.includes(factName)).length;
  }
  let renew = 0;
  if (renewOn.length > 0) {
    for (const name of new Set(renewOn)) {
      const counted = snap.factCounts.get(name) ?? 0;
      renew += counted > 0 ? counted : texts.filter((text) => text.includes(name)).length;
    }
  }
  return { named, total: texts.length, renew, tip: snap.token };
}

export function journalFactTail(workDir, limit) {
  const tip = storeTip(workDir) ?? 'missing-local-truth';
  return journalEventTexts(workDir)
    .slice(-limit)
    .map((text, index) => `${tip}:${index}:${factLabelFromEvent(text)}`);
}

/**
 * Watch `.git/wanxiang/events` directly. The directory may not exist at watcher
 * creation time, so attach upward and descend when runtime truth appears.
 */
export function watchJournal(workDir, onChange) {
  let closed = false;
  let eventsWatcher = null;
  let parentWatcher = null;
  let debounce = null;
  let lastTip = storeTip(workDir);

  const notify = () => {
    if (closed || debounce !== null) return;
    debounce = setImmediate(() => {
      debounce = null;
      if (closed) return;
      const tip = storeTip(workDir);
      if (tip !== lastTip) {
        lastTip = tip;
        onChange();
      } else {
        // Directory/file watchers can coalesce or race an append. Surface the wake;
        // expectation code rechecks durable facts and remains the correctness owner.
        onChange();
      }
    });
  };

  const stopEvents = () => {
    try { eventsWatcher?.close(); } catch {}
    eventsWatcher = null;
  };

  const attachEvents = () => {
    if (closed || eventsWatcher !== null) return;
    const directory = eventsDir(workDir);
    if (!fs.existsSync(directory)) return;
    try {
      eventsWatcher = fs.watch(directory, notify);
      eventsWatcher.on('error', () => {});
      try { parentWatcher?.close(); } catch {}
      parentWatcher = null;
      notify();
    } catch {}
  };

  attachEvents();
  if (eventsWatcher === null) {
    const common = gitCommonDir(workDir);
    const wanxiang = path.join(common, 'wanxiang');
    const watchRoot = fs.existsSync(wanxiang) ? wanxiang : common;
    try {
      parentWatcher = fs.watch(watchRoot, attachEvents);
      parentWatcher.on('error', () => {});
    } catch {}
    attachEvents();
  }

  return () => {
    closed = true;
    if (debounce !== null) {
      clearImmediate(debounce);
      debounce = null;
    }
    stopEvents();
    try { parentWatcher?.close(); } catch {}
    parentWatcher = null;
  };
}
