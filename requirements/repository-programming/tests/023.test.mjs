import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import {
  caseName,
  failureCode,
  failureReason,
  rewritten,
  run,
  runObserved,
} from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'
import { generate } from '../../../dist/Repository/Programming/Js/GeneratorSurface.js'
import {
  api as runtimeApi,
  createApi as createRuntimeApi,
  readPaths as runtimeReadPaths,
  run as runRuntime,
  stagedCount,
  stagedKinds,
} from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-edit-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const program = (body) => `class Js extends JsProgram {
  async run() {
${body}
    return { done: true };
  }
}`

const execute = (dir, body, language = 'en') =>
  run(dir, 'Coder', language, program(body), 2000, Date.now() + 60_000, 1 << 20, null)

test('WHAT[repository-programming-023] JS_EDIT_exact_batch_replaces_inserts_and_deletes_with_one_rewrite', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.js'), 'const alpha = 1;\nconst beta = 2;\nconst obsolete = true;\n', 'utf8')
    const outcome = await execute(dir, `    this.edit('a.js', [
      { find: 'const alpha = 1;', put: 'const alpha = 10;' },
      { find: 'const beta = 2;', put: 'const beta = 2;\\nconst gamma = 3;' },
      { find: 'const obsolete = true;\\n', put: '' },
    ]);`)

    assert.equal(caseName(outcome), 'Succeeded')
    assert.deepEqual(rewritten(outcome), ['a.js'], 'one edit call produces one rewritten path')
    assert.equal(
      readFileSync(join(dir, 'a.js'), 'utf8'),
      'const alpha = 10;\nconst beta = 2;\nconst gamma = 3;\n',
    )
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-023] JS_EDIT_only_surface_has_private_snapshot_read_without_public_file_member', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'alpha\n', 'utf8')
    const surface = generate('Coder', ['Edit'], 'en')
    assert.deepEqual(surface.members.map(member => member.memberName), ['edit', 'rewrite'])
    assert.equal(surface.baseClassSource.includes('file(path'), false)

    const handle = createRuntimeApi(dir)
    const outcome = await runRuntime(
      surface.baseClassSource,
      program(`    if (typeof this.file !== 'undefined') throw new Error('public Read leaked');
    this.edit('a.txt', { find: 'alpha', put: 'beta' });`),
      runtimeApi(handle),
      2000,
      Date.now() + 60_000,
      1 << 20,
    )

    assert.equal(outcome.ok, true)
    assert.equal(stagedCount(handle), 1)
    assert.deepEqual(stagedKinds(handle), ['Rewrite'])
    assert.deepEqual(runtimeReadPaths(handle), ['a.txt'])
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'alpha\n', 'runtime surface only stages')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-023] JS_EDIT_accepts_single_object_and_unambiguous_common_aliases', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'before\n', 'utf8')
    const first = await execute(dir, `    this.edit('a.txt', { oldText: 'before', newText: 'middle' });`)
    assert.equal(caseName(first), 'Succeeded')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'middle\n')

    const second = await execute(dir, `    this.edit('a.txt', { search: 'middle', replace: 'after' });`)
    assert.equal(caseName(second), 'Succeeded')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'after\n')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-023] JS_EDIT_all_applies_every_non_overlapping_string_or_regexp_match', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.js'), 'oldApi();\noldApi();\nconst oldApiValue = 1;\n', 'utf8')
    const outcome = await execute(dir, `    this.edit('a.js', {
      find: /\\boldApi\\b/g,
      put: 'newApi',
      all: true,
    });`)

    assert.equal(caseName(outcome), 'Succeeded')
    assert.equal(readFileSync(join(dir, 'a.js'), 'utf8'), 'newApi();\nnewApi();\nconst oldApiValue = 1;\n')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-023] JS_EDIT_preserves_sticky_regexp_as_write_authority', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'prefix target\n', 'utf8')
    const outcome = await execute(dir, `    this.edit('a.txt', {
      find: /target/y,
      put: 'changed',
    });`)

    assert.equal(caseName(outcome), 'Failed')
    assert.equal(failureCode(outcome), 'EDIT_NOT_FOUND')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'prefix target\n')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-023] JS_EDIT_preserves_a_consistent_CRLF_file_when_callers_author_LF', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'alpha\r\nbeta\r\n', 'utf8')
    const outcome = await execute(dir, `    this.edit('a.txt', {
      find: 'alpha\\nbeta',
      put: 'alpha\\ngamma\\nbeta',
    });`)

    assert.equal(caseName(outcome), 'Succeeded')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'alpha\r\ngamma\r\nbeta\r\n')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-023] JS_EDIT_every_change_addresses_the_original_snapshot_and_failure_is_atomic', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'alpha\n', 'utf8')
    const outcome = await execute(dir, `    this.edit('a.txt', [
      { find: 'alpha', put: 'beta' },
      { find: 'beta', put: 'gamma' },
    ]);`)

    assert.equal(caseName(outcome), 'Failed')
    assert.equal(failureCode(outcome), 'EDIT_NOT_FOUND')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'alpha\n')
    assert.match(String(failureReason(outcome)), /change 2/i)
    assert.match(String(failureReason(outcome)), /No changes from this edit call were staged\./)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-023] JS_EDIT_noop_succeeds_without_a_rewrite_intent', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'alpha\n', 'utf8')
    const outcome = await execute(dir, `    const report = this.edit('a.txt', {
      find: 'alpha',
      put: 'alpha',
    });
    if (report.changed !== false) throw new Error('expected no-op');`)

    assert.equal(caseName(outcome), 'Succeeded')
    assert.deepEqual(rewritten(outcome), [])
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'alpha\n')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-023] JS_EDIT_later_file_failure_discards_earlier_file_staging', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'alpha\n', 'utf8')
    writeFileSync(join(dir, 'b.txt'), 'bravo\n', 'utf8')
    const outcome = await execute(dir, `    this.edit('a.txt', { find: 'alpha', put: 'changed' });
    this.edit('b.txt', { find: 'missing', put: 'changed' });`)

    assert.equal(caseName(outcome), 'Failed')
    assert.equal(failureCode(outcome), 'EDIT_NOT_FOUND')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'alpha\n')
    assert.equal(readFileSync(join(dir, 'b.txt'), 'utf8'), 'bravo\n')
  } finally {
    cleanup()
  }
})
