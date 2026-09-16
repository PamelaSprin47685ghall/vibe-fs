// releaseOnly: true
import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { cpSync, existsSync, mkdtempSync, readFileSync, readdirSync, rmSync, statSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import test from 'node:test'
import { hasEmittedJsFiles } from '../../../../scripts/lib/owner-compile.mjs'

const ROOT = resolve(import.meta.dirname, '../../../..')
const IMPACT_FIXTURE = join(ROOT, 'requirements/structured-workflow/tests/fixtures/impact-cli')
const CLI = join(ROOT, 'requirements/structured-workflow/tests/fixtures/impact-cli-wrapper.mjs')

const findImpactProject = (root) => {
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
  cpSync(IMPACT_FIXTURE, dir, { recursive: true })
  writeFileSync(
    join(dir, 'Directory.Build.props'),
    `<Project>
  <PropertyGroup>
    <ImpactFixtureMark>1</ImpactFixtureMark>
  </PropertyGroup>
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

test('WHAT[STRUCTURED-WORKFLOW-012] compile-impact CLI compiles a focused production implementation change', { timeout: 120_000 }, () => {
  const dir = copyFixture()
  try {
    const { flags, outputDir } = baseArgs(dir)
    const alphaFs = join(dir, 'Alpha', 'Alpha.fs')

    const focused = runCli([alphaFs, ...flags])
    assert.equal(focused.status, 0, focused.stderr || focused.stdout)
    assert.match(focused.stdout, /\[owner-compile\] OK: Wanxiangshu\.Impact\.fsproj/)
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

    const propsPath = join(dir, 'Directory.Build.props')
    writeFileSync(propsPath, `${readFileSync(propsPath, 'utf8')}<!-- impact-cli full-fallback probe -->\n`, 'utf8')
    const fallback = runCli([propsPath, ...flags])
    assert.equal(fallback.status, 0, fallback.stderr || fallback.stdout)
    assert.match(fallback.stdout, /compiled full impact \(8 items\)/)
    assert.ok(
      findEmittedJs(outputDir, 'BetaTwo.js'),
      'full fallback must emit the whole fixture closure',
    )
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[STRUCTURED-WORKFLOW-012] compile-impact CLI emits fresh output into a scratch output dir and never writes a success manifest', { timeout: 120_000 }, () => {
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

    const manifestPath = join(scratchRoot, 'impact-manifest.json')
    assert.ok(!existsSync(manifestPath), 'compileIncremental must not write an authoritative manifest')

    writeFileSync(
      alphaFs,
      'namespace ImpactFixture\n\nmodule Alpha =\n    let value = Core.baseValue + 999\n',
      'utf8',
    )
    const result2 = runCli(flags)
    assert.equal(result2.status, 0, result2.stderr || result2.stdout)
    assert.match(result2.stdout, /compiled .* impact/)
    assert.ok(!result2.stdout.includes('up-to-date (cached)'), 'auto-detect must recompile, not report a cache hit')
    const afterPath = findEmittedJs(outputDir, 'Alpha.js')
    assert.ok(afterPath, 'emitted Alpha.js still exists after second run')
    const after = readFileSync(afterPath, 'utf8')
    assert.notEqual(after, before, 'auto-detected fixture change must produce an emergent emit delta')
    assert.ok(after.includes('baseValue + 999'), 'recompiled emit must carry the mutated fixture value')
    assert.ok(!existsSync(manifestPath), 'still no manifest written by a compile-only caller')
    assert.ok(
      !existsSync(join(outputDir, 'Foundation', 'FatalProcess.js')),
      'auto-detect emit stays fixture-scoped, never production sources',
    )
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[STRUCTURED-WORKFLOW-012] compile-impact CLI re-emits reverse consumers for inline body changes', { timeout: 120_000 }, () => {
  const dir = copyFixture()
  try {
    const { flags, outputDir } = baseArgs(dir)
    const coreFs = join(dir, 'Core', 'Core.fs')
    const alphaFs = join(dir, 'Alpha', 'Alpha.fs')

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

    const props = join(dir, 'Directory.Build.props')
    writeFileSync(props, `${readFileSync(props, 'utf8')}<!-- baseline -->\n`, 'utf8')
    const baseline = runCli([props, ...flags])
    assert.equal(baseline.status, 0, baseline.stderr || baseline.stdout)
    const baselineAlphaPath = findEmittedJs(outputDir, 'Alpha.js')
    assert.ok(baselineAlphaPath, 'baseline must emit Alpha.js')
    const baselineAlpha = readFileSync(baselineAlphaPath, 'utf8')
    assert.ok(baselineAlpha.includes('original'), 'baseline must embed the original inline body')

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

test('WHAT[STRUCTURED-WORKFLOW-012] deleting a source purges its stale JS from dist', { timeout: 120_000 }, () => {
  const dir = copyFixture()
  try {
    const { flags, outputDir } = baseArgs(dir)

    const alphaFs = join(dir, 'Alpha', 'Alpha.fs')
    const baseline = runCli([alphaFs, ...flags])
    assert.equal(baseline.status, 0, baseline.stderr || baseline.stdout)
    assert.ok(findEmittedJs(outputDir, 'Alpha.js'), 'baseline must emit Alpha.js')

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
