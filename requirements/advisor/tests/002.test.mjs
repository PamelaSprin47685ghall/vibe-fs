import assert from 'node:assert/strict'
import test from 'node:test'
import { plan } from '../../../dist/Execution/Delegation/Advisor/Surface.js'

const messages = [1, 2, 3].map((anchor) => ({ anchor, text: 'material-' + anchor }))
const request = (prefixUntil, thenMap = {}, history = []) => ({
  messages, declaration: { prefixUntil, thenMap }, history,
})
const count = (prompt) => (prompt.split('\n\n').slice(1).join('\n\n').match(/\[\[new_work_to_record\]\]/g) ?? []).length

test('WHAT[advisor-002] the shared view retains the full context and protects its prefix in blogger tables', () => {
  const planned = plan('en', [], request(3, { 1: 'ignored', 3: null }))
  assert.deepEqual(planned.deliveredAnchors, [1, 2, 3])
  assert.equal(count(planned.viewPrompt), 3)
  assert.equal(planned.viewPrompt.startsWith('# '), true)
  assert.equal(planned.viewPrompt.endsWith('\n'), true)
  assert.match(planned.viewPrompt, /material-1/)
  assert.match(planned.viewPrompt, /material-2/)
  assert.match(planned.viewPrompt, /material-3/)
  assert.doesNotMatch(planned.viewPrompt, /ignored/)
})

test('WHAT[advisor-002] interval projection includes prior summaries before emitting the view', () => {
  const planned = plan('en', [], request(0, { 1: 'one-and-two' }, [
    { anchor: 2, until: 3, summary: 'old-two-summary' },
  ]))
  assert.equal(count(planned.viewPrompt), 2)
  assert.match(planned.viewPrompt, /one-and-two/)
  assert.match(planned.viewPrompt, /material-3/)
  assert.doesNotMatch(planned.viewPrompt, /material-1|material-2|old-two-summary/)
})

test('WHAT[advisor-002] an empty context has no new material and keeps the authored header', () => {
  const planned = plan('en', [], { messages: [], declaration: { prefixUntil: 0, thenMap: {} } })
  assert.equal(planned.hasNewMaterial, false)
  assert.deepEqual(planned.deliveredAnchors, [])
  assert.equal(count(planned.viewPrompt), 0)
  assert.equal(planned.viewPrompt.startsWith('# '), true)
})
