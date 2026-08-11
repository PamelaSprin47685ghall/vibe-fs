import assert from 'node:assert/strict'
import test from 'node:test'

import { listItems, resultOf } from '../support/domain.mjs'

const {
  HostToolArguments_$ctor_4E60E31B: makeArgs,
  HostToolContext,
  ToolHostCodec_factory,
} = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
const { spec, execute } = await import('../../../dist/Infrastructure/OpenCode/Tools/EditQaTool.js')
const {
  beginTransaction,
  read,
  take,
  abort,
} = await import('../../../dist/Infrastructure/BookkeeperStaging.js')
const {
  bindSession,
  resetSessionPort,
} = await import('../../../dist/Infrastructure/BookkeeperRuntime.js')

const factory = ToolHostCodec_factory({
  tool: {
    schema: {
      string: () => ({ kind: 'string-schema' }),
      enum: (values) => ({ kind: 'enum-schema', values }),
    },
  },
})

const context = (sessionId) =>
  new HostToolContext(sessionId, undefined, undefined, undefined, undefined, () => () => {})

const parseError = (text) => {
  const match = String(text).match(/error = "(.*)"/)
  return match ? JSON.parse(`"${match[1]}"`) : String(text)
}

test('edit_qa_unique_replace_q_and_a', async () => {
  const tx = 'tx-edit-ok'
  const session = 'bk-edit-ok'
  beginTransaction(tx, 'keep the question', 'keep the answer')
  bindSession(session, tx, 'owner-1')
  try {
    const tool = spec(factory)
    assert.equal(tool.Name, 'edit-qa')
    assert.deepEqual(
      listItems(tool.Arguments).map((pair) => pair[0]),
      ['document', 'old_text', 'new_text'],
    )

    const qResult = await execute(
      makeArgs({ document: 'Q.md', old_text: 'the question', new_text: 'a canonical question' }),
      context(session),
    )
    assert.equal(String(qResult).includes('replaced'), true)
    assert.equal(resultOf(read(tx, 'Q.md')).value, 'keep a canonical question')

    const aResult = await execute(
      makeArgs({ document: 'A.md', old_text: 'keep the answer', new_text: 'summary of several answers' }),
      context(session),
    )
    assert.equal(String(aResult).includes('replaced'), true)
    assert.equal(resultOf(read(tx, 'A.md')).value, 'summary of several answers')

    const taken = resultOf(take(tx))
    assert.equal(taken.ok, true)
    assert.equal(taken.value[0], 'keep a canonical question')
    assert.equal(taken.value[1], 'summary of several answers')
  } finally {
    abort(tx)
    resetSessionPort()
  }
})

test('edit_qa_missing_old_text_fails', async () => {
  const tx = 'tx-edit-missing'
  const session = 'bk-edit-missing'
  beginTransaction(tx, 'only once', 'body')
  bindSession(session, tx, 'owner-1')
  try {
    const result = await execute(
      makeArgs({ document: 'Q.md', old_text: 'absent', new_text: 'x' }),
      context(session),
    )
    assert.equal(String(result).includes('error'), true)
    assert.equal(parseError(result).includes('not found'), true)
    assert.equal(resultOf(read(tx, 'Q.md')).value, 'only once')
  } finally {
    abort(tx)
    resetSessionPort()
  }
})

test('edit_qa_ambiguous_old_text_fails', async () => {
  const tx = 'tx-edit-amb'
  const session = 'bk-edit-amb'
  beginTransaction(tx, 'repeat repeat', 'body')
  bindSession(session, tx, 'owner-1')
  try {
    const result = await execute(
      makeArgs({ document: 'Q.md', old_text: 'repeat', new_text: 'once' }),
      context(session),
    )
    assert.equal(String(result).includes('error'), true)
    assert.equal(parseError(result).includes('ambiguous'), true)
    assert.equal(resultOf(read(tx, 'Q.md')).value, 'repeat repeat')
  } finally {
    abort(tx)
    resetSessionPort()
  }
})

test('edit_qa_rejects_unknown_document_and_unbound_session', async () => {
  const tx = 'tx-edit-doc'
  beginTransaction(tx, 'Q', 'A')
  try {
    const badDoc = await execute(
      makeArgs({ document: 'notes.md', old_text: 'Q', new_text: 'Z' }),
      context('no-such-session'),
    )
    assert.equal(String(badDoc).includes('error'), true)

    bindSession('bk-edit-doc', tx, 'owner-1')
    const badName = await execute(
      makeArgs({ document: 'notes.md', old_text: 'Q', new_text: 'Z' }),
      context('bk-edit-doc'),
    )
    assert.equal(String(badName).includes('Q.md'), true)
  } finally {
    abort(tx)
    resetSessionPort()
  }
})
