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

test('WHAT[JS-SEMANTIC-SURFACE-002] JS_SURFACE_002_forbidden_patterns_absent_from_semantic_tests', () => {
  assert.deepEqual(wholeScan, {}, 'no semantic test may carry forbidden patterns')
})

test('WHAT[JS-SEMANTIC-SURFACE-002] JS_SURFACE_002c_whole_semantic_test_zone_is_scanned', () => {
  const temporaryRoot = mkdtempSync(join(tmpdir(), 'js-semantic-zone-'))
  const fixturePaths = ['zone-probe.mjs', 'zone-probe.js'].map((name) =>
    join(temporaryRoot, 'requirements', 'probe', 'tests', 'support', name),
  )
  mkdirSync(dirname(fixturePaths[0]), { recursive: true })
  const entriesCall = ['Object.', 'entries'].join('')
  const fixtureSource = [
    'import { leak } ',
    'from ',
    `'${distImport('../../../../', 'Mission/Finality/Workflow.js')}'`,
    '\nexport const leak = (value) => value.',
    'f',
    'ields[0]\n',
    `export const prefixLookup = (mod) => ${entriesCall}(mod).find(([key]) => key.startsWith('${['Foo', '__'].join('')}'))?.[1]\n`,
    `export const suffixLookup = (key) => key.endsWith('${['_', 'Bar'].join('')}')\n`,
  ].join('')

  try {
    for (const fixturePath of fixturePaths) writeFileSync(fixturePath, fixtureSource)
    const scanned = scanAll(join(temporaryRoot, 'requirements'))
    for (const fixturePath of fixturePaths) {
      const hits = scanned[relativePath(fixturePath)]
      assert.ok(hits && hits.length > 0, `generated support fixture must be scanned: ${JSON.stringify(scanned)}`)
      assert.ok(hits.some((hit) => hit.rule === 'deep-dist-import'), 'generated fixture must report its internal import')
      assert.ok(hits.some((hit) => hit.rule === 'du-shape'), 'generated fixture must report its representation access')
      const discoveryHits = hits.filter((hit) => hit.rule === 'export-discovery' || hit.rule === 'mangled-lookup')
      assert.ok(
        discoveryHits.length >= 2,
        'generated fixture must report emitted-name discovery and both mangled prefix/suffix lookups',
      )
    }
  } finally {
    rmSync(temporaryRoot, { recursive: true, force: true })
  }
})

test('WHAT[JS-SEMANTIC-SURFACE-002] JS_SURFACE_002f_template_dist_import_is_detected', () => {
  const temporaryRoot = mkdtempSync(join(tmpdir(), 'js-template-import-'))
  const fixturePath = join(temporaryRoot, 'requirements', 'probe', 'tests', 'probe.test.mjs')
  mkdirSync(dirname(fixturePath), { recursive: true })
  try {
    // Split the template pattern so the scanner does not see this test file
    // itself as debt — the fixture is the subject, not this charter test.
    const distPart = ['dist', '/${modulePath}.'].join('')
    const jsPart = ['j', 's`, import.meta.url).pathname)'].join('')
    const fixtureSource = [
      'const load = (modulePath) => import(new ',
      'URL(`../../../',
      distPart,
      jsPart,
      '\nexport const probe = () => load("Internal/Module")',
    ].join('')
    writeFileSync(fixturePath, fixtureSource)
    const scanned = scanAll(join(temporaryRoot, 'requirements'))
    const hits = scanned[relativePath(fixturePath)]
    assert.ok(
      hits && hits.some((hit) => hit.rule === 'template-dist-import'),
      'new URL template dynamic import of dist must be detected as debt',
    )
  } finally {
    rmSync(temporaryRoot, { recursive: true, force: true })
  }
})
