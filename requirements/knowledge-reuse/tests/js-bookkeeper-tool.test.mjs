import assert from 'node:assert/strict'
import test from 'node:test'

import * as bookkeeper from '../../../dist/Repository/Knowledge/Casebook/BookkeeperSurface.js'

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
const factory = { tool: { schema: fakeSchema } }

const run = (sessionId, program) => bookkeeper.runProgram(sessionId, program)
const current = (txId) => {
  const staged = bookkeeper.snapshot(txId)
  assert.equal(staged.ok, true)
  return staged.value
}

test('WHAT[KNOWLEDGE-REUSE-006] js_bookkeeper_surface_is_program_only_and_has_case_sdk', () => {
  const tool = bookkeeper.contract(factory)
  assert.equal(tool.name, 'js-bookkeeper')
  assert.deepEqual(tool.argumentNames, ['program'])
  assert.match(tool.description, /question\(matches = \[\]\)/)
  assert.match(tool.description, /answer\(matches = \[\]\)/)
  assert.match(tool.description, /setQuestion\(newText\)/)
  assert.match(tool.description, /setAnswer\(newText\)/)
  assert.match(tool.description, /not a line number|不是行号/)
  assert.doesNotMatch(tool.description, /Q\.md|A\.md|old_text|new_text|filesystem/i)
})

test('WHAT[KNOWLEDGE-REUSE-006] js_bookkeeper_program_reshapes_question_and_answer_atomically', async () => {
  const tx = 'tx-js-bookkeeper-both'
  const session = 'bk-js-bookkeeper-both'
  bookkeeper.beginTransaction(tx, '## Goal\nkeep old goal\n## Constraints\nold constraint', '## Answer\nold answer\n## Evidence\nweak')
  bookkeeper.bindSession(session, tx, 'owner-1')

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

    const taken = bookkeeper.take(tx)
    assert.equal(taken.ok, true)
    assert.deepEqual(taken.value, currentCase(taken.value[0], taken.value[1]))
  } finally {
    bookkeeper.abort(tx)
    bookkeeper.resetRuntime()
  }
})

const currentCase = (question, answer) => [question, answer]

test('WHAT[KNOWLEDGE-REUSE-006] js_bookkeeper_zero_mutation_is_legal', async () => {
  const tx = 'tx-js-bookkeeper-idle'
  const session = 'bk-js-bookkeeper-idle'
  bookkeeper.beginTransaction(tx, 'Q', 'A')
  bookkeeper.bindSession(session, tx, 'owner-1')

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
    bookkeeper.abort(tx)
    bookkeeper.resetRuntime()
  }
})

test('WHAT[KNOWLEDGE-REUSE-006] js_bookkeeper_duplicate_set_rolls_back_the_whole_program', async () => {
  const tx = 'tx-js-bookkeeper-duplicate'
  const session = 'bk-js-bookkeeper-duplicate'
  bookkeeper.beginTransaction(tx, 'Q', 'A')
  bookkeeper.bindSession(session, tx, 'owner-1')

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

    assert.match(String(result), /setQuestion may be called at most once|setQuestion 在同一个 js-bookkeeper program 中最多只能调用一次/i)
    assert.deepEqual(current(tx), ['Q', 'A'])
  } finally {
    bookkeeper.abort(tx)
    bookkeeper.resetRuntime()
  }
})

test('WHAT[KNOWLEDGE-REUSE-006] js_bookkeeper_program_failure_rolls_back_staged_mutation', async () => {
  const tx = 'tx-js-bookkeeper-throw'
  const session = 'bk-js-bookkeeper-throw'
  bookkeeper.beginTransaction(tx, 'Q', 'A')
  bookkeeper.bindSession(session, tx, 'owner-1')

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
    bookkeeper.abort(tx)
    bookkeeper.resetRuntime()
  }
})

test('WHAT[KNOWLEDGE-REUSE-006] js_bookkeeper_unbound_session_cannot_change_a_case', async () => {
  const result = await run(
    'no-such-session',
    `class Js extends JsProgram {
      async run() {
        this.setQuestion("changed");
        return null;
      }
    }`,
  )

  assert.match(String(result), /no Bookkeeper transaction|没有 Bookkeeper transaction/i)
})
