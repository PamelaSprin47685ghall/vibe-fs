/**
 * diagnostics.js — Public surface for E2E failure diagnostics.
 *
 * Implementation split into two modules to honor the 200-line
 * Kolmogorov line budget:
 *   - diagnostics-collect.js: gatherDiagnostics() walks the scenario
 *     and produces a structured diagnostic record.
 *   - diagnostics-format.js: formatDiagnostics() renders the record
 *     as the human-readable console output.
 *
 * This file is the public entry point and the dumpDiagnostics()
 * orchestrator.
 */

import { gatherDiagnostics } from './diagnostics-collect.js';
import { formatDiagnostics } from './diagnostics-format.js';

export { gatherDiagnostics, formatDiagnostics };

/**
 * Dump diagnostics to console.error on test failure. Best-effort:
 * diagnostics-collection errors are surfaced, not rethrown.
 */
export async function dumpDiagnostics(scenario, err) {
  console.error(`\n${'═'.repeat(60)}`);
  console.error(`DIAGNOSTICS for: ${err.message}`);
  console.error(`${'═'.repeat(60)}`);
  try {
    const diag = await gatherDiagnostics(scenario);
    console.error(formatDiagnostics(diag));
  } catch (diagErr) {
    console.error(`Diagnostics collection failed: ${diagErr.message}`);
  }
  console.error(`${'═'.repeat(60)}\n`);
}
