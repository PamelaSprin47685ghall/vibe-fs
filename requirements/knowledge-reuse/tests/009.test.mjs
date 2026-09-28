import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdirSync, mkdtempSync, rmSync } from 'node:fs'
import { execFileSync } from 'node:child_process'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { sandbox, casebook, eventStore } from './support/casebook.mjs'
import * as lifecycle from '../../../dist/Repository/Knowledge/Casebook/LifecycleSurface.js'

test('WHAT[knowledge-reuse-009] absent marker disables feature and rejects the actual fetch tool', async () => {
  const local = sandbox({ enabled: false })
  try {
    assert.equal(casebook.featureEnabled(local.dir), false)
    assert.match(await local.fetch('Anything · 00000000'), /could not be read|无法.*读取|不可用/i)
    mkdirSync(join(local.dir, '.wanxiang', 'casebook'), { recursive: true })
    assert.equal(casebook.featureEnabled(local.dir), true)
  } finally { local.close() }
})

test('WHAT[knowledge-reuse-009] actual lifecycle finalization without a marker does not publish a case', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-casebook-off-'))
  execFileSync('git', ['init', '--quiet', dir])
  const store = eventStore.create(join(dir, '.git'), 'off-writer')
  try {
    lifecycle.enable(dir)
    lifecycle.notePrompt('off', 'Q')
    lifecycle.noteAnswer('off', 'A')
    assert.equal((await lifecycle.tryFinalize(dir, 'off')).ok, true)
    assert.equal(await casebook.fetchCaseByIdentity(store, 'off'), null)
  } finally { lifecycle.disable(); eventStore.dispose(store); rmSync(dir, { recursive: true, force: true }) }
})

test.todo('WHAT[knowledge-reuse-009] GAP-160: actual plugin excludes description and index and appends no Casebook facts while disabled')
