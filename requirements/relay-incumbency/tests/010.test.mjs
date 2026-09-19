import assert from 'node:assert/strict'
import test from 'node:test'
import * as relay from '../../../dist/Mission/Relay/Surface.js'



test('WHAT[relay-incumbency-010] road owns unique logical devops and active incumbent holds exclusive invocation authority', () => {
  const first = relay.openIncumbency(relay.empty(), 'road-1', 'inc-1', 'snapshot-1', 'authority-1')
  assert.equal(first.ok, true)

  if (typeof relay.roadDevOps === 'function') {
    const devops = relay.roadDevOps(first.state, 'road-1')
    assert.ok(devops !== null, 'road must have bound logical DevOps')
    assert.equal(devops.incumbentId, 'inc-1')
  } else {
    assert.fail('relay.roadDevOps is not yet implemented')
  }
})
