/** orchestrator-restart-publish — two linear scenarios, one driver */
import { runCanary } from '../support/scenario-driver.mjs';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../support/index.js';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('orchestrator-restart-publish scenario static gate failed');
}
let code = await runCanary('orchestrator-restart-publish');
if (code !== 0) process.exit(code);
code = await runCanary('orchestrator-restart-publish-conflict');
process.exit(code);
