// 真实插件路径下的委派夹具（纯 Host I/O）。
//
// 这个夹具只做一件事：把宿主消息按生产的顺序摆好，让生产接线自己跑完
// 剩下的路。它不含任何业务判定，也不读写任何持久事实：
//
//   物理 user 消息 → assistant 消息 → `experimental.chat.messages.transform`
//   （provider start 准入）→ 真实 `this.deliver`（由生产落唯一一条 durable
//   交付见证）→ `message.updated` → `session.idle`（生产发布终态）
//
// 终态由生产自己发布到共享终端端口；夹具**只观察**该端口，从不发布终态、
// 从不写 durable 见证。

import assert from 'node:assert/strict'
import { join } from 'node:path'
import * as eventsSurface from '../../../../dist/OpenCode/Host/EventsSurface.js'
import * as handleJournal from '../../../../dist/Execution/Delegation/Handle/JournalSurface.js'
import * as forkTool from '../../../../dist/Execution/Delegation/Fork/OpenCode/ToolSurface.js'
import {
  acceptAuthorityRoot,
  openIncumbency,
  withExecutablePlugin,
} from '../../../verification-system/tests/support/plugin-fixture.mjs'

/** The Host's physical user message of one child turn (the last one the child received). */
export const physicalUserMessageOf = (runtime, child) => {
  const physical = runtime.messages.filter((message) => message.id?.startsWith(`msg-${child}-`)).at(-1)
  assert.ok(physical, `the child ${child} must have received a physical user message`)
  return physical
}

/**
 * The shared terminal port is where the production terminal publication
 * lands. This observer only reads it; it publishes nothing.
 */
export const observeTerminals = async (runtime) => {
  const port = await runtime.terminalPort
  const seen = []
  const subscription = eventsSurface.subscribe(port, (sessionId, outcome) => {
    seen.push({ sessionId, outcome })
  })
  return {
    seen,
    dispose: () => eventsSurface.dispose(subscription),
    /** Wait until the production published this exact run's terminal. */
    awaitRun: async (child, run, { timeoutMs = 15000, kind = 'Completed' } = {}) => {
      const deadline = Date.now() + timeoutMs
      while (Date.now() < deadline) {
        const published = seen.some(
          (entry) =>
            entry.sessionId === child &&
            entry.outcome?.kind === kind &&
            (run === undefined || entry.outcome?.providerRun === run),
        )
        if (published) return
        await new Promise((resolve) => setTimeout(resolve, 25))
      }
      assert.fail(`the child ${child} never published its ${kind} terminal for ${run}`)
    },
  }
}

/**
 * One real child turn: the assistant message, its delivery, and its terminal.
 * A turn without a material delivers nothing — the turn still completes, but it
 * owes the parent no delivery.
 */
export const childTurn = async (hooks, runtime, child, run, prose, material, awaitTerminal, role = 'engineer') => {
  const physical = physicalUserMessageOf(runtime, child)
  const assistant = {
    info: {
      id: run,
      role: 'assistant',
      sessionID: child,
      parentID: physical.id,
      agent: role,
      providerID: 'provider',
      modelID: 'engineer-model',
      time: { created: 2 },
    },
    parts: [{ type: 'text', text: prose }],
  }
  runtime.pushHostMessage(child, assistant)
  const user = {
    info: {
      id: physical.id,
      role: 'user',
      sessionID: child,
      agent: role,
      model: { providerID: 'provider', modelID: 'engineer-model' },
      time: { created: 1 },
    },
    parts: physical.parts ?? [],
  }
  await hooks['experimental.chat.messages.transform']({ sessionID: child }, { messages: [user] })
  if (material !== null && material !== undefined) {
    await hooks.tool[`todowrite-for-${role}`].execute(
      {
        program: `class Js extends JsProgram { async run() { await this.deliver({ 0: ${JSON.stringify(material)} }); } }`,
      },
      { sessionID: child, agent: role, callID: `call-${run}`, messageID: run },
    )
  }
  assistant.info.time.completed = 3
  assistant.info.finish = 'stop'
  await hooks.event({ event: { type: 'message.updated', properties: { info: assistant.info } } })
  await hooks.event({ event: { type: 'session.idle', properties: { sessionID: child } } })
  if (awaitTerminal) await awaitTerminal(child, run)
}

/**
 * A later delivery into an ALREADY admitted work unit, under the SAME
 * physical input (hence the same authority root).
 *
 * The physical input has already spent its single provider step, so this
 * delivery does not re-enter the provider: it delivers into the open work
 * unit directly, and the turn's own terminal publication (message.updated →
 * session.idle) reports the proven completion of that same work unit.
 *
 * A new physical input (a resume) would admit a DIFFERENT work unit under a
 * different root — that proves a new obligation, not this one.
 */
export const deliverIntoOpenWork = async (hooks, runtime, child, run, prose, material, role = 'engineer') => {
  const physical = physicalUserMessageOf(runtime, child)
  const assistant = {
    info: {
      id: run,
      role: 'assistant',
      sessionID: child,
      parentID: physical.id,
      agent: role,
      providerID: 'provider',
      modelID: 'engineer-model',
      time: { created: 4 },
    },
    parts: [{ type: 'text', text: prose }],
  }
  runtime.pushHostMessage(child, assistant)
  await hooks.tool[`todowrite-for-${role}`].execute(
    {
      program: `class Js extends JsProgram { async run() { await this.deliver({ 0: ${JSON.stringify(material)} }); } }`,
    },
    { sessionID: child, agent: role, callID: `call-${run}`, messageID: run },
  )
  assistant.info.time.completed = 5
  assistant.info.finish = 'stop'
  await hooks.event({ event: { type: 'message.updated', properties: { info: assistant.info } } })
  await hooks.event({ event: { type: 'session.idle', properties: { sessionID: child } } })
}

/** `this.fork` through the real plugin surface. */
export const forkCall = (hooks, owner, name, charge, callID = 'call-fork') =>
  hooks.tool['todowrite-for-manager'].execute(
    {
      program: `class Js extends JsProgram { async run() { return await this.fork({ name: ${JSON.stringify(name)}, charge: ${JSON.stringify(charge)} }, { 1: null }); } }`,
    },
    { sessionID: owner, agent: 'manager', callID, messageID: `msg-${callID}` },
  )

/** `this.resume` through the real plugin surface. */
export const resumeCall = (hooks, owner, name, charge, callID = 'call-resume', attach) =>
  hooks.tool['todowrite-for-manager'].execute(
    {
      program: `class Js extends JsProgram { async run() { return await this.resume({ name: ${JSON.stringify(name)}, charge: ${JSON.stringify(charge)}${attach === undefined ? '' : `, attach: ${JSON.stringify(attach)}`} }, { 1: null }); } }`,
    },
    { sessionID: owner, agent: 'manager', callID, messageID: `msg-${callID}` },
  )

/** `this.join` through the real plugin surface. */
export const joinCall = (hooks, owner, callID = 'call-join') =>
  hooks.tool['todowrite-for-manager'].execute(
    { program: 'class Js extends JsProgram { async run() { return await this.join(); } }' },
    { sessionID: owner, agent: 'manager', callID, messageID: `msg-${callID}` },
  )

/** The delegated agent id of one fork / resume receipt. */
export const agentIdOf = (receipt) => {
  const agentId = /agentId = "([^"]+)"/.exec(receipt)?.[1]
  assert.ok(agentId, `the receipt must name the delegated agent: ${receipt}`)
  return agentId
}

/**
 * The work unit's lifecycle, read through the registered public surface
 * (`Handle/JournalSurface`). It is the same durable projection the fork
 * runtime reads — no second copy of the state.
 */
export const workLifecycleOf = (runtime, owner, agentId, child, root) =>
  handleJournal.snapshot(runtime.journal, owner, agentId, child, root).record?.lifecycle

/**
 * One real Manager session with its root admitted and its lifecycle opened.
 * The body receives the plugin runtime and the created child ids.
 */
export const withRealFork = async (owner, body) =>
  withExecutablePlugin(async (hooks, directory, createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, owner, 'manager')
    await openIncumbency(runtime, owner)
    return body({ hooks, directory, createdIds, runtime })
  })

/** The join records block — the byName index below it is a lookup mirror of the same records. */
export const joinRecordsOf = (result) => {
  const start = result.indexOf('[[data.records]]')
  assert.notEqual(start, -1, 'the join result must carry its records block')
  const end = result.indexOf('[data.byName.', start)
  return end === -1 ? result.slice(start) : result.slice(start, end)
}

/**
 * The work units of one owner, read back from a **cold** journal: a second
 * writer re-opens the durable event log of the same workspace and folds it
 * from scratch. It is the same cold-recovery surface the fork runtime uses —
 * the fixture adds no state of its own.
 */
export const coldWorkSnapshot = async (directory, owner) =>
  forkTool.coldWorkSnapshot(join(directory, '.Herdchestra'), owner)
