import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { parse as parseToml } from 'smol-toml'
import {
  caseName,
  failureCode,
  rewritten,
  created,
  run,
  render,
} from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'
import { generate } from '../../../dist/Repository/Programming/Js/GeneratorSurface.js'
import { commitPlan as fsCommitPlan } from '../../../dist/Repository/Programming/Js/FilesystemSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-asis-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}
const program = (body) => `class Js extends JsProgram {
  async run() {
${body}
    return { done: true };
  }
}`
const execute = (dir, body) =>
  run(dir, 'Coder', 'en', program(body), 2000, Date.now() + 60_000, 1 << 20, null)

test('WHAT[repository-programming-029] JS_ASIS_each_write_lands_immediately_and_later_failure_keeps_earlier_writes', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'alpha\n', 'utf8')
    writeFileSync(join(dir, 'b.txt'), 'bravo\n', 'utf8')
    // First write succeeds and persists; the second read fails on a missing path. Under
    // as-is semantics the first write stays on disk and is reported.
    const outcome = await execute(dir, `    const text = await this.cat('a.txt');
    const [begin, end] = await this.where(text, 'alpha');
    this.write('a.txt', text.slice(0, begin) + 'changed' + text.slice(end));
    await this.cat('missing.txt');`)

    assert.equal(caseName(outcome), 'Failed')
    assert.equal(failureCode(outcome), 'FILE_NOT_FOUND')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'changed\n', 'earlier write persisted')
    assert.equal(readFileSync(join(dir, 'b.txt'), 'utf8'), 'bravo\n', 'failed write did not land')
    assert.deepEqual(rewritten(outcome), ['a.txt'], 'failure reports the actually-written paths')
    assert.deepEqual(created(outcome), [])
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-029] JS_ASIS_success_reports_rewritten_and_created_in_fs', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'alpha\n', 'utf8')
    const outcome = await execute(dir, `    const text = await this.cat('a.txt');
    const [begin, end] = await this.where(text, 'alpha');
    this.write('a.txt', text.slice(0, begin) + 'beta' + text.slice(end));
    await this.write('new.txt', 'fresh');`)

    assert.equal(caseName(outcome), 'Succeeded')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'beta\n')
    assert.equal(readFileSync(join(dir, 'new.txt'), 'utf8'), 'fresh')
    assert.deepEqual(rewritten(outcome), ['a.txt'])
    assert.deepEqual(created(outcome), ['new.txt'])
    const doc = parseToml(render(outcome))
    assert.equal(doc.fs.rewritten[0], 'a.txt')
    assert.equal(doc.fs.created[0], 'new.txt')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-029] JS_ASIS_failure_renders_code_reason_and_applied_fs_effects', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'alpha\n', 'utf8')
    writeFileSync(join(dir, 'b.txt'), 'bravo\n', 'utf8')
    const outcome = await execute(dir, `    const text = await this.cat('a.txt');
    const [begin, end] = await this.where(text, 'alpha');
    this.write('a.txt', text.slice(0, begin) + 'changed' + text.slice(end));
    await this.cat('missing.txt');`)

    const rendered = render(outcome)
    assert.equal(rendered.startsWith('# failed\n'), true)
    const doc = parseToml(rendered)
    assert.equal(doc.code, 'FILE_NOT_FOUND')
    assert.ok(doc.reason.length > 0)
    assert.deepEqual(doc.fs.rewritten, ['a.txt'], 'failure carries the already-applied fs list')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-029] JS_ASIS_failure_without_effects_omits_fs_section', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'alpha\n', 'utf8')
    const outcome = await execute(dir, `    await this.cat('missing.txt');`)

    assert.equal(caseName(outcome), 'Failed')
    assert.equal(failureCode(outcome), 'FILE_NOT_FOUND')
    assert.deepEqual(rewritten(outcome), [])
    const rendered = render(outcome)
    const doc = parseToml(rendered)
    assert.equal(doc.code, 'FILE_NOT_FOUND')
    assert.equal(doc.fs, undefined, 'no applied effects means no fs section')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-029] JS_ASIS_write_precheck_reports_FILE_CHANGED_and_leaves_target_untouched', () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'current\n', 'utf8')
    // A single-mutation commit whose expectedCurrent does not match the disk
    // must fail with FILE_CHANGED and write nothing.
    const result = fsCommitPlan(dir, [
      { kind: 'rewrite', path: 'a.txt', expectedCurrent: 'stale\n', newText: 'new\n' },
    ])
    assert.equal(result.ok, false)
    assert.equal(result.code, 'FILE_CHANGED')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'current\n', 'target untouched')
  } finally {
    cleanup()
  }
})