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

/** Canonical event JSON texts under refs/wanxiang/store (events/**/*.jsonl). */
export const journalEventLines = (workDir) => {
  const tip = storeTip(workDir);
  if (!tip) return [];
  let listing = '';
  try {
    listing = execFileSync('git', ['-C', workDir, 'ls-tree', '-r', '--name-only', tip], {
      encoding: 'utf8',
    });
  } catch {
    return [];
  }
  return listing
    .split('\n')
    .filter((entry) => entry.startsWith('events/') && entry.endsWith('.jsonl'))
    .map((entry) => {
      try {
        return execFileSync('git', ['-C', workDir, 'show', `${tip}:${entry}`], {
          encoding: 'utf8',
          maxBuffer: 16 * 1024 * 1024,
        });
      } catch {
        return '';
      }
    })
    .filter((text) => text.trim() !== '');
};

const journalEventTexts = journalEventLines;

const factLabelFromEvent = (text) => {
  try {
    const event = JSON.parse(text);
    const payload = event?.payload;
    const fact = payload?.Fact;
    if (Array.isArray(fact) && typeof fact[0] === 'string') return fact[0];
    if (Array.isArray(fact) && Array.isArray(fact[1]) && typeof fact[1][0] === 'string') {
      return fact[1][0];
    }
    if (typeof fact === 'string') return fact;
    // Fall back to scanning nested AgentFact case names.
    const match = text.match(/"(?:Prompt|Fallback|Review|Execution|Orchestrator|Companion|Context|Host|Runtime)[^"]*"|([A-Z][A-Za-z0-9]+)/);
    return match?.[1] ?? 'UnknownFact';
  } catch {
    return 'malformed';
  }
};

export function readJournal(workDir, factName, renewOn = []) {
  const texts = journalEventTexts(workDir);
  let named = 0;
  let renew = 0;
  const renewNames = renewOn.length > 0 ? new Set(renewOn) : null;
  for (const text of texts) {
    if (factName !== undefined && text.includes(factName)) named += 1;
    if (renewNames !== null) {
      for (const name of renewNames) {
        if (text.includes(name)) {
          renew += 1;
          break;
        }
      }
    }
  }
  return { named, total: texts.length, renew, tip: storeTip(workDir) };
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
