import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import {
  execute,
  caseName,
  parkedHandle,
  parkedPayload,
  render,
  clearParkedSites,
} from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-run-send-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const program = (body) => `class Js extends JsProgram {
  async run() {
${body}
  }
}`

let runCount = 0
const runProgram = (dir, body, sessionId = 'sess-058') => {
  runCount += 1
  return execute(dir, 'DevOps', 'en', sessionId, { name: `run-058-${runCount}`, program: program(body) }, 20_000, Date.now() + 120_000, 1 << 20, null)
}

test('WHAT[repository-programming-058] send to an unknown child fails with CALL_CHILD_UNKNOWN', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const outcome = await runProgram(
      dir,
      `    return await this.send("hi");`,
    )
    assert.equal(caseName(outcome), 'Failed')
    assert.match(render(outcome), /CALL_CHILD_UNKNOWN/)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-058] signal to an unknown child fails with CALL_CHILD_UNKNOWN', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const outcome = await runProgram(
      dir,
      `    return await this.signal("SIGTERM");`,
    )
    assert.equal(caseName(outcome), 'Failed')
    assert.match(render(outcome), /CALL_CHILD_UNKNOWN/)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-058] this.call automatic park carries incremental delta in payload', async () => {
  const { dir, cleanup } = sandbox()
  let outcome = null
  try {
    outcome = await runProgram(
      dir,
      `    return await this.call("node -e \\"process.stdout.write('hello-delta\\\\n'); setTimeout(() => process.exit(0), 1000);\\"", { timeoutMs: 100 });`,
    )
    assert.equal(caseName(outcome), 'Parked')
    const payload = parkedPayload(outcome)
    assert.equal(payload.__jsYield, true)
    assert.ok(payload.delta, 'yield payload must carry delta')
    assert.match(payload.delta.stdout, /hello-delta/)
  } finally {
    const handle = outcome && parkedHandle(outcome)
    if (handle) {
      await execute(dir, 'DevOps', 'en', 'sess-058', { abandon: { handle } }, 2000, Date.now() + 60_000, 1 << 20, null)
    }
    clearParkedSites()
    cleanup()
  }
})
