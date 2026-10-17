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
