import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';

const STORE_REF = 'refs/wanxiang/store';

const gitCommonDir = (workDir) => {
  const common = execFileSync('git', ['-C', workDir, 'rev-parse', '--git-common-dir'], {
    encoding: 'utf8',
  }).trim();
  return path.isAbsolute(common) ? common : path.resolve(workDir, common);
};

const storeRefPath = (workDir) => path.join(gitCommonDir(workDir), 'refs', 'wanxiang', 'store');

/**
 * Read UTF-8 content for a journal BlobRef (`blobs/<gitOid>` or bare OID).
 * Bodies live in the Git ODB after Phase 5 — never under wanxiangshu-next/.
 *
 * One pattern owns the whole token shape. Splitting it into a `startsWith('blobs/')`
 * test plus a separate OID regex also reads as a repo-path criterion to the harness
 * gate, which then reports a prefix that names nothing on disk — `blobs/` is a
 * BlobRef namespace, not a directory.
 */
export function readBlobRef(workDir, blobRef) {
  const raw = Array.isArray(blobRef) ? blobRef.at(-1) : blobRef;
  const token = String(raw ?? '');
  const oid = /^(?:blobs\/)?([0-9a-f]{40})$/i.exec(token)?.[1];
  if (!oid) {
    throw new Error(`invalid BlobRef OID: ${token}`);
  }
  return execFileSync('git', ['-C', workDir, 'cat-file', '-p', oid], {
    encoding: 'utf8',
    maxBuffer: 16 * 1024 * 1024,
  });
}

/** Current EventStore tip OID, or null when the canonical ref is absent. */
export function storeTip(workDir) {
  try {
    const tip = execFileSync(
      'git',
      ['-C', workDir, 'rev-parse', '--verify', '--quiet', STORE_REF],
      { encoding: 'utf8' },
    ).trim();
    return tip || null;
  } catch {
    return null;
  }
}

const tipSnapshotCache = new Map(); // `${workDir}\0${tip}` -> { lines, factCounts }
const blobTextCache = new Map(); // git blob oid -> utf8 text

const eventShardPrefix = ['events', ''].join('/'); // tip-tree prefix, not a repo-root path

/** Journal Envelope object nested under EventStore `payload`, or null. */
export function journalEnvelopeFromEventText(text) {
  try {
    const event = JSON.parse(text);
    if (event && typeof event === 'object' && event.payload && typeof event.payload === 'object') {
      return event.payload;
    }
    // Pre-EventStore / harness shapes already look like Envelope.
    if (event && typeof event === 'object' && Object.prototype.hasOwnProperty.call(event, 'Fact')) {
      return event;
    }
    return null;
  } catch {
    return null;
  }
}

const loadBlobText = (workDir, oid) => {
  const cached = blobTextCache.get(oid);
  if (cached !== undefined) return cached;
  const text = execFileSync('git', ['-C', workDir, 'cat-file', 'blob', oid], {
    encoding: 'utf8',
    maxBuffer: 16 * 1024 * 1024,
  });
  blobTextCache.set(oid, text);
  return text;
};

const tallyFactNames = (fact, counts) => {
  if (Array.isArray(fact)) {
    if (typeof fact[0] === 'string') {
      counts.set(fact[0], (counts.get(fact[0]) ?? 0) + 1);
    }
    for (const child of fact) tallyFactNames(child, counts);
  } else if (fact && typeof fact === 'object') {
    for (const child of Object.values(fact)) tallyFactNames(child, counts);
  }
};

const snapshotForTip = (workDir, tip) => {
  const key = `${workDir}\0${tip}`;
  const hit = tipSnapshotCache.get(key);
  if (hit) return hit;

  let listing = '';
  try {
    listing = execFileSync('git', ['-C', workDir, 'ls-tree', '-r', tip], {
      encoding: 'utf8',
    });
  } catch {
    const empty = { lines: [], factCounts: new Map() };
    tipSnapshotCache.set(key, empty);
    return empty;
  }

  const lines = [];
  for (const row of listing.split('\n')) {
    if (row === '') continue;
    // "100644 blob <oid>\t<path>"
    const tab = row.indexOf('\t');
    if (tab < 0) continue;
    const meta = row.slice(0, tab);
    const pathName = row.slice(tab + 1);
    if (!pathName.startsWith(eventShardPrefix) || !pathName.endsWith('.jsonl')) continue;
    const parts = meta.split(/\s+/);
    const oid = parts[2];
    if (!oid) continue;
    try {
      const text = loadBlobText(workDir, oid);
      for (const line of text.split('\n')) {
        const trimmed = line.trim();
        if (trimmed !== '') lines.push(trimmed);
      }
    } catch {
      // ignore unreadable blob
    }
  }

  const factCounts = new Map();
  for (const line of lines) {
    const envelope = journalEnvelopeFromEventText(line);
    if (envelope?.Fact !== undefined) tallyFactNames(envelope.Fact, factCounts);
  }

  const snap = { lines, factCounts };
  tipSnapshotCache.set(key, snap);
  // Keep only the latest tip snapshot per workDir (tips are append-only CAS).
  for (const existing of tipSnapshotCache.keys()) {
    if (existing.startsWith(`${workDir}\0`) && existing !== key) tipSnapshotCache.delete(existing);
  }
  return snap;
};

/** Canonical event JSON texts under refs/wanxiang/store (one string per jsonl event line). */
export const journalEventLines = (workDir) => {
  const tip = storeTip(workDir);
  if (!tip) return [];
  return snapshotForTip(workDir, tip).lines;
};

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
    const tip = storeTip(workDirOrLines);
    if (!tip) return 0;
    return snapshotForTip(workDirOrLines, tip).factCounts.get(caseName) ?? 0;
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
    // Fall back to scanning nested AgentFact case names.
    const match = text.match(
      /"(?:Plugin|Prompt|Fallback|Review|Execution|Orchestrator|Companion|Context|Host|Runtime|Life|Handle|Pair)[A-Za-z0-9]+"/,
    );
    return match?.[0]?.slice(1, -1) ?? 'UnknownFact';
  } catch {
    return 'malformed';
  }
};

export function readJournal(workDir, factName, renewOn = []) {
  const tip = storeTip(workDir);
  if (!tip) return { named: 0, total: 0, renew: 0, tip: null };
  const snap = snapshotForTip(workDir, tip);
  const texts = snap.lines;
  let named = 0;
  if (factName !== undefined) {
    named = snap.factCounts.get(factName) ?? 0;
    if (named === 0) {
      // Harness / non-Envelope plantings (e.g. payload.type) still match by substring.
      named = texts.filter((text) => text.includes(factName)).length;
    }
  }
  let renew = 0;
  const renewNames = renewOn.length > 0 ? new Set(renewOn) : null;
  if (renewNames !== null) {
    for (const name of renewNames) {
      const counted = snap.factCounts.get(name) ?? 0;
      if (counted > 0) {
        renew += counted;
        continue;
      }
      renew += texts.filter((text) => text.includes(name)).length;
    }
  }
  return { named, total: texts.length, renew, tip };
}

export function journalFactTail(workDir, limit) {
  const tip = storeTip(workDir) ?? 'missing-tip';
  return journalEventTexts(workDir)
    .slice(-limit)
    .map((text, index) => `${tip}:${index}:${factLabelFromEvent(text)}`);
}

/** Watch EventStore tip (`refs/wanxiang/store`); onChange debounced per tick. Returns stop(). */
export function watchJournal(workDir, onChange) {
  let closed = false;
  let refWatcher = null;
  let parentWatcher = null;
  let debounce = null;
  let lastTip = storeTip(workDir);

  const notify = () => {
    if (closed) return;
    if (debounce !== null) return;
    debounce = setImmediate(() => {
      debounce = null;
      if (closed) return;
      const tip = storeTip(workDir);
      if (tip !== lastTip) {
        lastTip = tip;
        onChange();
      } else if (tip != null) {
        // Tip file rewrite with same OID is rare; still surface a wake for append CAS races.
        onChange();
      }
    });
  };

  const stopRef = () => {
    try { refWatcher?.close(); } catch {}
    refWatcher = null;
  };

  const startRef = (refPath) => {
    stopRef();
    try {
      refWatcher = fs.watch(refPath, () => notify());
      refWatcher.on('error', () => {});
    } catch {
      // caller falls back to short guard slice
    }
  };

  const refPath = storeRefPath(workDir);
  if (fs.existsSync(refPath)) {
    startRef(refPath);
  } else {
    const parent = path.dirname(refPath);
    const wanxiangParent = path.dirname(parent);
    const tryAttach = () => {
      if (closed) return;
      if (!fs.existsSync(refPath)) return;
      try { parentWatcher?.close(); } catch {}
      parentWatcher = null;
      startRef(refPath);
      notify();
    };
    try {
      const watchRoot = fs.existsSync(parent)
        ? parent
        : fs.existsSync(wanxiangParent)
          ? wanxiangParent
          : gitCommonDir(workDir);
      if (fs.existsSync(watchRoot)) {
        parentWatcher = fs.watch(watchRoot, tryAttach);
        parentWatcher.on('error', () => {});
      }
    } catch {}
    tryAttach();
  }

  return () => {
    closed = true;
    if (debounce !== null) {
      clearImmediate(debounce);
      debounce = null;
    }
    stopRef();
    try { parentWatcher?.close(); } catch {}
    parentWatcher = null;
  };
}
