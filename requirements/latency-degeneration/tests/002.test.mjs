import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { interruptAttemptAdapterProbe } from '../../../dist/OpenCode/Host/SessionsSurface.js'

test('WHAT[latency-degeneration-007] interrupting a Provider attempt retains a managed child session for its next continuation',async()=>{
  const physical=await interruptAttemptAdapterProbe()
  assert.equal(physical.created,true)
  assert.equal(physical.childInterrupted,true)
  assert.equal(physical.childStillManagedAfterInterrupt,true,
    'the continuation must address the original still-managed Session, not a session terminated by AbortSession')
  assert.equal(physical.rootRejected,true,
    'the external operator owns interruptions of a user-facing root Session')
})

test('WHAT[latency-degeneration-007] production latency watchdog uses the attempt interruption port, not session termination',()=>{
  const source=readFileSync(new URL('../../../src/Wanxiangshu/OpenCode/Plugin/PluginHooks.fs',import.meta.url),'utf8')
  const beginning=source.indexOf('let latencyPorts: LatencyWatchdogPorts')
  const end=source.indexOf('let latencyWatchdog =',beginning)
  assert.ok(beginning>0&&end>beginning)
  const wiring=source.slice(beginning,end)
  assert.match(wiring,/Abort = fun sessionId ->[\s\S]*?sessionPort\.InterruptAttempt/)
  assert.doesNotMatch(wiring,/sessionPort\.AbortSession/,
    'a watchdog must not tear down the session it needs to resume')
  assert.match(wiring,/HostSessionNudge\.sendContinuationResult/,
    'the interruption must send the existing managed continuation, not only record censored latency')
})
