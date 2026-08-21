import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, rmSync, utimesSync, writeFileSync } from 'node:fs'
import { execFileSync } from 'node:child_process'
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
