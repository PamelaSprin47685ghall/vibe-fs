import assert from 'node:assert/strict'
import test from 'node:test'
import { existsSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
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
  const dir = mkdtempSync(join(tmpdir(), 'wxs-run-050-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const program = (body) => `class Js extends JsProgram {
  async run() {
${body}
  }
}`

let runCount = 0
const runProgram = (dir, body, sessionId = 'sess-050') => {
  runCount += 1
  return execute(dir, 'DevOps', 'en', sessionId, { name: `run-050-${runCount}`, program: program(body) }, 5000, Date.now() + 60_000, 1 << 20, null)
}

const runText = async (dir, body) => render(await runProgram(dir, body))

test('WHAT[repository-programming-050] a class command is refused with CALL_COMMAND_UNSUPPORTED', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const doc = await runText(
      dir,
      `    return await this.call(class { maxDuration = 1000; next() { return null; } });`,
    )
    assert.match(doc, /# failed/)
    assert.match(doc, /CALL_COMMAND_UNSUPPORTED/)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-050] a function command is refused with CALL_COMMAND_UNSUPPORTED', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const doc = await runText(
      dir,
      `    return await this.call(function() { return "echo hi"; });`,
    )
    assert.match(doc, /# failed/)
    assert.match(doc, /CALL_COMMAND_UNSUPPORTED/)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-050] an invalid non-string command is refused with CALL_COMMAND_UNSUPPORTED', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const doc = await runText(
      dir,
      `    return await this.call(12345);`,
    )
    assert.match(doc, /# failed/)
    assert.match(doc, /CALL_COMMAND_UNSUPPORTED/)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-050] the host runs one foreground command and reports structured outcome', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const doc = await runText(
      dir,
      `    return await this.call("echo hello", "read the file directly");`,
    )
    assert.match(doc, /# ok/)
    assert.match(doc, /status = "completed"/)
    assert.match(doc, /hello/)
    assert.match(doc, /exitCode = 0/)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-050] non-zero exit code is reported as data without throwing', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const doc = await runText(
      dir,
      `    return await this.call("exit 4", "read the file directly");`,
    )
    assert.match(doc, /# ok/)
    assert.match(doc, /exitCode = 4/)
    assert.match(doc, /status = "completed"/)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-050] planB text bytes price the automatic yield window', async () => {
  const { dir, cleanup } = sandbox()
  let outcome = null
  try {
    outcome = await runProgram(
      dir,
      `    return await this.call("sleep 5", "ab");`,
    )
    assert.equal(caseName(outcome), 'Parked')
    const payload = parkedPayload(outcome)
    assert.equal(payload.__jsYield, true)
    assert.equal(payload.planB, 'ab')
    assert.equal(payload.budgetMs, 2 * 50)
    assert.ok(payload.delta, 'yield payload must carry delta')
  } finally {
    const handle = outcome && parkedHandle(outcome)
    if (handle) {
      await execute(dir, 'DevOps', 'en', 'sess-050', { abandon: { handle } }, 2000, Date.now() + 60_000, 1 << 20, null)
    }
    clearParkedSites()
    cleanup()
  }
})

test('WHAT[repository-programming-050] a missing planB is refused and planB byte length prices the budget', async () => {
  const { dir, cleanup } = sandbox()
  let outcome = null
  try {
    const missing = await runText(dir, `    return await this.call("printf identical");`)
    assert.match(missing, /CALL_COMMAND_UNSUPPORTED/)

    outcome = await runProgram(dir, `    return await this.call("sleep 5", "abcdef");`)
    assert.equal(caseName(outcome), 'Parked')
    const payload = parkedPayload(outcome)
    assert.equal(payload.__jsYield, true)
    assert.equal(payload.planB, 'abcdef')
    assert.equal(payload.budgetMs, 6 * 50)
  } finally {
    const handle = outcome && parkedHandle(outcome)
    if (handle) {
      await execute(dir, 'DevOps', 'en', 'sess-050', { abandon: { handle } }, 2000, Date.now() + 60_000, 1 << 20, null)
    }
    clearParkedSites()
    cleanup()
  }
})
