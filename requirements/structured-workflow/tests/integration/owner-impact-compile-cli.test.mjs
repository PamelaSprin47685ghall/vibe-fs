import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { cpSync, existsSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import test from 'node:test'
import { hasEmittedJsFiles } from '../../../../scripts/lib/owner-compile.mjs'

const ROOT = resolve(import.meta.dirname, '../../../..')
const FIXTURE = join(ROOT, 'requirements/structured-workflow/tests/fixtures/impact-cli')
const CLI = join(ROOT, 'scripts/compile-impact.mjs')

// Original three CLI rounds (full-repo compiles of src/Wanxiangshu) mapped onto
// the small fixture graph: Core (shared foundation) <- Alpha, Core <- Beta.
// - Round 1 (explicit Alpha.fs change) claimed "focused": on the real repo the
//   FatalProcess.fs plan was actually full (impact-exceeds-full-threshold). On
//   the fixture Alpha selects {Alpha, Core} = 2/4 sources <= 0.6, so focused
//   genuinely holds and Beta bytes must be absent from the emit.
// - Round 2 (explicit change + -o, then a no-changed-path re-run) exercised the
//   compile-only contract (never writes impact-manifest.json). Kept as-is but
//   rooted at the fixture: the second run auto-detects the mutated Alpha.fs and
//   must show an emergent emit delta, not just a compile log line.
// - Round 3 (no changed path) fell back to a full-repo compile. Replaced by an
//   explicit toolchain change (mutated Directory.Build.props) that must select
//   the full fallback (reason toolchain-or-project-change), preserving the
//   backed-out full-fallback contract without compiling the production repo.
const findImpactProject = (root) => {
  const pending = [root]
  while (pending.length > 0) {
    const current = pending.pop()
    for (const entry of readdirSync(current, { withFileTypes: true })) {
      const path = join(current, entry.name)
      if (entry.isDirectory()) {
        if (entry.name === 'node_modules') continue
        pending.push(path)
        continue
      }
      if (entry.name === 'Wanxiangshu.Impact.fsproj') {
        return path
      }
    }
  }
  return null
}

// Copy the fixture to an isolated temp dir so test mutations never dirty the
// committed fixture. Re-point the copied Directory.Build.props import at the
// absolute repo root (the committed relative import only resolves in place).
const copyFixture = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wanxiangshu-impact-fixture-'))
  cpSync(FIXTURE, dir, { recursive: true })
  writeFileSync(
    join(dir, 'Directory.Build.props'),
    `<Project>
  <PropertyGroup>
    <ImpactFixtureMark>1</ImpactFixtureMark>
  </PropertyGroup>
  <Import Project="${ROOT}/Directory.Build.props" />
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

const baseArgs = (dir) => {
  const scratchRoot = join(dir, 'scratch')
  const outputDir = join(dir, 'out')
  return {
    scratchRoot,
    outputDir,
    flags: [
      '--aggregate', join(dir, 'Aggregate.fsproj'),
      '--projects', dir,
      '--props', join(dir, 'Directory.Build.props'),
      '--scratch', scratchRoot,
      '-o', outputDir,
    ],
  }
}

test('WHAT[STRUCTURED-WORKFLOW-012] compile-impact CLI compiles a focused production implementation change', { timeout: 120_000 }, () => {
  const dir = copyFixture()
  try {
    const { flags, outputDir } = baseArgs(dir)
    const alphaFs = join(dir, 'Alpha', 'Alpha.fs')

    // Round a: explicit fixture .fs change stays focused on the small graph.
    const focused = runCli([alphaFs, ...flags])
    assert.equal(focused.status, 0, focused.stderr || focused.stdout)
    assert.match(focused.stdout, /\[owner-compile\] OK: Wanxiangshu\.Impact\.fsproj/)
    assert.match(focused.stdout, /compiled focused impact \(2 items\)/)

    const projectPath = findImpactProject(dir)
    assert.ok(projectPath, 'CLI must materialize Wanxiangshu.Impact.fsproj for the fixture')
    const xml = readFileSync(projectPath, 'utf8')
    assert.ok(!xml.includes('<ProjectReference'), 'impact CLI must not hand the owner ProjectReference graph to Fable')
    assert.match(xml, /Alpha\/Alpha\.fs/)
    assert.ok(!xml.includes('Beta/BetaOne.fs'), 'focused compile must exclude the unimpacted Beta sources')
    assert.ok(hasEmittedJsFiles(outputDir), 'focused impact compile must emit JavaScript')
    assert.ok(existsSync(join(outputDir, 'Alpha', 'Alpha.js')), 'focused emit must contain the changed Alpha module')
    assert.ok(existsSync(join(outputDir, 'Core', 'Core.js')), 'focused emit must contain the Alpha forward closure')
    assert.ok(!existsSync(join(outputDir, 'Beta', 'BetaOne.js')), 'focused emit must not contain unimpacted Beta bytes')
    assert.ok(
      !existsSync(join(outputDir, 'Foundation', 'FatalProcess.js')),
      'emit must be fixture-scoped bytes, never production sources',
    )

    // Round c: mutating the fixture Directory.Build.props triggers full fallback.
    const propsPath = join(dir, 'Directory.Build.props')
    writeFileSync(propsPath, `${readFileSync(propsPath, 'utf8')}<!-- impact-cli full-fallback probe -->\n`, 'utf8')
    const fallback = runCli([propsPath, ...flags])
    assert.equal(fallback.status, 0, fallback.stderr || fallback.stdout)
    assert.match(fallback.stdout, /compiled full impact \(4 items\)/)
    assert.ok(
      existsSync(join(outputDir, 'Beta', 'BetaTwo.js')),
      'full fallback must emit the whole fixture closure',
    )
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[STRUCTURED-WORKFLOW-012] compile-impact CLI emits fresh output into a scratch output dir and never writes a success manifest', { timeout: 120_000 }, () => {
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
    const alphaJsPath = join(outputDir, 'Alpha', 'Alpha.js')
    assert.ok(existsSync(alphaJsPath), 'focused flat compile must preserve the fixture emitter output layout')
    const before = readFileSync(alphaJsPath, 'utf8')
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
    const after = readFileSync(alphaJsPath, 'utf8')
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
