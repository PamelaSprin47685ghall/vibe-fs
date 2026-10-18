import { resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { superviseNodeTest } from '../../../../requirements/verification-system/tests/e2e/support/supervise-node-test.mjs'
import { PROJECT_CHECK_TIMEOUT_MS, WATCHDOG_TIMEOUT_MS } from '../../../../requirements/verification-system/tests/e2e/support/time-budget.js'

const groups = {
  binary: ['requirements/process-execution/tests/017.test.mjs'],
  storage: [
    'requirements/durable-events/tests/005.test.mjs',
    'requirements/durable-events/tests/017.test.mjs',
    'requirements/durable-convergence/tests/008.test.mjs',
    'requirements/durable-convergence/tests/009.test.mjs',
    'requirements/durable-convergence/tests/010.test.mjs',
    'requirements/durable-convergence/tests/011.test.mjs',
  ],
  closure: [
    'requirements/process-execution/tests/012.test.mjs',
    'requirements/durable-events/tests/022.test.mjs',
  ],
  host: ['requirements/prefix-stability/tests/014.test.mjs'],
}
const phase = process.argv[2]
if (!groups[phase]) throw new Error('Choose binary, storage, closure, or host; build this checkout first')
process.chdir(resolve(fileURLToPath(new URL('../../../../', import.meta.url))))
await superviseNodeTest({
  files: groups[phase],
  label: `D2 ${phase}`,
  silenceMs: Math.max(WATCHDOG_TIMEOUT_MS, PROJECT_CHECK_TIMEOUT_MS + 5000),
  env: { ...process.env, WXS_TIER_INTEGRATION: '1', PER_TEST_TIMEOUT_MS: String(PROJECT_CHECK_TIMEOUT_MS) },
  logPrefix: `d2:${phase}`,
})
