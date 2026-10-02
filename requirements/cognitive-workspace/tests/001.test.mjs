import assert from 'node:assert/strict'
import { existsSync, readdirSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = process.cwd()

test('WHAT[cognitive-workspace-001] the persistent Cognition production directory is gone', () => {
  const cognitionDir = resolve(root, 'src/Wanxiangshu/Participant/Cognition')
  assert.equal(
    existsSync(cognitionDir) ? readdirSync(cognitionDir).length : 0,
    0,
    'no production Cognition files remain; an empty filesystem directory is inert and untracked',
  )
  assert.equal(
    existsSync(resolve(root, 'src/Wanxiangshu/Wanxiangshu.Owner.cognitive-workspace.participant-cognition-workspace.fsproj')),
    false,
  )
})
