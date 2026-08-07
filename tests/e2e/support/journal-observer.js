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
