import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { run, caseName, render } from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-compose-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const program = (body) => `class Js extends JsProgram {
  async run() {
${body}
    return { done: true };
  }
}`

const runProgram = (dir, body) =>
  run(dir, 'Engineer', 'en', program(body), 2000, Date.now() + 60_000, 1 << 20, null)

test('WHAT[repository-programming-047] find and replace composes cat, where, slice and write', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'alpha old omega', 'utf8')
    const outcome = await runProgram(dir, `    const text = await this.cat('a.txt');
    const [begin, end] = await this.where(text, 'old');
    this.write('a.txt', text.slice(0, begin) + 'new' + text.slice(end));`)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'alpha new omega')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-047] batch replacement consumes whereAll in one program', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'x a x b x', 'utf8')
    const outcome = await runProgram(dir, `    let text = await this.cat('a.txt');
    const spans = this.whereAll(text, 'x');
    for (const [begin, end] of [...spans].reverse()) {
      text = text.slice(0, begin) + 'y' + text.slice(end);
    }
    this.write('a.txt', text);`)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'y a y b y')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-047] append composes cat and write', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'head\n', 'utf8')
    const outcome = await runProgram(dir, `    const text = await this.cat('a.txt');
    this.write('a.txt', text + 'tail\\n');`)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'head\ntail\n')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-047] anchor slicing composes two where calls with from', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const outcome = await runProgram(dir, `    const text = 'BEGIN one MIDDLE two END';
    const [, afterOne] = await this.where(text, 'one');
    const [beforeTwo] = await this.where(text, 'two', afterOne);
    return { slice: text.slice(afterOne, beforeTwo) };`)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.match(render(outcome), /slice = " MIDDLE "/)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-047] grep and glob remain composable with the primitive surface', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'needle a\n', 'utf8')
    writeFileSync(join(dir, 'b.txt'), 'needle b\n', 'utf8')
    writeFileSync(join(dir, 'c.js'), 'needle c\n', 'utf8')
    const outcome = await runProgram(dir, `    const paths = await this.glob('*.txt');
    const hits = await this.grep('needle', '*.txt');
    return { pathCount: paths.paths.length, hitCount: hits.matches.length };`)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.match(render(outcome), /pathCount = 2/)
    assert.match(render(outcome), /hitCount = 2/)
  } finally {
    cleanup()
  }
})
