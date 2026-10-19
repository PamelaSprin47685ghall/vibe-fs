import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = process.cwd()
const read = (path) => readFileSync(resolve(root, path), 'utf8')

// Superseded by ANCHOR view.extra and assume: OrdinaryTurnWorkflow and DeliverTool no longer consume DeferredWork.
test('WHAT[attention-regulation-005] OrdinaryTurnWorkflow and DeliverTool have no DeferredWork presentation or pendingAttentionWorkPairs hooks', () => {
  const workflow = read('src/Wanxiangshu/Composition/Turn/OrdinaryTurnWorkflow.fs')
  const deliver = read('src/Wanxiangshu/Mission/Work/OpenCode/DeliverTool.fs')
  assert.doesNotMatch(workflow, /presentPendingDeferredWork|presentDeferredWork|DeferredWorkConsumed|defer-presentation/)
  assert.doesNotMatch(deliver, /pendingDeferred|pendingAttentionWorkPairs|Interaction\.Attention/)
})
