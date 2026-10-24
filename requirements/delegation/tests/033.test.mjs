import assert from 'node:assert/strict'
import test from 'node:test'
import { createJoinProbe, joinProbeDeliver } from '../../../dist/Execution/Delegation/Fork/JoinSurface.js'
import {
  childTurn,
  forkCall,
  observeTerminals,
  withRealFork,
} from './support/real-fork.mjs'

const prose = 'DELIVERED PROSE'

test('WHAT[delegation-033] clean natural stop settles a journal-less run with the final assistant prose', { timeout: 3000 }, async () => {
  const result = await joinProbeDeliver(createJoinProbe(), [], 'current report')
  assert.equal(result.status, 'completed')
  assert.equal(result.body, 'current report')
  assert.doesNotMatch(result.body, /\[\[session\]\]/, 'the natural-stop return is prose, never a session payload')
  assert.deepEqual(result.returned, [], 'a natural stop carries no structured return material')
})

test('WHAT[delegation-033] clean natural stop settles the child run without requiring a deliver member', async () => {
  const owner = 'ses-owner-033'
  await withRealFork(owner, async ({ hooks, createdIds, runtime }) => {
    const terminals = await observeTerminals(runtime)
    try {
      const first = await forkCall(hooks, owner, 'Ada', 'DELIVER THIS WORK')
      assert.match(first, /ok = true/)
      const child = createdIds[0]
      await childTurn(hooks, runtime, child, `run-${child}-1`, prose, 'RESULT-PROSE', terminals.awaitRun)

      const completed = terminals.seen.find(
        (entry) => entry.sessionId === child && entry.outcome?.kind === 'Completed',
      )
      assert.ok(completed, 'the delivering run publishes its completed terminal')
    } finally {
      terminals.dispose()
    }
  })
})
