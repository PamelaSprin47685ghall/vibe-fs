import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import * as learning from '../../../dist/Enforcer/InstitutionalLearning/Surface.js'

test('WHAT[INSTITUTIONAL-LEARNING-007] celebrate surfaces deferred work at tail while regret surfaces zero and replay is idempotent', () => {
  // 1. Static ordering & scoping verification in InstitutionalLearningTools.fs
  const toolsSource = readFileSync('src/Wanxiangshu/OpenCode/Tools/InstitutionalLearningTools.fs', 'utf8')

  // pendingFor must query pendingAttentionWorkPairs ONLY for Celebrate and return [] for Regret
  const pendingForMatch = toolsSource.match(/let private pendingFor kind durable sessionId =\s*([\s\S]*?)let private commitLearning/)
  assert.ok(pendingForMatch, 'pendingFor function must exist')
  assert.match(pendingForMatch[1], /ExperienceKind\.Celebrate ->/, 'Celebrate queries attention-regulation pending work')
  assert.match(pendingForMatch[1], /ExperienceKind\.Regret -> \[\]/, 'Regret returns empty list for pending work')

  // dispositionInstructions must be concatenated BEFORE resurfacedInstructions (tail placement)
  assert.match(toolsSource, /dispositionInstructions language disposition\s*@\s*resurfacedInstructions language pending/,
    'resurfaced deferred work must be placed strictly at the tail of disposition instructions')

  // ResurfacedDeferredWorkIds must record pending work IDs in the committed fact
  assert.match(toolsSource, /ResurfacedDeferredWorkIds = pending \|> List\.map fst/,
    'committed fact must record resurfaced deferred work IDs')

  // 2. Dynamic projection & replay idempotence on Surface
  const state0 = learning.empty()
  const occurrence = 'occ-il007-1'
  const session = 'ses-il007'

  // Commit celebrate with resurfaced items
  const committedCelebrate = learning.commit(
    session,
    occurrence,
    'celebrate',
    'experienced success',
    'rev-1',
    'ABSORB',
    'disposition result\n---\nDeferred work: task-1',
    ['def-1', 'def-2'],
    state0
  )
  assert.ok(committedCelebrate, 'commit celebrate must return updated projection state')

  // Commit regret with zero resurfaced items
  const occurrenceRegret = 'occ-il007-2'
  const committedRegret = learning.commit(
    session,
    occurrenceRegret,
    'regret',
    'experienced failure',
    'rev-1',
    'DISCARD',
    'discarded disposition',
    [],
    committedCelebrate
  )
  assert.ok(committedRegret, 'commit regret must return updated projection state')

  // Idempotent replay: committing with same occurrence returns identical state without duplicate append
  const replayed = learning.commit(
    session,
    occurrence,
    'celebrate',
    'experienced success',
    'rev-1',
    'ABSORB',
    'disposition result\n---\nDeferred work: task-1',
    ['def-1', 'def-2'],
    committedRegret
  )
  assert.ok(replayed, 'replaying committed occurrence must succeed idempotently')
})
