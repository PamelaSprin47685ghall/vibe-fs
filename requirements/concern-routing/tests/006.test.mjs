import assert from 'node:assert/strict'
import { existsSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = process.cwd()

// Superseded by this.fork / this.join / this.deliver / this.ask: AttentionConcernJournalAdapter and retireMailboxesOf are deleted.
test('WHAT[concern-routing-006] AttentionConcernJournalAdapter and its durable adapter shard are gone', () => {
  assert.equal(
    existsSync(resolve(root, 'src/Wanxiangshu/Composition/Durable/AttentionConcernJournalAdapter.fs')),
    false,
  )
  assert.equal(
    existsSync(resolve(root, 'src/Wanxiangshu/Composition/Durable/AttentionConcernJournalAdapter.fsi')),
    false,
  )
  assert.equal(
    existsSync(resolve(root, 'src/Wanxiangshu/Wanxiangshu.Owner.durable-composition.durable-adapter-attention-concern.fsproj')),
    false,
  )
})
