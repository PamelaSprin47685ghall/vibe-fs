// tests/unit/session/distiller-ownership.test.mjs — EXEC-014: the run tool's
// map/reduce Distiller children are Host-owned and parent-invisible.
//
// Regression for the "call join before end" bug: a DurableParentHandle distiller
// leaks into HandleProjection.listable, so TerminalPolicy.outstandingBackground
// (EXEC-016) blocks the caller's suicide long after `run` returned its summary.

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import { agentJournal, caseOf, handleProjection, sessionId } from '../support/domain.mjs'

const toolRuntimeScopeModule = await import('../../../dist/Infrastructure/OpenCode/Tools/ToolRuntimeScope.js')
const { ToolRuntimeScope } = toolRuntimeScopeModule
const executorRuntimeFor = Object.entries(toolRuntimeScopeModule).find(
  ([name, value]) => name.includes('ExecutorRuntimeFor') && typeof value === 'function',
)?.[1]
assert.equal(typeof executorRuntimeFor, 'function', 'ExecutorRuntimeFor export must be discoverable')

const hostForkAgentModule = await import('../../../dist/Session/HostForkAgent.js')
const fork = Object.entries(hostForkAgentModule).find(
  ([name, value]) => name.includes('_HostForkRuntime_Fork_') && typeof value === 'function',
)?.[1]
assert.equal(typeof fork, 'function', 'HostForkRuntime Fork export must be discoverable')

const { Role } = await import('../../../dist/Kernel/Roles.js')
const { HostToolContext } = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')

const PARENT = sessionId('ses_distiller')

const fakeSessions = () => ({
  CreateChildSession: async () => ({ tag: 0, fields: [sessionId('child-dist')] }),
  AbortSession: async () => ({ tag: 0, fields: [] }),
  SendPrompt: async () => ({ tag: 0, fields: [sessionId('msg-1')] }),
  SendPromptAsync: async () => ({ tag: 0, fields: [sessionId('msg-1')] }),
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  ListChildren: async () => ({ tag: 0, fields: [{ tag: 0, fields: [] }] }),
})

test('EXEC_014_distiller_fork_is_host_owned_hidden_and_parent_invisible', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-dist-'))
  const created = agentJournal.create({ directory: dir })
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  try {
    const scope = new ToolRuntimeScope(
      fakeSessions(),
      created.journal,
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
    )

    const ctx = new HostToolContext(
      'ses_distiller',
      undefined,
      undefined,
      undefined,
      undefined,
      () => () => {},
    )
    const runtime = executorRuntimeFor(scope, ctx)

    const result = await fork(runtime, 'dist-1', Role.Distiller, 'fast-distiller', 'summarize this')
    assert.equal(result.tag, 0, result.tag === 1 ? result.fields[0] : '')

    const projection = agentJournal.handleProjection(created.journal, PARENT)
    const linked = handleProjection.linkedChildren(projection)
    assert.equal(linked.length, 1)
    assert.equal(caseOf(linked[0].Ownership), 'HostOwnedHidden')
    // The regression: a distiller must not enter the parent's listable surface
    // (which drives EXEC-016 outstandingBackground / suicide guard).
    assert.equal(handleProjection.listable(projection).length, 0)
  } finally {
    try {
      created.dispose()
    } catch {}
    rmSync(dir, { recursive: true, force: true })
  }
})
