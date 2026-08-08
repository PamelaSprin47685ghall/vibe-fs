import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';

const runtimeDirectory = (workDir) => {
  const common = execFileSync('git', ['-C', workDir, 'rev-parse', '--git-common-dir'], {
    encoding: 'utf8',
  }).trim();
  return path.join(
    path.isAbsolute(common) ? common : path.resolve(workDir, common),
    'wanxiangshu-next',
    'runtimes',
  );
};

const journalLines = (workDir) => {
  const directory = runtimeDirectory(workDir);
  if (!fs.existsSync(directory)) return [];
  return fs.readdirSync(directory)
    .filter((file) => file.endsWith('.ndjson'))
    .sort()
    .flatMap((file) => fs.readFileSync(path.join(directory, file), 'utf8')
      .split('\n')
      .filter((line) => line.trim() !== '')
      .map((line) => ({ file, line })));
};

export function readJournal(workDir, factName, renewOn = []) {
  const directory = runtimeDirectory(workDir);
  if (!fs.existsSync(directory)) return { named: 0, total: 0, renew: 0 };

  let named = 0;
  let total = 0;
  let renew = 0;
  const renewNames = renewOn.length > 0 ? new Set(renewOn) : null;
  for (const file of fs.readdirSync(directory)) {
    if (!file.endsWith('.ndjson')) continue;
    for (const line of fs.readFileSync(path.join(directory, file), 'utf8').split('\n')) {
      if (line.trim() === '') continue;
      total += 1;
      if (factName !== undefined && line.includes(factName)) named += 1;
      if (renewNames !== null) {
        for (const name of renewNames) {
          if (line.includes(name)) {
            renew += 1;
            break;
          }
        }
      }
    }
  }
  return { named, total, renew };
}

export function journalFactTail(workDir, limit) {
  return journalLines(workDir).slice(-limit).map(({ file, line }) => {
    try {
      const envelope = JSON.parse(line);
      const fact = envelope?.Fact?.[1]?.[0] ?? 'UnknownFact';
      const sequence = envelope?.LocalSeq?.[1] ?? '?';
      return `${file}:${sequence}:${fact}`;
    } catch {
      return `${file}:malformed`;
    }
  });
}

/** Watch runtime *.ndjson; onChange debounced to one call per tick. Returns stop(). */
export function watchJournal(workDir, onChange) {
  let closed = false;
  let dirWatcher = null;
  let parentWatcher = null;
  let debounce = null;

  const notify = () => {
    if (closed) return;
    if (debounce !== null) return;
    debounce = setImmediate(() => {
      debounce = null;
      if (!closed) onChange();
    });
  };

  const stopDir = () => {
    try { dirWatcher?.close(); } catch {}
    dirWatcher = null;
  };

  const startDir = (directory) => {
    stopDir();
    try {
      dirWatcher = fs.watch(directory, (_event, file) => {
        if (file == null || file.endsWith('.ndjson')) notify();
      });
      dirWatcher.on('error', () => {});
    } catch {
      // caller falls back to short guard slice
    }
  };

  const directory = runtimeDirectory(workDir);
  if (fs.existsSync(directory)) {
    startDir(directory);
  } else {
    const parent = path.dirname(directory);
    const tryAttach = () => {
      if (closed) return;
      if (!fs.existsSync(directory)) return;
      try { parentWatcher?.close(); } catch {}
      parentWatcher = null;
      startDir(directory);
      notify();
    };
    try {
      if (fs.existsSync(parent)) {
        parentWatcher = fs.watch(parent, tryAttach);
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
    stopDir();
    try { parentWatcher?.close(); } catch {}
    parentWatcher = null;
  };
}
