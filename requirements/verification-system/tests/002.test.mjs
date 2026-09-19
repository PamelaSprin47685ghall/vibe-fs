import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { existsSync, mkdtempSync, mkdirSync, writeFileSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const { E2E_ROOT_REL, SOLE_ENTRY, e2eTestCaseFiles, scanE2EWatchdogFeed } = await import("./e2e/support/watchdog-feed-scan.mjs");

const makeTempRoot = (layout) => {
  const root = mkdtempSync(join(tmpdir(), 'e2e-wdf-fc-'))
  const e2e = join(root, E2E_ROOT_REL)
  if (layout.e2eDir !== false) mkdirSync(e2e, { recursive: true })
  for (const name of layout.files ?? []) {
    writeFileSync(join(e2e, name), '// throwaway\n')
  }
  if (layout.e2eIsFile) {
    rmSync(e2e, { recursive: true, force: true })
    writeFileSync(e2e, 'not a directory\n')
  }
  return root
}
const cleanup = (root) => rmSync(root, { recursive: true, force: true })

test('WHAT[verification-system-002] sole top-level e2e entry is 014.test.mjs', () => {
  // One World：第 4 层恰好一个真实 E2E 入口。顶层文件清单必须包含
  // tests/e2e/014.test.mjs（唯一 Long Stroke）。
  const files = e2eTestCaseFiles()

  assert.ok(
    files.some((file) => file.endsWith('/tests/e2e/014.test.mjs') || file.endsWith('tests/e2e/014.test.mjs')),
    'expected top-level sole entry e2e/014.test.mjs (verification-system package) in scope',
  )
})
test('WHAT[verification-system-002] missing sole 014.test.mjs fails closed', () => {
  // One World：第 4 层恰好一个真实 E2E 入口。e2e dir exists but the sole
  // entry is absent → must throw, not report green with the other files.
  const root = makeTempRoot({ files: ['other.test.mjs'] })
  try {
    assert.throws(
      () => e2eTestCaseFiles(root),
      new RegExp(`missing sole top-level e2e entry ${SOLE_ENTRY}`),
      'missing sole 014.test.mjs must fail closed (throw)',
    )
  } finally {
    cleanup(root)
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { verify, verificationSteps } = await import("../../../scripts/verify.mjs");
const { checks } = await import("../../../scripts/check.mjs");
const { existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, readlinkSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { basename, dirname, join, resolve } = await import("node:path");
const { fileURLToPath } = await import("node:url");
const { default: test } = await import("node:test");

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

test('WHAT[verification-system-002] release ladder includes clean build, exactly one e2e and one package step', async () => {
  const tmpLogDir = mkdtempSync(join(tmpdir(), 'proof-ladder-release-'))
  const sink = createMemorySink()
  const spawned = []
  const fakeRunStep = async ({ label, argv }) => {
    spawned.push({ label, argv: argv.map((arg) => String(arg)) })
    return { label, ok: true, exitCode: 0, signal: null, durationMs: 0 }
  }

  try {
    const { exitCode, outcome, steps } = await verify({
      release: true,
      runStep: fakeRunStep,
      output: sink,
      logDirectory: tmpLogDir,
})
  assert.equal(exitCode, 0)
    assert.equal(outcome, 'pass')
    assert.ok(steps.every((s) => s.status === 'ok'))

  const releaseLabels = spawned.map((entry) => entry.label)
    assert.deepEqual(releaseLabels, [
    'format:check',
    'check',
    'build',
    'unit',
    'integration',
      'e2e',
      'package',
  ])

    const buildCall = spawned.find((s) => s.label === 'build')
    assert.ok(buildCall.argv.includes('--clean'), 'release build argv must contain --clean')

    const e2eCalls = spawned.filter((s) => s.label === 'e2e')
    assert.equal(e2eCalls.length, 1, 'release must have exactly one e2e step')
    assert.ok(existsSync(e2eCalls[0].argv[0]), 'e2e target must exist as real file')

    const packageCalls = spawned.filter((s) => s.label === 'package')
    assert.equal(packageCalls.length, 1, 'release must have exactly one package step')
    assert.ok(existsSync(packageCalls[0].argv[0]), 'package target must exist as real file')

    assert.match(sink.output, /PASS  verify release/)
  } finally {
    rmSync(tmpLogDir, { recursive: true, force: true })
  }
})
}
