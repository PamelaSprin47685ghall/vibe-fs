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

test('WHAT[JS-SEMANTIC-SURFACE-001] JS_SURFACE_001_all_semantic_tests_are_mjs', () => {
  const testFiles = walk(join(ROOT, 'requirements'), ['.test.mjs', '.test.js', '.test.fs', '.test.ts', '.test.fsx'])
  assert.deepEqual(
    testFiles.filter((file) => !file.endsWith('.test.mjs')).map(relativePath),
    [],
    'every automated semantic test must be a .mjs file',
  )
})
