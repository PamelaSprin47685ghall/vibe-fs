import assert from 'node:assert/strict'
import test from 'node:test'

import { listItems, resultOf } from '../../../tests/unit/support/domain.mjs'
import {
  BookkeeperRuntime_bindSession as bindSession,
  BookkeeperRuntime_resetSessionPort as resetSessionPort,
} from '../../../dist/Infrastructure/BookkeeperRuntime.js'

const {
  HostToolArguments_$ctor_4E60E31B: makeArgs,
  HostToolContext,
  ToolHostCodec_factory,
} = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
const { spec, execute } = await import('../../../dist/Infrastructure/OpenCode/Tools/JsBookkeeperTool.js')
const { beginTransaction, snapshot, take, abort } = await import('../../../dist/Infrastructure/BookkeeperStaging.js')

const schemaNode = (kind, extra = {}) => ({
  kind,
  ...extra,
  describe: () => schemaNode(`${kind}-described`, extra),
  optional: () => schemaNode(`${kind}-optional`, extra),
})
const fakeSchema = {
  string: () => schemaNode('string'),
  enum: (values) => schemaNode('enum', { values }),
}
const factory = ToolHostCodec_factory({ tool: { schema: fakeSchema } })

const context = (sessionId) =>
  new HostToolContext(sessionId, undefined, undefined, undefined, undefined, () => () => {})

const run = (sessionId, program) => execute(makeArgs({ program }), context(sessionId))

const current = (txId) => {
  const staged = resultOf(snapshot(txId))
  assert.equal(staged.ok, true)
  return staged.value
}

test('js_bookkeeper_surface_is_program_only_and_has_case_sdk', () => {
  const tool = spec(factory)
  assert.equal(tool.Name, 'js-bookkeeper')
  assert.deepEqual(listItems(tool.Arguments).map((pair) => pair[0]), ['program'])
  assert.match(tool.Description, /question\(matches = \[\]\)/)
  assert.match(tool.Description, /answer\(matches = \[\]\)/)
  assert.match(tool.Description, /setQuestion\(newText\)/)
  assert.match(tool.Description, /setAnswer\(newText\)/)
  assert.match(tool.Description, /not a line number|不是行号/)
  assert.doesNotMatch(tool.Description, /Q\.md|A\.md|old_text|new_text|filesystem/i)
})

test('js_bookkeeper_program_reshapes_question_and_answer_atomically', async () => {
  const tx = 'tx-js-bookkeeper-both'
  const session = 'bk-js-bookkeeper-both'
  beginTransaction(tx, '## Goal\nkeep old goal\n## Constraints\nold constraint', '## Answer\nold answer\n## Evidence\nweak')
  bindSession(session, tx, 'owner-1')

  try {
    const result = await run(
      session,
      `class Js extends JsProgram {
        async run() {
          const question = this.question([
            ["goal", "afterGoal", "## Goal"],
            ["constraints", "afterConstraints", "## Constraints"],
          ]);
          const answer = this.answer([
            ["claim", "afterClaim", "## Answer"],
            ["evidence", "afterEvidence", "## Evidence"],
          ]);

          this.setQuestion(
            question.text("^", "constraints")
              + "## Constraints\\nnew constraint"
          );
          this.setAnswer(
            "## Answer\\nnew answer\\n"
              + answer.text("evidence", "$")
          );
          return { changed: true, source: "coherent case" };
        }
      }`,
    )

    assert.match(String(result), /changed = true/)
    assert.deepEqual(current(tx), ['## Goal\nkeep old goal\n## Constraints\nnew constraint', '## Answer\nnew answer\n## Evidence\nweak'])

    const taken = resultOf(take(tx))
    assert.equal(taken.ok, true)
    assert.deepEqual(taken.value, currentCase(taken.value[0], taken.value[1]))
  } finally {
    abort(tx)
    resetSessionPort()
  }
})

const currentCase = (question, answer) => [question, answer]

test('js_bookkeeper_zero_mutation_is_legal', async () => {
  const tx = 'tx-js-bookkeeper-idle'
  const session = 'bk-js-bookkeeper-idle'
  beginTransaction(tx, 'Q', 'A')
  bindSession(session, tx, 'owner-1')

  try {
    const result = await run(
      session,
      `class Js extends JsProgram {
        async run() {
          return { changed: false, question: this.question().text(), answer: this.answer().text() };
        }
      }`,
    )

    assert.match(String(result), /changed = false/)
    assert.deepEqual(current(tx), ['Q', 'A'])
  } finally {
    abort(tx)
    resetSessionPort()
  }
})

test('js_bookkeeper_duplicate_set_rolls_back_the_whole_program', async () => {
  const tx = 'tx-js-bookkeeper-duplicate'
  const session = 'bk-js-bookkeeper-duplicate'
  beginTransaction(tx, 'Q', 'A')
  bindSession(session, tx, 'owner-1')

  try {
    const result = await run(
      session,
      `class Js extends JsProgram {
        async run() {
          this.setQuestion("Q1");
          this.setQuestion("Q2");
          this.setAnswer("A1");
          return { changed: true };
        }
      }`,
    )

    assert.match(String(result), /setQuestion may be called at most once/i)
    assert.deepEqual(current(tx), ['Q', 'A'])
  } finally {
    abort(tx)
    resetSessionPort()
  }
})

test('js_bookkeeper_program_failure_rolls_back_staged_mutation', async () => {
  const tx = 'tx-js-bookkeeper-throw'
  const session = 'bk-js-bookkeeper-throw'
  beginTransaction(tx, 'Q', 'A')
  bindSession(session, tx, 'owner-1')

  try {
    const result = await run(
      session,
      `class Js extends JsProgram {
        async run() {
          this.setAnswer("changed");
          throw new Error("semantic stop");
        }
      }`,
    )

    assert.match(String(result), /semantic stop/)
    assert.deepEqual(current(tx), ['Q', 'A'])
  } finally {
    abort(tx)
    resetSessionPort()
  }
})

test('js_bookkeeper_unbound_session_cannot_change_a_case', async () => {
  const result = await run(
    'no-such-session',
    `class Js extends JsProgram {
      async run() {
        this.setQuestion("changed");
        return null;
      }
    }`,
  )

  assert.match(String(result), /no Bookkeeper transaction/i)
})
