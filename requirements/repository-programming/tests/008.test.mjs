import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, writeFileSync, rmSync, mkdirSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { createApi, api: apiOf, stagedCount, stagedKinds, run } = await import("../../../dist/Repository/Programming/Js/RuntimeSurface.js");
const { generate } = await import("../../../dist/Repository/Programming/Js/GeneratorSurface.js");

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bindings-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}
const coderSurface = () => generate('Coder', ['Read', 'Write', 'Edit', 'Glob', 'Grep'], 'en')

test('WHAT[repository-programming-008] JS007_bindings_path_boundary_denies_escape', () => {
  const { dir, cleanup } = sandbox()
  try {
    const binding = createApi(dir)
    const denied = apiOf(binding).js.read('../outside.txt')
    assert.equal(denied.ok, false)
    assert.equal(denied.code, 'PATH_DENIED')
    const deniedWrite = apiOf(binding).js.write('../outside.txt', 'x')
    assert.equal(deniedWrite.ok, false)
    assert.equal(deniedWrite.code, 'PATH_DENIED')
  } finally {
    cleanup()
  }
})
test('WHAT[repository-programming-008] JS007_bindings_glob_lists_matching_paths', async () => {
  const { dir, cleanup } = sandbox()
  try {
    mkdirSync(join(dir, 'src'))
    writeFileSync(join(dir, 'src', 'a.fs'), 'x', 'utf8')
    writeFileSync(join(dir, 'src', 'b.txt'), 'y', 'utf8')
    const result = await apiOf(createApi(dir)).js.glob('src/*.fs')
    assert.equal(result.ok, true)
    assert.deepEqual(result.paths, ['src/a.fs'])
    assert.equal('truncated' in result, false)
  } finally {
    cleanup()
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { readUtf8, glob, findAnchor, requireUnique, grep, commitPlan, rollbackPlan } = await import("../../../dist/Repository/Programming/Js/FilesystemSurface.js");

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

test('WHAT[repository-programming-008] JS007_glob_deterministic_enumeration', async () => {
  const { dir, cleanup } = sandbox()
  try {
    mkdirSync(join(dir, 'src'))
    mkdirSync(join(dir, 'src', 'deep'))
    writeFileSync(join(dir, 'src', 'a.fs'), 'a', 'utf8')
    writeFileSync(join(dir, 'src', 'b.fs'), 'b', 'utf8')
    writeFileSync(join(dir, 'src', 'deep', 'c.fs'), 'c', 'utf8')
    writeFileSync(join(dir, 'readme.md'), 'r', 'utf8')

    const all = unwrap(await glob(dir, '**/*.fs'))
    assert.deepEqual(all.paths, ['src/a.fs', 'src/b.fs', 'src/deep/c.fs'])
    assert.equal('truncated' in all, false)
    const nested = unwrap(await glob(dir, '*.fs'))
    assert.deepEqual(nested.paths, ['src/a.fs', 'src/b.fs', 'src/deep/c.fs'])
    const shallow = unwrap(await glob(dir, 'src/*.fs'))
    assert.deepEqual(shallow.paths, ['src/a.fs', 'src/b.fs'])
    const zeroStar = unwrap(await glob(dir, 'src/**/*.fs'))
    assert.deepEqual(zeroStar.paths, ['src/a.fs', 'src/b.fs', 'src/deep/c.fs'])
  } finally {
    cleanup()
  }
})
test('WHAT[repository-programming-008] JS007_glob_gitignore_skips_git_and_ignored', async () => {
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

    const listing = unwrap(await glob(dir, '**/*'))
    const paths = listing.paths
    assert.equal(paths.some((p) => p.startsWith('.git/') || p === '.git'), false)
    assert.equal(paths.includes('secret.txt'), false)
    assert.equal(paths.includes('dist/out.js'), false)
    assert.equal(paths.includes('src/keep.fs'), true)
    assert.equal(paths.includes('readme.md'), true)
    assert.equal(paths.includes('.gitignore'), true)

    const braces = unwrap(await glob(dir, '**/*.{fs,md}'))
    assert.deepEqual(braces.paths, ['readme.md', 'src/keep.fs'])
  } finally {
    cleanup()
  }
})
}
