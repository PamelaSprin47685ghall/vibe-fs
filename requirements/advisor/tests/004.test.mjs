import assert from 'node:assert/strict'
import test from 'node:test'
import { existsSync } from 'node:fs'
import { join } from 'node:path'
import { parse } from 'smol-toml'
import * as events from '../../../dist/OpenCode/Host/EventsSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import { applyView } from '../../../dist/Repository/Programming/Js/Anchor/AnchorSurface.js'
import { acceptAuthorityRoot, openIncumbency, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

const textOf = prompt => prompt.body.parts.filter(part => part.type === 'text').map(part => part.text).join('\n')

test('WHAT[advisor-004] the registered member reuses its live seat and refuses an empty new answer instead of replaying previous advice', async () => {
  await withExecutablePlugin(async (hooks, directory, children, runtime) => {
    const caller = 'advisor-live-answer-owner'
    await acceptAuthorityRoot(runtime, caller, 'manager', 'advisor-owner-root')
    await openIncumbency(runtime, caller)
    runtime.pushHostMessage(caller, { id: 'caller-material', role: 'user', parts: [{ type: 'text', text: 'CALLER_MATERIAL' }] })
    applyView(caller, [{ id: 'caller-material', text: 'CALLER_MATERIAL' }])
    const authority = dispatch.projectionObservation(runtime.journal, caller)
    const tool = hooks.tool['todowrite-for-manager']
    const originalSend = runtime.client.session.promptAsync
    let pending
    let sent
    let targetCount
    runtime.client.session.promptAsync = async args => {
      const receipt = await originalSend(args)
      if (runtime.prompts.length >= targetCount) sent?.()
      return receipt
    }

    const consult = async (intent, answer, expected) => {
      const start = runtime.prompts.length
      targetCount = start + 2
      const ready = new Promise(resolve => { sent = resolve })
      pending = tool.execute({
        program: `class Js extends JsProgram { async run() { return await this.advisor(${JSON.stringify(intent)}, { prefixUntil: 100, thenMap: {} }); } }`,
      }, { sessionID: caller, agent: 'manager', messageID: 'advisor-owner-assistant', callID: `advisor-${start}` })
      await Promise.race([ready, pending.then(output => { throw new Error(`consult ended before its two prompts: ${output}`) })])
      assert.equal(children.length, 1, 'one live caller reuses its actual child')
      const child = children[0]
      if (answer) runtime.pushHostMessage(child, { id: `advice-${start}`, role: 'assistant', parts: [{ type: 'text', text: answer }] })
      assert.equal(events.notifyCompletedForRun(runtime.terminalPort, child, `advisor-run-${start}`, answer, answer, 'advisor'), true)
      const envelope = parse(await pending)
      assert.ok(envelope.data, JSON.stringify(envelope))
      const result = envelope.data
      pending = null
      assert.equal(result.ok, expected)
      assert.deepEqual(dispatch.projectionObservation(runtime.journal, caller), authority, 'consulting cannot create caller authority or claims')
      assert.equal(textOf(runtime.prompts[start + 1]), intent, 'intent is its own physical prompt')
      return { result, view: textOf(runtime.prompts[start]), child }
    }

    try {
      const first = await consult('Review the current material.', 'FIRST_ADVICE', true)
      assert.equal(first.result.advice, 'FIRST_ADVICE')
      assert.match(first.view, /CALLER_MATERIAL/)
      assert.doesNotMatch(first.view, /Review the current material\./)
      const second = await consult('There is no new answer.', '', false)
      assert.equal(second.result.code, 'advisor-unavailable')
      assert.equal(second.result.advice, undefined)
      assert.doesNotMatch(second.view, /CALLER_MATERIAL/, 'the successful prior consult advanced only this live cursor')
      const refused = await hooks.tool['todowrite-for-engineer'].execute({
        program: 'class Js extends JsProgram { async run() { await this.write("advisor-must-not-write", "bad"); } }',
      }, { sessionID: first.child, agent: 'advisor', messageID: 'advisor-assistant', callID: 'advisor-write' })
      assert.match(refused, /denied|not allowed|not available|cannot|不可|无权|不能/i)
      assert.equal(existsSync(join(directory, 'advisor-must-not-write')), false)
    } finally {
      runtime.client.session.promptAsync = originalSend
      if (pending && children.length > 0) {
        events.notifyForAuthority(runtime.terminalPort, children[0], 'Failed', `${children[0]}-root`, 'test cleanup')
        await pending.catch(() => {})
      }
    }
  })
})
