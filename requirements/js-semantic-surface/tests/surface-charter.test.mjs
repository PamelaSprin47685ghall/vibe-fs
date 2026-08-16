// requirements/js-semantic-surface/tests/surface-charter.test.mjs
//
// The META contract is proved by observable scanner/manifest properties. The
// charter never grants itself an exemption: compiler/build verification is the
// only explicit quarantine, while migration debt remains visible to the gate.

import assert from 'node:assert/strict'
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { dirname, join, relative, resolve } from 'node:path'
import { tmpdir } from 'node:os'
import test from 'node:test'
import { fileURLToPath } from 'node:url'

import { assertJsData, assertOpaque, isJsData } from '../../verification-system/tests/support/js-contract.mjs'
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

// ── 001: all semantic tests are JavaScript ──────────────────────────────────

test('WHAT[JS-SEMANTIC-SURFACE-001] JS_SURFACE_001_all_semantic_tests_are_mjs', () => {
  const testFiles = walk(join(ROOT, 'requirements'), ['.test.mjs', '.test.js', '.test.fs', '.test.ts', '.test.fsx'])
  assert.deepEqual(
    testFiles.filter((file) => !file.endsWith('.test.mjs')).map(relativePath),
    [],
    'every automated semantic test must be a .mjs file',
  )
})

// ── 002: the gate observes actual whole-corpus debt ─────────────────────────

test('WHAT[JS-SEMANTIC-SURFACE-002] JS_SURFACE_002_forbidden_patterns_absent_from_semantic_tests', () => {
  const actual = scanAll()
  const baselinePath = join(ROOT, 'scripts/checks/js-boundary-baseline.json')
  const baseline = existsSync(baselinePath) ? JSON.parse(readFileSync(baselinePath, 'utf8')) : {}
  const excess = []

  for (const file of Reflect.ownKeys(actual)) {
    const allowed = baseline[file] ?? {}
    const byRule = {}
    for (const hit of actual[file]) byRule[hit.rule] = (byRule[hit.rule] ?? 0) + 1
    for (const rule of Reflect.ownKeys(byRule)) {
      if (byRule[rule] > (allowed[rule] ?? 0)) excess.push(`${file}: ${rule} ${allowed[rule] ?? 0} -> ${byRule[rule]}`)
    }
    if (!Object.prototype.hasOwnProperty.call(baseline, file)) excess.push(`${file}: NEW debt`)
  }

  assert.deepEqual(excess, [], `forbidden patterns beyond the approved migration ledger must be absent: ${excess.join('; ')}`)
  if (!existsSync(baselinePath)) assert.deepEqual(actual, {}, 'the ledger may disappear only after absolute zero')
})

test('WHAT[JS-SEMANTIC-SURFACE-002] JS_SURFACE_002c_whole_semantic_test_zone_is_scanned', () => {
  const temporaryRoot = mkdtempSync(join(tmpdir(), 'js-semantic-zone-'))
  const fixturePath = join(temporaryRoot, 'requirements', 'probe', 'tests', 'support', 'zone-probe.mjs')
  mkdirSync(dirname(fixturePath), { recursive: true })
  const entriesCall = ['Object.', 'entries'].join('')
  const fixtureSource = [
    'import { leak } ',
    'from ',
    "'../../../../dist/Mission/Finality/Workflow.js'",
    '\nexport const leak = (value) => value.',
    'f',
    'ields[0]\n',
    `export const prefixLookup = (mod) => ${entriesCall}(mod).find(([key]) => key.startsWith('${['Foo', '__'].join('')}'))?.[1]\n`,
    `export const suffixLookup = (key) => key.endsWith('${['_', 'Bar'].join('')}')\n`,
  ].join('')

  try {
    writeFileSync(fixturePath, fixtureSource)
    const scanned = scanAll(join(temporaryRoot, 'requirements'))
    const hits = scanned[relativePath(fixturePath)]
    assert.ok(hits && hits.length > 0, `generated support fixture must be scanned: ${JSON.stringify(scanned)}`)
    assert.ok(hits.some((hit) => hit.rule === 'deep-dist-import'), 'generated fixture must report its internal import')
    assert.ok(hits.some((hit) => hit.rule === 'du-shape'), 'generated fixture must report its representation access')
    const discoveryHits = hits.filter((hit) => hit.rule === 'export-discovery' || hit.rule === 'mangled-lookup')
    assert.ok(
      discoveryHits.length >= 2,
      'generated fixture must report emitted-name discovery and both mangled prefix/suffix lookups',
    )
  } finally {
    rmSync(temporaryRoot, { recursive: true, force: true })
  }
})

// ── 003: law → owner → source → compiled surface → contract evidence ────────

test('WHAT[JS-SEMANTIC-SURFACE-003] JS_SURFACE_003_law_owner_surface_registry', () => {
  const failures = validateSurfaceManifest(SURFACE_MANIFEST, ROOT)
  assert.deepEqual(failures, [], failures.join('\n'))
})

test('WHAT[JS-SEMANTIC-SURFACE-003] JS_SURFACE_003_every_registered_surface_has_a_contract_test', () => {
  assert.ok(SURFACE_MANIFEST.length > 0)
  assert.deepEqual(validateSurfaceManifest(SURFACE_MANIFEST, ROOT), [])
})

test('WHAT[JS-SEMANTIC-SURFACE-002] JS_SURFACE_002b_registered_surfaces_exist_in_the_production_source_tree', () => {
  assert.deepEqual(validateSurfaceManifest(SURFACE_MANIFEST, ROOT), [])
})

// ── 004: a debt-bearing helper is not a new direct test subject ─────────────

test('WHAT[JS-SEMANTIC-SURFACE-004] JS_SURFACE_004_helper_not_directly_tested', () => {
  const baselinePath = join(ROOT, 'scripts/checks/js-boundary-baseline.json')
  const baseline = existsSync(baselinePath) ? JSON.parse(readFileSync(baselinePath, 'utf8')) : {}
  const scan = scanAll()
  const files = new Set(semanticTestFiles().map(relativePath))
  const violations = []

  for (const { importer, target } of semanticImportEdges()) {
    const importerRel = relativePath(importer)
    const targetRel = relativePath(target)
    if (!files.has(targetRel) || targetRel.endsWith('.test.mjs')) continue
    if (targetRel.endsWith('verification-system/tests/support/js-contract.mjs')) continue
    if ((scan[targetRel] ?? []).length === 0) continue
    if (Object.prototype.hasOwnProperty.call(baseline, targetRel)) continue
    violations.push(`${importerRel} imports debt-bearing helper ${targetRel}`)
  }

  assert.deepEqual(violations, [], violations.join('\n'))
})

// ── 005: JS-native data and opaque capability validators ────────────────────

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

// ── 006: only compiler/build verification may be explicitly quarantined ────

test('WHAT[JS-SEMANTIC-SURFACE-006] JS_SURFACE_006_fable_representation_not_contract', () => {
  const existing = [...BUILD_VERIFICATION_FILES].filter((file) => existsSync(join(ROOT, file)))
  assert.ok(existing.length > 0, 'compiler/build quarantine must have at least one live entry')

  for (const file of existing) {
    const compilerOrDistribution =
      file.startsWith('requirements/verification-system/') || file.startsWith('requirements/distribution/')
    assert.equal(compilerOrDistribution, true, `quarantine ${file} must remain outside product semantic packages`)
    const text = read(file)
    const knowsCompiledSurface =
      text.includes('dist' + '/') ||
      text.includes('fable' + '_modules') ||
      text.includes('F' + 'Sharp') ||
      text.includes('.' + 'fields') ||
      text.includes('.' + 'tag')
    assert.equal(knowsCompiledSurface, true, `quarantine ${file} must prove it knows a compiled/representation subject`)
  }
})
