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
  test(`WHAT[VERIFICATION-SYSTEM-001] verify halts and marks subsequent steps not-run when ${failingLabel} fails`, async () => {
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

test('WHAT[VERIFICATION-SYSTEM-010] acceptance criteria only tighten — a failing gate propagates failure', async () => {
  // 行为面：导入 check.mjs 的 main/runChecks，以失败 gate 测试，必须返回非零退出码。
  const dir = mkdtempSync(join(tmpdir(), 'proof-ladder-fail-'))
  try {
    const checksDir = join(dir, 'checks')
    mkdirSync(checksDir)
    writeFileSync(join(checksDir, 'failing.mjs'), 'export function check() { return { issues: [{ code: "fail", message: "boom" }] } }\n')
    const { main } = await import('../../../scripts/check.mjs')
    const exitCode = await main([], { checkList: [join(checksDir, 'failing.mjs')] })
    assert.equal(exitCode, 1, 'failing gate must return nonzero exit code')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[VERIFICATION-SYSTEM-010] acceptance criteria only tighten — an unreadable gate is failure', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'proof-ladder-missing-'))
  try {
    const { main } = await import('../../../scripts/check.mjs')
    const exitCode = await main([], { checkList: [join(dir, 'checks/does-not-exist.mjs')] })
    assert.equal(exitCode, 1, 'unreadable gate must return 1')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})
