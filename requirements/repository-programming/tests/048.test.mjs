import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import {
  execute,
  caseName,
  parkedSource,
  render,
} from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-park-source-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const program = (body) => `class Js extends JsProgram {
  async run() {
${body}
    return { done: true };
  }
}`

const runProgram = (dir, body) =>
  execute(dir, 'Coder', 'en', 'sess-048', { program: program(body) }, 2000, Date.now() + 60_000, 1 << 20, null)

const markedLine = (source) => source.split('\n').find((line) => line.startsWith('>| '))

test('WHAT[repository-programming-048] parked payload carries the full program with the current line marked', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const outcome = await runProgram(dir, `    const decision = await this.park({ stage: 'review' });
    return { decided: decision.value };`)
    assert.equal(caseName(outcome), 'Parked')
    const source = parkedSource(outcome)
    assert.equal(typeof source, 'string')
    assert.equal(source.includes('class Js extends JsProgram'), true)
    assert.equal(source.includes('return { decided: decision.value };'), true)
    assert.match(markedLine(source), /await this\.park/)
    assert.equal(source.split('\n').some((line) => line.startsWith(' | ')), true)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-048] where ambiguity marks the model call site, not the generated base class', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const outcome = await runProgram(dir, `    const text = 'a x b x c';
    const [begin, end] = await this.where(text, 'x');
    return { begin, end };`)
    assert.equal(caseName(outcome), 'Parked')
    const source = parkedSource(outcome)
    assert.match(markedLine(source), /await this\.where/)
    assert.equal(markedLine(source).includes('_api'), false)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-048] the rendered parked document carries the marked source as a literal block', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const outcome = await runProgram(dir, `    await this.park({ why: 'source test' });`)
    const doc = render(outcome)
    assert.match(doc, /source = '''/)
    assert.match(doc, /^>\| /m)
    assert.match(doc, /await this\.park/)
  } finally {
    cleanup()
  }
})

// Moved from tests/065.test.mjs: these cases already own clause [048] (parked source line marker); one test file per clause.
{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const {
  execute,
  caseName,
  failureCode,
  parkedHandle,
  parkedSource,
} = await import("../../../dist/Repository/Programming/Js/WorkflowSurface.js");

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
}
