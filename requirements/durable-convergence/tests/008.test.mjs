import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as hook from '../../../dist/Git/Hook/Surface.js'
import { createBareWorkspace, readRemoteStoreOid } from '../../verification-system/tests/support/dumb-remote.mjs'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'
import { event } from './support/events.mjs'
import { appendFact, assertFacts, runHook } from './support/hooks.mjs'

integrationTest('WHAT[durable-convergence-008] reference-transaction imports the observed snapshot and publishes independent local truth in one hook process', async () => {
  const workspace = createBareWorkspace(['left', 'right'])
  try {
    const left = workspace.client('left')
    const right = workspace.client('right')
    const a = event('a'.repeat(40), [], { writer: 'left' })
    const b = event('b'.repeat(40), [], { writer: 'right' })
    await appendFact(left, 'writer-left', a)
    await appendFact(right, 'writer-right', b)
    runHook(left)
    const before = readRemoteStoreOid(workspace.bare)
    execFileSync('git', ['-C', right, 'fetch', '-q', 'origin', '+refs/wanxiang/store:refs/wanxiang/remotes/origin/store'])
    const update = `${'0'.repeat(40)} ${before} refs/wanxiang/remotes/origin/store\n`
    runHook(right, 'reference-transaction', 'committed', update)
    const after = readRemoteStoreOid(workspace.bare)
    assert.notEqual(after, before, 'local independent truth must also be published')
    assertFacts(right, [a, b])
    runHook(left)
    assertFacts(left, [a, b])
    runHook(right, 'reference-transaction', 'committed', update)
    assert.equal(readRemoteStoreOid(workspace.bare), after, 'stale observed input cannot replace newer union')
  } finally {
    workspace.cleanup()
  }
})

const storeLine = remote => `+refs/wanxiang/store:refs/wanxiang/remotes/${remote}/store`
const headsLine = remote => `+refs/heads/*:refs/remotes/${remote}/*`
const git = (repo, ...args) => execFileSync('git', ['-C', repo, ...args], { encoding: 'utf8' })
const fetchSpecs = (repo, remote) => git(repo, 'config', '--get-all', `remote.${remote}.fetch`).trim().split('\n')

test('WHAT[durable-convergence-008] ensure adds both fetch mappings to every remote while preserving custom order and remaining idempotent', () => {
  const repo = mkdtempSync(join(tmpdir(), 'wxs-fetch-baseline-'))
  try {
    git(repo, 'init', '--quiet')
    for (const remote of ['origin', 'upstream']) {
      git(repo, 'remote', 'add', remote, `https://example.com/${remote}.git`)
      git(repo, 'config', '--replace-all', `remote.${remote}.fetch`, '+refs/tags/*:refs/tags/*')
      git(repo, 'config', '--add', `remote.${remote}.fetch`, `+refs/review/*:refs/remotes/${remote}/review/*`)
    }
    const before = new Map(['origin', 'upstream'].map(remote => [remote, fetchSpecs(repo, remote)]))
    assert.equal(hook.ensure(repo), true)
    const after = new Map()
    for (const remote of ['origin', 'upstream']) {
      const specs = fetchSpecs(repo, remote)
      assert.deepEqual(specs.slice(0, 2), before.get(remote))
      assert.equal(specs.length, 4)
      assert.ok(specs.includes(storeLine(remote)))
      assert.ok(specs.includes(headsLine(remote)))
      after.set(remote, specs)
    }
    assert.equal(hook.ensure(repo), true)
    for (const remote of ['origin', 'upstream']) assert.deepEqual(fetchSpecs(repo, remote), after.get(remote))
  } finally {
    rmSync(repo, { recursive: true, force: true })
  }
})

test('WHAT[durable-convergence-008] ensure repairs a store-only remote without replacing its existing line', () => {
  const repo = mkdtempSync(join(tmpdir(), 'wxs-fetch-store-only-'))
  try {
    git(repo, 'init', '--quiet')
    git(repo, 'remote', 'add', 'origin', 'https://example.com/repo.git')
    git(repo, 'config', '--replace-all', 'remote.origin.fetch', storeLine('origin'))
    assert.deepEqual(fetchSpecs(repo, 'origin'), [storeLine('origin')])
    assert.equal(hook.ensure(repo), true)
    assert.deepEqual(fetchSpecs(repo, 'origin'), [storeLine('origin'), headsLine('origin')])
    assert.equal(hook.ensure(repo), true)
    assert.deepEqual(fetchSpecs(repo, 'origin'), [storeLine('origin'), headsLine('origin')])
  } finally {
    rmSync(repo, { recursive: true, force: true })
  }
})

test.todo('WHAT[durable-convergence-008] real plugin load leaves Git configuration unchanged and first durability activation installs hooks without starting synchronization (GAP-151)')
test.todo('WHAT[durable-convergence-008] controlled CAS competition and replacement crash cuts preserve all facts while clean no-op leaves unseen remote progress untouched (GAP-151)')
