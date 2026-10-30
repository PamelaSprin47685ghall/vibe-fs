import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as Observation from '../../../dist/Composition/Turn/Observation.js'
import * as identity from '../../../dist/Foundation/Identity.js'
import * as owner from '../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js'

test('WHAT[provider-attempt-recovery-026] missing active profile is terminal, never superseded', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-recovery-026-none-'))
  const created = await journal.JournalSurface_bootWithWriterId(directory, 'writer-026-none', 'runtime-026-none', 1, '2026-01-01T00:00:00Z')
  try {
    const verdict = await owner.continueSameProviderRecoveryVerdict(created.journal, 'ses-026-none', 'msg-root', 'run-026')
    assert.equal(verdict, 'Terminal')
  } finally {
    journal.JournalSurface_dispose(created.journal)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[provider-attempt-recovery-026] a different authority root stays a silent supersession', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-recovery-026-takeover-'))
  const created = await journal.JournalSurface_bootWithWriterId(directory, 'writer-026-takeover', 'runtime-026-takeover', 1, '2026-01-01T00:00:00Z')
  try {
    const accepted = await owner.acceptHumanRoot(created.journal, 'ses-026-takeover', 'msg-root', 'engineer')
    assert.equal(accepted.ok, true)
    const verdict = await owner.continueSameProviderRecoveryVerdict(created.journal, 'ses-026-takeover', 'msg-other', 'run-026')
    assert.equal(verdict, 'Superseded')
  } finally {
    journal.JournalSurface_dispose(created.journal)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[provider-attempt-recovery-026] terminal verdict notifies terminal failure on the real stop-or-continue path', async () => {
  const { mkdtempSync, rmSync } = await import('node:fs')
  const { tmpdir } = await import('node:os')
  const { join } = await import('node:path')
  const EventsSurface = await import('../../../dist/OpenCode/Host/EventsSurface.js')
  const Workflow = await import('../../../dist/Participant/Provider/Attempt/Fallback/Workflow.js')

  const directory = mkdtempSync(join(tmpdir(), 'wxs-recovery-026-terminal-'))
  const created = await journal.JournalSurface_bootWithWriterId(directory, 'writer-026-terminal', 'runtime-026-terminal', 1, '2026-01-01T00:00:00Z')
  try {
    // journal 先建立 authority root（对齐第二用例搭台先例）
    const accepted = await owner.acceptHumanRoot(created.journal, 'ses-026-terminal', 'msg-026-terminal-root', 'engineer')
    assert.equal(accepted.ok, true, 'authority root must be accepted before behavior test')

    // 事件假体：沿 016 先例（EventsSurface.create() + subscribe 捕获）
    const eventPort = EventsSurface.create()
    const terminals = []
    EventsSurface.subscribe(eventPort, (sessionId, outcome) => terminals.push({ sessionId, outcome }))

    // turn 用编译构造器（ReconciledTurn extends Record，位置参数构造）
    const turn = new Observation.ReconciledTurn(
      identity.SessionIdModule_create('ses-026-terminal'),
      identity.PhysicalUserMessageIdModule_create('msg-026-terminal'),
      identity.AuthorityRootUserMessageIdModule_create('msg-026-terminal-root'),
      identity.ProviderRunIdentityModule_create('run-026-terminal'),
      null,   // Role：缺 profile 场景下 Terminal 分支不触 Role 分派
      null,   // Directory
      [],     // Parts
      null,   // Finish
      null,   // ErrorName
      null,   // Model
      null,   // Outcome
      null,   // Observation
    )

    // 真链路：continueAfterConfirmedFailure → stopOrContinueLongWork → notifyFailure
    await Workflow.continueAfterConfirmedFailure(
      null,        // sessionPort：缺 profile 走 Terminal 分支，不触 SendPrompt
      null,        // rootWorkspace：同上
      eventPort,
      created.journal.Journal,
      turn,
      { tag: 6 },  // ExecutionFailure.ProviderTransient（Model.js 第 41 行：tag 6）
      'No active authority profile',
      'probe continuation prompt',
    )

    // 可观察失败断言：NotifyTerminal 真实发出、载荷 Failed
    assert.equal(terminals.length, 1, JSON.stringify(terminals))
    assert.equal(terminals[0].outcome.kind, 'Failed', 'terminal outcome must be Failed')
    assert.ok(String(terminals[0].outcome.text || '').includes('No active authority profile'), 'failure reason must cite the missing profile')
  } finally {
    journal.JournalSurface_dispose(created.journal)
    rmSync(directory, { recursive: true, force: true })
  }
})
