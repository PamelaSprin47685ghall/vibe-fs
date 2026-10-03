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

test('WHAT[sphinx-v2-034] the adapter declares dispatch and cancel but not unobservable reads', () => {
  // 查询真实 Adapter 的能力声明，不读取内部对象图。
  // 正向对照使空集不能假绿；负向对照禁止宣称当前 session port 没有的读取。
  // 这不证明 dispatch/abort 已在真实 Host 完成，也不取得任何物理 receipt。
  const names = capabilities(null)
  assert.equal(Array.isArray(names), true, 'capabilities cross as a native array')
  const claimed = new Set(names)
  for (const supported of ['dispatch', 'request-cancel']) {
    assert.equal(claimed.has(supported), true, supported)
  }

  // This session port exposes no message or tool-result read, no status query and no
  // dispatch lookup. Advertising any of them would promise receipts it cannot obtain.
  for (const unobtainable of ['read-result', 'read-status', 'reconcile', 'message-read', 'tool-result-read']) {
    assert.equal(claimed.has(unobtainable), false, unobtainable)
  }

  // Every advertised name must be a member of the adapter's own dispatch surface, so a
  // later capability has to be added there rather than only to the list.
  const dispatchSurface = new Set(['dispatch', 'request-cancel'])
  for (const name of claimed) {
    assert.equal(dispatchSurface.has(name), true, name)
  }
})

test.todo('WHAT[sphinx-v2-034] actual Host receipts bind work identity and cancel awaits real child resource termination while late results cannot enter semantic state')
