import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

test('WHAT[verification-system-010] acceptance criteria only tighten — a failing gate propagates failure', async () => {
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

test('WHAT[verification-system-010] acceptance criteria only tighten — an unreadable gate is failure', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'proof-ladder-missing-'))
  try {
    const { main } = await import('../../../scripts/check.mjs')
    const exitCode = await main([], { checkList: [join(dir, 'checks/does-not-exist.mjs')] })
    assert.equal(exitCode, 1, 'unreadable gate must return 1')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})
