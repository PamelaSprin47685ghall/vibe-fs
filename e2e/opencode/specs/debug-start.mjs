import { ProcessHost } from '../harness/process-host.js';
import { StrictMockProvider } from '../harness/strict-mock-provider.js';
import { createIsolatedEnv } from '../harness/isolated-env.js';
import { resolvePluginPath } from '../harness/scenario-paths.js';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';

const scenarioDir = fs.mkdtempSync(path.join(os.tmpdir(), 'oc-dbg-'));
const workDir = path.join(scenarioDir, 'workspace');
fs.mkdirSync(workDir, { recursive: true });

const provider = new StrictMockProvider();
const providerUrl = await provider.start();
const host = new ProcessHost();

try {
  await host.start({
    scenarioDir,
    providerUrl: `${providerUrl}/v1`,
    pluginPaths: [resolvePluginPath('opencode')],
    contextLimit: 20000,
  });
  console.log('host baseUrl', host.baseUrl);
  const res = await fetch(`${host.baseUrl}/event`, {
    headers: { 'Accept': 'text/event-stream', 'x-opencode-directory': workDir },
  });
  const body = await res.text();
  console.log('event status', res.status, 'body', body.slice(0, 200));
  const apiRes = await fetch(`${host.baseUrl}/api/session`, { method: 'GET' });
  console.log('api/session status', apiRes.status, await apiRes.text());
} catch (e) {
  console.error('ERR', e.message);
} finally {
  console.log('--- STDOUT ---');
  console.log(host.stdoutLog);
  console.log('--- STDERR ---');
  console.log(host.stderrLog);
  try { host.stop({ assert: false }); } catch {}
  try { provider.stop(); } catch {}
}
