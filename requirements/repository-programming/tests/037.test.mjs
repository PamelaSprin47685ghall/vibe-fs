import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { parse as parseToml } from 'smol-toml'
import {
  execute,
  caseName,
  parkedHandle,
  parkedPayload,
  render,
} from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-parkrender-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const program = (body) => `class Js extends JsProgram {
  async run() {
${body}
    return { done: true };
  }
}`

const runProgram = (dir, body, sessionId = 'sess-037') =>
  execute(dir, 'Coder', 'en', sessionId, { program: program(body) }, 2000, Date.now() + 60_000, 1 << 20, null)

test('WHAT[repository-programming-037] a parked outcome renders as a top-level parked payload with handle and payload fields', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const outcome = await runProgram(dir, `    const decision = await this.park({ stage: 'review', count: 3 });
    return { decided: decision.value };`)
    assert.equal(caseName(outcome), 'Parked')
    const handle = parkedHandle(outcome)

    const doc = parseToml(render(outcome))
    assert.equal(doc.handle, handle)
    assert.equal(doc.stage, 'review')
    assert.equal(doc.count, 3)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-037] the parked payload is the program data, not a host verdict', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const outcome = await runProgram(dir, `    await this.park({ question: 'proceed?', choices: ['yes', 'no'] });`)
    assert.equal(caseName(outcome), 'Parked')
    assert.deepEqual(parkedPayload(outcome), { question: 'proceed?', choices: ['yes', 'no'] })
  } finally {
    cleanup()
  }
})
