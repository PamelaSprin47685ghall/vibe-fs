import { runScenario } from '../harness/scenario.js';
import nudgeSubTests from './p0-canary-tests-nudge-sub.js';

const exitCode = await runScenario({
  plugin: true,
  timeoutMs: 120000,
  contextLimit: 100000,
  allowSynthetic: true,
  allowTitleGen: true,
}, nudgeSubTests);
process.exit(exitCode);
