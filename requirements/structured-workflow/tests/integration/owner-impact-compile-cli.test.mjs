import assert from 'node:assert/strict'
import { spawn, spawnSync } from 'node:child_process'
import { mkdtempSync, readFileSync, readdirSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import test from 'node:test'
import { hasEmittedJsFiles } from '../../../../scripts/lib/owner-compile.mjs'

const ROOT = resolve(import.meta.dirname, '../../../..')
const CHANGED = join(ROOT, 'src/Wanxiangshu/Foundation/FatalProcess.fs')
const CLI = join(ROOT, 'scripts/compile-impact.mjs')

const findImpactProject = (scratchRoot) => {
  const pending = [scratchRoot]
  while (pending.length > 0) {
    const current = pending.pop()
    for (const entry of readdirSync(current, { withFileTypes: true })) {
      const path = join(current, entry.name)
      if (entry.isDirectory()) {
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

const killProcessGroup = (child) => {
  if (!child?.pid) return
  try {
    process.kill(-child.pid, 'SIGTERM')
  } catch {
    child.kill('SIGTERM')
  }
}

test('WHAT[STRUCTURED-WORKFLOW-012] compile-impact CLI compiles a focused production implementation change', { timeout: 120_000 }, () => {
  const scratchRoot = mkdtempSync(join(tmpdir(), 'wanxiangshu-impact-cli-'))
  try {
    const result = spawnSync(
      process.execPath,
      [CLI, CHANGED, '--scratch', scratchRoot],
      { cwd: ROOT, encoding: 'utf8', timeout: 110_000 },
    )
    assert.equal(result.status, 0, result.stderr || result.stdout)
    assert.match(result.stdout, /\[owner-compile\] OK: Wanxiangshu\.Impact\.fsproj/)

    const projectPath = findImpactProject(scratchRoot)
    assert.ok(projectPath, 'CLI must materialize Wanxiangshu.Impact.fsproj')
    const xml = readFileSync(projectPath, 'utf8')
    assert.ok(!xml.includes('<ProjectReference'), 'impact CLI must not hand the owner ProjectReference graph to Fable')
    assert.match(xml, /Foundation\/FatalProcess\.fs/)
    assert.ok(hasEmittedJsFiles(scratchRoot), 'focused impact compile must emit JavaScript')
  } finally {
    rmSync(scratchRoot, { recursive: true, force: true })
  }
})

test('WHAT[STRUCTURED-WORKFLOW-012] compile-impact CLI incremental compile detects and caches fresh output', { timeout: 120_000 }, () => {
  const scratchRoot = mkdtempSync(join(tmpdir(), 'wanxiangshu-impact-inc-'))
  const outputDir = join(scratchRoot, 'out')
  try {
    const result1 = spawnSync(
      process.execPath,
      [CLI, CHANGED, '--scratch', scratchRoot, '-o', outputDir],
      { cwd: ROOT, encoding: 'utf8', timeout: 110_000 },
    )
    assert.equal(result1.status, 0, result1.stderr || result1.stdout)
    assert.match(result1.stdout, /\[owner-compile\] OK: Wanxiangshu\.Impact\.fsproj/)

    const projectPath = findImpactProject(scratchRoot)
    assert.ok(projectPath, 'CLI must materialize Wanxiangshu.Impact.fsproj')
    const xml = readFileSync(projectPath, 'utf8')
    assert.ok(!xml.includes('<ProjectReference'), 'impact CLI must not hand the owner ProjectReference graph to Fable')
    assert.ok(hasEmittedJsFiles(outputDir), 'focused impact compile must emit JavaScript to output directory')

    const result2 = spawnSync(
      process.execPath,
      [CLI, '--scratch', scratchRoot, '-o', outputDir],
      { cwd: ROOT, encoding: 'utf8', timeout: 110_000 },
    )
    assert.equal(result2.status, 0, result2.stderr || result2.stdout)
    assert.match(result2.stdout, /up-to-date \(cached\)/)
    assert.doesNotMatch(result2.stdout, /Started Fable compilation/)
  } finally {
    rmSync(scratchRoot, { recursive: true, force: true })
  }
})
