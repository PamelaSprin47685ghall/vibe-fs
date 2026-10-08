import assert from 'node:assert/strict'
import test from 'node:test'

import {isError, errorValue} from '../../../dist/Sphinx/V2/Core/Surface.js'
import {
  Tool_decodeWorkSubmit as decodeWorkSubmit,
  Tool_refusalCode as refusalCode,
  Tool_refusalPath as refusalPath,
  Tool_refusalMessage as refusalMessage,
} from '../../../dist/Sphinx/V2/Hosts/Mcp/Tool.js'
import {capabilities} from '../../../dist/Sphinx/V2/Hosts/OpenCode/Surface.js'
import {Host, admittedWithReceipt, withHost, hostRequest} from './host-support.mjs'
import {retryable, fatal, acceptanceUnknown} from '../../../dist/Interaction/Dispatch/DispatchSurface.js'

const refusalOf = result => {
  assert.equal(isError(result), true, 'expected a refused decode')
  const refusal = errorValue(result)
  return {code: refusalCode(refusal), path: refusalPath(refusal), message: refusalMessage(refusal)}
}

const answer = extra => ({
  commandId: 'cmd-1',
  inquiryId: 'inquiry-1',
  workId: 'work-1',
  attempt: 1,
  fence: 'fence-1',
  canonicalResult: '{"answer":"42"}',
  resultSchema: {id: 'sphinx.answer@1', hash: 'a1b2'},
  clusterId: 'cluster-1',
  ...extra,
})

import {recoveryAction, recoveryMaySpend} from '../../../dist/Sphinx/V2/Runtime/Surface.js'

test('WHAT[sphinx-v2-034] recovery classifies an unconfirmed cancellation as awaiting terminal', () => {
  assert.equal(recoveryAction('CancelPending'), 'await-terminal')
  assert.equal(recoveryMaySpend('CancelPending'), false)
})

test('WHAT[sphinx-v2-034] the work identity is checked rather than carried over from a receipt', () => {
  // 本组验证结果入参的身份形状，不证明与真实 work ticket 或 Host receipt 已匹配。
  // Four questions for the receipt binding:
  //   what fails   - a receipt missing the attempt, fence or schema hash that identifies
  //                  which physical attempt produced it;
  //   what holds   - the decode is refused at the exact field, so a receipt can never be
  //                  read as belonging to attempt 1 or to an unnamed attempt;
  //   cleanup      - nothing was admitted, so no work identity needs releasing;
  //   never happens- no missing identity is defaulted to zero, rounded, or read from a
  //                  sibling field.
  const {attempt, ...withoutAttempt} = answer()
  assert.equal(refusalOf(decodeWorkSubmit(withoutAttempt)).path, 'attempt')

  // 非正、非整数、非数字或超出安全整数的 attempt 不得归一成合法身份。
  for (const attempt of [0, 1.5, -1, '1', true, null, NaN, Infinity, Number.MAX_SAFE_INTEGER + 1]) {
    const refusal = refusalOf(decodeWorkSubmit(answer({attempt})))
    assert.equal(refusal.code, 'INVALID_SCHEMA', String(attempt))
    assert.equal(refusal.path, 'attempt', String(attempt))
  }

  // A blank fence is refused rather than accepted as an unnamed attempt.
  assert.equal(refusalOf(decodeWorkSubmit(answer({fence: '  '}))).path, 'fence')

  // 这里只验证 schema 引用形状，不证明 hash 已核对真实 schema 内容。
  assert.equal(refusalOf(decodeWorkSubmit(answer({resultSchema: {id: 'sphinx.answer@1'}}))).path, 'hash')
  assert.equal(refusalOf(decodeWorkSubmit(answer({resultSchema: {hash: 'a1b2'}}))).path, 'id')
  for (const resultSchema of [undefined, null, [], 'sphinx.answer@1', 1]) {
    const refusal = refusalOf(decodeWorkSubmit(answer({resultSchema})))
    assert.equal(refusal.code, 'INVALID_SCHEMA')
    assert.equal(refusal.path, 'resultSchema')
  }

  // The answer bytes are never emptied on the way in.
  assert.equal(refusalOf(decodeWorkSubmit(answer({canonicalResult: '  '}))).path, 'canonicalResult')
})

test('WHAT[sphinx-v2-034] the adapter declares only its owned dispatch boundary', () => {
  // 查询真实 Adapter 的能力声明，不读取内部对象图。
  // 正向对照使空集不能假绿；负向对照禁止宣称当前 Adapter 尚未组合的读取。
  // 这不证明 dispatch/abort 已在真实 Host 完成，也不取得任何物理 receipt。
  const names = capabilities()
  assert.equal(Array.isArray(names), true, 'capabilities cross as a native array')
  const claimed = new Set(names)
  for (const supported of ['dispatch']) {
    assert.equal(claimed.has(supported), true, supported)
  }

  // This adapter has not composed the Host's message read, status or dispatch lookup.
  // Advertising them would promise receipts it cannot currently obtain.
  for (const unobtainable of ['request-cancel', 'read-result', 'read-status', 'reconcile', 'message-read', 'tool-result-read']) {
    assert.equal(claimed.has(unobtainable), false, unobtainable)
  }

  // Every advertised name must be a member of the adapter's own dispatch surface, so a
  // later capability has to be added there rather than only to the list.
  const dispatchSurface = new Set(['dispatch'])
  for (const name of claimed) {
    assert.equal(dispatchSurface.has(name), true, name)
  }
})

test.todo('WHAT[sphinx-v2-034] actual Host receipts bind work identity and cancel awaits real child resource termination while late results cannot enter semantic state')

test('WHAT[sphinx-v2-034] actual Adapter uses the captured nested Host owner independently of InquiryId', async () => {
  await withHost(async (probe, owner) => {
    const execution = Host.startDispatch(probe, 'inquiry-is-not-owner', hostRequest())
    await Host.awaitHostPrompts(probe, 1)
    const observed = Host.hostRecording(probe)
    const prompt = observed.prompts[0]
    assert.equal(Host.returnHostOutcome(probe, prompt.sessionId, prompt.index, admittedWithReceipt('actual-transport-receipt')), true)
    const physical = await Host.confirmHostPhysical(probe, prompt.index, 'actual-user-message')
    assert.equal(physical.ok, true)
    await Host.dispatchAdmission(execution)
    assert.deepEqual(observed.createSibling, [], 'the real owner uses the managed delegation child path')
    assert.equal(observed.createChild[0].parentSessionId, owner, 'logical parent remains the nested original owner')
    assert.deepEqual(observed.listedFamilies, ['host-family-root'], 'family observation uses the actual family root')
    assert.equal(prompt.agent, 'engineer')
    assert.equal(prompt.model, null)
    assert.equal(typeof prompt.metadata.wanxiangshu_prompt_key, 'string')
    assert.ok(prompt.listenerCount > 0, 'the original owner installs terminal observation before send')
  })
})

test('WHAT[sphinx-v2-034] a captured owner without active durable authority cannot create or send a child', async () => {
  await withHost(async probe => {
    const execution = Host.startDispatch(probe, 'inquiry-is-not-owner', hostRequest())
    await new Promise(resolve => setImmediate(resolve))
    for (const prompt of Host.hostRecording(probe).prompts) {
      Host.returnHostOutcome(probe, prompt.sessionId, prompt.index, admittedWithReceipt('actual-transport-receipt'))
    }
    assert.equal((await Host.dispatchAdmission(execution)).kind, 'NotDispatched')
    const observed = Host.hostRecording(probe)
    assert.deepEqual(observed.prompts, [])
    assert.deepEqual(observed.createSibling, [])
    assert.deepEqual(observed.createChild, [])
    assert.deepEqual(observed.listedFamilies, [])
  }, 'unadmitted-owner')
})

const remainsPending = async promise => Promise.race([
  promise.then(value => ({kind: 'resolved', value})),
  new Promise(resolve => setImmediate(() => resolve({kind: 'pending'}))),
])

test('WHAT[sphinx-v2-034] a transport receipt remains pending until actual managed physical ingress and its exact terminal', async () => {
  await withHost(async probe => {
    const execution = Host.startDispatch(probe, 'receipt-is-not-physical', hostRequest())
    const admission = Host.dispatchAdmission(execution)
    const completion = Host.dispatchCompletion(execution)
    await Host.awaitHostPrompts(probe, 1)
    const prompt = Host.hostRecording(probe).prompts[0]
    const receipt = 'transport-only-73d4'
    assert.equal(Host.returnHostOutcome(probe, prompt.sessionId, prompt.index, admittedWithReceipt(receipt)), true)
    assert.deepEqual(await remainsPending(admission), {kind: 'pending'})
    assert.deepEqual(await remainsPending(completion), {kind: 'pending'})
    const physical = await Host.confirmHostPhysical(probe, prompt.index, 'physical-user-81a3')
    assert.equal(physical.ok, true)
    const accepted = await admission
    assert.deepEqual(accepted, {
      kind: 'Accepted', sessionId: prompt.sessionId,
      promptKey: prompt.metadata.wanxiangshu_prompt_key,
      hostOutcome: {kind: 'AdmittedWithReceipt', value: receipt},
      physicalUserMessageId: physical.physicalUserMessageId,
      authorityRootUserMessageId: physical.authorityRootUserMessageId,
    })
    assert.notEqual(accepted.sessionId, receipt)
    assert.notEqual(accepted.physicalUserMessageId, receipt)
    assert.notEqual(accepted.promptKey, hostRequest().payload.dispatchIntentId)
    assert.equal(await Host.settleHostTerminal(probe, prompt.sessionId, 'foreign-physical', physical.authorityRootUserMessageId, 'foreign-run', 'foreign answer'), false)
    assert.deepEqual(await remainsPending(completion), {kind: 'pending'})
    assert.equal(await Host.settleHostTerminal(probe, prompt.sessionId, physical.physicalUserMessageId, physical.authorityRootUserMessageId, 'actual-provider-run', 'actual formal answer'), true)
    assert.deepEqual(await completion, {
      ok: true, value: {
        sessionId: prompt.sessionId,
        physicalUserMessageId: physical.physicalUserMessageId,
        authorityRootUserMessageId: physical.authorityRootUserMessageId,
        providerRun: 'actual-provider-run', formalText: 'actual formal answer',
      },
    })
  })
})

test('WHAT[sphinx-v2-034] nonaccepted Host outcomes retain their own key and never fabricate physical acceptance', async t => {
  for (const [name, outcome, kind] of [
    ['retryable', retryable('host retryable'), 'Refused'],
    ['fatal', fatal('host fatal'), 'Refused'],
    ['unknown', acceptanceUnknown('host unknown'), 'Unconfirmed'],
  ]) {
    await t.test('WHAT[sphinx-v2-034] ' + name + ' retains the original Host evidence', async () => {
      await withHost(async probe => {
        const execution = Host.startDispatch(probe, 'nonaccepted-inquiry', hostRequest())
        await Host.awaitHostPrompts(probe, 1)
        const prompt = Host.hostRecording(probe).prompts[0]
        assert.equal(Host.returnHostOutcome(probe, prompt.sessionId, prompt.index, outcome), true)
        const observed = await Host.dispatchAdmission(execution)
        assert.equal(observed.kind, kind)
        assert.equal(observed.sessionId, prompt.sessionId)
        assert.equal(observed.promptKey, prompt.metadata.wanxiangshu_prompt_key)
        assert.equal(Object.hasOwn(observed, 'physicalUserMessageId'), false)
        assert.equal(Object.hasOwn(observed, 'authorityRootUserMessageId'), false)
        assert.equal(Host.hostRecording(probe).prompts.length, 1)
      })
    })
  }
})
