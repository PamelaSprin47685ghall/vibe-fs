import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { TEARDOWN_IDLE_MS, WAIT_FACT_WINDOW_MS } from './time-budget.js';

export const receiptAcceptanceGatePluginPath = fileURLToPath(
  new URL('./receipt-acceptance-gate-plugin.mjs', import.meta.url),
);

const artifactFile = (directory, name, index) => path.join(directory, `${name}-${index}.json`);

const validIndex = (index) => {
  if (!Number.isInteger(index) || index < 1) {
    throw new Error(`receipt acceptance gate index must be a positive integer: ${index}`);
  }
  return index;
};

const readArtifact = (file) =>
  fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : null;

const failureError = (index, failure) =>
  new Error(`receipt acceptance gate ${index} failed: ${failure?.error ?? JSON.stringify(failure)}`);

export class ReceiptAcceptanceGate {
  constructor(directory) {
    this.directory = directory;
  }

  hold(index) {
    return readArtifact(artifactFile(this.directory, 'hold', validIndex(index)));
  }

  release(index) {
    const token = { index: validIndex(index) };
    fs.writeFileSync(artifactFile(this.directory, 'release', index), JSON.stringify(token));
    return token;
  }

  settlement(index) {
    return readArtifact(artifactFile(this.directory, 'settled', validIndex(index)));
  }

  failure(index) {
    return readArtifact(artifactFile(this.directory, 'failure', validIndex(index)));
  }

  awaitHold(index, timeoutMs = WAIT_FACT_WINDOW_MS) {
    return this.#await(index, timeoutMs, `hold ${index}`, () => this.hold(index));
  }

  awaitRelease(index, timeoutMs = WAIT_FACT_WINDOW_MS) {
    return this.#await(index, timeoutMs, `release ${index}`, () => {
      const settled = this.settlement(index);
      if (!settled) return undefined;
      if (settled.status !== 'released') throw failureError(index, settled);
      return settled;
    });
  }

  assertHealthy() {
    const failures = fs
      .readdirSync(this.directory)
      .filter((file) => /^failure-\d+\.json$/.test(file))
      .map((file) => readArtifact(path.join(this.directory, file)));
    if (failures.length > 0) throw failureError('deferred', failures[0]);
  }

  async cleanup(timeoutMs = TEARDOWN_IDLE_MS) {
    const held = fs
      .readdirSync(this.directory)
      .map((file) => /^hold-(\d+)\.json$/.exec(file))
      .filter(Boolean)
      .map((match) => Number(match[1]));
    for (const index of held) this.release(index);
    await Promise.all(held.map((index) => this.awaitRelease(index, timeoutMs)));
    this.assertHealthy();
  }

  #await(index, timeoutMs, label, select) {
    validIndex(index);
    return new Promise((resolve, reject) => {
      let watcher;
      let timer;
      let settled = false;
      const finish = (error, value) => {
        if (settled) return;
        settled = true;
        clearTimeout(timer);
        try { watcher?.close(); } catch {}
        if (error) reject(error);
        else resolve(value);
      };
      const observe = () => {
        try {
          const failure = this.failure(index);
          if (failure) return finish(failureError(index, failure));
          const value = select();
          if (value !== undefined && value !== null) finish(null, value);
        } catch (error) {
          finish(error);
        }
      };

      try {
        watcher = fs.watch(this.directory, { persistent: false }, observe);
        timer = setTimeout(
          () => finish(new Error(`receipt acceptance gate did not ${label}`)),
          timeoutMs,
        );
        observe();
      } catch (error) {
        finish(error);
      }
    });
  }
}
