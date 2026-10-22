import assert from 'node:assert/strict'
import test from 'node:test'
import { existsSync, mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  LOCALE_FILES,
  PROVIDER_ROOT,
  listSemanticResourceDirs,
  scanParity,
  scanPlaceholderParity,
} from '../../../scripts/checks/language-parity-gate.mjs'

const root = fileURLToPath(new URL('../../..', import.meta.url))

const resourceTrees = [
  resolve(root, PROVIDER_ROOT),
  resolve(root, 'node_modules/wanxiangli/resources/provider'),
]

test('WHAT[distribution-012] every shipped semantic resource carries both declared language leaves', () => {
  for (const providerAbs of resourceTrees) {
    if (!existsSync(providerAbs)) {
      // A bundled tree is only checked when it is actually installed.
      continue
    }
    const semanticDirs = listSemanticResourceDirs(providerAbs)
    const missing = scanParity(semanticDirs, providerAbs)
    assert.deepEqual(missing, [], `${providerAbs}: missing locale leaves`)
  }
})

test('WHAT[distribution-012] every shipped semantic resource carries the same placeholder set in both languages', () => {
  for (const providerAbs of resourceTrees) {
    if (!existsSync(providerAbs)) continue
    const semanticDirs = listSemanticResourceDirs(providerAbs)
    const unequal = scanPlaceholderParity(semanticDirs, providerAbs)
    assert.deepEqual(unequal, [], `${providerAbs}: placeholder set mismatch`)
  }
})

test('WHAT[distribution-012] the distribution check names the specific missing file', () => {
  const directory = mkdtempSync(join(tmpdir(), 'distribution-resources-'))
  try {
    mkdirSync(join(directory, 'role/example'), { recursive: true })
    writeFileSync(join(directory, 'role/example/en.md'), 'Hello, {{name}}.')
    const missing = scanParity(['role/example'], directory)
    assert.equal(missing.length, 1)
    assert.equal(missing[0].code, 'missing-zh-cn')
    assert.equal(missing[0].path, `${PROVIDER_ROOT}/role/example/zh-CN.md`, 'the named file is the missing one')
    assert.match(missing[0].detail, /zh-CN|zh-cn|simplified|missing/i)
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[distribution-012] the distribution check names the specific file whose placeholders differ', () => {
  const directory = mkdtempSync(join(tmpdir(), 'distribution-resources-'))
  try {
    mkdirSync(join(directory, 'role/example'), { recursive: true })
    writeFileSync(join(directory, 'role/example/en.md'), 'Hello, {{name}}.')
    writeFileSync(join(directory, 'role/example/zh-CN.md'), '{{greeting}}，你好。')
    const unequal = scanPlaceholderParity(['role/example'], directory)
    assert.equal(unequal.length, 1)
    assert.equal(unequal[0].code, 'placeholder-parity')
    assert.equal(unequal[0].path, `${PROVIDER_ROOT}/role/example`, 'the named path is the differing pair')
    assert.match(unequal[0].detail, /only-en|only-zh-CN|differ/i)
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[distribution-012] the declared language leaves are exactly the two locale files', () => {
  assert.deepEqual(LOCALE_FILES, ['en.md', 'zh-CN.md'])
})
