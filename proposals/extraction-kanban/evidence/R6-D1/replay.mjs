import { resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawnSync } from 'node:child_process'

const repository = resolve(fileURLToPath(new URL('../../../../', import.meta.url)))
const processFiles = ['004', '006', '009', '010', '011', '013', '014', '015', '016']
  .map(number => `requirements/process-execution/tests/${number}.test.mjs`)
const changeFiles = ['003', '005', '008']
  .map(number => `requirements/change-integration/tests/${number}.test.mjs`)
const files = [...processFiles, ...changeFiles]
const phase = process.argv[2]

if (phase === 'changed') {
  const result = spawnSync(process.execPath, ['requirements/verification-system/tests/run.mjs'], {
    cwd: repository,
    stdio: 'inherit',
    env: { ...process.env, WXS_TIER_INTEGRATION: '1', TESTS_MJS_FILES: files.join(',') },
  })
  if (result.error) throw result.error
  process.exitCode = result.status ?? 1
} else if (phase === 'integration') {
  process.chdir(repository)
  const { superviseNodeTest } = await import('../../../../requirements/verification-system/tests/e2e/support/supervise-node-test.mjs')
  const { WATCHDOG_TIMEOUT_MS, PROJECT_CHECK_TIMEOUT_MS } = await import('../../../../requirements/verification-system/tests/e2e/support/time-budget.js')
  await superviseNodeTest({
    files: [
      'requirements/process-execution/tests/012.test.mjs',
      'requirements/change-integration/tests/005.test.mjs',
      'requirements/change-integration/tests/008.test.mjs',
      'requirements/durable-convergence/tests/008.test.mjs',
      'requirements/durable-convergence/tests/009.test.mjs',
      'requirements/prefix-stability/tests/014.test.mjs',
    ],
    label: 'D1 supplemental integration',
    silenceMs: Math.max(WATCHDOG_TIMEOUT_MS, PROJECT_CHECK_TIMEOUT_MS + 5000),
    env: { ...process.env, WXS_TIER_INTEGRATION: '1', PER_TEST_TIMEOUT_MS: String(PROJECT_CHECK_TIMEOUT_MS) },
    logPrefix: 'd1:integration',
  })
} else {
  throw new Error('Choose changed or integration. Build the checkout before running either phase.')
}
