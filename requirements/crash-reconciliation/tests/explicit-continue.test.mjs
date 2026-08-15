import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import {
  agentFact,
  agentJournal,
  handleId,
  handleOwnership,
  journalRevision,
  okResult,
  promptDispatcher,
  resultOf,
  roles,
  sessionId,
  stream,
} from '../../verification-system/tests/support/domain.mjs'

import { registerCommand, before as continueBefore } from '../../../dist/OpenCode/Host/ExplicitSessionResume.js'
import {
  ToolRuntimeScope_$ctor_18E8298F as createToolScope,
  ToolRuntimeScope__AdoptExistingChild_875B0C9 as adoptExistingChild,
  ToolRuntimeScope__RuntimeFor_Z939596C as runtimeFor,
  ToolRuntimeScope__DisposeAsync as disposeToolScope,
} from '../../../dist/OpenCode/Tools/ToolRuntimeScope.js'
import { HostForkRuntime__TryFindAgent_Z721C83C5 as tryFindAgent } from '../../../dist/Execution/Delegation/Fork/Host/Runtime.js'
import { Wanxiangshu_Execution_Delegation_Fork_Host_HostForkRuntime__HostForkRuntime_Reuse_Z591EF019 as reuseExisting } from '../../../dist/Execution/Delegation/Fork/Host/Agent.js'

const withJournal = async (fn) => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-explicit-continue-'))
  const created = await agentJournal.create({ directory })
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  try {
    await fn(created.journal)
  } finally {
    created.dispose()
    rmSync(directory, { recursive: true, force: true })
  }
}

const linkChild = async (journal, parent = 'ses_resume_parent', child = 'ses_resume_child', agent = 'resume-agent') => {
  const fact = agentFact('HandleLinked', {
    ParentSessionId: sessionId(parent),
    ChildSessionId: sessionId(child),
    Handle: handleId.agent(agent),
    TargetAgent: 'deep-coder',
    Byname: 'Ada',
    CanonicalRole: roles.of('Coder'),
    Ownership: handleOwnership.durableParentHandle(),
  })
  const result = await agentJournal.appendAgent(stream.session(sessionId(parent)), undefined, fact, journal)
  assert.equal(result.ok, true, JSON.stringify(result.error))
}

test('CRASH_018_config_registers_visible_continue_command', () => {
  const config = { command: { existing: { template: 'keep me' } } }
  registerCommand(config)
  assert.equal(config.command.existing.template, 'keep me')
  assert.match(config.command.continue.template, /explicitly requested session continuation/i)
  assert.match(config.command.continue.description, /resume.*restart/i)
})

test('CRASH_018_non_continue_command_is_a_noop', async () => {
  let adopted = false
  const output = { parts: [] }
  await continueBefore(undefined, undefined, () => { adopted = true; return okResult(undefined) }, {
    command: 'other',
    sessionID: 'ses_any',
    arguments: '',
  }, output)
  assert.equal(adopted, false)
  assert.deepEqual(output.parts, [])
})

test('CRASH_018_continue_discloses_restart_keeps_broken_tool_visible_and_process_locally_reenlists_survivor', async () => {
  await withJournal(async (journal) => {
    await linkChild(journal)
    const beforeRevision = journalRevision.value(agentJournal.revision(journal))

    const snapshot = {
      GetMessages: async (childSessionId) => {
        assert.equal(childSessionId.fields[0], 'ses_resume_child')
        return okResult([])
      },
    }

    const sentPrompts = []
    const sessions = {
      SubscribeTerminal: () => ({ Dispose() {} }),
      SendPrompt: async (childId, text) => {
        sentPrompts.push({ childId: childId.fields[0], text })
        return promptDispatcher.admittedWithPhysicalMessage('msg-after-explicit-continue')
      },
      AbortSession: async () => okResult(undefined),
      InterruptSessionOnly: async () => okResult(undefined),
      AbortChildren: async () => {},
      CreateSiblingSession: async () => { throw new Error('not expected') },
      TryGetParentSession: async () => okResult(undefined),
      CreateChildSession: async () => { throw new Error('not expected') },
      ListChildren: async () => okResult([]),
      FamilyRootOf: (id) => id,
    }

    // The scope is production ToolRuntimeScope. Explicit adoption uses no Host
    // transport; SendPrompt is only reached below when the LLM chooses a new reuse.
    const scope = createToolScope(
      sessions,
      journal,
      undefined,
      undefined,
      new Map(),
      () => undefined,
      new Set(),
      new Map(),
      undefined,
      undefined,
      undefined,
      snapshot,
      undefined,
      undefined,
      undefined,
    )

    try {
      const output = { parts: [] }
      const adopt = (parentId, record) => adoptExistingChild(scope, parentId, record)

      await continueBefore(journal, snapshot, adopt, {
        command: '/continue',
        sessionID: 'ses_resume_parent',
        arguments: 'continue the unfinished implementation',
      }, output)

      assert.equal(output.parts.length, 1)
      const briefing = output.parts[0].text
      assert.match(briefing, /explicitly invoked \/continue/i)
      assert.match(briefing, /just restarted/i)
      assert.match(briefing, /remains interrupted\/failed/i)
      assert.match(briefing, /do not hide it/i)
      assert.match(briefing, /byname=Ada/)
      assert.match(briefing, /session_id=ses_resume_child/)
      assert.match(briefing, /prior_handle_state=active-at-crash/)
      assert.match(briefing, /fork with the existing byname and a new charge/i)
      assert.match(briefing, /continue the unfinished implementation/)

      const runtimeResult = resultOf(runtimeFor(scope, { SessionId: 'ses_resume_parent' }))
      assert.equal(runtimeResult.ok, true)
      const restored = tryFindAgent(runtimeResult.value, 'resume-agent')
      assert.ok(restored, 'surviving child must be available to the normal fork reuse path')
      assert.equal(restored.ChildSessionId.fields[0], 'ses_resume_child')

      assert.equal(
        journalRevision.value(agentJournal.revision(journal)),
        beforeRevision,
        '/continue discovery/adoption must not append recovery facts or rewrite the broken tool',
      )

      const second = { parts: [] }
      await continueBefore(journal, snapshot, adopt, {
        command: 'continue',
        sessionID: 'ses_resume_parent',
        arguments: '',
      }, second)
      assert.match(second.parts[0].text, /byname=Ada/)
      assert.equal(
        journalRevision.value(agentJournal.revision(journal)),
        beforeRevision,
        'repeated /continue is durable-idempotent',
      )

      const reuseResult = resultOf(
        await reuseExisting(
          runtimeResult.value,
          'resume-agent',
          'new charge chosen by the LLM after seeing the restart briefing',
          undefined,
          undefined,
        ),
      )
      assert.equal(reuseResult.ok, true, JSON.stringify(reuseResult.error))
      assert.equal(sentPrompts.length, 1)
      assert.equal(sentPrompts[0].childId, 'ses_resume_child', 'normal reuse must target the surviving physical child')
      assert.match(sentPrompts[0].text, /new charge chosen by the LLM/)
      assert.ok(
        journalRevision.value(agentJournal.revision(journal)) > beforeRevision,
        'the first new reuse, not /continue discovery, is allowed to create new durable business facts',
      )
    } finally {
      await disposeToolScope(scope)
    }
  })
})

test('CRASH_017_new_process_runtime_dispose_does_not_claim_or_abort_old_active_handle', async () => {
  await withJournal(async (journal) => {
    await linkChild(journal, 'ses_old_parent', 'ses_old_child', 'old-agent')
    const beforeRevision = journalRevision.value(agentJournal.revision(journal))
    const aborted = []
    const sessions = {
      SubscribeTerminal: () => ({ Dispose() {} }),
      SendPrompt: async () => { throw new Error('not expected') },
      AbortSession: async (childId) => { aborted.push(childId.fields[0]); return okResult(undefined) },
      InterruptSessionOnly: async () => okResult(undefined),
      AbortChildren: async () => {},
      CreateSiblingSession: async () => { throw new Error('not expected') },
      TryGetParentSession: async () => okResult(undefined),
      CreateChildSession: async () => { throw new Error('not expected') },
      ListChildren: async () => okResult([]),
      FamilyRootOf: (id) => id,
    }
    const scope = createToolScope(
      sessions,
      journal,
      undefined,
      undefined,
      new Map(),
      () => undefined,
      new Set(),
      new Map(),
      undefined,
      undefined,
      undefined,
      undefined,
      undefined,
      undefined,
      undefined,
    )

    try {
      const created = resultOf(runtimeFor(scope, { SessionId: 'ses_old_parent' }))
      assert.equal(created.ok, true)
    } finally {
      await disposeToolScope(scope)
    }

    assert.deepEqual(aborted, [], 'durable Active from a previous process is not current-process teardown ownership')
    assert.equal(
      journalRevision.value(agentJournal.revision(journal)),
      beforeRevision,
      'creating and disposing a fresh runtime must not append abandonment for an old broken tool',
    )
  })
})

test('CRASH_018_missing_snapshot_is_visible_and_does_not_adopt_or_fail_future_use', async () => {
  await withJournal(async (journal) => {
    await linkChild(journal, 'ses_no_snapshot_parent', 'ses_no_snapshot_child', 'no-snapshot-agent')
    const beforeRevision = journalRevision.value(agentJournal.revision(journal))
    let adopted = false
    const output = { parts: [] }

    await continueBefore(journal, undefined, () => {
      adopted = true
      return okResult(undefined)
    }, {
      command: 'continue',
      sessionID: 'ses_no_snapshot_parent',
      arguments: '',
    }, output)

    assert.equal(adopted, false)
    assert.match(output.parts[0].text, /snapshot-port-unavailable/)
    assert.match(output.parts[0].text, /previous.*tool.*interrupted\/failed|tool invocation.*interrupted\/failed/i)
    assert.equal(journalRevision.value(agentJournal.revision(journal)), beforeRevision)
  })
})
