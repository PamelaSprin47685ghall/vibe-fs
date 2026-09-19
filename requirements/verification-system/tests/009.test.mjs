import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { existsSync, mkdtempSync, mkdirSync, writeFileSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const { E2E_ROOT_REL, SOLE_ENTRY, e2eTestCaseFiles, scanE2EWatchdogFeed } = await import("./e2e/support/watchdog-feed-scan.mjs");

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

test('WHAT[verification-system-009] missing e2e root fails closed, not green with zero files', () => {
  // A gate whose path criterion points at a non-existent directory is a fake
  // gate (always-passing). Missing root must throw, never return [].
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
test('WHAT[verification-system-009] non-directory e2e root fails closed', () => {
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
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, mkdirSync, writeFileSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { default: path } = await import("node:path");
const { fileURLToPath } = await import("node:url");
const { assessIntegrationEntryCoverage } = await import("./support/integration-entry-coverage.mjs");
const { discoverSuiteTests } = await import("./support/discover-suite-tests.mjs");
const { integrationNodeTestSteps, selectIntegrationSteps } = await import("./support/integration-node-test-steps.mjs");
const { walk } = await import("../../../scripts/lib/walk.mjs");

const assess = (discoveredTests, wiredTests, childOwnedTests = []) =>
  assessIntegrationEntryCoverage({ discoveredTests, wiredTests, childOwnedTests })
const here = path.dirname(fileURLToPath(import.meta.url))
const root = path.resolve(here, '../../..')
const packageIntegrationDir = path.join(root, 'requirements/distribution/tests/integration/package')
const normalize = (file) => path.relative(root, file).split(path.sep).join('/')

test('WHAT[verification-system-009] integration entry coverage accepts an exact reachable set', () => {
  assert.deepEqual(
    assess(
      ['requirements/a/tests/integration/a.test.mjs', 'requirements/b/tests/integration/b.test.mjs'],
      ['requirements/a/tests/integration/a.test.mjs', 'requirements/b/tests/integration/b.test.mjs'],
    ),
    { ok: true, missingFromEntry: [], staleEntry: [], duplicateWiring: [] },
  )
})
test('WHAT[verification-system-009] integration entry coverage delegates the exact declared child-owned set', () => {
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
test('WHAT[verification-system-009] discoverSuiteTests lists every package *.test.mjs and excludes the runner', () => {
  const discovered = discoverSuiteTests(packageIntegrationDir)
  // The four real package suites are all picked up.
  assert.ok(discovered.includes('001.test.mjs'))
  assert.ok(discovered.includes('003.test.mjs'))
  assert.ok(discovered.includes('004.test.mjs'))
  assert.ok(discovered.includes('008.test.mjs'))
  // The runner itself and any non-test file are excluded by suffix.
  assert.ok(!discovered.includes('run.mjs'))
  // Deterministic, sorted, deduplicated.
  assert.deepEqual(discovered, [...new Set(discovered)].sort())
})
test('WHAT[verification-system-009] discoverSuiteTests auto-includes an added test and excludes non-test files', () => {
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
test('WHAT[verification-system-009] discoverSuiteTests fail-closes on an unreadable directory', () => {
  // A non-existent directory yields an empty set rather than throwing; the
  // child runner turns an empty set into a non-zero exit.
  assert.deepEqual(discoverSuiteTests(path.join(tmpdir(), 'does-not-exist-suite-xyz')), [])
})
test('WHAT[verification-system-009] parent delegation set equals the child-executed set (no drift)', () => {
  // Both the parent entry and the child runner consume discoverSuiteTests on
  // the same directory, so the delegated set must be exactly the set the child
  // runs. If these ever diverge, an added package test is silently omitted.
  const childExecuted = discoverSuiteTests(packageIntegrationDir)
  const parentDelegated = discoverSuiteTests(packageIntegrationDir)
  assert.deepEqual(childExecuted, parentDelegated)
  assert.ok(childExecuted.length > 0, 'package integration dir must own at least one suite')
})
test('WHAT[verification-system-009] the real integration entry covers every discovered integration test', () => {
  // Behavior proof against the real repository state: walk requirements for
  // *.test.mjs under tests/integration, delegate the exact discovered package
  // set, and assert the entry coverage is green using the SAME wired set the
  // parent run.mjs executes (single source of truth in
  // integration-node-test-steps.mjs). An added package test is delegated
  // automatically; an added non-package test that is not wired into the shared
  // steps makes this go red.
  const discoveredIntegrationTests = walk(path.join(root, 'requirements'), ['.test.mjs'])
    .map(normalize)
    .filter((file) => file.includes('/tests/integration/'))
  const childOwnedIntegrationTests = discoverSuiteTests(packageIntegrationDir).map((name) =>
    normalize(path.join(packageIntegrationDir, name)),
  )
  const wiredIntegrationTests = integrationNodeTestSteps(root).flatMap((step) =>
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
}

{
const { default: assert } = await import("node:assert/strict");
const { verify, verificationSteps } = await import("../../../scripts/verify.mjs");
const { checks } = await import("../../../scripts/check.mjs");
const { existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, readlinkSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { basename, dirname, join, resolve } = await import("node:path");
const { fileURLToPath } = await import("node:url");
const { default: test } = await import("node:test");

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')
function createMemorySink() {
  let buf = ''
  return {
    write(chunk) {
      buf += chunk
    },
    get output() {
      return buf
    },
  }
  }
for (const failingLabel of ['format:check', 'check', 'build']) {
  test(`WHAT[verification-system-001] verify halts and marks subsequent steps not-run when ${failingLabel} fails`, async () => {
    const tmpLogDir = mkdtempSync(join(tmpdir(), 'proof-ladder-fail-'))
    const sink = createMemorySink()
  const spawned = []
  const fakeRunStep = async ({ label, argv }) => {
      spawned.push(label)
      if (label === failingLabel) {
        return { label, ok: false, exitCode: 1, signal: null, durationMs: 5 }
  }
      return { label, ok: true, exitCode: 0, signal: null, durationMs: 5 }
    }

    try {
      const result = await verify({
        release: false,
        runStep: fakeRunStep,
        output: sink,
        logDirectory: tmpLogDir,
})
      assert.equal(result.exitCode, 1)
      assert.equal(result.outcome, 'fail')
      assert.equal(spawned.at(-1), failingLabel, `execution must stop after ${failingLabel}`)

      const failedIdx = result.steps.findIndex((s) => s.label === failingLabel)
      assert.ok(failedIdx >= 0)
      assert.equal(result.steps[failedIdx].status, 'failed')
      for (let i = failedIdx + 1; i < result.steps.length; i++) {
        assert.equal(result.steps[i].status, 'not-run', `step ${result.steps[i].label} must be marked not-run`)
    }
      assert.match(sink.output, /FAIL  verify daily/)
    } finally {
      rmSync(tmpLogDir, { recursive: true, force: true })
    }
})
  }

test('WHAT[verification-system-009] every ladder step target exists as a real file', () => {
  // 层序里的每个入口都必须是真实文件：指向不存在文件的命令恒为「没跑到」，
  // 层序 pin 就退化成文字装饰（VERIFY-004 静态门禁必须命中真实路径）。
  const required = [
    'scripts/check.mjs',
    'scripts/build.mjs',
    'requirements/verification-system/tests/run.mjs',
    'requirements/verification-system/tests/integration/run.mjs',
    'requirements/distribution/tests/integration/package/run.mjs',
    'scripts/warmup-opencode.mjs',
    'requirements/verification-system/tests/e2e/014.test.mjs',
  ]
  for (const rel of required) {
    assert.ok(existsSync(join(ROOT, rel)), `ladder step target missing: ${rel}`)
  }
})
test('WHAT[verification-system-009] every wired gate path exists and checks set matches scripts/checks directory', () => {
  for (const gatePath of checks) {
    assert.ok(
      existsSync(gatePath),
      `check.mjs wires a gate that does not exist: ${gatePath}`,
    )
  }

  const dirEntries = readdirSync(join(ROOT, 'scripts/checks'))
  const fsxFiles = dirEntries.filter((f) => f.endsWith('.fsx'))
  assert.deepEqual(fsxFiles, [], 'scripts/checks must contain no .fsx custom compiler executables')

  // Data files that are not gate scripts
  const nonGateFiles = new Set([
    'owner-impact-corpus.json',
    'subsystems.json',
    'release-closure-nodes.json',
    // Helper / non-gate modules:
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
test('WHAT[verification-system-009] no custom FCS executable remains after the full-repo ban', () => {
  // GAP-031 全仓自定义 FCS 禁令：扫描链是被删除，不是被禁用。任何自定义 FCS
  // 可执行物（驱动 FSharp.Compiler.Service 的 .fsx，或 shell 到 `dotnet fsi` /
  // 加载 Fable+FCS 程序集 / import 已删除扫描入口的 JS gate）都必须变红。
  // 正常 Fable 编译边界（`dotnet tool run fable`、build.mjs、compile-impact CLI）
  // 不在判据内，本测试不碰它们。
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
}

{
const { default: assert } = await import("node:assert/strict");
const { copyFileSync, mkdirSync, mkdtempSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { spawnSync } = await import("node:child_process");
const { default: test } = await import("node:test");
const { fileURLToPath } = await import("node:url");

const repositoryRoot = fileURLToPath(new URL('../../..', import.meta.url))
const write = (root, path, text) => {
  const target = join(root, path)
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

test('WHAT[verification-system-009] repository closure gates reject an unassigned production source and package member', () => {
  const fixture = mkdtempSync(join(tmpdir(), 'repository-closure-'))

  try {
    mkdirSync(join(fixture, 'scripts/checks'), { recursive: true })
    mkdirSync(join(fixture, 'scripts/lib'), { recursive: true })
    copyFileSync(
      join(repositoryRoot, 'scripts/checks/subsystems.mjs'),
      join(fixture, 'scripts/checks/subsystems.mjs'),
    )
    copyFileSync(
      join(repositoryRoot, 'scripts/lib/compile-shards.mjs'),
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
      join(repositoryRoot, 'requirements/distribution/tests/004.test.mjs'),
      join(fixture, 'requirements/distribution/tests/004.test.mjs'),
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
      'requirements/distribution/tests/004.test.mjs',
    ])
    assert.equal(packageGate.status, 1, packageGate.stderr || packageGate.stdout)
    assert.match(`${packageGate.stdout}\n${packageGate.stderr}`, /files whitelist must include dist/)
  } finally {
    rmSync(fixture, { recursive: true, force: true })
  }
})
}
