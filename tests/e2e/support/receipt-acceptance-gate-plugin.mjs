import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { WAIT_FACT_WINDOW_MS } from './time-budget.js';

const directory = process.env.WANXIANGSHU_E2E_ACCEPTANCE_GATE_DIR;
const pluginPath = process.env.WANXIANGSHU_E2E_ACCEPTANCE_GATE_PLUGIN;
const config = JSON.parse(process.env.WANXIANGSHU_E2E_ACCEPTANCE_GATE_CONFIG ?? '{}');

if (!directory || !pluginPath) throw new Error('receipt acceptance gate requires directory and plugin path');

const releaseDeadlineMs = Number(config.releaseDeadlineMs ?? WAIT_FACT_WINDOW_MS);
if (!Number.isFinite(releaseDeadlineMs) || releaseDeadlineMs <= 0) {
  throw new Error(`receipt acceptance gate releaseDeadlineMs must be positive: ${config.releaseDeadlineMs}`);
}

const metadataOf = (input, output) => [
  input?.metadata,
  output?.metadata,
  output?.message?.metadata,
  ...(output?.parts ?? []).map((part) => part?.metadata),
].find((metadata) => metadata?.wanxiangshu_origin === config.origin);

const artifactFile = (name, index) => path.join(directory, `${name}-${index}.json`);
const releaseFile = (index) => artifactFile('release', index);
const settlementFile = (index) => artifactFile('settled', index);
const failureFile = (index) => artifactFile('failure', index);

const waitForRelease = (index) => {
  if (fs.existsSync(releaseFile(index))) return Promise.resolve();
  return new Promise((resolve, reject) => {
    let watcher;
    let timer;
    let settled = false;
    const finish = (error) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      try { watcher?.close(); } catch {}
      if (error) reject(error);
      else resolve();
    };
    const released = () => {
      if (fs.existsSync(releaseFile(index))) finish();
    };

    try {
      watcher = fs.watch(directory, { persistent: false }, (_event, changed) => {
        if (changed !== `release-${index}.json`) return;
        released();
      });
      timer = setTimeout(
        () => finish(new Error(`receipt acceptance gate release ${index} timed out`)),
        releaseDeadlineMs,
      );
      released();
    } catch (error) {
      finish(error);
    }
  });
};

const recordFailure = (index, error) => {
  const failure = { error: String(error?.stack ?? error) };
  fs.writeFileSync(failureFile(index), JSON.stringify(failure));
  fs.writeFileSync(settlementFile(index), JSON.stringify({ status: 'failed', ...failure }));
};

const releaseAndAccept = async (index, accept, messageInput, messageOutput) => {
  try {
    await waitForRelease(index);
    await accept?.(messageInput, messageOutput);
    fs.writeFileSync(settlementFile(index), JSON.stringify({ status: 'released' }));
  } catch (error) {
    recordFailure(index, error);
  }
};

const wrapped = await import(pathToFileURL(pluginPath).href);
const production = wrapped.default;
let occurrence = 0;

export default {
  id: 'wanxiangshu-e2e-receipt-acceptance-gate',
  async server(input) {
    const hooks = await production.server(input);
    const accept = hooks['chat.message'];

    return {
      ...hooks,
      'chat.message': async (messageInput, messageOutput) => {
        const metadata = metadataOf(messageInput, messageOutput);
        if (!metadata) return accept?.(messageInput, messageOutput);

        const index = ++occurrence;
        const mode = config.plan?.[index - 1];
        if (!mode) return accept?.(messageInput, messageOutput);

        fs.writeFileSync(
          artifactFile('hold', index),
          JSON.stringify({
            mode,
            origin: metadata.wanxiangshu_origin,
            promptKey: metadata.wanxiangshu_prompt_key,
            sessionID: messageInput?.sessionID,
          }),
        );

        if (mode !== 'defer') {
          const error = new Error(`unsupported receipt acceptance gate mode: ${mode}`);
          recordFailure(index, error);
          throw error;
        }

        void releaseAndAccept(index, accept, messageInput, messageOutput);
      },
    };
  },
};
