import test from 'node:test'
import assert from 'node:assert/strict'
import { planRequest, planFilter, appendTailOnce, budgetSuffix, turnBudget, isScopeInitialized, onTurnStart, resetScopeState, clearSessionState } from '../../../dist/Context/Scope/Surface.js'

const user = (key,time=1) => ({ kind: 'user', operator: true, key, timestamp: time, parts: [] })
const assistant = (...parts) => ({ kind: 'assistant', operator: false, parts })
const call = (id,tool) => ({ id,tool })
const result = (id) => ({ kind: 'result', operator: false, resultFor: id, resultField: 'content', parts: [] })

test('WHAT[context-compression-033] latest reset keeps historical reset calls and their paired results', () => {
  const entries = [
    user('first'), assistant(call('old','read')), result('old'),
    assistant(call('reset-1','resetScope')), result('reset-1'),
    user('steer',2), assistant(call('before','read'),call('reset-2','resetScope'),call('after','read')),
    result('before'),result('reset-2'),result('after'),
  ]
  const selected = planFilter(entries)
  assert.deepEqual(selected.map(s=>s.index),[0,3,4,5,6,8,9])
  assert.deepEqual(Array.from(selected[4].parts),[1,2])
})

test('WHAT[context-compression-033] reset removes old OMP tool results and preserves user context and new tool', () => {
  const messages=[user('goal'),assistant(call('old','read')),result('old'),assistant(call('r','resetScope')),result('r'),assistant(call('new','edit')),result('new')]
  assert.deepEqual(planFilter(messages).map(s=>s.index),[0,3,4,5,6])
})

test('WHAT[context-compression-034] reset preserves prompt identity and budget until a new operator prompt arrives', () => {
  const sid='scope-034-budget'
  clearSessionState(sid)
  const first=user('first',1000)
  planRequest(sid,[first])
  onTurnStart(sid)
  assert.equal(isScopeInitialized(sid),false)
  resetScopeState(sid,5)
  assert.equal(planRequest(sid,[first,assistant(call('reset','resetScope'))]).budget,5)
  onTurnStart(sid)
  assert.equal(turnBudget(sid),4)
  onTurnStart(sid)
  assert.equal(turnBudget(sid),3)
  const second=user('second',2000)
  const next=planRequest(sid,[first,assistant(call('reset','resetScope')),second])
  assert.equal(next.budget,0)
  assert.equal(next.initialized,false)
  resetScopeState(sid,8)
  assert.equal(turnBudget(sid),8)
  clearSessionState(sid)
})

test('WHAT[context-compression-034] request-copy suffix does not double append for text or blocks', () => {
  const suffix='[Remaining turnBudget: 5]'
  assert.equal(budgetSuffix(5),suffix)
  const once=appendTailOnce('output',suffix)
  assert.equal(once,'output\0\uFEFF'+suffix)
  assert.equal(appendTailOnce(once,suffix),once)
  const blocks=[{type:'text',text:'error body'}]
  const next=appendTailOnce(blocks,suffix)
  assert.equal(next[0].text,'error body\0\uFEFF'+suffix)
  assert.deepEqual(appendTailOnce(next,suffix),next)
  assert.equal(blocks[0].text,'error body')
})

test('WHAT[context-compression-034] HNI planning carries the exact mandatory initialization reminder', () => {
  const sid='scope-034-reminder'
  const plan=planRequest(sid,[user('new')])
  const reminder='[System: Estimate the turnBudget for the first fine-grained task block (state closed-loop). Then call resetScope immediately, before doing anything else.]'
  assert.equal(plan.hints[0].tail,reminder)
  assert.equal(plan.hints[0].kind,'user')
  clearSessionState(sid)
})

test('WHAT[context-compression-034] user-attributed custom messages survive, agent-attributed custom ones do not', () => {
  const message=(operator)=>({kind:'custom',operator,parts:[]})
  const inputs=[user('task'),message(true),message(false),assistant(call('read','read')),result('read'),assistant(call('reset','resetScope')),result('reset')]
  assert.deepEqual(planFilter(inputs).map(s=>s.index),[0,1,5,6])
})
