import assert from 'node:assert/strict'
import { verify, verificationSteps } from '../../../scripts/verify.mjs'
import { checks } from '../../../scripts/check.mjs'
import { existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, readlinkSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { basename, dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')

const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

function createMemorySink() {
  let buf = ''
  return {
    write(chunk) {
      buf += chunk
    },
    get output() {
      return buf
    },
  }
  }

for (const failingLabel of ['format:check', 'check', 'build']) {
  test(`WHAT[verification-system-001] verify halts and marks subsequent steps not-run when ${failingLabel} fails`, async () => {
    const tmpLogDir = mkdtempSync(join(tmpdir(), 'proof-ladder-fail-'))
    const sink = createMemorySink()
  const spawned = []
  const fakeRunStep = async ({ label, argv }) => {
      spawned.push(label)
      if (label === failingLabel) {
        return { label, ok: false, exitCode: 1, signal: null, durationMs: 5 }
  }
      return { label, ok: true, exitCode: 0, signal: null, durationMs: 5 }
    }

    try {
      const result = await verify({
        release: false,
        runStep: fakeRunStep,
        output: sink,
        logDirectory: tmpLogDir,
})
      assert.equal(result.exitCode, 1)
      assert.equal(result.outcome, 'fail')
      assert.equal(spawned.at(-1), failingLabel, `execution must stop after ${failingLabel}`)

      const failedIdx = result.steps.findIndex((s) => s.label === failingLabel)
      assert.ok(failedIdx >= 0)
      assert.equal(result.steps[failedIdx].status, 'failed')
      for (let i = failedIdx + 1; i < result.steps.length; i++) {
        assert.equal(result.steps[i].status, 'not-run', `step ${result.steps[i].label} must be marked not-run`)
    }
      assert.match(sink.output, /FAIL  verify daily/)
    } finally {
      rmSync(tmpLogDir, { recursive: true, force: true })
    }
})
  }

test('WHAT[verification-system-001] format-build-test ladder pins the stage order', async () => {
  const { scripts } = JSON.parse(read('package.json'))
  const command = scripts['format-build-test']
  assert.equal(typeof command, 'string', 'package.json scripts.format-build-test must exist')
  assert.equal(command, 'node scripts/verify.mjs', 'daily pipeline must dispatch to verify.mjs')

  const tmpLogDir = mkdtempSync(join(tmpdir(), 'proof-ladder-logs-'))
  const sink = createMemorySink()
  const spawned = []
  const fakeRunStep = async ({ label, argv }) => {
    spawned.push({ label, argv: argv.map((arg) => String(arg)) })
    return { label, ok: true, exitCode: 0, signal: null, durationMs: 0 }
  }

  try {
    const defaultLatest = join(ROOT, '.fable-build/verify-logs/latest')
    const beforeLink = existsSync(defaultLatest) ? readlinkSync(defaultLatest) : null

    const result = await verify({
      release: false,
      runStep: fakeRunStep,
      output: sink,
      logDirectory: tmpLogDir,
})
    assert.equal(result.exitCode, 0, 'daily verify under a green step spy must succeed')
    assert.equal(result.outcome, 'pass')
    assert.ok(result.steps.every((s) => s.status === 'ok'))

  const labels = spawned.map((entry) => entry.label)
  assert.deepEqual(labels, [
    'format:check',
    'check',
    'build',
    'unit',
    'integration',
  ])

    const buildArgs = spawned.find((s) => s.label === 'build').argv
    assert.ok(buildArgs.some((a) => a.includes('scripts/build.mjs')), 'build must dispatch to build.mjs')
    assert.ok(existsSync(buildArgs[0]), 'build target must exist as real file')
  const unitArgs = spawned.find((s) => s.label === 'unit').argv
  assert.ok(unitArgs.some((a) => a.includes('requirements/verification-system/tests/run.mjs')))
    assert.ok(existsSync(unitArgs[0]), 'unit target must exist as real file')
  const integrationArgs = spawned.find((s) => s.label === 'integration').argv
  assert.ok(integrationArgs.some((a) => a.includes('tests/integration/run.mjs')))
    assert.ok(existsSync(integrationArgs[0]), 'integration target must exist as real file')

    assert.match(sink.output, /PASS  verify daily/)
    const afterLink = existsSync(defaultLatest) ? readlinkSync(defaultLatest) : null
    assert.equal(afterLink, beforeLink, 'default verify-logs/latest must not be modified')
  } finally {
    rmSync(tmpLogDir, { recursive: true, force: true })
  }
})

test('WHAT[verification-system-001] verify step env strips TESTS_MJS_FILES and propagates verbose flag', () => {
  const hostEnvWithOverride = {
    ...process.env,
    TESTS_MJS_FILES: 'requirements/verification-system/tests/fake.test.mjs',
  }

  const dailyStepsVerbose = verificationSteps({
    root: ROOT,
    release: false,
    verbose: true,
    env: hostEnvWithOverride,
  })
  const unitVerbose = dailyStepsVerbose.find((s) => s.label === 'unit')
  const integrationVerbose = dailyStepsVerbose.find((s) => s.label === 'integration')

  assert.ok(unitVerbose, 'unit step must exist')
  assert.ok(integrationVerbose, 'integration step must exist')
  assert.equal('TESTS_MJS_FILES' in unitVerbose.env, false, 'unit step env must not contain TESTS_MJS_FILES')
  assert.equal('TESTS_MJS_FILES' in integrationVerbose.env, false, 'integration step env must not contain TESTS_MJS_FILES')
  assert.equal(unitVerbose.env.NODE_TEST_VERBOSE, '1', 'unit step env must receive NODE_TEST_VERBOSE=1 when verbose=true')
  assert.equal(integrationVerbose.env.NODE_TEST_VERBOSE, '1', 'integration step env must receive NODE_TEST_VERBOSE=1 when verbose=true')
  assert.equal(unitVerbose.env.WXS_E2E_QUIET, '1', 'unit step env must keep WXS_E2E_QUIET')
  assert.equal(integrationVerbose.env.WXS_E2E_QUIET, '1', 'integration step env must keep WXS_E2E_QUIET')

  const dailyStepsNonVerbose = verificationSteps({
    root: ROOT,
    release: false,
    verbose: false,
    env: hostEnvWithOverride,
  })
  const unitNonVerbose = dailyStepsNonVerbose.find((s) => s.label === 'unit')
  assert.equal('NODE_TEST_VERBOSE' in unitNonVerbose.env, false, 'unit step env must not set NODE_TEST_VERBOSE when verbose=false')

  const releaseSteps = verificationSteps({
    root: ROOT,
    release: true,
    verbose: true,
    env: hostEnvWithOverride,
  })
  const e2e = releaseSteps.find((s) => s.label === 'e2e')
  const pkg = releaseSteps.find((s) => s.label === 'package')
  assert.ok(e2e && pkg)
  assert.equal('TESTS_MJS_FILES' in e2e.env, false, 'e2e step env must not contain TESTS_MJS_FILES')
  assert.equal('TESTS_MJS_FILES' in pkg.env, false, 'package step env must not contain TESTS_MJS_FILES')
})

test('WHAT[verification-system-001] verify --profile emits stage timings and returns profile array', async () => {
  const tmpLogDir = mkdtempSync(join(tmpdir(), 'proof-ladder-profile-'))
  const sink = createMemorySink()
  const fakeRunStep = async ({ label }) => ({
    label,
    ok: true,
    exitCode: 0,
    signal: null,
    durationMs: 25,
  })

  try {
    const result = await verify({
      release: false,
      verbose: false,
      profile: true,
      runStep: fakeRunStep,
      output: sink,
      logDirectory: tmpLogDir,
    })

    assert.equal(result.exitCode, 0)
    assert.ok(Array.isArray(result.profile), 'verify result must contain profile array')
    assert.equal(result.profile.length, 5)
    assert.deepEqual(
      result.profile.map((p) => p.stage),
      ['format:check', 'check', 'build', 'unit', 'integration'],
    )
    assert.ok(result.steps.every((s) => typeof s.wallMs === 'number'), 'each step must have wallMs')
    assert.match(sink.output, /PASS verify daily · 各阶段耗时:/)
    assert.match(sink.output, /format:check\s+25ms/)
  } finally {
    rmSync(tmpLogDir, { recursive: true, force: true })
  }
})
