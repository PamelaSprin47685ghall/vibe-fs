import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, readFileSync, rmSync, utimesSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import { loopDetectorRepositoryInputFiles } from '../../../scripts/lib/loop-detector-repository-corpus.mjs'
import { checkBuildFreshness } from './support/build-freshness.mjs'

test('WHAT[VERIFICATION-SYSTEM-008] build freshness includes repository-derived artifact inputs beyond F# sources', () => {
  const root = mkdtempSync(join(tmpdir(), 'wanxiang-build-freshness-'))
  const productionRoot = join(root, 'src')
  const buildRoot = join(root, 'dist')
  const productionSource = join(productionRoot, 'Main.fs')
  const repositoryInput = join(root, 'requirements.md')
  const artifact = join(buildRoot, 'Main.js')
  const gitignore = join(root, '.gitignore')

  execFileSync('git', ['init', '-q', root])
  mkdirSync(productionRoot, { recursive: true })
  mkdirSync(buildRoot, { recursive: true })
  writeFileSync(productionSource, 'module Main\n', 'utf8')
  writeFileSync(repositoryInput, 'new repository corpus\n', 'utf8')
  writeFileSync(artifact, 'export {}\n', 'utf8')
  writeFileSync(gitignore, 'dist/\n', 'utf8')
  execFileSync('git', ['-C', root, 'add', 'requirements.md'])

  utimesSync(productionSource, 10, 10)
  utimesSync(artifact, 20, 20)
  utimesSync(repositoryInput, 30, 30)
  utimesSync(gitignore, 10, 10)

  const freshness = checkBuildFreshness({ productionRoot, buildRoot, repositoryRoot: root })

  assert.equal(freshness.ok, false)
  assert.match(freshness.reason, /requirements\.md/)
})

test('WHAT[VERIFICATION-SYSTEM-008] local repository export shards are excluded from the build corpus', () => {
  const root = mkdtempSync(join(tmpdir(), 'wanxiang-build-corpus-ignore-'))
  try {
    execFileSync('git', ['init', '-q', root])
    writeFileSync(join(root, '.gitignore'), 'repomix-src-part*.xml\n', 'utf8')
    writeFileSync(join(root, 'keep.md'), 'real repository input\n', 'utf8')
    writeFileSync(join(root, 'repomix-src-part1.xml'), '<temporary-export/>\n', 'utf8')
    execFileSync('git', ['-C', root, 'add', '.gitignore', 'keep.md'])

    const inputs = loopDetectorRepositoryInputFiles(root)
    assert.ok(inputs.includes(join(root, 'keep.md')), 'tracked repository input must remain in the corpus')
    assert.ok(
      !inputs.includes(join(root, 'repomix-src-part1.xml')),
      'ignored local repository export must never become a build-freshness input',
    )
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})

test('WHAT[VERIFICATION-SYSTEM-008] Fable build compiles once and never accepts watch-daemon freshness guesses', () => {
  const build = readFileSync(new URL('../../../scripts/build.mjs', import.meta.url), 'utf8')

  assert.match(
    build,
    /'tool',[\s\S]*'run',[\s\S]*'fable',[\s\S]*'src\/Wanxiangshu\/Wanxiangshu\.fsproj'/,
    'build must invoke the real Fable compiler for the current source tree',
  )
  assert.match(
    build,
    /['"]-c['"],[\s\S]*['"]Debug['"]/,
    'one-shot compile must preserve the established watch-build Debug configuration',
  )
  assert.match(build, /child\.once\('exit',[\s\S]*result\.code !== 0/)
  assert.doesNotMatch(build, /FableBarrier|fable-daemon|fable-cycle-ack|['"]watch['"]/)
})

test('WHAT[VERIFICATION-SYSTEM-008] one-shot build removes the previous artifact tree before compiling', () => {
  const build = readFileSync(new URL('../../../scripts/build.mjs', import.meta.url), 'utf8')
  const compileStart = build.indexOf('async function compileFable()')
  const spawnAt = build.indexOf('const child = spawn(', compileStart)
  const compilePrefix = build.slice(compileStart, spawnAt)

  assert.ok(compileStart >= 0 && spawnAt > compileStart, 'compileFable must own the one-shot compiler boundary')
  assert.match(
    compilePrefix,
    /fs\.rmSync\(dist,\s*\{\s*recursive:\s*true,\s*force:\s*true\s*\}\)/,
    'a removed F# source must not leave a stale JS module in the next package',
  )
  assert.match(compilePrefix, /fs\.mkdirSync\(dist,\s*\{\s*recursive:\s*true\s*\}\)/)
  assert.ok(
    compilePrefix.indexOf('fs.rmSync(dist') < compilePrefix.indexOf('fs.mkdirSync(dist'),
    'artifact cleanup must precede recreating the compiler output directory',
  )
})
