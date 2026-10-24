import test from 'node:test'
import { fixture, assert, data, caseName, failureCode, parkedName, parkedPayload, parkedSource, render, created, readFileSync, writeFileSync, existsSync, pendingNames } from './support/named-program.fixture.mjs'

test('WHAT[repository-programming-037] parked rendering includes named state, original payload and complete source', async t => {
  const h=fixture(t,'037')
  const result=await h.run('review',"await this.park({question:'proceed?',choices:['yes','no']});return 1;")
  const receipt=parkedPayload(result)
  assert.equal(receipt.name,'review');assert.equal(receipt.state,'parked')
  assert.deepEqual(receipt.payload,{question:'proceed?',choices:['yes','no']})
  assert.equal(Object.hasOwn(receipt,'handle'),false)
  assert.match(receipt.source,/class Js extends JsProgram/)
  assert.match(receipt.source,/return 1;/)
  assert.match(render(result),/name = "review"/)
  assert.match(render(result),/state = "parked"/)
  assert.doesNotMatch(render(result),/^handle =/m)
})
