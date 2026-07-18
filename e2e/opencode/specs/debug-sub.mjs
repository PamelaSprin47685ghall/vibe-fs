import { runScenario } from '../harness/scenario.js';
import nudgeSubTests from './p0-canary-tests-nudge-sub.js';

const subTest = nudgeSubTests.find((t) => t.name.includes('OC-SUB-001'));

const exitCode = await runScenario({
  plugin: true,
  timeoutMs: 120000,
  contextLimit: 100000,
  allowSynthetic: true,
  allowTitleGen: true,
}, [subTest]);
process.exit(exitCode);
