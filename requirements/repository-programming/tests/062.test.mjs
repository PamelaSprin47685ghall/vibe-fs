// LC-009: 跨 session unpark/abandon 互斥与拒绝守卫
import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import {
  execute,
  caseName,
  parkedHandle,
  failureCode,
  failureReason,
  clearParkedSites,
} from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-lc009-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const program = (body) => `class Js extends JsProgram {
  async run() {
${body}
    return { done: true };
  }
}`

test('WHAT[repository-programming-062] session A park handle cannot be unparked or abandoned by session B, but A can abandon', async () => {
  const { dir, cleanup } = sandbox()
  try {
    clearParkedSites()

    // Session A parks and gets handle
    const parked = await execute(
      dir,
      'Coder',
      'en',
      'sess-lc009-a',
      { program: program(`    await this.park({ reason: 'testing-lc-009' });`) },
      2000,
      Date.now() + 60_000,
      1 << 20,
      null,
    )
    assert.equal(caseName(parked), 'Parked')
    const handle = parkedHandle(parked)
    assert.ok(typeof handle === 'string' && handle.length > 0)

    // Session B unpark must fail with another session refusal
    const unparkB = await execute(
      dir,
      'Coder',
      'en',
      'sess-lc009-b',
      { unpark: { handle, value: { approved: true } } },
      2000,
      Date.now() + 60_000,
      1 << 20,
      null,
    )
    assert.equal(caseName(unparkB), 'Failed')
    assert.equal(failureCode(unparkB), 'INVALID_ARGUMENT')
    assert.match(failureReason(unparkB), /another session/)

    // Session B abandon must fail with another session refusal
    const abandonB = await execute(
      dir,
      'Coder',
      'en',
      'sess-lc009-b',
      { abandon: { handle } },
      2000,
      Date.now() + 60_000,
      1 << 20,
      null,
    )
    assert.equal(caseName(abandonB), 'Failed')
    assert.equal(failureCode(abandonB), 'INVALID_ARGUMENT')
    assert.match(failureReason(abandonB), /another session/)

    // Session A itself can still abandon its own handle
    const abandonA = await execute(
      dir,
      'Coder',
      'en',
      'sess-lc009-a',
      { abandon: { handle } },
      2000,
      Date.now() + 60_000,
      1 << 20,
      null,
    )
    assert.equal(caseName(abandonA), 'Failed')
    assert.equal(failureCode(abandonA), 'ABANDONED')
  } finally {
    clearParkedSites()
    cleanup()
  }
})
