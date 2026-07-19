import fs from 'node:fs';
import { readNdjsonLines } from './p0-canary-utils.js';

export async function waitForCondition(read, predicate, timeoutMs) {
  const deadline = Date.now() + timeoutMs;

  while (Date.now() <= deadline) {
    const value = read();
    if (predicate(value)) return value;
    await new Promise((resolve) => setImmediate(resolve));
  }

  throw new Error(`Timed out waiting for condition after ${timeoutMs}ms`);
}

export async function waitForNdjson(workDir, predicate, timeoutMs) {
  const check = () => {
    const events = readNdjsonLines(workDir);
    return predicate(events) ? events : null;
  };

  const initial = check();
  if (initial) return initial;

  return new Promise((resolve, reject) => {
    let finished = false;
    const watcher = fs.watch(workDir, { persistent: false }, (_event, filename) => {
      if (finished || (filename && filename.toString() !== '.wanxiangshu.ndjson')) return;
      try {
        const events = check();
        if (events) finish(null, events);
      } catch (error) {
        finish(error);
      }
    });

    const timer = setTimeout(() => {
      const events = readNdjsonLines(workDir);
      finish(new Error(`Timed out waiting for NDJSON condition after ${timeoutMs}ms; kinds=${events.map((e) => e.Kind).join(',')}`));
    }, timeoutMs);

    const finish = (error, value) => {
      if (finished) return;
      finished = true;
      clearTimeout(timer);
      watcher.close();
      if (error) reject(error);
      else resolve(value);
    };

    watcher.on('error', finish);
  });
}
