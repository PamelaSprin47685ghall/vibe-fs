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

test('WHAT[js-semantic-surface-004] JS_SURFACE_004_helper_not_directly_tested', () => {
  const violations = []

  for (const { importer, target } of wholeSemanticImportEdges) {
    const importerRel = relativePath(importer)
    const targetRel = relativePath(target)
    if (!wholeSemanticFiles.has(targetRel) || targetRel.endsWith('.test.mjs')) continue
    if (targetRel.endsWith('verification-system/tests/support/js-contract.mjs')) continue
    if ((wholeScan[targetRel] ?? []).length === 0) continue
    violations.push(`${importerRel} imports debt-bearing helper ${targetRel}`)
  }

  assert.deepEqual(violations, [], violations.join('\n'))
})

test('WHAT[js-semantic-surface-004] JS_SURFACE_004b_support_to_support_transitive_edge_is_scanned', () => {
  const temporaryRoot = mkdtempSync(join(tmpdir(), 'js-transitive-edge-'))
  const testPath = join(temporaryRoot, 'requirements', 'probe', 'tests', 'probe.test.mjs')
  const supportA = join(temporaryRoot, 'requirements', 'probe', 'tests', 'support', 'a.mjs')
  const supportB = join(temporaryRoot, 'requirements', 'probe', 'tests', 'support', 'b.mjs')
  mkdirSync(dirname(supportA), { recursive: true })
  try {
    writeFileSync(testPath, "import './support/a.mjs'\n")
    writeFileSync(supportA, "import './b.mjs'\n")
    writeFileSync(supportB, "export const x = 1\n")
    const edges = semanticImportEdges(join(temporaryRoot, 'requirements'))
    const edgeStrings = edges.map((e) => `${relativePath(e.importer)} -> ${relativePath(e.target)}`)
    assert.ok(
      edgeStrings.some((s) => s.includes('a.mjs') && s.includes('b.mjs')),
      `support→support transitive edge must be traversed: ${edgeStrings.join('; ')}`,
    )
  } finally {
    rmSync(temporaryRoot, { recursive: true, force: true })
  }
})
