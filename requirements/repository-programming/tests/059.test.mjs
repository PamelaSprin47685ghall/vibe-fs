import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { execute, caseName } from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'
import {
  deliveredValue,
  clearDeliveredValue,
} from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-delivered-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const program = (body) => `class Js extends JsProgram {
  async run() {
${body}
  }
}`

const runProgram = (dir, body, sessionId) =>
  execute(dir, 'Manager', 'en', sessionId, { program: program(body) }, 3000, Date.now() + 60_000, 1 << 20, null)

test('WHAT[repository-programming-059] js.deliver records the handed-over value for the completion path', async () => {
  const { dir, cleanup } = sandbox()
  const sessionId = `sess-delivered-${Math.random().toString(36).slice(2, 8)}`
  try {
    clearDeliveredValue(sessionId)
    const outcome = await runProgram(
      dir,
      `    await this.deliver({ summary: "indexed the parser", files: 3 });
    return { unreachable: true };`,
      sessionId,
    )
    assert.equal(caseName(outcome), 'Succeeded')
    const delivered = deliveredValue(sessionId)
    assert.equal(delivered.summary, 'indexed the parser')
    assert.equal(delivered.files, 3)
  } finally {
    clearDeliveredValue(sessionId)
    cleanup()
  }
})

test('WHAT[repository-programming-059] an ordinary return leaves the delivered cell untouched', async () => {
  const { dir, cleanup } = sandbox()
  const sessionId = `sess-return-${Math.random().toString(36).slice(2, 8)}`
  try {
    clearDeliveredValue(sessionId)
    const outcome = await runProgram(dir, `    return { plain: true };`, sessionId)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.equal(deliveredValue(sessionId), null, 'a plain return is not a delivery')
  } finally {
    clearDeliveredValue(sessionId)
    cleanup()
  }
})
