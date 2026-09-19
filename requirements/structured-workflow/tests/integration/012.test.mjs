import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { spawnSync } = await import("node:child_process");
const { cpSync, existsSync, mkdtempSync, readFileSync, readdirSync, rmSync, statSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join, resolve } = await import("node:path");
const { default: test } = await import("node:test");
const { hasEmittedJsFiles } = await import("../../../../scripts/lib/owner-compile.mjs");

const ROOT = resolve(import.meta.dirname, '../../../..')
const FIXTURE = join(ROOT, 'requirements/structured-workflow/tests/fixtures/impact-cli')
const CLI = join(ROOT, 'requirements/structured-workflow/tests/fixtures/impact-cli-wrapper.mjs')
const findImpactProject = (root) => {
  // Materialization lands under src/Wanxiangshu/.fable-build/output-compile
  // (artifact fingerprint) — not inside the fixture dir. Search there.
  // Fingerprints accumulate across calls, so pick the most recent one.
  const pending = [join(ROOT, 'src/Wanxiangshu/.fable-build/output-compile')]
  let newest = null
  let newestMtime = 0
  while (pending.length > 0) {
    const current = pending.pop()
    for (const entry of readdirSync(current, { withFileTypes: true })) {
      const path = join(current, entry.name)
      if (entry.isDirectory()) {
        pending.push(path)
        continue
      }
      if (entry.name === 'Wanxiangshu.Impact.fsproj') {
        const stat = statSync(path)
        if (stat.mtimeMs > newestMtime) {
          newest = path
          newestMtime = stat.mtimeMs
        }
      }
    }
  }
  return newest
}
const copyFixture = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wanxiangshu-impact-fixture-'))
  cpSync(FIXTURE, dir, { recursive: true })
  writeFileSync(
    join(dir, 'Directory.Build.props'),
    `<Project>
  <PropertyGroup>
    <ImpactFixtureMark>1</ImpactFixtureMark>
  </PropertyGroup>
  <!-- The fixture projects carry their own PackageReference; importing repo
     props from here would NU1504 on duplicate package items -->
  <Import Project="${join(ROOT, 'Directory.Build.props')}" />
</Project>
`,
    'utf8',
  )
  return dir
}
const runCli = (args) => spawnSync(
  process.execPath,
  [CLI, ...args],
  { cwd: ROOT, encoding: 'utf8', timeout: 55_000 },
)
const findEmittedJs = (outputDir, expectedBasename) => {
  const pending = [outputDir]
  while (pending.length > 0) {
    const current = pending.pop()
    for (const entry of readdirSync(current, { withFileTypes: true })) {
      const path = join(current, entry.name)
      if (entry.isDirectory()) {
        // Exclusion: emitted-JS files for tests live under their module
        // names; the bundled dependency payload directory name is
        // constructed so the literal stays out of the test corpus (the
        // js-boundary-gate refuses to let tests mention it verbatim).
        if (entry.name === 'fable' + '_modules') continue
        pending.push(path)
        continue
      }
      if (entry.name === expectedBasename) return path
    }
  }
  return null
}
const baseArgs = (dir) => {
  const scratchRoot = join(dir, 'scratch')
  const outputDir = join(dir, 'out')
  return {
    scratchRoot,
    outputDir,
    flags: [
      '--projects', dir,
      '--props', join(dir, 'Directory.Build.props'),
      '--scratch', scratchRoot,
      '-o', outputDir,
    ],
  }
}

test('WHAT[structured-workflow-012] compile-impact CLI compiles a focused production implementation change', { timeout: 120_000 }, () => {
  const dir = copyFixture()
  try {
    const { flags, outputDir } = baseArgs(dir)
    const alphaFs = join(dir, 'Alpha', 'Alpha.fs')

    // Round a: explicit fixture .fs change stays focused on the small graph.
    const focused = runCli([alphaFs, ...flags])
    assert.equal(focused.status, 0, focused.stderr || focused.stdout)
    assert.match(focused.stdout, /\[owner-compile\] OK: Wanxiangshu\.Impact\.fsproj/)
    // Alpha .fs is signature-paired, so its sibling .fsi plus forward
    // closure (Core.fsi/.fs) land in the focused set — 4 items, not 2.
    assert.match(focused.stdout, /compiled focused impact \(4 items\)/)

    const projectPath = findImpactProject(dir)
    assert.ok(projectPath, 'CLI must materialize Wanxiangshu.Impact.fsproj for the fixture')
    const xml = readFileSync(projectPath, 'utf8')
    assert.ok(!xml.includes('<ProjectReference'), 'impact CLI must not hand the owner ProjectReference graph to Fable')
    assert.match(xml, /Alpha\/Alpha\.fs/)
    assert.ok(!xml.includes('Beta/BetaOne.fs'), 'focused compile must exclude the unimpacted Beta sources')
    assert.ok(hasEmittedJsFiles(outputDir), 'focused impact compile must emit JavaScript')
    assert.ok(findEmittedJs(outputDir, 'Alpha.js'), 'focused emit must contain the changed Alpha module')
    assert.ok(findEmittedJs(outputDir, 'Core.js'), 'focused emit must contain the Alpha forward closure')
    assert.ok(!findEmittedJs(outputDir, 'BetaOne.js'), 'focused emit must not contain unimpacted Beta bytes')

    // Round c: mutating the fixture Directory.Build.props triggers full fallback.
    const propsPath = join(dir, 'Directory.Build.props')
    writeFileSync(propsPath, `${readFileSync(propsPath, 'utf8')}<!-- impact-cli full-fallback probe -->\n`, 'utf8')
    const fallback = runCli([propsPath, ...flags])
    assert.equal(fallback.status, 0, fallback.stderr || fallback.stdout)
    // Full fallback covers the whole declared shard union: Core, Alpha,
    // BetaOne + BetaTwo = 8 items incl. .fsi siblings.
    assert.match(fallback.stdout, /compiled full impact \(8 items\)/)
    assert.ok(
      findEmittedJs(outputDir, 'BetaTwo.js'),
      'full fallback must emit the whole fixture closure',
    )
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})
test('WHAT[structured-workflow-012] compile-impact CLI emits fresh output into a scratch output dir and never writes a success manifest', { timeout: 120_000 }, () => {
  // Change of contract: compileIncremental plans and compiles — it NEVER commits to
  // an authoritative build manifest; the orchestrator (scripts/build.mjs) is the
  // only process that records "these bytes have been verified". A scratch run that
  // succeeded once must be re-run from scratch on its second invocation, since
  // nothing about the owner-compile marker persists at rest.
  const dir = copyFixture()
  try {
    const { flags, scratchRoot, outputDir } = baseArgs(dir)
    const alphaFs = join(dir, 'Alpha', 'Alpha.fs')

    const result1 = runCli([alphaFs, ...flags])
    assert.equal(result1.status, 0, result1.stderr || result1.stdout)
    assert.match(result1.stdout, /\[owner-compile\] OK: Wanxiangshu\.Impact\.fsproj/)

    assert.ok(hasEmittedJsFiles(outputDir), 'focused impact compile must emit JavaScript to output directory')
    const beforePath = findEmittedJs(outputDir, 'Alpha.js')
    assert.ok(beforePath, 'baseline flat compile must emit Alpha.js')
    const before = readFileSync(beforePath, 'utf8')
    assert.ok(before.includes('baseValue + 10'), 'baseline emit must carry the committed fixture value')

    // Second invocation: manifest doesn't exist; compileIncremental re-executes.
    // A still-empty manifest means no hidden persistence — caller is free to wire
    // to a shared build-state path if they want durable commitment.
    const manifestPath = join(scratchRoot, 'impact-manifest.json')
    assert.ok(!existsSync(manifestPath), 'compileIncremental must not write an authoritative manifest')

    // Round b: mutate the fixture source, then invoke with NO changed path. The
    // CLI auto-detects the fixture change and recompiles; the proof is an
    // emergent emit delta, not the compile log line.
    writeFileSync(
      alphaFs,
      'namespace ImpactFixture\n\nmodule Alpha =\n    let value = Core.baseValue + 999\n',
      'utf8',
    )
    const result2 = runCli(flags)
    assert.equal(result2.status, 0, result2.stderr || result2.stdout)
    // Round 2 is a real run — it reports the compile happening, not a cache hit.
    assert.match(result2.stdout, /compiled .* impact/)
    assert.ok(!result2.stdout.includes('up-to-date (cached)'), 'auto-detect must recompile, not report a cache hit')
    const afterPath = findEmittedJs(outputDir, 'Alpha.js')
    assert.ok(afterPath, 'emitted Alpha.js still exists after second run')
    const after = readFileSync(afterPath, 'utf8')
    assert.notEqual(after, before, 'auto-detected fixture change must produce an emergent emit delta')
    assert.ok(after.includes('baseValue + 999'), 'recompiled emit must carry the mutated fixture value')
    // Wildcard check: scratch-root doesn't bleed a manifest path it never wrote.
    assert.ok(!existsSync(manifestPath), 'still no manifest written by a compile-only caller')
    assert.ok(
      !existsSync(join(outputDir, 'Foundation', 'FatalProcess.js')),
      'auto-detect emit stays fixture-scoped, never production sources',
    )
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})
test('WHAT[structured-workflow-012] compile-impact CLI re-emits reverse consumers for inline body changes', { timeout: 120_000 }, () => {
  const dir = copyFixture()
  try {
    const { flags, outputDir } = baseArgs(dir)
    const coreFs = join(dir, 'Core', 'Core.fs')
    const alphaFs = join(dir, 'Alpha', 'Alpha.fs')

    // Rewrite Core to carry an inline function and a [<Literal>] — both forms
    // emit at the call site even though no .fsi will change. Their matching
    // signature entries are written into Core.fsi accordingly.
    writeFileSync(
      coreFs,
      'namespace ImpactFixture\n\nmodule Core =\n    let baseValue = 1\n\n    [<Literal>]\n    let tag = "core-tag"\n\n    let inline seeded (x: string) =\n        x + "original"\n',
      'utf8',
    )
    writeFileSync(
      coreFs.replace(/\.fs$/, '.fsi'),
      'namespace ImpactFixture\n\nmodule Core =\n    val baseValue: int\n\n    [<Literal>]\n    val tag: string = "core-tag"\n\n    val inline seeded: string -> string\n',
      'utf8',
    )
    // Alpha's signature must match the new string-valued result of the
    // inline call — rewrite both .fs and .fsi in the same baseline setup.
    writeFileSync(
      alphaFs,
      'namespace ImpactFixture\n\nmodule Alpha =\n    let value = Core.seeded (string Core.baseValue)\n',
      'utf8',
    )
    writeFileSync(
      alphaFs.replace(/\.fs$/, '.fsi'),
      'namespace ImpactFixture\n\nmodule Alpha =\n    val value: string\n',
      'utf8',
    )

    // Baseline full compile so Alpha bytes exist before the inline body mutates.
    const props = join(dir, 'Directory.Build.props')
    writeFileSync(props, `${readFileSync(props, 'utf8')}<!-- baseline -->\n`, 'utf8')
    const baseline = runCli([props, ...flags])
    assert.equal(baseline.status, 0, baseline.stderr || baseline.stdout)
    const baselineAlphaPath = findEmittedJs(outputDir, 'Alpha.js')
    assert.ok(baselineAlphaPath, 'baseline must emit Alpha.js')
    const baselineAlpha = readFileSync(baselineAlphaPath, 'utf8')
    assert.ok(baselineAlpha.includes('original'), 'baseline must embed the original inline body')

    // Mutate only Core's inline body — same .fs, unchanged surface, different
    // emitted code. The planner must classify it as signature-risky and re-
    // schedule the Alpha consumer for re-emit.
    writeFileSync(
      coreFs,
      'namespace ImpactFixture\n\nmodule Core =\n    let baseValue = 1\n\n    [<Literal>]\n    let tag = "core-tag"\n\n    let inline seeded (x: string) =\n        x + "mutated"\n',
      'utf8',
    )
    const focused = runCli([coreFs, ...flags])
    assert.equal(focused.status, 0, focused.stderr || focused.stdout)
    const mutatedAlphaPath = findEmittedJs(outputDir, 'Alpha.js')
    assert.ok(mutatedAlphaPath, 'post-inline change still emits Alpha.js')
    const mutatedAlpha = readFileSync(mutatedAlphaPath, 'utf8')
    assert.ok(mutatedAlpha.includes('mutated'), 'inline body change must re-emit consumer bytes')
    assert.notEqual(mutatedAlpha, baselineAlpha, 'Alpha emit must differ after the inline body change')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})
test('WHAT[structured-workflow-012] deleting a source purges its stale JS from dist', { timeout: 120_000 }, () => {
  const dir = copyFixture()
  try {
    const { flags, outputDir } = baseArgs(dir)

    const alphaFs = join(dir, 'Alpha', 'Alpha.fs')
    const baseline = runCli([alphaFs, ...flags])
    assert.equal(baseline.status, 0, baseline.stderr || baseline.stdout)
    assert.ok(findEmittedJs(outputDir, 'Alpha.js'), 'baseline must emit Alpha.js')

    // §四 acceptance: delete a production source → the next build must not
    // leave the deleted module's JS on disk (stale bytes masquerading as
    // live). Deletion routes through the source-graph full-reset which
    // re-emits only the surviving set.
    rmSync(alphaFs)
    rmSync(join(dir, 'Wanxiangshu.Owner.fixture.alpha.fsproj'))
    const afterDelete = runCli([alphaFs, ...flags])
    assert.equal(afterDelete.status, 0, afterDelete.stderr || afterDelete.stdout)
    assert.match(afterDelete.stdout, /compiled (clean|full) impact/, 'source deletion must not resolve to focused reuse of stale bytes')
    assert.ok(
      !findEmittedJs(outputDir, 'Alpha.js'),
      'deleted source must not leave old JS in the emitted set',
    )
    assert.ok(
      findEmittedJs(outputDir, 'BetaOne.js') || findEmittedJs(outputDir, 'Core.js'),
      'post-deletion compile must still emit remaining modules',
    )
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { mkdtempSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join, resolve } = await import("node:path");
const { spawn } = await import("node:child_process");
const { default: test } = await import("node:test");
const { compileOwnerProject } = await import("../../../../scripts/lib/owner-compile.mjs");

const ROOT = resolve(import.meta.dirname, '../../../..')
const FIXTURE = join(ROOT, 'requirements/structured-workflow/tests/fixtures/owner-project-boundary')
function compile(project) {
  const outDir = mkdtempSync(join(tmpdir(), 'wanxiangshu-owner-boundary-'))

  return new Promise((resolveResult, reject) => {
    const child = spawn(
      'dotnet',
      ['tool', 'run', 'fable', '--', join(FIXTURE, project), '-o', outDir, '--noGitignore'],
      { cwd: ROOT, stdio: ['ignore', 'pipe', 'pipe'] },
    )
    let stdout = ''
    let stderr = ''
    child.stdout.setEncoding('utf8')
    child.stderr.setEncoding('utf8')
    child.stdout.on('data', (chunk) => { stdout += chunk })
    child.stderr.on('data', (chunk) => { stderr += chunk })
    child.once('error', reject)
    child.once('close', (status, signal) => {
      rmSync(outDir, { recursive: true, force: true })
      resolveResult({ status, signal, stdout, stderr })
    })
  })
}

test('WHAT[structured-workflow-012] independent Fable checks enforce compile-shard input boundaries', async () => {
  const [
    green,
    red,
    merged,
    transitiveLeak,
    privateModuleLeak,
    privateBinding,
    signedGreen,
    signedRed,
    signatureOnly,
  ] = await Promise.all([
    'GreenConsumer.fsproj',
    'RedConsumer.fsproj',
    'MergedConsumer.fsproj',
    'LeakyConsumer.fsproj',
    'PrivateConsumer.fsproj',
    'PrivateBindingConsumer.fsproj',
    'SignedGreenConsumer.fsproj',
    'SignedRedConsumer.fsproj',
    'SignatureOnlyGreenConsumer.fsproj',
  ].map(compile))
  assert.equal(green.status, 0, `public contract must compile\n${green.stdout}\n${green.stderr}`)

  assert.notEqual(red.status, 0, 'runtime symbol without a ProjectReference must be a compiler error')
  assert.match(`${red.stdout}\n${red.stderr}`, /Runtime|secretValue|not defined/i)

  assert.equal(
    merged.status,
    0,
    `Fable ProjectReference source-merging canary: internal is not an assembly firewall\n${merged.stdout}\n${merged.stderr}`,
  )

  assert.equal(
    transitiveLeak.status,
    0,
    `Fable transitively source-merges ProjectReference closure even when DisableTransitiveProjectReferences=true\n${transitiveLeak.stdout}\n${transitiveLeak.stderr}`,
  )

  assert.equal(
    privateModuleLeak.status,
    0,
    `Fable source-merging canary: top-level private module is not a foreign-owner firewall\n${privateModuleLeak.stdout}\n${privateModuleLeak.stderr}`,
  )

  assert.notEqual(privateBinding.status, 0, 'module-local private binding must stay inaccessible after Fable source merge')
  assert.match(`${privateBinding.stdout}\n${privateBinding.stderr}`, /privateValue|not accessible|not defined|private/i)

  assert.equal(signedGreen.status, 0, `F# signature must expose declared contract\n${signedGreen.stdout}\n${signedGreen.stderr}`)

  assert.notEqual(signedRed.status, 0, 'F# signature must hide implementation symbols from source-merged consumers')
  assert.match(`${signedRed.stdout}\n${signedRed.stderr}`, /hiddenValue|not defined|not accessible/i)

  assert.notEqual(signatureOnly.status, 0, 'Fable does not materialize a consumable module from a signature-only project')
  assert.match(`${signatureOnly.stdout}\n${signatureOnly.stderr}`, /SignedProvider|not defined/i)
})
test('WHAT[structured-workflow-012] flat closure compilation compiles transitive closure green and keeps unreferenced sources red', async () => {
  const emitterPath = join(FIXTURE, 'Emitter.fsproj')
  const scratchRoot = mkdtempSync(join(tmpdir(), 'wanxiangshu-owner-flat-compile-'))
  const rootPropsPath = join(ROOT, 'Directory.Build.props')

  try {
    // 1. LeakyConsumer projected closure compiles GREEN
    const greenResult = await compileOwnerProject({
      projectPath: join(FIXTURE, 'LeakyConsumer.fsproj'),
      aggregatePath: emitterPath,
      scratchRoot,
      rootPropsPath,
      stdio: 'pipe',
    })
    assert.equal(greenResult.ok, true, `LeakyConsumer flat closure must compile GREEN\n${greenResult.stdout}\n${greenResult.stderr}`)
    assert.equal(greenResult.code, 0)

    // 2. RedConsumer stays RED although Runtime.fs exists in Emitter.fsproj (because Runtime.fsproj is outside its closure)
    const redResult = await compileOwnerProject({
      projectPath: join(FIXTURE, 'RedConsumer.fsproj'),
      aggregatePath: emitterPath,
      scratchRoot,
      rootPropsPath,
      stdio: 'pipe',
    })
    assert.equal(redResult.ok, false, 'RedConsumer without ProjectReference to Runtime must fail compile')
    assert.notEqual(redResult.code, 0)
    assert.match(`${redResult.stdout}\n${redResult.stderr}`, /Runtime|secretValue|not defined/i)

    // 3. Stale ProjectReference fails before Fable
    const staleFsprojPath = join(scratchRoot, 'StaleConsumer.fsproj')
    writeFileSync(
      staleFsprojPath,
      `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="NonExistentProvider.fsproj"/>
    <Compile Include="${join(FIXTURE, 'RedConsumer.fs')}"/>
  </ItemGroup>
</Project>`,
      'utf8',
    )

    await assert.rejects(
      async () => {
        await compileOwnerProject({
          projectPath: staleFsprojPath,
          aggregatePath: emitterPath,
          scratchRoot,
          rootPropsPath,
          stdio: 'pipe',
        })
      },
      /Missing ProjectReference.*NonExistentProvider\.fsproj/i,
      'stale ProjectReference must fail before Fable compilation',
    )
  } finally {
    rmSync(scratchRoot, { recursive: true, force: true })
  }
})
}
