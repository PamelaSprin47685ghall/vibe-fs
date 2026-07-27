/** orchestrator-restart-publish — two linear scripts, one driver */
import { runCanary } from '../canary-driver.mjs';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../index.js';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('orchestrator-restart-publish canary static gate failed');
}
let code = await runCanary('orchestrator-restart-publish.json');
if (code !== 0) process.exit(code);
code = await runCanary('orchestrator-restart-publish-conflict.json');
process.exit(code);
