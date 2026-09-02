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

test('WHAT[STRUCTURED-WORKFLOW-012] compile-impact CLI watch canary uses one flat Fable process', { timeout: 120_000 }, async () => {
  const scratchRoot = mkdtempSync(join(tmpdir(), 'wanxiangshu-impact-watch-'))
  const child = spawn(
    process.execPath,
    [CLI, CHANGED, '--scratch', scratchRoot, '--watch'],
    {
      cwd: ROOT,
      stdio: ['ignore', 'pipe', 'pipe'],
      detached: true,
    },
  )

  let output = ''
  const append = (chunk) => {
    output += chunk
  }
  child.stdout.setEncoding('utf8')
  child.stderr.setEncoding('utf8')
  child.stdout.on('data', append)
  child.stderr.on('data', append)

  try {
    await new Promise((resolveReady, reject) => {
      let settled = false
      let timer
      let poll
      const isReady = () =>
        /Watching|compilation finished|Compiled in|Fable compilation finished/i.test(output)
        || hasEmittedJsFiles(scratchRoot)

      const finish = (error) => {
        if (settled) return
        settled = true
        clearTimeout(timer)
        clearInterval(poll)
        child.stdout.off('data', onData)
        child.stderr.off('data', onData)
        if (error) reject(error)
        else resolveReady()
      }

      const onData = () => {
        if (isReady()) finish()
      }
      timer = setTimeout(() => {
        finish(new Error(`watch canary timed out\n${output}`))
      }, 110_000)
      poll = setInterval(onData, 500)
      child.stdout.on('data', onData)
      child.stderr.on('data', onData)
      child.once('error', (error) => finish(error))
      child.once('exit', (code, signal) => {
        if (isReady()) {
          finish()
          return
        }
        finish(new Error(`watch exited before first compile code=${code} signal=${signal}\n${output}`))
      })
    })

    const projectPath = findImpactProject(scratchRoot)
    assert.ok(projectPath, 'watch canary must materialize Wanxiangshu.Impact.fsproj')
    const xml = readFileSync(projectPath, 'utf8')
    assert.ok(!xml.includes('<ProjectReference'), 'watch must reuse the flat impact project, not the owner graph')
    assert.ok(hasEmittedJsFiles(scratchRoot), 'watch canary must emit JavaScript on first compile')
  } finally {
    killProcessGroup(child)
    await new Promise((resolveWait) => {
      const timer = setTimeout(resolveWait, 5_000)
      child.once('exit', () => {
        clearTimeout(timer)
        resolveWait()
      })
    })
    rmSync(scratchRoot, { recursive: true, force: true })
  }
})
