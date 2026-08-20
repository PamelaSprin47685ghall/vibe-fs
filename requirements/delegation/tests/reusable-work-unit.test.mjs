import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

test('WHAT[DELEG-026] reusable delegation has no durable program-counter/state-machine vocabulary', () => {
  const handoff = readFileSync(new URL('../../../src/Wanxiangshu/Execution/Delegation/Handoff.fs', import.meta.url), 'utf8')
  const ledger = readFileSync(new URL('../../../src/Wanxiangshu/Execution/Delegation/HandoffLedger.fs', import.meta.url), 'utf8')
  const facts = readFileSync(new URL('../../../src/Wanxiangshu/Execution/Delegation/Facts.fs', import.meta.url), 'utf8')

  for (const source of [handoff, ledger, facts]) {
    assert.doesNotMatch(source, /WorkUnitStarted|WorkUnitFinished|WorkUnitAbandoned|ActiveWorkUnit|CurrentStage|NextAction/)
  }
  assert.doesNotMatch(ledger, /advanceHandoff|DelegateSessionId/)
  assert.match(ledger, /DelegationHandoffCompleted/)
})

test('WHAT[DELEG-026/027] fork admission failures happen before dispatch and active assignment never becomes BusyAgentNudge', () => {
  const forkTool = readFileSync(
    new URL('../../../src/Wanxiangshu/Execution/Delegation/Fork/OpenCode/Tool.fs', import.meta.url),
    'utf8',
  )

  const newFork = forkTool.slice(forkTool.indexOf('let private commitNewManagerFork'), forkTool.indexOf('let private finishNewManagerFork'))
  assert.ok(newFork.indexOf('recordFissionAffinity') < newFork.indexOf('runManagerFork'))

  const reuse = forkTool.slice(forkTool.indexOf('let private commitIdleReuse'), forkTool.indexOf('let private reuseWhileIdle'))
  assert.ok(reuse.indexOf('recordFissionAffinity') < reuse.indexOf('runManagerReuse'))

  const active = forkTool.slice(forkTool.indexOf('let private reuseWhileActive'), forkTool.indexOf('let private commitIdleReuse'))
  assert.doesNotMatch(active, /runtime\.Reuse|BusyAgentNudge|ChargeCarried/)
})

test('WHAT[DELEG-025] reusable fork terminal failure is guarded by the accepted authority root', () => {
  const lifecycle = readFileSync(
    new URL('../../../src/Wanxiangshu/Execution/Delegation/Fork/Host/RunLifecycle.fs', import.meta.url),
    'utf8',
  )

  assert.match(lifecycle, /TerminalStop\.belongsTo root stop/)
  assert.match(lifecycle, /Failed stop when not \(stopBelongsToRun run stop\) -> Task\.FromResult\(\(\)\)/)
})
