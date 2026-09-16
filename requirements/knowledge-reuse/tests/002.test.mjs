import assert from 'node:assert/strict'
import test from 'node:test'
import * as domain from '../../../dist/Knowledge/Casebook/DomainSurface.js'
import * as fetchTool from '../../../dist/OpenCode/Tools/KnowledgeFetchSurface.js'
import * as wiring from '../../../dist/OpenCode/Host/KnowledgeLifecycleSurface.js'

test('WHAT[KNOWLEDGE-REUSE-002] CASE002_fold_captured_and_refreshed_keeps_qa_verbatim', () => {
  const q = ' What is   the exact   answer? '
  const a = '  The exact\nanswer is 42.  '
  const c = domain.createCase('c1', q, a, [], '2026-01-01T00:00:00Z')
  assert.equal(c.question, q)
  assert.equal(c.answer, a)

  const refreshed = domain.refreshCase(c, '  New answer\nverbatim. ', [], '2026-01-02T00:00:00Z')
  assert.equal(refreshed.question, q)
  assert.equal(refreshed.answer, '  New answer\nverbatim. ')
})

test('WHAT[KNOWLEDGE-REUSE-002] CASE004_fetch_returns_exact_canonical_a', async () => {
  const q = 'exact question'
  const a = 'verbatim answer with \n newlines and   spaces'
  const res = await fetchTool.executeDirect({ shelfmark: 'c-exact', question: q, answer: a, observations: [] })
  assert.equal(res.ok, true)
  assert.match(res.output, /verbatim answer with \n newlines and   spaces/)
})

test('WHAT[KNOWLEDGE-REUSE-002] lifecycle_notePrompt_noteAnswer_tryFinalize_creates_case_once', async () => {
  let state = wiring.emptyState
  state = wiring.notePrompt('ses-1', 'what is x?', state)
  state = wiring.noteAnswer('ses-1', 'x is 10', state)
  const finalized = wiring.tryFinalize('ses-1', state)
  assert.equal(finalized.published, true)
  assert.equal(finalized.entry.question, 'what is x?')
  assert.equal(finalized.entry.answer, 'x is 10')
})
