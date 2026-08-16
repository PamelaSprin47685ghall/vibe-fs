// Zone-debt regression fixture (TASK.md §8, JS-SEMANTIC-SURFACE-002).
//
// DELIBERATELY VIOLATES the boundary: a support/fixtures file that
// deep-imports an internal dist module and reads `.fields`.
//
// The js-boundary-gate must scan the WHOLE semantic-test zone
// (support/**/*.mjs, fixtures/**/*.mjs, *-contract.mjs), not only
// *.test.mjs — otherwise forbidden knowledge simply moves one directory
// deeper and the gate is theater. surface-charter.test.mjs asserts this
// fixture is visible to the scanner and carries debt.
//
// Never imported by any test; it exists to be scanned.

import { FinalityWorkflow } from '../../../../dist/Mission/Finality/Workflow.js'

export const leak = (outcome) => outcome.fields[0]
