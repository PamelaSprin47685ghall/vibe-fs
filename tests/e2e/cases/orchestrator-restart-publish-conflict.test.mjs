/**
 * orchestrator-restart-publish-conflict — the same publish path when the target branch has
 * moved under the Manager, so the publish must rebase rather than fast-forward.
 *
 * Split from `orchestrator-restart-publish.test.mjs` for scheduling, not for scope: the runner
 * bounds parallelism per case FILE, so a second scenario behind the same file serializes two
 * Host lifetimes inside one pool slot.
 */
import { runCanary } from '../support/scenario-driver.mjs';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../support/index.js';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('orchestrator-restart-publish-conflict scenario static gate failed');
}

process.exit(await runCanary('orchestrator-restart-publish-conflict'));
