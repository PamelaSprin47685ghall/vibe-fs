import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { createApi, api as apiOf } from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'
import { grep } from '../../../dist/Repository/Programming/Js/FilesystemSurface.js'

const exact = (text) => ({ kind: 'exact', text })
const regex = (text) => ({ kind: 'regex', text })

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bindings-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}
const ok = (result) => result.ok
const codeOf = (result) => result.code
const unwrap = (result) => {
  assert.equal(result.ok, true, `expected Ok, got ${JSON.stringify(result.error)}`)
  return result.value
}

test('WHAT[REPOSITORY-PROGRAMMING-009] JS010_bindings_grep_returns_matches', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'one two one', 'utf8')
    const result = await apiOf(createApi(dir)).js.grep('one', '*.txt')
    assert.equal(result.ok, true)
    assert.deepEqual(result.matches.map((m) => m.text), ['one', 'one'])
    assert.deepEqual(result.matches.map((m) => m.path), ['a.txt', 'a.txt'])
    assert.deepEqual(result.matches.map((m) => m.line), [1, 1])
    assert.equal('truncated' in result, false)
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
