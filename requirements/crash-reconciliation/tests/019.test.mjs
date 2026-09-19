import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import * as resume from '../../../dist/OpenCode/Host/ExplicitResumeSurface.js'

test('WHAT[crash-reconciliation-019] external effects close 4 phase contract and ambiguous evidence fails closed without durable pc', async () => {
  // 1. Non-continue command is disclosure-only / no-op and never triggers automatic command replay
  const statusOutput = await resume.run('status', 'ses-crash019', '')
  assert.deepEqual(statusOutput.parts, [], 'non-continue command must not trigger automatic execution replay')

  // 2. Continue with missing session discloses restart without minting completion or second execution authority
  const missingSessionOutput = await resume.run('continue', '', '')
  assert.equal(missingSessionOutput.parts.length, 1)
  assert.match(missingSessionOutput.parts[0].text, /no session id was supplied/i)
  assert.match(missingSessionOutput.parts[0].text, /previous interrupted tool remains failed/i)

  // 3. Invariant: Durable recovery states and facts contain zero program counters (ResumeAt, RecoveryStage, RecoveryStep, NextAction)
  const factsSource = readFileSync('src/Wanxiangshu/Execution/Session/ChatExecution/Facts.fs', 'utf8')
  assert.doesNotMatch(factsSource, /ResumeAt/, 'ChatExecution facts must not persist ResumeAt program counter')
  assert.doesNotMatch(factsSource, /RecoveryStage/, 'ChatExecution facts must not persist RecoveryStage program counter')
  assert.doesNotMatch(factsSource, /RecoveryStep/, 'ChatExecution facts must not persist RecoveryStep program counter')
  assert.doesNotMatch(factsSource, /NextAction/, 'ChatExecution facts must not persist NextAction program counter')

  const recoveryHostSource = readFileSync('src/Wanxiangshu/OpenCode/Host/SessionRecoveryHost.fs', 'utf8')
  assert.doesNotMatch(recoveryHostSource, /ResumeAt/, 'SessionRecoveryHost must not contain ResumeAt')
  assert.doesNotMatch(recoveryHostSource, /NextAction/, 'SessionRecoveryHost must not contain NextAction')

  // 4. Invariant: ambiguous physical observation fails closed to manual intervention or reconcile physical
  assert.match(recoveryHostSource, /ProviderPhysicalObservation\.ReceiptAmbiguous/, 'receipt ambiguous observation must exist')
  assert.match(recoveryHostSource, /ReceiptAmbiguous ->/, 'ambiguous physical receipt must be explicitly handled')
})
