// tests/unit/js-tools/js-bindings.test.mjs — G5 Phase B-4: runtime bindings +
// sandbox integration.
//
// The api object is the only authority a model program sees (JS-011). Reads
// and searches return JSON-compatible objects; mutations only stage (JS-012);
// path boundary is PATH_DENIED (JS-007).

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, writeFileSync, rmSync, mkdirSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { createApi } from '../../../dist/Repository/Programming/Js/ToolsBindings.js'
import { run, wrapProgram } from '../../../dist/Process/JsSandbox.js'
import { JsToolGenerator_generate as generate } from '../../../dist/Repository/Programming/Js/Surface.js'
import { JsDescriptionAssets_load as loadJsProse } from '../../../dist/Repository/Programming/Js/OpenCode/ToolHost.js'
import { ProviderLanguage } from '../../../dist/Participant/Provider/Language.js'
import { ToolPermission } from '../../../dist/Foundation/Roles.js'
import { FsSet } from '../../verification-system/tests/support/domain.mjs'
import { listItems, resultOf } from '../../verification-system/tests/support/domain.mjs'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bindings-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const permissionComparer = { Compare: (a, b) => a.CompareTo(b) }
const jsProse = () => loadJsProse(ProviderLanguage.English)
const coderCaps = FsSet.ofArray(
  [ToolPermission.Read, ToolPermission.Write, ToolPermission.Edit, ToolPermission.Glob, ToolPermission.Grep],
  permissionComparer,
)

test('WHAT[REPOSITORY-PROGRAMMING-007] JS005_bindings_file_reads_utf8', () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    const staging = []
    const api = createApi(dir, staging)
    const result = api.js.read('a.txt')
    assert.equal(result.ok, true)
    assert.equal(result.text, 'hello')
    assert.equal(result.byteCount, 5)
    const missing = api.js.read('nope.txt')
    assert.equal(missing.ok, false)
    assert.equal(missing.code, 'FILE_NOT_FOUND')
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-008] JS007_bindings_path_boundary_denies_escape', () => {
  const { dir, cleanup } = sandbox()
  try {
    const staging = []
    const api = createApi(dir, staging)
    const denied = api.js.read('../outside.txt')
    assert.equal(denied.ok, false)
    assert.equal(denied.code, 'PATH_DENIED')
    const deniedWrite = api.js.write('../outside.txt', 'x')
    assert.equal(deniedWrite.ok, false)
    assert.equal(deniedWrite.code, 'PATH_DENIED')
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-008] JS007_bindings_glob_lists_matching_paths', () => {
  const { dir, cleanup } = sandbox()
  try {
    mkdirSync(join(dir, 'src'))
    writeFileSync(join(dir, 'src', 'a.fs'), 'x', 'utf8')
    writeFileSync(join(dir, 'src', 'b.txt'), 'y', 'utf8')
    const api = createApi(dir, [])
    const result = api.js.glob('src/*.fs')
    assert.equal(result.ok, true)
    assert.deepEqual(result.paths, ['src/a.fs'])
    assert.equal('truncated' in result, false)
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-009] JS010_bindings_grep_returns_matches', () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'one two one', 'utf8')
    const api = createApi(dir, [])
    const result = api.js.grep('one', '*.txt')
    assert.equal(result.ok, true)
    assert.deepEqual(result.matches.map((m) => m.text), ['one', 'one'])
    assert.deepEqual(result.matches.map((m) => m.path), ['a.txt', 'a.txt'])
    assert.deepEqual(result.matches.map((m) => m.line), [1, 1])
    assert.equal('truncated' in result, false)
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-010] JS008_012_bindings_rewrite_requires_existing_target', () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'old text', 'utf8')
    const staging = []
    const api = createApi(dir, staging)
    const result = api.js.edit('a.txt', 'new text')
    assert.equal(result.ok, true)
    const missing = api.js.edit('nope.txt', 'x')
    assert.equal(missing.ok, false)
    assert.equal(missing.code, 'FILE_NOT_FOUND')
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-012] JS008_012_bindings_rewrite_stages_without_touching_disk', () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'old text', 'utf8')
    const staging = []
    const api = createApi(dir, staging)
    const result = api.js.edit('a.txt', 'new text')
    assert.equal(result.ok, true)
    assert.equal(staging.length, 1)
    const disk = api.js.read('a.txt')
    assert.equal(disk.text, 'old text')
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-010] JS009_012_bindings_write_stages_create', () => {
  const { dir, cleanup } = sandbox()
  try {
    const staging = []
    const api = createApi(dir, staging)
    const result = api.js.write('new.txt', 'fresh')
    assert.equal(result.ok, true)
    assert.equal(staging.length, 1)
    assert.equal(staging[0].tag === 0 ? 'Rewrite' : 'Create', 'Create')
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-012] JS009_012_bindings_write_leaves_disk_untouched', () => {
  const { dir, cleanup } = sandbox()
  try {
    const staging = []
    const api = createApi(dir, staging)
    const result = api.js.write('new.txt', 'fresh')
    assert.equal(result.ok, true)
    // disk untouched
    const disk = api.js.read('new.txt')
    assert.equal(disk.ok, false)
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-006] JS011_sandbox_program_uses_bindings_end_to_end', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello world', 'utf8')
    const staging = []
    const api = createApi(dir, staging)
    const surface = generate('Coder', coderCaps, jsProse())
    const program = `class Js extends JsProgram {
  async run() {
    const view = await this.file('a.txt', [['begin', 'end', 'hello']]);
    this.rewrite('a.txt', view.text('^', 'begin') + 'goodbye' + view.text('end', '$'));
    return { before: view.text() };
  }
}`
    const wrapped = wrapProgram(surface.BaseClassSource, program, Date.now() + 60_000)
    const result = resultOf(await run(wrapped, api, 2000, 1 << 20))
    assert.equal(result.ok, true)
    assert.deepEqual(JSON.parse(result.value), { before: 'hello world' })
    assert.equal(staging.length, 1, 'rewrite staged through the binding')
    // the mutation is staged but disk is untouched until commit
    const disk = api.js.read('a.txt')
    assert.equal(disk.text, 'hello world')
  } finally {
    cleanup()
  }
})
