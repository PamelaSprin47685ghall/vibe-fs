import { spawnSync } from 'node:child_process'
import { resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const files = Array.from({ length: 12 }, (_, index) =>
  `requirements/requirement-grounding/tests/${String(index + 1).padStart(3, '0')}.test.mjs`)
files.push(
  'requirements/repository-programming/tests/008.test.mjs',
  'requirements/repository-programming/tests/009.test.mjs',
)
const result = spawnSync(process.execPath, ['requirements/verification-system/tests/run.mjs'], {
  cwd: resolve(fileURLToPath(new URL('../../../../', import.meta.url))),
  stdio: 'inherit',
  env: { ...process.env, WXS_TIER_INTEGRATION: '1', TESTS_MJS_FILES: files.join(',') },
})
if (result.error) throw result.error
process.exitCode = result.status ?? 1
