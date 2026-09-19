// requirements/verification-system/tests/integration/run.mjs — sole integration child/warmup orchestrator.

import { spawnSync } from 'node:child_process'
import { readdirSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { WATCHDOG_TIMEOUT_MS, PROJECT_CHECK_TIMEOUT_MS } from '../e2e/support/time-budget.js'
import { superviseNodeTest } from '../e2e/support/supervise-node-test.mjs'

process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'

const here = path.dirname(fileURLToPath(import.meta.url))
const root = path.resolve(here, '../../../..')

// ── output layer: one line per group, details only on failure ───────────────
//
// Node-step children (superviseNodeTest) keep inherited stdio — their compact
// reporter is already summary-only — and each step adds a single ✓/✗ line with
// its wall time. Spawned children (warmup, package/harness runners) are piped:
// success collapses to one ✓ line, failure prints the captured tail.
const fmtSecs = (ms) => `${(ms / 1000).toFixed(1)}s`
const tailLines = (text, n = 40) => String(text ?? '').split('\n').slice(-n).join('\n')
const summarizeChildCounts = (output) => {
  const m = String(output ?? '').match(/(\d+) passed, (\d+) failed[^\n]*/)
  return m ? `, ${m[0].trim()}` : ''
}

const suiteStarted = Date.now()
let okGroups = 0
const failedGroups = []

// Cold opencode binary on a fresh machine / GHA pays multi-second first-launch cost.
// Warm once before any step that may spawn Host or load Host-adjacent paths.
{
  const warm = spawnSync(process.execPath, [path.join(root, 'scripts/warmup-opencode.mjs')], {
    cwd: root,
    stdio: ['ignore', 'pipe', 'pipe'],
    encoding: 'utf8',
    env: process.env,
  })
  if (warm.status !== 0) {
    console.error(`✗ integration: opencode warmup (exit ${warm.status ?? 1})`)
    console.error(tailLines(`${warm.stdout ?? ''}\n${warm.stderr ?? ''}`))
    process.exit(warm.status === null ? 1 : warm.status)
  }
}

const INTEGRATION_PER_TEST_TIMEOUT_MS = Math.max(
  Number(process.env.PER_TEST_TIMEOUT_MS) || 0,
  15_000,
)

const childSteps = [
  {
    label: 'package/run.mjs (distribution)',
    args: [path.join(root, 'requirements/distribution/tests/integration/package/run.mjs')],
  },
  {
    label: 'harness/run.mjs (verification-system)',
    args: [path.join(root, 'requirements/verification-system/tests/integration/harness/run.mjs')],
  },
]

// Suites are discovered dynamically across all requirement packages' top-level tests/*.test.mjs.
// Distribution is excluded here because it runs via its own child step package/run.mjs.
const requirementsDir = path.join(root, 'requirements')
const suites = readdirSync(requirementsDir, { withFileTypes: true })
  .filter((entry) => entry.isDirectory() && entry.name !== 'distribution')
  .sort((a, b) => a.name.localeCompare(b.name))
  .flatMap((entry) => {
    const testsDir = path.join(requirementsDir, entry.name, 'tests')
    try {
      return readdirSync(testsDir)
        .filter((name) => name.endsWith('.test.mjs'))
        .sort()
        .map((name) => path.join(testsDir, name))
        .filter((file) => {
          const text = readFileSync(file, 'utf8')
          return text.includes('integrationTest') || text.includes('WXS_TIER_INTEGRATION')
        })
    } catch {
      return []
    }
  })

const isDryRun = process.argv.includes('--dry-run') || process.argv.includes('--print')
if (isDryRun) {
  console.log('integration: dry run')
  console.log(`  discovered suites: ${suites.length} files`)
  for (const file of suites) {
    console.log(`    ${path.relative(root, file).split(path.sep).join('/')}`)
  }
  for (const step of childSteps) console.log(`  child: ${step.label}`)
  process.exit(0)
}

if (suites.length > 0) {
  const stepStart = Date.now()
  const hasCompilerTests = suites.some((f) => f.includes('structured-workflow'))
  const perTestTimeoutMs = hasCompilerTests ? PROJECT_CHECK_TIMEOUT_MS : INTEGRATION_PER_TEST_TIMEOUT_MS
  try {
    await superviseNodeTest({
      files: suites,
      label: 'tests/integration/suites',
      silenceMs: Math.max(WATCHDOG_TIMEOUT_MS, perTestTimeoutMs + 5_000),
      logPrefix: 'integration:suites',
      env: {
        ...process.env,
        WXS_TIER_INTEGRATION: '1',
        PER_TEST_TIMEOUT_MS: String(perTestTimeoutMs),
      },
      throwOnFailure: true,
    })
    okGroups++
    console.log(`✓ suites (${suites.length} files, ${fmtSecs(Date.now() - stepStart)})`)
  } catch (err) {
    failedGroups.push({ label: 'suites', err })
    console.error(`✗ suites (${fmtSecs(Date.now() - stepStart)}): ${err.message}`)
  }
}

for (const step of childSteps) {
  const childStart = Date.now()
  const result = spawnSync(process.execPath, step.args, {
    cwd: root,
    stdio: ['ignore', 'pipe', 'pipe'],
    encoding: 'utf8',
    env: process.env,
  })
  const elapsed = Date.now() - childStart
  if (result.status === 0) {
    okGroups++
    console.log(`✓ ${step.label} (${fmtSecs(elapsed)}${summarizeChildCounts(result.stdout)})`)
  } else {
    const failed = result.status === null ? 1 : result.status
    failedGroups.push({ label: step.label, exitCode: failed, stdout: result.stdout, stderr: result.stderr })
    console.error(`✗ ${step.label} (exit ${failed}, ${fmtSecs(elapsed)})`)
    if (result.stdout) console.error(`--- stdout tail ---\n${tailLines(result.stdout, 30)}`)
    if (result.stderr) console.error(`--- stderr tail ---\n${tailLines(result.stderr, 30)}`)
  }
}

const totalSecs = fmtSecs(Date.now() - suiteStarted)
if (failedGroups.length === 0) {
  console.log(`\nintegration: ${okGroups} groups passed (${totalSecs})`)
  process.exit(0)
} else {
  console.error(`\nintegration: ${failedGroups.length} failed, ${okGroups} passed (${totalSecs})`)
  process.exit(1)
}
