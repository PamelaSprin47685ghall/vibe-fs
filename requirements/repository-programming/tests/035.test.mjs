import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync, readFileSync, existsSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import {
  execute,
  caseName,
  parkedHandle,
} from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-cantpark-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const program = (body) => `class Js extends JsProgram {
  async run() {
${body}
    return { done: true };
  }
}`

const runProgram = (dir, body, sessionId = 'sess-035') =>
  execute(dir, 'Coder', 'en', sessionId, { program: program(body) }, 2000, Date.now() + 60_000, 1 << 20, null)

const abandon = (dir, handle, sessionId = 'sess-035') =>
  execute(dir, 'Coder', 'en', sessionId, { abandon: { handle } }, 2000, Date.now() + 60_000, 1 << 20, null)

test('WHAT[repository-programming-035] park during the abandon cleanup window is refused with CANNOT_PARK_WHILE_ABANDONING', async () => {
  const { dir, cleanup } = sandbox()
  try {
    // The finally tries to park again. Park must be refused during the
    // cleanup window; the refusal is an `{ ok: false, code:
    // CANNOT_PARK_WHILE_ABANDONING }` result, which the program records by
    // writing a marker file (as-is writes stay on disk even during cleanup).
    const body = `    try {
      await this.park({ stage: 'first' });
    } finally {
      const again = await this.park({ stage: 'again' });
      if (again && again.ok === false && again.code === 'CANNOT_PARK_WHILE_ABANDONING') {
        this.write('refused.txt', again.code);
      }
    }
    return { survived: true };`
    const parked = await runProgram(dir, body)
    assert.equal(caseName(parked), 'Parked')

    await abandon(dir, parkedHandle(parked))

    assert.equal(readFileSync(join(dir, 'refused.txt'), 'utf8'), 'CANNOT_PARK_WHILE_ABANDONING', 'park was refused during the cleanup window')
  } finally {
    cleanup()
  }
})
