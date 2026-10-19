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
  clearParkedSites,
} from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-limit-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const program = (body) => `class Js extends JsProgram {
  async run() {
${body}
    return { done: true };
  }
}`

// Each parked program parks once and stays parked; the site table is shared
// across calls in the same process (module-level, PLAN S2 step 3).
const runProgram = (dir, body, sessionId = 'sess-036') =>
  execute(dir, 'Coder', 'en', sessionId, { program: program(body) }, 2000, Date.now() + 60_000, 1 << 20, null)

test('WHAT[repository-programming-036] the parked-site table refuses beyond the concurrency ceiling with CONCURRENCY_LIMIT', async () => {
  const { dir, cleanup } = sandbox()
  try {
    // Clear the process-live table so earlier tests do not leak parked sites
    // into this ceiling test.
    clearParkedSites()

    // The ceiling is 8 (PLAN S2 step 3 default). Park 8 programs, then the
    // 9th park must fail with CONCURRENCY_LIMIT (no queue).
    const parked = []
    for (let i = 0; i < 8; i += 1) {
      const outcome = await runProgram(dir, `    await this.park({ i: ${i} });\n    return { i: ${i} };`)
      assert.equal(caseName(outcome), 'Parked', `park ${i} should succeed`)
      parked.push(parkedHandle(outcome))
    }

    const ninth = await runProgram(dir, `    await this.park({ i: 8 });\n    return { i: 8 };`)
    assert.equal(caseName(ninth), 'Failed')
    assert.equal(failureCode(ninth), 'CONCURRENCY_LIMIT')

    // Resolving one parked site frees a slot: the next park succeeds.
    const abandoned = await execute(dir, 'Coder', 'en', 'sess-036', { abandon: { handle: parked[0] } }, 2000, Date.now() + 60_000, 1 << 20, null)
    assert.equal(caseName(abandoned), 'Failed', 'abandon ends with Failed (Abandoned)')

    const afterFree = await runProgram(dir, `    await this.park({ i: 9 });\n    return { i: 9 };`)
    assert.equal(caseName(afterFree), 'Parked', 'a freed slot admits a new park')
  } finally {
    cleanup()
  }
})
