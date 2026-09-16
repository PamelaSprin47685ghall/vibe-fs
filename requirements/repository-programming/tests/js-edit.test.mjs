// Edit capability's progressive surface: easy exact edits use edit(), while
// rewrite() remains the unbounded whole-file escape hatch. Every edit call is
// planned against one immutable snapshot and stages at most one mutation.

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

test('WHAT[REPOSITORY-PROGRAMMING-023] JS_EDIT_exact_batch_replaces_inserts_and_deletes_with_one_rewrite', async () => {
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

test('WHAT[REPOSITORY-PROGRAMMING-023] JS_EDIT_only_surface_has_private_snapshot_read_without_public_file_member', async () => {
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

test('WHAT[REPOSITORY-PROGRAMMING-023] JS_EDIT_accepts_single_object_and_unambiguous_common_aliases', async () => {
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

test('WHAT[REPOSITORY-PROGRAMMING-023] JS_EDIT_all_applies_every_non_overlapping_string_or_regexp_match', async () => {
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

test('WHAT[REPOSITORY-PROGRAMMING-023] JS_EDIT_preserves_sticky_regexp_as_write_authority', async () => {
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

test('WHAT[REPOSITORY-PROGRAMMING-023] JS_EDIT_preserves_a_consistent_CRLF_file_when_callers_author_LF', async () => {
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

test('WHAT[REPOSITORY-PROGRAMMING-023] JS_EDIT_every_change_addresses_the_original_snapshot_and_failure_is_atomic', async () => {
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

test('WHAT[REPOSITORY-PROGRAMMING-024] JS_EDIT_near_match_is_copy_ready_diagnostic_but_never_write_authority', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.js'), 'const timeout = 1000;\nstart(timeout);\n', 'utf8')
    const outcome = await execute(dir, `    this.edit('a.js', {
      find: 'const timout = 1000;',
      put: 'const timeout = 5000;',
    });`)

    assert.equal(caseName(outcome), 'Failed')
    assert.equal(failureCode(outcome), 'EDIT_NOT_FOUND')
    assert.equal(readFileSync(join(dir, 'a.js'), 'utf8'), 'const timeout = 1000;\nstart(timeout);\n')
    const reason = String(failureReason(outcome))
    assert.match(reason, /a\.js/)
    assert.match(reason, /Current file near the closest candidate/)
    assert.match(reason, /Copy-ready corrected change/)
    assert.match(reason, /const timeout = 1000;/)
    assert.match(reason, /No changes from this edit call were staged\./)
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-024] JS_EDIT_ambiguous_match_returns_candidates_and_two_safe_next_moves', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'section A\nvalue = 1\nsection B\nvalue = 1\n', 'utf8')
    const outcome = await execute(dir, `    this.edit('a.txt', {
      find: 'value = 1',
      put: 'value = 2',
    });`)

    assert.equal(caseName(outcome), 'Failed')
    assert.equal(failureCode(outcome), 'EDIT_AMBIGUOUS')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'section A\nvalue = 1\nsection B\nvalue = 1\n')
    const reason = String(failureReason(outcome))
    assert.match(reason, /lines? 2/i)
    assert.match(reason, /lines? 4/i)
    assert.match(reason, /unique surrounding context/i)
    assert.match(reason, /all: true/)
    assert.match(reason, /No changes from this edit call were staged\./)
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-024] JS_EDIT_overlap_and_invalid_shape_have_stable_codes_and_zero_commit', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'alpha beta gamma\n', 'utf8')
    const overlap = await execute(dir, `    this.edit('a.txt', [
      { find: 'alpha beta', put: 'first' },
      { find: 'beta gamma', put: 'second' },
    ]);`)
    assert.equal(caseName(overlap), 'Failed')
    assert.equal(failureCode(overlap), 'EDIT_OVERLAP')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'alpha beta gamma\n')
    assert.match(String(failureReason(overlap)), /merge them into one/i)

    const invalid = await execute(dir, `    this.edit('a.txt', { find: '', put: 'x' });`)
    assert.equal(caseName(invalid), 'Failed')
    assert.equal(failureCode(invalid), 'INVALID_EDIT')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'alpha beta gamma\n')
    assert.match(String(failureReason(invalid)), /\{ find, put, all\? \}/)
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-024] JS_EDIT_rejects_invalid_path_unknown_fields_and_exotic_change_objects', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'alpha\n', 'utf8')

    const invalidPath = await execute(dir, `    this.edit('', { find: 'alpha', put: 'beta' });`)
    assert.equal(caseName(invalidPath), 'Failed')
    assert.equal(failureCode(invalidPath), 'INVALID_EDIT', 'path validation precedes Host reading')

    const typo = await execute(dir, `    this.edit('a.txt', {
      find: 'alpha',
      put: 'beta',
      al: true,
    });`)
    assert.equal(caseName(typo), 'Failed')
    assert.equal(failureCode(typo), 'INVALID_EDIT')
    assert.match(String(failureReason(typo)), /unknown field.*al/i)
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'alpha\n')

    const exotic = await execute(dir, `    const change = Object.assign(new Date(0), {
      find: 'alpha',
      put: 'beta',
    });
    this.edit('a.txt', change);`)
    assert.equal(caseName(exotic), 'Failed')
    assert.equal(failureCode(exotic), 'INVALID_EDIT')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'alpha\n')

    const editOnly = generate('Coder', ['Edit'], 'en')
    const handle = createRuntimeApi(dir)
    const malformedMissingPath = await runRuntime(
      editOnly.baseClassSource,
      program(`    this.edit('missing.txt', {
      find: 'alpha',
      put: 'beta',
      al: true,
    });`),
      runtimeApi(handle),
      2000,
      Date.now() + 60_000,
      1 << 20,
    )
    assert.equal(malformedMissingPath.ok, false)
    assert.equal(malformedMissingPath.code, 'INVALID_EDIT')
    assert.deepEqual(runtimeReadPaths(handle), [], 'pure declaration failure precedes target read')
    assert.equal(stagedCount(handle), 0)
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-023] JS_EDIT_noop_succeeds_without_a_rewrite_intent', async () => {
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

test('WHAT[REPOSITORY-PROGRAMMING-023] JS_EDIT_later_file_failure_discards_earlier_file_staging', async () => {
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

test('WHAT[REPOSITORY-PROGRAMMING-014] JS_EDIT_target_read_is_observed_and_external_change_wins', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'alpha\n', 'utf8')
    const outcome = await runObserved(
      dir,
      'Coder',
      'en',
      program(`    this.edit('a.txt', { find: 'alpha', put: 'beta' });`),
      2000,
      Date.now() + 60_000,
      1 << 20,
      null,
      async (readPaths, effectPaths) => {
        assert.deepEqual(readPaths, ['a.txt'])
        assert.deepEqual(effectPaths, ['a.txt'])
        writeFileSync(join(dir, 'a.txt'), 'external\n', 'utf8')
      },
    )

    assert.equal(caseName(outcome), 'Failed')
    assert.equal(failureCode(outcome), 'FILE_CHANGED')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'external\n')
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-024] JS_EDIT_diagnostics_are_bounded_and_echo_the_attempted_find', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const longLine = `${'x'.repeat(6000)} const timeout = 1000; ${'y'.repeat(6000)}`
    writeFileSync(join(dir, 'a.js'), `${longLine}\n`, 'utf8')
    const hugePut = 'z'.repeat(8000)
    const outcome = await execute(dir, `    this.edit('a.js', {
      find: 'const timout = 1000;',
      put: ${JSON.stringify(hugePut)},
    });`)

    assert.equal(caseName(outcome), 'Failed')
    assert.equal(failureCode(outcome), 'EDIT_NOT_FOUND')
    const reason = String(failureReason(outcome))
    assert.match(reason, /Attempted find:/)
    assert.match(reason, /const timout = 1000;/)
    assert.ok(reason.length < 6000, `diagnostic must be bounded, got ${reason.length} chars`)
    assert.equal(readFileSync(join(dir, 'a.js'), 'utf8'), `${longLine}\n`)

    const unknownFields = await execute(dir, `    const change = {
      find: 'const timeout = 1000;',
      put: 'const timeout = 5000;',
    };
    for (let i = 0; i < 1000; i += 1) change['unknown_' + i + '_' + 'q'.repeat(100)] = true;
    this.edit('a.js', change);`)
    assert.equal(caseName(unknownFields), 'Failed')
    assert.equal(failureCode(unknownFields), 'INVALID_EDIT')
    assert.ok(String(failureReason(unknownFields)).length < 1500)
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-024] JS_EDIT_copy_ready_fix_uses_the_exact_candidate_subspan', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.js'), 'prefix const timeout = 1000; suffix\n', 'utf8')
    const outcome = await execute(dir, `    this.edit('a.js', {
      find: 'const timout = 1000;',
      put: 'const timeout = 5000;',
    });`)

    assert.equal(caseName(outcome), 'Failed')
    assert.equal(failureCode(outcome), 'EDIT_NOT_FOUND')
    const reason = String(failureReason(outcome))
    assert.match(reason, /"find": "const timeout = 1000;"/)
    assert.doesNotMatch(reason, /"find": "prefix const timeout/)
    assert.equal(readFileSync(join(dir, 'a.js'), 'utf8'), 'prefix const timeout = 1000; suffix\n')
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-024] JS_EDIT_failure_control_language_is_localized', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'value = 1\nvalue = 1\n', 'utf8')
    const ambiguous = await execute(dir, `    this.edit('a.txt', {
      find: 'value = 1',
      put: 'value = 2',
    });`, 'zh-CN')

    assert.equal(caseName(ambiguous), 'Failed')
    assert.equal(failureCode(ambiguous), 'EDIT_AMBIGUOUS')
    const ambiguousReason = String(failureReason(ambiguous))
    assert.match(ambiguousReason, /候选 1[:：] 行 1/)
    assert.match(ambiguousReason, /本次 edit 调用没有暂存任何修改/)
    assert.doesNotMatch(ambiguousReason, /Candidate|No changes from/)

    const invalid = await execute(dir, `    this.edit('a.txt', {
      find: 'value = 1',
      put: 'value = 2',
      al: true,
    });`, 'zh-CN')
    assert.equal(caseName(invalid), 'Failed')
    assert.equal(failureCode(invalid), 'INVALID_EDIT')
    assert.match(String(failureReason(invalid)), /未知字段： al/)
  } finally {
    cleanup()
  }
})
