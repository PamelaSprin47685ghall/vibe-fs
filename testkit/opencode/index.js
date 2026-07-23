/**
 * index.js — Public re-exports for testkit/opencode harness.
 */

export { createIsolatedEnv } from './isolated-env.js';
export { ProcessHost } from './process-host.js';
export { StrictMockProvider, extractToolNames } from './strict-mock-provider.js';
export { EventProbe } from './event-probe.js';
export { Scenario, setupScenario, teardownScenario, runScenario, FsOracle, HttpClient, getSessionId } from './scenario.js';
export { gatherDiagnostics, formatDiagnostics, dumpDiagnostics } from './diagnostics.js';
export { runStaticGate, runStabilityGate } from './stability-checker.js';
