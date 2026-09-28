import assert from 'node:assert/strict'
import test from 'node:test'
import * as Attempt from '../../../dist/Context/Companion/CompressionSurface.js'
import * as Authority from '../../../dist/Interaction/Authority/Surface.js'

const plan = (role) => Attempt.attemptPlan({ role, kind: 'work-main', noCandidateReason: 'NoCoverage' })

test('WHAT[participant-identity-005] provider planning selects the identity prompt from profile Role', () => {
  const engineer = plan('engineer')
  const repeated = plan('engineer')
  const devops = plan('devops')

  assert.equal(engineer.systemPromptId, engineer.participantIdentity.role)
  assert.equal(repeated.systemPromptId, engineer.systemPromptId)
  assert.deepEqual(repeated.toolCapabilities, engineer.toolCapabilities)
  assert.equal(devops.systemPromptId, devops.participantIdentity.role)
  assert.equal(Authority.systemPromptIdForRole(engineer.participantIdentity.role), engineer.systemPromptId)
  assert.equal(Authority.systemPromptIdForRole(devops.participantIdentity.role), devops.systemPromptId)
  assert.notDeepEqual(devops.toolCapabilities, engineer.toolCapabilities)
  assert.equal(engineer.toolCapabilities.includes('Write'), true)
  assert.equal(devops.toolCapabilities.includes('Exec'), true)
})
