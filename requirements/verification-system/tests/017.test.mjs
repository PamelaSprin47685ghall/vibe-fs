import assert from 'node:assert/strict'
import { verify, verificationSteps } from '../../../scripts/verify.mjs'
import { existsSync, mkdirSync, mkdtempSync, readFileSync, readlinkSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { discoverIntegrationTests, discoverRepositoryIntegrationTests } from './support/discover-suite-tests.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')

const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

// These cases test orchestration. Input-set correctness has its own 016 probes;
// repeatedly hashing the whole developer checkout obscures this boundary.
function inputFixture(directory) {
  const root = join(directory, 'inputs')
  for (const name of ['src', 'scripts', 'requirements', 'resources']) {
    mkdirSync(join(root, name), { recursive: true })
  }
  writeFileSync(join(root, 'src/Input.fs'), 'module Input\nlet value = 1\n')
  return root
}

function assertRealEntry(fixtureRoot, command) {
  assert.ok(existsSync(resolve(ROOT, relative(fixtureRoot, command))), `real entry missing: ${command}`)
}

test('WHAT[verification-system-017] repository and package entries own distinct real integration suites', () => {
  const repositoryFiles = discoverRepositoryIntegrationTests(ROOT)
  const packageFiles = discoverIntegrationTests(join(ROOT, 'requirements/distribution/tests'))
  const files = [...repositoryFiles, ...packageFiles]
  assert.ok(repositoryFiles.length > 0)
  assert.ok(packageFiles.length > 0)
  assert.equal(new Set(files).size, files.length)
  assert.ok(files.every((file) => existsSync(file)))
  assert.ok(repositoryFiles.every((file) => !file.startsWith(join(ROOT, 'requirements/distribution/'))))
})

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

test('WHAT[verification-system-017] release adds one Long Stroke and package proof after a clean build', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'release-order-'))
  const root = inputFixture(directory)
  const calls = []
  try {
    const result = await verify({ root, release: true, logDirectory: directory, output: createMemorySink(),
      runStep: async ({ label, argv }) => {
        calls.push({ label, argv })
        return { label, ok: true, exitCode: 0, signal: null, durationMs: 0 }
      },
    })
    assert.equal(result.exitCode, 0)
    assert.deepEqual(calls.map(({ label }) => label), ['format:check', 'check', 'build', 'unit', 'integration', 'e2e', 'package'])
    assert.ok(calls.find(({ label }) => label === 'build').argv.includes('--clean'))
    for (const label of ['e2e', 'package']) assertRealEntry(root, calls.find((call) => call.label === label).argv[0])
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[verification-system-017] a failed stage stops the ladder without retry or a success result', async () => {
  for (const failed of ['unit', 'integration', 'e2e', 'package']) {
    const directory = mkdtempSync(join(tmpdir(), 'release-failure-'))
    const root = inputFixture(directory)
    const calls = []
    try {
      const result = await verify({ root, release: true, logDirectory: directory, output: createMemorySink(),
        runStep: async ({ label }) => {
          calls.push(label)
          return { label, ok: label !== failed, exitCode: label === failed ? 1 : 0, signal: null, durationMs: 0 }
        },
      })
      assert.equal(result.exitCode, 1)
      assert.equal(result.outcome, 'fail')
      const order = ['format:check', 'check', 'build', 'unit', 'integration', 'e2e', 'package']
      assert.deepEqual(calls, order.slice(0, order.indexOf(failed) + 1))
    } finally {
      rmSync(directory, { recursive: true, force: true })
    }
  }
})

test('WHAT[verification-system-017] daily verification executes each stage in order', async () => {
  const { scripts } = JSON.parse(read('package.json'))
  const command = scripts['format-build-test']
  assert.equal(typeof command, 'string', 'package.json scripts.format-build-test must exist')
  assert.equal(command, 'node scripts/verify.mjs', 'daily pipeline must dispatch to verify.mjs')

  const tmpLogDir = mkdtempSync(join(tmpdir(), 'proof-ladder-logs-'))
  const root = inputFixture(tmpLogDir)
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
      root,
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
    assertRealEntry(root, buildArgs[0])
  const unitArgs = spawned.find((s) => s.label === 'unit').argv
  assert.ok(unitArgs.some((a) => a.includes('requirements/verification-system/tests/run.mjs')))
    assertRealEntry(root, unitArgs[0])
  const integrationArgs = spawned.find((s) => s.label === 'integration').argv
  assert.ok(integrationArgs.some((a) => a.includes('tests/integration/run.mjs')))
    assertRealEntry(root, integrationArgs[0])

    assert.match(sink.output, /PASS  verify daily/)
    const afterLink = existsSync(defaultLatest) ? readlinkSync(defaultLatest) : null
    assert.equal(afterLink, beforeLink, 'default verify-logs/latest must not be modified')
  } finally {
    rmSync(tmpLogDir, { recursive: true, force: true })
  }
})

test('WHAT[verification-system-017] formal verification cannot inherit a narrowed test selection', () => {
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

test('WHAT[verification-system-017] profiled verification reports the stages actually executed', async () => {
  const tmpLogDir = mkdtempSync(join(tmpdir(), 'proof-ladder-profile-'))
  const root = inputFixture(tmpLogDir)
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
      root,
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
