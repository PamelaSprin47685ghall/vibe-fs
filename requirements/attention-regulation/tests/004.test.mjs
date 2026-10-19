import assert from 'node:assert/strict'
import { existsSync, readdirSync, statSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = process.cwd()
const listFiles = (dir) => {
  if (!existsSync(dir)) return []
  return readdirSync(dir).flatMap((entry) => {
    const full = resolve(dir, entry)
    return statSync(full).isDirectory() ? listFiles(full) : [full]
  })
}

// Superseded by ANCHOR view.extra and assume: the Interaction/Attention runtime and AttentionTools modules are deleted.
test('WHAT[attention-regulation-004] the Interaction/Attention directory, AttentionTools, and compile shards are gone', () => {
  const attentionDir = resolve(root, 'src/Wanxiangshu/Interaction/Attention')
  assert.equal(
    listFiles(attentionDir).length,
    0,
    'no production Interaction/Attention files remain',
  )
  assert.equal(existsSync(resolve(root, 'src/Wanxiangshu/OpenCode/Tools/AttentionTools.fs')), false)
  assert.equal(existsSync(resolve(root, 'src/Wanxiangshu/OpenCode/Tools/AttentionToolSurface.fs')), false)
  assert.equal(existsSync(resolve(root, 'src/Wanxiangshu/Composition/Durable/AttentionConcernJournalAdapter.fs')), false)
  assert.equal(
    existsSync(resolve(root, 'src/Wanxiangshu/Wanxiangshu.Owner.attention-regulation.interaction-attention-facts.fsproj')),
    false,
  )
  assert.equal(
    existsSync(resolve(root, 'src/Wanxiangshu/Wanxiangshu.Owner.attention-regulation.interaction-attention-fold.fsproj')),
    false,
  )
})
