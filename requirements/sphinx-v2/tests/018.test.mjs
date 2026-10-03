import assert from 'node:assert/strict'
import test from 'node:test'

import {Surface_isOk as isOk, Surface_isError as isError, Surface_errorValue as errorValue} from '../../../dist/Sphinx/V2/Core/Surface.js'
import {
  Tool_decodeWorkSubmit as decodeWorkSubmit,
  Tool_refusalCode as refusalCode,
  Tool_refusalPath as refusalPath,
  Tool_refusalMessage as refusalMessage,
} from '../../../dist/Sphinx/V2/Hosts/Mcp/Tool.js'

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

// WHAT[sphinx-v2-018]: a work result carries the answer and its schema, nothing else.
// 本组只验证 ingress 的具名拒绝和合法输入；不证明 Runtime 已接纳结果或写入事实。
// code/path/message 由公开 accessor 读取，不依赖编译 record 的布局。

test('WHAT[sphinx-v2-018] a work result carrying a certificate patch, a budget debit, an event write or a goal revision is refused by name', () => {
  for (const field of ['certificatePatches', 'budgetDebit', 'events', 'goalRevision', 'goalAmendment']) {
    const refusal = refusalOf(decodeWorkSubmit(answer({[field]: {}})))
    assert.equal(refusal.code, 'WORK_RESULT_EXCEEDS_ROLE', field)
    assert.equal(refusal.path, field, field)
  }
})

test('WHAT[sphinx-v2-018] a work result carrying the answer bytes, its schema and its work identity is accepted', () => {
  assert.equal(isOk(decodeWorkSubmit(answer())), true)
})

test('WHAT[sphinx-v2-018] a refused over-reach carries no business fact of its own', () => {
  // Four questions for this refusal path:
  //   what fails   - a submit naming a field its role does not own;
  //   what holds   - the call is refused, and the refusal carries no answer, no work
  //                  identity and no schema, so a worker cannot read a result back out
  //                  of a call that changed nothing;
  //   cleanup      - nothing was allocated, so nothing needs releasing;
  //   never happens- no result is accepted under a different field name, and the
  //                  over-reaching field is named rather than silently dropped.
  for (const field of ['certificatePatches', 'budgetDebit', 'events', 'goalRevision', 'goalAmendment']) {
    const refusal = refusalOf(decodeWorkSubmit(answer({[field]: {}})))
    assert.equal(refusal.code, 'WORK_RESULT_EXCEEDS_ROLE', field)
    assert.equal(refusal.path, field, field)
    assert.equal(typeof refusal.message, 'string', field)
    assert.notEqual(refusal.message.trim(), '', field)
    assert.equal(refusal.message.includes(JSON.stringify(answer())), false, field)
    assert.equal(refusal.message.includes(answer().canonicalResult), false, field)
  }
})
