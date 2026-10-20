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
} from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'
import { createApi, api as apiOf } from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-where-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const program = (body) => `class Js extends JsProgram {
  async run() {
${body}
    return { done: true };
  }
}`

const runProgram = (dir, body, sessionId = 'sess-046') =>
  execute(dir, 'Coder', 'en', sessionId, { program: program(body) }, 2000, Date.now() + 60_000, 1 << 20, null)

test('WHAT[repository-programming-046] whereAll collapses whitespace runs and returns original offsets', () => {
  const { dir, cleanup } = sandbox()
  try {
    const content = 'alpha   beta\n gamma'
    const result = apiOf(createApi(dir)).whereAll(content, 'alpha beta gamma')
    assert.equal(result.ok, true)
    assert.equal(result.spans.length, 1)
    const [begin, end] = result.spans[0]
    assert.equal(content.slice(begin, end), content, 'the span covers the original normalized run')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-046] whereAll returns every non-overlapping match and honors from', () => {
  const { dir, cleanup } = sandbox()
  try {
    const binding = createApi(dir)
    const content = 'x middle x tail x'
    const all = apiOf(binding).whereAll(content, 'x')
    assert.equal(all.spans.length, 3)
    const later = apiOf(binding).whereAll(content, 'x', 2)
    assert.equal(later.spans.length, 2)
    assert.equal(later.spans[0][0], 'x middle '.length)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-046] where returns the unique span without parking', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const outcome = await runProgram(dir, `    const text = 'alpha beta gamma';
    const [begin, end] = await this.where(text, 'beta');
    return { hit: text.slice(begin, end) };`)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.match(render(outcome), /hit = "beta"/)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-046] where parks on zero matches with needle, empty candidates and nearest', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const outcome = await runProgram(dir, `    const span = await this.where('alpha beta gamma', 'alpah');
    return { span };`)
    assert.equal(caseName(outcome), 'Parked')
    assert.equal(typeof parkedHandle(outcome), 'string')
    const payload = parkedPayload(outcome)
    assert.equal(payload.needle, 'alpah')
    assert.deepEqual(payload.candidates, [])
    assert.equal(payload.nearest.text, 'alpha beta gamma')
    assert.match(render(outcome), /# parked/)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-046] where parks on multiple matches and resumes with the injected span', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const outcome = await runProgram(dir, `    const text = 'a x b x c';
    const [begin, end] = await this.where(text, 'x');
    return { hit: text.slice(begin, end), begin, end };`)
    assert.equal(caseName(outcome), 'Parked')
    const payload = parkedPayload(outcome)
    assert.equal(payload.candidates.length, 2)
    assert.equal(payload.candidates[0].span[0], 2)
    assert.equal(payload.candidates[1].span[0], 6)

    const resumed = await execute(
      dir,
      'Coder',
      'en',
      'sess-046',
      { unpark: { handle: parkedHandle(outcome), value: payload.candidates[1].span } },
      2000,
      Date.now() + 60_000,
      1 << 20,
      null,
    )
    assert.equal(caseName(resumed), 'Succeeded')
    assert.match(render(resumed), /hit = "x"/)
    assert.match(render(resumed), /begin = 6/)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-046] a wrong injected span fails naturally downstream', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const outcome = await runProgram(dir, `    const text = 'a x b x c';
    const [begin, end] = await this.where(text, 'x');
    return { hit: text.slice(begin, end).toUpperCase() };`)
    assert.equal(caseName(outcome), 'Parked')
    const resumed = await execute(
      dir,
      'Coder',
      'en',
      'sess-046',
      { unpark: { handle: parkedHandle(outcome), value: { not: 'a span' } } },
      2000,
      Date.now() + 60_000,
      1 << 20,
      null,
    )
    assert.equal(caseName(resumed), 'Failed')
    assert.equal(render(resumed).includes('# failed'), true)
  } finally {
    cleanup()
  }
})
