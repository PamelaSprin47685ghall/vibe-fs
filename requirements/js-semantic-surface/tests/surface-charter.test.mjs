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
import { isBuildVerificationOnlyBaseline, run as runBoundaryGate } from '../../../scripts/checks/js-boundary-gate.mjs'
import { usesSurface, validateSurfaceManifest } from '../../../scripts/checks/js-surface-manifest.mjs'
import {
  BUILD_VERIFICATION_FILES,
  SURFACE_CONSUMERS,
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
  if (!existsSync(baselinePath)) {
    assert.deepEqual(actual, {}, 'the ledger may disappear only after absolute zero')
  } else if (Object.keys(actual).length === 0) {
    assert.equal(
      isBuildVerificationOnlyBaseline(baseline),
      true,
      'zero semantic debt requires a deleted ledger or explicit build-verification exemptions',
    )
  }
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

test('WHAT[JS-SEMANTIC-SURFACE-002] JS_SURFACE_002d_zero-debt_generate_removes_empty_ledger', () => {
  const temporaryRoot = mkdtempSync(join(tmpdir(), 'js-boundary-ledger-'))
  const baselinePath = join(temporaryRoot, 'js-boundary-baseline.json')
  try {
    writeFileSync(baselinePath, '{}\n')
    assert.equal(runBoundaryGate({ args: ['--generate', `--out=${baselinePath}`], root: ROOT }), 0)
    assert.equal(existsSync(baselinePath), false)
  } finally {
    rmSync(temporaryRoot, { recursive: true, force: true })
  }
})

test('WHAT[JS-SEMANTIC-SURFACE-002] JS_SURFACE_002e_build-verification_ledger_exemption_survives_zero-debt_cleanup', () => {
  const temporaryRoot = mkdtempSync(join(tmpdir(), 'js-boundary-exemption-'))
  const baselinePath = join(temporaryRoot, 'js-boundary-baseline.json')
  const exemption = { [BUILD_VERIFICATION_FILES.values().next().value]: { 'fable-modules': 1 } }
  try {
    writeFileSync(baselinePath, `${JSON.stringify(exemption)}\n`)
    assert.equal(runBoundaryGate({ args: ['--generate', `--out=${baselinePath}`], root: ROOT }), 0)
    assert.deepEqual(JSON.parse(readFileSync(baselinePath, 'utf8')), exemption)
    assert.equal(runBoundaryGate({ args: [`--out=${baselinePath}`], root: ROOT }), 0)
  } finally {
    rmSync(temporaryRoot, { recursive: true, force: true })
  }
})

// ── 003: law → owner → surface → compiled surface → contract evidence ────────

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

test('WHAT[JS-SEMANTIC-SURFACE-003] JS_SURFACE_003_manifest_rejects_unemitted_or_unauthorized_evidence', () => {
  const temporaryRoot = mkdtempSync(join(tmpdir(), 'js-surface-manifest-'))
  const ownerWhat = join(temporaryRoot, 'requirements', 'owner', 'WHAT.md')
    const source = join(temporaryRoot, 'src', 'Wanxiangshu', 'Owner', 'Surface.fs')
  const fsproj = join(temporaryRoot, 'src', 'Wanxiangshu', 'Wanxiangshu.fsproj')
  const dist = join(temporaryRoot, 'dist', 'Owner', 'Surface.js')
  const testFile = join(temporaryRoot, 'requirements', 'owner', 'tests', 'surface.test.mjs')
  mkdirSync(dirname(ownerWhat), { recursive: true })
  mkdirSync(dirname(source), { recursive: true })
  mkdirSync(dirname(dist), { recursive: true })
  mkdirSync(dirname(testFile), { recursive: true })

  try {
    writeFileSync(ownerWhat, '# OWNER-001\n')
        writeFileSync(source, 'module Owner.Surface\n')
    writeFileSync(fsproj, '<Project><ItemGroup><Compile Include="Owner/Surface.fs"/></ItemGroup></Project>')
    writeFileSync(dist, 'export const value = 1\n')
    writeFileSync(testFile, ['import * as surface ', 'from ', `'${distImport('../../../', 'Owner/Surface.js')}'\nvoid surface\n`].join(''))
    const entry = {
      module: 'Owner/Surface.js',
      owner: 'owner',
      laws: ['OWNER-001'],
      source: 'src/Wanxiangshu/Owner/Surface.fs',
      representation: 'json',
      kind: 'pure',
    }

    assert.equal(usesSurface(readFileSync(testFile, 'utf8'), entry.module), true)
    assert.match(validateSurfaceManifest([entry], temporaryRoot).join('\n'), /no active contract test WHAT law authorizes/)

    rmSync(dist)
    assert.match(validateSurfaceManifest([entry], temporaryRoot).join('\n'), /missing emitted surface/)
  } finally {
    rmSync(temporaryRoot, { recursive: true, force: true })
  }
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

// ── 007: scanner regression — three bypasses that must fail ─────────────────

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

test('WHAT[JS-SEMANTIC-SURFACE-004] JS_SURFACE_004b_support_to_support_transitive_edge_is_scanned', () => {
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

test('WHAT[JS-SEMANTIC-SURFACE-003] JS_SURFACE_003c_usesSurface_rejects_dead_string_and_recognizes_active_imports', () => {
  const module = 'Owner/Surface.js'
  const deadStringSource = [
    `import { value } from '${distImport('../../../', module)}'`,
    "// const value = 'Owner/Surface.js'",
    "const label = 'value'",
    'export const noop = () => null',
  ].join('\n')
  assert.equal(usesSurface(deadStringSource, module), false, 'binding name in a string/comment is not an active use')

  const activeDefaultSource = [
    `import surface from '${distImport('../../../', module)}'`,
    'export const call = () => surface()',
  ].join('\n')
  assert.equal(usesSurface(activeDefaultSource, module), true, 'active default import must be recognized')

  const activeNamedSource = [
    `import { value as v } from '${distImport('../../../', module)}'`,
    'export const call = () => v()',
  ].join('\n')
  assert.equal(usesSurface(activeNamedSource, module), true, 'active named import with alias must be recognized')

  const activeNamespaceSource = [
    `import * as surface from '${distImport('../../../', module)}'`,
    'export const call = () => surface.value()',
  ].join('\n')
  assert.equal(usesSurface(activeNamespaceSource, module), true, 'active namespace import must be recognized')

  const deadDefaultSource = [
    `import surface from '${distImport('../../../', module)}'`,
    'export const noop = () => null',
  ].join('\n')
  assert.equal(usesSurface(deadDefaultSource, module), false, 'imported but never referenced default binding is not active')
})

// ── 003d: per-consumer unauthorized import rejection ───────────────────────
// Registration grants no blanket import authority. A test that actively uses a
// surface must carry a matching law WHAT tag in the owner's tests directory,
// or its package must be declared as an explicit cross-owner consumer. An
// unrelated test importing the surface is a false green, not proof.

test('WHAT[JS-SEMANTIC-SURFACE-003] JS_SURFACE_003d_manifest_rejects_unauthorized_active_consumer', () => {
  const temporaryRoot = mkdtempSync(join(tmpdir(), 'js-surface-unauth-'))
  const ownerWhat = join(temporaryRoot, 'requirements', 'owner', 'WHAT.md')
    const rogueWhat = join(temporaryRoot, 'requirements', 'rogue', 'WHAT.md')
    const source = join(temporaryRoot, 'src', 'Wanxiangshu', 'Owner', 'Surface.fs')
  const fsproj = join(temporaryRoot, 'src', 'Wanxiangshu', 'Wanxiangshu.fsproj')
  const dist = join(temporaryRoot, 'dist', 'Owner', 'Surface.js')
  const ownerTest = join(temporaryRoot, 'requirements', 'owner', 'tests', 'owner.test.mjs')
  const rogueTest = join(temporaryRoot, 'requirements', 'rogue', 'tests', 'rogue.test.mjs')
  mkdirSync(dirname(ownerWhat), { recursive: true })
  mkdirSync(dirname(rogueWhat), { recursive: true })
  mkdirSync(dirname(source), { recursive: true })
  mkdirSync(dirname(dist), { recursive: true })
  mkdirSync(dirname(ownerTest), { recursive: true })
  mkdirSync(dirname(rogueTest), { recursive: true })

  try {
    writeFileSync(ownerWhat, '# OWNER-001\n')
        writeFileSync(rogueWhat, '# ROGUE-001\n')
        writeFileSync(source, 'module Owner.Surface\n')
    writeFileSync(fsproj, '<Project><ItemGroup><Compile Include="Owner/Surface.fs"/></ItemGroup></Project>')
    writeFileSync(dist, 'export const value = 1\n')
    // Authorized: owner dir + matching WHAT tag
    writeFileSync(ownerTest, `import { value } from '${distImport('../../../', 'Owner/Surface.js')}'\ntest('WHAT[OWNER-001] authorized', () => { value() })\n`)
    // Unauthorized: rogue dir, non-matching WHAT tag, no declared consumer edge
    writeFileSync(rogueTest, `import { value } from '${distImport('../../../', 'Owner/Surface.js')}'\ntest('WHAT[ROGUE-001] unauthorized', () => { value() })\n`)

    const entry = {
      module: 'Owner/Surface.js',
      owner: 'owner',
      laws: ['OWNER-001'],
      source: 'src/Wanxiangshu/Owner/Surface.fs',
      representation: 'json',
      kind: 'pure',
    }
    const failures = validateSurfaceManifest([entry], temporaryRoot)
    const unauthorized = failures.filter((f) => f.includes('unauthorized active import'))
    assert.equal(unauthorized.length, 1, `exactly one unauthorized import failure, got: ${failures.join('\n')}`)
    assert.match(unauthorized[0], /rogue\.test\.mjs/)
    assert.match(unauthorized[0], /package rogue has no law or declared consumer edge/)
  } finally {
    rmSync(temporaryRoot, { recursive: true, force: true })
  }
})

// ── 003e: stale consumer metadata for unregistered modules is rejected ──────

test('WHAT[JS-SEMANTIC-SURFACE-003] JS_SURFACE_003e_manifest_rejects_stale_consumer_metadata', () => {
  // SURFACE_CONSUMERS must not carry entries for modules removed from the
  // manifest. A stale entry grants phantom import authority.
  const manifestModules = new Set(SURFACE_MANIFEST.map((e) => e.module))
  const stale = Object.keys(SURFACE_CONSUMERS).filter((m) => !manifestModules.has(m))
  assert.deepEqual(stale, [], `stale SURFACE_CONSUMERS entries for unregistered modules: ${stale.join(', ')}`)
})
