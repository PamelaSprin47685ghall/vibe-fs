import assert from 'node:assert/strict'
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { dirname, join, relative, resolve } from 'node:path'
import { tmpdir } from 'node:os'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { assertJsData, assertOpaque, isJsData } from '../../verification-system/tests/support/js-contract.mjs'
import { validateModuleLinkage } from '../../../scripts/checks/js-module-linkage.mjs'
import { validateSurfaceManifest } from '../../../scripts/checks/js-surface-manifest.mjs'
import {
  BUILD_VERIFICATION_FILES,
  SURFACE_MANIFEST,
  scanAll,
  semanticImportEdges,
  semanticTestFiles,
} from '../../../scripts/lib/test-surface-scan.mjs'
import { walk } from '../../../scripts/lib/walk.mjs'

const ROOT = resolve(join(dirname(fileURLToPath(import.meta.url)), '../../..'))

const read = (path) => readFileSync(join(ROOT, path), 'utf8')

const relativePath = (path) => relative(process.cwd(), path).replace(/\\/g, '/')

const distImport = (prefix, module) => `${prefix}dist/${module}`

const wholeScan = scanAll(join(ROOT, 'requirements'))

const wholeSemanticFiles = new Set(semanticTestFiles(join(ROOT, 'requirements')).map(relativePath))

const wholeSemanticImportEdges = semanticImportEdges(join(ROOT, 'requirements'))

test('WHAT[JS-SEMANTIC-SURFACE-005] JS_SURFACE_005_js_native_representation_rules', () => {
  assert.equal(isJsData(null), true)
  assert.equal(isJsData('s'), true)
  assert.equal(isJsData(42), true)
  assert.equal(isJsData(true), true)
  assert.equal(isJsData(10n), true)
  assert.equal(isJsData([]), true)
  assert.equal(isJsData({ a: [1, { b: 'c' }] }), true)

  assert.equal(isJsData({ tag: 0, fields: ['x'] }), false)
  assert.equal(isJsData({ cases: () => ['A', 'B'] }), false)
  assert.equal(isJsData({ head: 1, tail: null }), false)
  assert.equal(isJsData({ $reflection: {}, value: 1 }), false)
  assert.equal(isJsData(new Date()), false)

  const fsharpMapLike = new (class {
    constructor() {
      this.size = 0
    }
    entries() {
      return []
    }
  })()
  assert.equal(isJsData(fsharpMapLike), false)
  assert.throws(() => assertJsData({ tag: 1, fields: [] }), /JS-native/)
  assert.equal(assertJsData({ ok: true, value: [1, 2] }).ok, true)

  assert.equal(assertOpaque({}, 'h') !== undefined, true)
  assert.equal(assertOpaque(() => {}, 'f') !== undefined, true)
  assert.throws(() => assertOpaque('s'), /opaque/)
  assert.throws(() => assertOpaque(1), /opaque/)
})
