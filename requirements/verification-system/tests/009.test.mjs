import assert from 'node:assert/strict'
import {
  copyFileSync,
  existsSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  readdirSync,
  rmSync,
  writeFileSync,
} from 'node:fs'
import { tmpdir } from 'node:os'
import path, { basename, dirname, join, resolve } from 'node:path'
import { spawnSync } from 'node:child_process'
import test from 'node:test'
import { fileURLToPath } from 'node:url'

import { checks } from '../../../scripts/check.mjs'
import { E2E_ROOT_REL, e2eTestCaseFiles } from './e2e/support/watchdog-feed-scan.mjs'
import { assessIntegrationEntryCoverage } from './support/integration-entry-coverage.mjs'
import { discoverSuiteTests } from './support/discover-suite-tests.mjs'
import { integrationNodeTestSteps } from './support/integration-node-test-steps.mjs'
import { walk } from '../../../scripts/lib/walk.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')
const packageIntegrationDir = path.join(ROOT, 'requirements/distribution/tests/integration/package')
const normalize = (file) => path.relative(ROOT, file).split(path.sep).join('/')
const assess = (discoveredTests, wiredTests, childOwnedTests = []) =>
  assessIntegrationEntryCoverage({ discoveredTests, wiredTests, childOwnedTests })

const write = (root, relPath, text) => {
  const target = join(root, relPath)
  mkdirSync(join(target, '..'), { recursive: true })
  writeFileSync(target, text)
}

const runNode = (root, args) => {
  const env = { ...process.env }
  delete env.NODE_TEST_CONTEXT
  return spawnSync(process.execPath, args, {
    cwd: root,
    encoding: 'utf8',
    env,
  })
}

const makeTempRoot = (layout) => {
  const root = mkdtempSync(join(tmpdir(), 'e2e-wdf-fc-'))
  const e2e = join(root, E2E_ROOT_REL)
  if (layout.e2eDir !== false) mkdirSync(e2e, { recursive: true })
  for (const name of layout.files ?? []) {
    writeFileSync(join(e2e, name), '// throwaway\n')
  }
  if (layout.e2eIsFile) {
    rmSync(e2e, { recursive: true, force: true })
    writeFileSync(e2e, 'not a directory\n')
  }
  return root
}

const cleanup = (root) => rmSync(root, { recursive: true, force: true })

// ── Repository closure gates ────────────────────────────────────────────────

test('WHAT[VERIFICATION-SYSTEM-009] repository closure gates reject an unassigned production source and package member', () => {
  const fixture = mkdtempSync(join(tmpdir(), 'repository-closure-'))

  try {
    mkdirSync(join(fixture, 'scripts/checks'), { recursive: true })
    mkdirSync(join(fixture, 'scripts/lib'), { recursive: true })
    copyFileSync(
      join(ROOT, 'scripts/checks/subsystems.mjs'),
      join(fixture, 'scripts/checks/subsystems.mjs'),
    )
    copyFileSync(
      join(ROOT, 'scripts/lib/compile-shards.mjs'),
      join(fixture, 'scripts/lib/compile-shards.mjs'),
    )
    write(fixture, 'scripts/checks/subsystems.json', JSON.stringify({
      schema_version: 1,
      subsystems: [{ id: 'fixture', legacy_owners: [] }],
    }))
    write(fixture, 'src/Wanxiangshu/Wanxiangshu.fsproj', `
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <Compile Include="Owned.fsi"/>
    <Compile Include="Owned.fs"/>
  </ItemGroup>
</Project>
`)
    write(fixture, 'src/Wanxiangshu/Wanxiangshu.Shard.fixture.owned.fsproj', `
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <WanxiangshuSubsystem>fixture</WanxiangshuSubsystem>
    <WanxiangshuCompileShard>owned</WanxiangshuCompileShard>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Owned.fsi"/>
    <Compile Include="Owned.fs"/>
  </ItemGroup>
</Project>
`)
    write(fixture, 'src/Wanxiangshu/Owned.fsi', 'namespace ClosureFixture\n')
    write(fixture, 'src/Wanxiangshu/Owned.fs', 'namespace ClosureFixture\n')
    write(fixture, 'src/Wanxiangshu/Unowned.fs', 'namespace ClosureFixture\n')

    const subsystemGate = runNode(fixture, ['scripts/checks/subsystems.mjs'])
    assert.equal(subsystemGate.status, 1, subsystemGate.stderr || subsystemGate.stdout)
    assert.match(subsystemGate.stderr, /production source coverage mismatch/)
    assert.match(subsystemGate.stderr, /src\/Wanxiangshu\/Unowned\.fs/)

    mkdirSync(join(fixture, 'requirements/distribution/tests'), { recursive: true })
    copyFileSync(
      join(ROOT, 'requirements/distribution/tests/package-policy.test.mjs'),
      join(fixture, 'requirements/distribution/tests/package-policy.test.mjs'),
    )
    write(fixture, 'package.json', JSON.stringify({
      main: './dist/OpenCode/Plugin/Plugin.js',
      exports: { '.': './dist/OpenCode/Plugin/Plugin.js' },
      files: ['resources/'],
      scripts: {},
    }))

    mkdirSync(join(fixture, 'dist/OpenCode/Plugin'), { recursive: true })
    write(fixture, 'dist/OpenCode/Plugin/Plugin.js', 'export default {}\n')

    const packageGate = runNode(fixture, [
      '--test',
      '--test-name-pattern=DISTRIBUTION_files_whitelist_is_explicit',
      'requirements/distribution/tests/package-policy.test.mjs',
    ])
    assert.equal(packageGate.status, 1, packageGate.stderr || packageGate.stdout)
    assert.match(`${packageGate.stdout}\n${packageGate.stderr}`, /files whitelist must include dist/)
  } finally {
    rmSync(fixture, { recursive: true, force: true })
  }
})

// ── Ladder step targets and wired gate existence ────────────────────────────

test('WHAT[VERIFICATION-SYSTEM-009] every ladder step target exists as a real file', () => {
  const e2eTarget = 'requirements/verification-system/tests/e2e/014.test.mjs'
  const required = [
    'scripts/check.mjs',
    'scripts/build.mjs',
    'requirements/verification-system/tests/run.mjs',
    'requirements/verification-system/tests/integration/run.mjs',
    'requirements/distribution/tests/integration/package/run.mjs',
    'scripts/warmup-opencode.mjs',
    e2eTarget,
  ]
  for (const rel of required) {
    assert.ok(existsSync(join(ROOT, rel)), `ladder step target missing: ${rel}`)
  }
})

test('WHAT[VERIFICATION-SYSTEM-009] every wired gate path exists and checks set matches scripts/checks directory', () => {
  for (const gatePath of checks) {
    assert.ok(
      existsSync(gatePath),
      `check.mjs wires a gate that does not exist: ${gatePath}`,
    )
  }

  const dirEntries = readdirSync(join(ROOT, 'scripts/checks'))
  const fsxFiles = dirEntries.filter((f) => f.endsWith('.fsx'))
  assert.deepEqual(fsxFiles, [], 'scripts/checks must contain no .fsx custom compiler executables')

  const nonGateFiles = new Set([
    'owner-impact-corpus.json',
    'subsystems.json',
    'release-closure-nodes.json',
    'fsharp-control-pyramid-guide.mjs',
    'js-module-linkage.mjs',
    'js-surface-gate.mjs',
    'js-surface-manifest.mjs',
  ])

  const wiredBasenames = new Set(checks.map((p) => basename(p)))
  const gateScriptsInDir = dirEntries.filter((f) => f.endsWith('.mjs') && !nonGateFiles.has(f))

  assert.deepEqual(
    [...wiredBasenames].sort(),
    gateScriptsInDir.sort(),
    'checks registered in check.mjs must equal gate scripts in scripts/checks',
  )
})

test('WHAT[VERIFICATION-SYSTEM-009] no custom FCS executable remains after the full-repo ban', () => {
  for (const rel of [
    'scripts/checks/locality-symbol-uses.fsx',
    'scripts/checks/locality-dependencies.mjs',
    'scripts/checks/locality-slice-report.mjs',
    'scripts/lib/locality-dependencies.mjs',
  ]) {
    assert.ok(!existsSync(join(ROOT, rel)), `custom FCS executable must stay deleted: ${rel}`)
  }
  assert.deepEqual(
    readdirSync(join(ROOT, 'scripts/checks')).filter((name) => name.endsWith('.fsx')),
    [],
    'scripts/checks must contain no .fsx custom compiler executables',
  )
  const banned = [
    'FSharp.Compiler.Service',
    'Fable.Compiler.dll',
    'Fable.AST.dll',
    'locality-symbol-uses',
    'scanCompilerObservationsV1',
    'scanDslCompilerEvidence',
    'runLocalityDependencyScan',
    'scanProductionLocalitySliceReportV1',
  ]
  const hits = []
  const walkScripts = (dir) => {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      const full = join(dir, entry.name)
      if (entry.isDirectory()) walkScripts(full)
      else if (entry.name.endsWith('.mjs')) {
        const text = readFileSync(full, 'utf8')
        for (const token of banned) {
          if (text.includes(token)) hits.push(`${full}: ${token}`)
        }
      }
    }
  }
  walkScripts(join(ROOT, 'scripts'))
  assert.deepEqual(hits, [], 'scripts must not reference deleted custom FCS executables or assemblies')
})

// ── E2E root fail-closed ────────────────────────────────────────────────────

test('WHAT[VERIFICATION-SYSTEM-009] missing e2e root fails closed, not green with zero files', () => {
  const root = mkdtempSync(join(tmpdir(), 'e2e-wdf-fc-'))
  try {
    assert.throws(
      () => e2eTestCaseFiles(root),
      /e2e root missing or unreadable/,
      'missing e2e root must fail closed (throw), not return []',
    )
  } finally {
    cleanup(root)
  }
})

test('WHAT[VERIFICATION-SYSTEM-009] non-directory e2e root fails closed', () => {
  const root = makeTempRoot({ e2eIsFile: true })
  try {
    assert.throws(
      () => e2eTestCaseFiles(root),
      /e2e root is not a directory/,
      'a file where the e2e root directory should be must fail closed',
    )
  } finally {
    cleanup(root)
  }
})

// ── Integration entry coverage and suite discovery ──────────────────────────

test('WHAT[VERIFICATION-SYSTEM-009] integration entry coverage accepts an exact reachable set', () => {
  assert.deepEqual(
    assess(
      ['requirements/a/tests/integration/a.test.mjs', 'requirements/b/tests/integration/b.test.mjs'],
      ['requirements/a/tests/integration/a.test.mjs', 'requirements/b/tests/integration/b.test.mjs'],
    ),
    { ok: true, missingFromEntry: [], staleEntry: [], duplicateWiring: [] },
  )
})

test('WHAT[VERIFICATION-SYSTEM-009] integration entry coverage delegates the exact declared child-owned set', () => {
  assert.deepEqual(
    assess(
      [
        'requirements/a/tests/integration/a.test.mjs',
        'requirements/distribution/tests/integration/package/layout.test.mjs',
      ],
      ['requirements/a/tests/integration/a.test.mjs'],
      ['requirements/distribution/tests/integration/package/layout.test.mjs'],
    ),
    { ok: true, missingFromEntry: [], staleEntry: [], duplicateWiring: [] },
  )
})

test('WHAT[VERIFICATION-SYSTEM-009] discoverSuiteTests lists every package *.test.mjs and excludes the runner', () => {
  const discovered = discoverSuiteTests(packageIntegrationDir)
  assert.ok(discovered.includes('001.test.mjs'))
  assert.ok(discovered.includes('003.test.mjs'))
  assert.ok(discovered.includes('004.test.mjs'))
  assert.ok(discovered.includes('008.test.mjs'))
  assert.ok(!discovered.includes('run.mjs'))
  assert.deepEqual(discovered, [...new Set(discovered)].sort())
})

test('WHAT[VERIFICATION-SYSTEM-009] discoverSuiteTests auto-includes an added test and excludes non-test files', () => {
  const scratch = mkdtempSync(path.join(tmpdir(), 'pkg-suite-'))
  try {
    writeFileSync(path.join(scratch, 'alpha.test.mjs'), '// noop\n')
    writeFileSync(path.join(scratch, 'beta.test.mjs'), '// noop\n')
    writeFileSync(path.join(scratch, 'run.mjs'), '// runner\n')
    writeFileSync(path.join(scratch, 'helper.mjs'), '// helper\n')
    mkdirSync(path.join(scratch, 'nested'))
    writeFileSync(path.join(scratch, 'nested', 'ignored.test.mjs'), '// nested\n')
    const discovered = discoverSuiteTests(scratch)
    assert.deepEqual(discovered, ['alpha.test.mjs', 'beta.test.mjs'])
  } finally {
    rmSync(scratch, { recursive: true, force: true })
  }
})

test('WHAT[VERIFICATION-SYSTEM-009] discoverSuiteTests fail-closes on an unreadable directory', () => {
  assert.deepEqual(discoverSuiteTests(path.join(tmpdir(), 'does-not-exist-suite-xyz')), [])
})

test('WHAT[VERIFICATION-SYSTEM-009] parent delegation set equals the child-executed set (no drift)', () => {
  const childExecuted = discoverSuiteTests(packageIntegrationDir)
  const parentDelegated = discoverSuiteTests(packageIntegrationDir)
  assert.deepEqual(childExecuted, parentDelegated)
  assert.ok(childExecuted.length > 0, 'package integration dir must own at least one suite')
})

test('WHAT[VERIFICATION-SYSTEM-009] the real integration entry covers every discovered integration test', () => {
  const discoveredIntegrationTests = walk(path.join(ROOT, 'requirements'), ['.test.mjs'])
    .map(normalize)
    .filter((file) => file.includes('/tests/integration/'))
  const childOwnedIntegrationTests = discoverSuiteTests(packageIntegrationDir).map((name) =>
    normalize(path.join(packageIntegrationDir, name)),
  )
  const wiredIntegrationTests = integrationNodeTestSteps(ROOT).flatMap((step) =>
    step.files.map(normalize),
  )
  const result = assessIntegrationEntryCoverage({
    discoveredTests: discoveredIntegrationTests,
    wiredTests: wiredIntegrationTests,
    childOwnedTests: childOwnedIntegrationTests,
  })
  assert.equal(result.ok, true, JSON.stringify(result, null, 2))
  assert.deepEqual(childOwnedIntegrationTests.sort(), [
    'requirements/distribution/tests/integration/package/001.test.mjs',
    'requirements/distribution/tests/integration/package/003.test.mjs',
    'requirements/distribution/tests/integration/package/004.test.mjs',
    'requirements/distribution/tests/integration/package/008.test.mjs',
  ])
})
