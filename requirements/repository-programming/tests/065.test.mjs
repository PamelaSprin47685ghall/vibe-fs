import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import {
  execute,
  caseName,
  failureCode,
  parkedHandle,
  parkedSource,
} from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'

const MARK = '>' + '| '
const PLAIN = ' ' + '| '

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-park-source-eval-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const runProgram = (dir, body) =>
  execute(dir, 'Coder', 'en', 'sess-065', { program: body }, 2000, Date.now() + 60_000, 1 << 20, null)

const abandon = (dir, handle) =>
  execute(dir, 'Coder', 'en', 'sess-065', { abandon: { handle } }, 2000, Date.now() + 60_000, 1 << 10, null)

const markedLine = (source) => source.split('\n').find((line) => line.startsWith(MARK))

// A program that parks from inside an evaluated string. The evaluated
// snippet carries its own line numbering, so its stack position is a line of
// the snippet, not a line of the program — yet it can coincide numerically
// with a program line. The marker must stay on the program's own call site.
const evalProgram = `class Js extends JsProgram {
  async run() {
    const snippet = ["const a = 1;"];
    while (snippet.length < 149) snippet.push("const v" + snippet.length + " = " + snippet.length + ";");
    snippet.push("await this.park({ stage: 'from eval' });");
    await eval("(async () => { " + snippet.join("\\n") + " })()");
    return { done: true };
  }
}`

test('WHAT[repository-programming-048] a park reached through an evaluated string marks the program call site, not the snippet line', async () => {
  const { dir, cleanup } = sandbox()
  let handle
  try {
    const outcome = await runProgram(dir, evalProgram)
    assert.equal(caseName(outcome), 'Parked')
    handle = parkedHandle(outcome)
    assert.ok(handle, 'the parked outcome must carry a handle')
    const source = parkedSource(outcome)
    const marked = markedLine(source)
    assert.ok(marked, 'the parked source must carry the execution marker')
    assert.match(marked, /await eval\(/)
    assert.equal(marked.includes('this.park'), false, 'the marker must not land on the evaluated snippet line')
    assert.equal(source.split('\n').some((line) => line.startsWith(PLAIN)), true)
  } finally {
    // The parked site is a live handle: it is consumed by an explicit
    // abandon, not left behind for the directory cleanup to hide.
    if (handle) {
      const released = await abandon(dir, handle)
      assert.equal(caseName(released), 'Failed')
      assert.equal(failureCode(released), 'ABANDONED')
    }
    cleanup()
  }
})
