import assert from 'node:assert/strict'
import test from 'node:test'
import * as owner from '../../../dist/Context/Companion/RuntimeSurface.js'

const ctx = owner
const parkedTransform = owner
const KEY = 'ses-blog-par-017'

test('WHAT[provider-attempt-recovery-017] PAR_017_blogger_retry_replaces_exact_physical_binding_before_redispatch', () => {
  const scope = parkedTransform.scope()
  const failed = ctx.main({ requestId: 'request-failed', toml: 'failed' })
  const replacement = ctx.main({ requestId: 'request-replacement', toml: 'replacement' })

  // 1. Old request claimed
  assert.equal(parkedTransform.claimCurrentRequest(scope, KEY, failed), 'Claimed')

  // 2. Failed request released
  assert.equal(parkedTransform.releaseCurrentRequest(scope, KEY, 'request-failed'), 'Released')

  // 3. Replacement request claimed
  assert.equal(parkedTransform.claimCurrentRequest(scope, KEY, replacement), 'Claimed')

  // 4. Stale release of old request must conflict with replacement
  assert.equal(
    parkedTransform.releaseCurrentRequest(scope, KEY, 'request-failed'),
    'Conflict:request-replacement',
    'old request cannot release replacement ownership',
  )

  // 5. Active request is the replacement
  assert.equal(parkedTransform.peekCurrentRequest(scope, KEY)?.toml, 'replacement')

  // Mutation test: trying to claim with old request again when new request is active causes Conflict
  assert.equal(
    parkedTransform.claimCurrentRequest(scope, KEY, failed),
    'Conflict:request-replacement',
  )
})
