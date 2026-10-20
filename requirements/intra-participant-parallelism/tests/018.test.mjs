// WHAT[intra-participant-parallelism-018] Fission 上下文继承与成员签名。
//
// 两条性质：
// (a) `this.fission(payload)` 的成员签名逐字不变：只有 payload 一个形参，
//     不带 reportMapping（同身份换执行道，不是委派）。
// (b) fission 的 lane 无条件继承本对话衣钵：lane 提示里的上下文来源是顶层
//     整理后的当前会话（AnchorLedger.context），不是旧的交付记录视图
//     （scope.ViewForSession / JsDeliveredValues）。
//
// 断言全部落在可观测行为上：编程面成员的形参数、宿主入口收到的实参、
// lane 实际收到的提示文本。没有源码文本断言。

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { caseName, execute, render } from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'
import {
  clearDelegationHost,
  installDelegationHost,
  recordDeliveredView,
} from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'
import { anchors, applyView, clearAll } from '../../../dist/Repository/Programming/Js/Anchor/AnchorSurface.js'
import {
  acceptAuthorityRoot,
  openIncumbency,
  withExecutablePlugin,
} from '../../verification-system/tests/support/plugin-fixture.mjs'

// 当前会话上下文的内容（lane 必须继承它）。
const CURRENT_CONTEXT = 'CURRENT-SESSION-CONTEXT-MARKER'
// 旧交付记录视图的内容（lane 必须不继承它）。
const DELIVERED_RECORD = 'OLD-DELIVERED-RECORD-VIEW'

const program = (body) => `class Js extends JsProgram {
  async run() {
${body}
  }
}`

const notExercised = async () => {
  throw new Error('this delegation member must not reach the delegation host')
}

const promptSessionId = (prompt) =>
  prompt?.path?.id ?? prompt?.sessionID ?? prompt?.sessionId ?? ''

const promptText = (prompt) =>
  (prompt?.body?.parts ?? prompt?.parts ?? []).map((part) => part?.text ?? '').join('\n')

/** Bounded wait for a production effect; never an unbounded hang. */
const waitFor = async (read, what, timeoutMs = 2000) => {
  const deadline = Date.now() + timeoutMs
  for (;;) {
    const value = read()
    if (value) return value
    if (Date.now() > deadline) throw new Error(`timed out waiting for ${what}`)
    await new Promise((resolve) => setTimeout(resolve, 10))
  }
}

test('WHAT[intra-participant-parallelism-018] the fission member takes the payload alone: its signature carries no report mapping', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-fission-member-'))
  const engineerSession = `ses-fission-member-${Math.random().toString(36).slice(2, 8)}`
  const managerSession = `${engineerSession}-manager`
  const calls = []
  try {
    installDelegationHost(
      engineerSession,
      notExercised,
      notExercised,
      async (...args) => {
        calls.push(args)
        return { ok: true, groupId: 'group-fission-member' }
      },
      notExercised,
      notExercised,
    )
    installDelegationHost(managerSession, notExercised, notExercised, notExercised, notExercised, notExercised)

    const engineerOutcome = await execute(
      dir,
      'Engineer',
      'en',
      engineerSession,
      {
        program: program(
          `    const arity = String(this.fission.length);
    const receipt = await this.fission({ charge: "explore three designs", lanes: 2 });
    return { arity, groupId: receipt.groupId };`,
        ),
      },
      3000,
      Date.now() + 60_000,
      1 << 20,
      null,
    )
    assert.equal(caseName(engineerOutcome), 'Succeeded')
    const engineerDoc = render(engineerOutcome)
    assert.match(engineerDoc, /arity = "1"/, 'the fission member declares one parameter: the payload')
    assert.match(engineerDoc, /groupId = "group-fission-member"/, 'the fission receipt reaches the program as data')

    // The host entry is reached with the payload alone — no report mapping crosses.
    assert.equal(calls.length, 1, 'the fission host entry is reached once')
    assert.equal(calls[0].length, 1, 'the fission host entry receives the payload alone')
    assert.equal(calls[0][0].charge, 'explore three designs')
    assert.equal(calls[0][0].lanes, 2)

    // The contrast: a delegation member that does carry a report mapping has two
    // parameters. Fission is the same identity on another lane, not a delegation.
    const managerOutcome = await execute(
      dir,
      'Manager',
      'en',
      managerSession,
      { program: program(`    return { forkArity: String(this.fork.length) };`) },
      3000,
      Date.now() + 60_000,
      1 << 20,
      null,
    )
    assert.equal(caseName(managerOutcome), 'Succeeded')
    assert.match(render(managerOutcome), /forkArity = "2"/, 'a delegation member declares the report mapping')
  } finally {
    clearDelegationHost(engineerSession)
    clearDelegationHost(managerSession)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[intra-participant-parallelism-018] a fission lane inherits the current session context instead of the old delivered record view', async () => {
  try {
    await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
      const managerSession = 'ses-fission-context-manager'
      await acceptAuthorityRoot(runtime, managerSession, 'manager')
      await openIncumbency(runtime, managerSession)

      const forked = await hooks.tool.fork.execute(
        { name: 'fission-context', charge: 'collect the lane materials' },
        { sessionID: managerSession, agent: 'manager' },
      )
      assert.match(forked, /现已接下这项托付|carries this charge now/i)
      const engineerSession = createdIds[0]

      // The owner's current conversation context (the anchor ledger of the
      // session) and the owner's last delivered record view, with different
      // content: the lane must inherit the first and not the second.
      applyView(engineerSession, [{ id: 'ctx-current', text: CURRENT_CONTEXT }])
      const currentAnchor = anchors(engineerSession).find((entry) => entry.messageId === 'ctx-current')
      assert.ok(currentAnchor, 'the current conversation context must be anchored')
      recordDeliveredView(engineerSession, {
        prefixUntil: 0,
        thenMap: { [currentAnchor.anchor]: DELIVERED_RECORD },
      })

      const fissioned = await hooks.tool.fission.execute(
        { prompts: ['lane one', 'lane two'] },
        {
          sessionID: engineerSession,
          agent: 'engineer',
          callID: 'call-fission-context',
          messageID: 'run-fission-context',
        },
      )
      assert.match(fissioned, /status\s*=\s*"fissioned"/i)

      const laneSessions = createdIds.filter((id) => id !== engineerSession)
      assert.ok(laneSessions.length >= 2, 'the fission created the lane sessions')

      const lanePrompts = await waitFor(() => {
        const byContent = runtime.prompts.filter((prompt) => {
          const text = promptText(prompt)
          return text.includes('lane one') || text.includes('lane two')
        })
        return new Set(byContent.map(promptSessionId)).size >= 2 ? byContent : null
      }, 'the two lane prompts')

      const laneTextsBySession = new Map()
      for (const prompt of lanePrompts) {
        const session = promptSessionId(prompt)
        if (!laneTextsBySession.has(session)) laneTextsBySession.set(session, promptText(prompt))
      }
      assert.equal(laneTextsBySession.size, 2, 'each lane session receives one startup prompt')
      for (const [session, text] of laneTextsBySession) {
        assert.ok(
          laneSessions.includes(session),
          'the lane startup prompt is sent to a lane session',
        )
        assert.ok(
          text.includes(CURRENT_CONTEXT),
          `the lane must inherit the current session context: ${text}`,
        )
        assert.equal(
          text.includes(DELIVERED_RECORD),
          false,
          `the lane must not inherit the old delivered record view: ${text}`,
        )
      }
      const laneTexts = [...laneTextsBySession.values()]
      assert.ok(laneTexts.some((text) => text.includes('lane one')), 'the first lane carries its own input')
      assert.ok(laneTexts.some((text) => text.includes('lane two')), 'the second lane carries its own input')
    })
  } finally {
    clearAll()
  }
})
