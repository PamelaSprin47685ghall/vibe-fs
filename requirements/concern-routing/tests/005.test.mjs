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

// Superseded by js.fork / js.join / js.deliver / js.ask: the Interaction/Concern directory, ConcernTools, and compile shards are deleted.
test('WHAT[concern-routing-005] the Interaction/Concern directory, ConcernTools, and compile shards are gone', () => {
  const concernDir = resolve(root, 'src/Wanxiangshu/Interaction/Concern')
  assert.equal(
    listFiles(concernDir).length,
    0,
    'no production Interaction/Concern files remain',
  )
  assert.equal(existsSync(resolve(root, 'src/Wanxiangshu/OpenCode/Tools/ConcernTools.fs')), false)
  assert.equal(existsSync(resolve(root, 'src/Wanxiangshu/OpenCode/Tools/ConcernTools.fsi')), false)
  assert.equal(
    existsSync(resolve(root, 'src/Wanxiangshu/Wanxiangshu.Owner.concern-routing.interaction-concern-facts.fsproj')),
    false,
  )
  assert.equal(
    existsSync(resolve(root, 'src/Wanxiangshu/Wanxiangshu.Owner.concern-routing.interaction-concern-fold.fsproj')),
    false,
  )
})
