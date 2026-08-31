// tests/unit/js-tools/js-tools-fs.test.mjs — G5 Phase B-4: filesystem adapter
// (JS-005/006/007/013/015).
//
// Strict UTF-8 reads, ordered anchor matching, full glob, all-or-nothing
// commit with rollback. Pure Node fs against per-test temp directories.

import assert from 'node:assert/strict'
import test from 'node:test'
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import {
  readUtf8,
  glob,
  findAnchor,
  requireUnique,
  grep,
  commitPlan,
  rollbackPlan,
} from '../../../dist/Repository/Programming/Js/FilesystemSurface.js'

const exact = (text) => ({ kind: 'exact', text })
const regex = (text) => ({ kind: 'regex', text })

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-jstools-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}
const ok = (result) => result.ok
const codeOf = (result) => result.code
const unwrap = (result) => {
  assert.equal(result.ok, true, `expected Ok, got ${JSON.stringify(result.error)}`)
  return result.value
}

test('WHAT[REPOSITORY-PROGRAMMING-007] JS005_readUtf8_reads_and_classifies', () => {
  const { dir, cleanup } = sandbox()
  try {
    const file = join(dir, 'a.txt')
    writeFileSync(file, 'hello', 'utf8')
    assert.equal(unwrap(readUtf8(file)), 'hello')
    assert.equal(codeOf(readUtf8(join(dir, 'missing.txt'))), 'FILE_NOT_FOUND')
    // invalid UTF-8 bytes → INVALID_UTF8, never silent replacement
    writeFileSync(join(dir, 'bad.bin'), Buffer.from([0xff, 0xfe, 0xfd]))
    assert.equal(codeOf(readUtf8(join(dir, 'bad.bin'))), 'INVALID_UTF8')
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-007] JS006_findAnchor_ordered_string_and_regex', () => {
  const text = 'a b a b a'
  // exact, occurrence 1/2/3
  assert.deepEqual(unwrap(findAnchor(text, exact('a'), 1)), [0, 1])
  assert.deepEqual(unwrap(findAnchor(text, exact('a'), 2)), [4, 5])
  assert.deepEqual(unwrap(findAnchor(text, exact('a'), 3)), [8, 9])
  assert.equal(codeOf(findAnchor(text, exact('a'), 4)), 'ANCHOR_NOT_FOUND')
  // regex
  assert.deepEqual(unwrap(findAnchor(text, regex('b a'), 1)), [2, 5])
  assert.deepEqual(unwrap(findAnchor(text, regex('b a'), 2)), [6, 9])
  // zero-width: ^ anchors at absolute file start
  assert.deepEqual(unwrap(findAnchor(text, regex('^'), 1)), [0, 0])
  assert.equal(codeOf(findAnchor(text, regex('('), 1)), 'INVALID_ANCHOR_PATTERN')
})

test('WHAT[REPOSITORY-PROGRAMMING-007] JS006_requireUnique_refuses_ambiguous_anchors', () => {
  const text = 'x y x'
  assert.deepEqual(unwrap(requireUnique(text, exact('y'))), [2, 3])
  assert.equal(codeOf(requireUnique(text, exact('x'))), 'ANCHOR_NOT_UNIQUE')
  assert.equal(codeOf(requireUnique(text, exact('z'))), 'ANCHOR_NOT_FOUND')
})

test('WHAT[REPOSITORY-PROGRAMMING-008] JS007_glob_deterministic_enumeration', () => {
  const { dir, cleanup } = sandbox()
  try {
    mkdirSync(join(dir, 'src'))
    mkdirSync(join(dir, 'src', 'deep'))
    writeFileSync(join(dir, 'src', 'a.fs'), 'a', 'utf8')
    writeFileSync(join(dir, 'src', 'b.fs'), 'b', 'utf8')
    writeFileSync(join(dir, 'src', 'deep', 'c.fs'), 'c', 'utf8')
    writeFileSync(join(dir, 'readme.md'), 'r', 'utf8')

    const all = unwrap(glob(dir, '**/*.fs'))
    assert.deepEqual(all.paths, ['src/a.fs', 'src/b.fs', 'src/deep/c.fs'])
    assert.equal('truncated' in all, false)
    const nested = unwrap(glob(dir, '*.fs'))
    assert.deepEqual(nested.paths, ['src/a.fs', 'src/b.fs', 'src/deep/c.fs'])
    const shallow = unwrap(glob(dir, 'src/*.fs'))
    assert.deepEqual(shallow.paths, ['src/a.fs', 'src/b.fs'])
    const zeroStar = unwrap(glob(dir, 'src/**/*.fs'))
    assert.deepEqual(zeroStar.paths, ['src/a.fs', 'src/b.fs', 'src/deep/c.fs'])
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-008] JS007_glob_gitignore_skips_git_and_ignored', () => {
  const { dir, cleanup } = sandbox()
  try {
    mkdirSync(join(dir, '.git', 'objects'), { recursive: true })
    mkdirSync(join(dir, 'dist'))
    mkdirSync(join(dir, 'src'))
    writeFileSync(join(dir, '.git', 'HEAD'), 'ref', 'utf8')
    writeFileSync(join(dir, 'dist', 'out.js'), 'x', 'utf8')
    writeFileSync(join(dir, 'secret.txt'), 's', 'utf8')
    writeFileSync(join(dir, 'src', 'keep.fs'), 'k', 'utf8')
    writeFileSync(join(dir, 'readme.md'), 'r', 'utf8')
    writeFileSync(join(dir, '.gitignore'), 'secret.txt\n/dist/\n', 'utf8')

    const listing = unwrap(glob(dir, '**/*'))
    const paths = listing.paths
    assert.equal(paths.some((p) => p.startsWith('.git/') || p === '.git'), false)
    assert.equal(paths.includes('secret.txt'), false)
    assert.equal(paths.includes('dist/out.js'), false)
    assert.equal(paths.includes('src/keep.fs'), true)
    assert.equal(paths.includes('readme.md'), true)
    assert.equal(paths.includes('.gitignore'), true)

    const braces = unwrap(glob(dir, '**/*.{fs,md}'))
    assert.deepEqual(braces.paths, ['readme.md', 'src/keep.fs'])
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-009] JS020_grep_returns_line_column_and_skips_ignored', () => {
  const { dir, cleanup } = sandbox()
  try {
    mkdirSync(join(dir, 'src'))
    mkdirSync(join(dir, 'dist'))
    writeFileSync(join(dir, 'src', 'a.fs'), 'alpha\nTODO: one\n', 'utf8')
    writeFileSync(join(dir, 'dist', 'skip.js'), 'TODO: hidden\n', 'utf8')
    writeFileSync(join(dir, '.gitignore'), '/dist/\n', 'utf8')

    const listing = unwrap(grep(dir, regex('TODO:.+'), 'src/**/*.fs'))
    const hits = listing.matches
    assert.equal(hits.length, 1)
    assert.equal(hits[0].path, 'src/a.fs')
    assert.equal(hits[0].line, 2)
    assert.equal(hits[0].column, 1)
    assert.equal(hits[0].text, 'TODO: one')
    assert.equal('truncated' in listing, false)
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-013] JS013_commitPlan_all_or_nothing', () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'oldA', 'utf8')
    // commit two files
    const plan = [
      { kind: 'rewrite', path: 'a.txt', expectedCurrent: 'oldA', newText: 'newA' },
      { kind: 'create', path: 'b.txt', newText: 'newB' },
    ]
    assert.equal(ok(commitPlan(dir, plan)), true)
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'newA')
    assert.equal(readFileSync(join(dir, 'b.txt'), 'utf8'), 'newB')
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-013] JS013_commitPlan_aborts_before_write_when_snapshot_fails', () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'oldA', 'utf8')
    // a directory target cannot be snapshotted → Phase 1 aborts BEFORE any write
    mkdirSync(join(dir, 'blocked'))
    const plan = [
      { kind: 'rewrite', path: 'a.txt', expectedCurrent: 'oldA', newText: 'newA' },
      { kind: 'rewrite', path: 'blocked', expectedCurrent: 'old', newText: 'nope' },
    ]
    assert.equal(codeOf(commitPlan(dir, plan)), 'FILE_CHANGED')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'oldA', 'no write happened at all')
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-013] JS013_commitPlan_rolls_back_written_files_on_write_failure', () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'oldA', 'utf8')
    // second target's parent directory does not exist → write fails → the
    // already-written first file must be rolled back (all-or-nothing)
    const plan = [
      { kind: 'rewrite', path: 'a.txt', expectedCurrent: 'oldA', newText: 'newA' },
      { kind: 'create', path: 'x/y.txt', newText: 'nope' },
    ]
    assert.equal(codeOf(commitPlan(dir, plan)), 'TRANSACTION_COMMIT_FAILED')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'oldA', 'first write rolled back')
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-014] JS014_commitPlan_rejects_a_create_race_without_overwriting', () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'new.txt'), 'external', 'utf8')
    const result = commitPlan(dir, [{ kind: 'create', path: 'new.txt', newText: 'tool' }])
    assert.equal(codeOf(result), 'FILE_CHANGED')
    assert.equal(readFileSync(join(dir, 'new.txt'), 'utf8'), 'external')
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-014] JS014_commitPlan_rejects_a_stale_rewrite_without_overwriting', () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'external', 'utf8')
    const result = commitPlan(dir, [
      { kind: 'rewrite', path: 'a.txt', expectedCurrent: 'snapshot', newText: 'tool' },
    ])
    assert.equal(codeOf(result), 'FILE_CHANGED')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'external')
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-015] JS015_rollbackPlan_is_CAS_and_preserves_third_party_changes', () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'oldA', 'utf8')
    // simulate a partial commit, then roll back
    writeFileSync(join(dir, 'c.txt'), 'oldC', 'utf8')
    const committed = commitPlan(dir, [
      { kind: 'rewrite', path: 'a.txt', expectedCurrent: 'oldA', newText: 'newA' },
      { kind: 'create', path: 'b.txt', newText: 'newB' },
      { kind: 'rewrite', path: 'c.txt', expectedCurrent: 'oldC', newText: 'newC' },
    ])
    assert.equal(ok(committed), true)
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'newA')
    assert.equal(readFileSync(join(dir, 'b.txt'), 'utf8'), 'newB')
    assert.equal(readFileSync(join(dir, 'c.txt'), 'utf8'), 'newC')
    writeFileSync(join(dir, 'c.txt'), 'third-party', 'utf8')
    const rollback = [
      { kind: 'restore', path: 'c.txt', expectedCurrent: 'newC', originalText: 'oldC' },
      { kind: 'removeCreated', path: 'b.txt', expectedCurrent: 'newB' },
      { kind: 'restore', path: 'a.txt', expectedCurrent: 'newA', originalText: 'oldA' },
    ]
    rollbackPlan(dir, rollback)
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'oldA')
    assert.equal(existsSync(join(dir, 'b.txt')), false)
    assert.equal(readFileSync(join(dir, 'c.txt'), 'utf8'), 'third-party')
  } finally {
    cleanup()
  }
})
