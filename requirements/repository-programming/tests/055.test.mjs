import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { run, caseName, render } from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-parallel-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const program = (body) => `class Js extends JsProgram {
  async run() {
${body}
  }
}`

const runProgram = (dir, body) =>
  run(dir, 'Engineer', 'en', program(body), 5000, Date.now() + 60_000, 1 << 20, null)

test('WHAT[repository-programming-055] Promise.all runs independent reads and writes inside one program', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'alpha', 'utf8')
    writeFileSync(join(dir, 'b.txt'), 'beta', 'utf8')
    const outcome = await runProgram(
      dir,
      `    const [a, b] = await Promise.all([this.cat('a.txt'), this.cat('b.txt')]);
    await Promise.all([this.write('c.txt', a + b), this.write('d.txt', b + a)]);
    return { a, b };`,
    )
    assert.equal(caseName(outcome), 'Succeeded')
    assert.equal(readFileSync(join(dir, 'c.txt'), 'utf8'), 'alphabeta')
    assert.equal(readFileSync(join(dir, 'd.txt'), 'utf8'), 'betaalpha')
    assert.match(render(outcome), /a = "alpha"/)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-055] allSettled keeps landed writes when one parallel branch fails', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const outcome = await runProgram(
      dir,
      `    const results = await Promise.allSettled([this.cat('missing.txt'), this.write('kept.txt', 'landed')]);
    return { failed: results[0].status, second: results[1].status };`,
    )
    assert.equal(caseName(outcome), 'Succeeded')
    assert.equal(readFileSync(join(dir, 'kept.txt'), 'utf8'), 'landed', 'the independent branch lands as-is')
    const doc = render(outcome)
    assert.match(doc, /failed = "rejected"/)
    assert.match(doc, /second = "fulfilled"/)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-055] a sixteen-way cat batch completes inside one program', async () => {
  const { dir, cleanup } = sandbox()
  try {
    for (let index = 0; index < 16; index += 1) {
      writeFileSync(join(dir, `f${index}.txt`), String(index), 'utf8')
    }
    const outcome = await runProgram(
      dir,
      `    const texts = await Promise.all(Array.from({ length: 16 }, (_, i) => this.cat('f' + i + '.txt')));
    return { count: texts.length, sum: texts.reduce((total, text) => total + Number(text), 0) };`,
    )
    assert.equal(caseName(outcome), 'Succeeded')
    const doc = render(outcome)
    assert.match(doc, /count = 16/)
    assert.match(doc, /sum = 120/)
  } finally {
    cleanup()
  }
})
