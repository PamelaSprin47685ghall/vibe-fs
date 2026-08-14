// requirements/review-judgement/tests/judge-tool-contract.test.mjs
//
// REVIEW-JUDGEMENT-001 (REVIEW-001): the judgement surface is `judge(verdict)`
// with exactly two typed values, no description field, and a receipt that does
// not echo the verdict. The tests fail if a third value starts parsing, a
// description field sneaks into the schema, or the receipt starts echoing.

import assert from 'node:assert/strict'
import test from 'node:test'
import { caseOf, listItems, payloadOf, providerLanguage, providerResources, resultOf } from '../../../tests/unit/support/domain.mjs'

const { StaticTools_reviewerVerdictOfString, StaticTools_reviewerVerdictSchemaJson } = await import(
  '../../../dist/Tools/StaticTools.js'
)
const { spec } = await import('../../../dist/Infrastructure/OpenCode/Tools/JudgeTool.js')
const { ToolHostCodec_factory } = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
const { ToolRuntimeScope } = await import('../../../dist/Infrastructure/OpenCode/Tools/ToolRuntimeScope.js')

const parse = (value) => resultOf(StaticTools_reviewerVerdictOfString(value))

test('REVIEW_001_verdict_schema_allows_only_the_verdict_argument', () => {
  // `additionalProperties: false` + exactly one property is the schema-level
  // proof that judge has no description field (and no future companion field).
  const schema = JSON.parse(StaticTools_reviewerVerdictSchemaJson)

  assert.deepEqual(Object.keys(schema.properties).sort(), ['verdict'])
  assert.deepEqual(schema.properties.verdict.enum, ['PERFECT', 'REVISE'])
  assert.equal(schema.properties.verdict.type, 'string')
  assert.deepEqual(schema.required, ['verdict'])
  assert.equal(schema.additionalProperties, false, 'additionalProperties must be false: no description field')
})

test('REVIEW_001_verdict_parse_is_exact_perfect_or_revise', () => {
  // The parser is deliberately independent of assistant text: a verdict is a
  // tool argument, never something inferred from a transcript.
  assert.equal(parse('PERFECT').ok, true)
  assert.equal(caseOf(parse('PERFECT').value), 'Perfect')
  assert.equal(parse('REVISE').ok, true)
  assert.equal(caseOf(parse('REVISE').value), 'Revise')

  // Anything else is refused — no aliases, no case folding, no future values.
  for (const garbage of ['APPROVE', 'revise', 'REVISE ', 'perfect', 'PASS', '', 'judgement']) {
    assert.equal(parse(garbage).ok, false, `'${garbage}' must be refused`)
    assert.equal(parse(garbage).error, 'verdict must be exactly PERFECT or REVISE')
  }
})

test('REVIEW_001_tool_spec_exposes_judge_with_a_single_verdict_argument', () => {
  const fakeSchema = { enum: (values) => ({ values }) }
  const factory = ToolHostCodec_factory({ tool: { schema: fakeSchema } })
  const scope = new ToolRuntimeScope(
    undefined,
    undefined,
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

  const tool = spec(factory, scope)

  assert.equal(tool.Name, 'judge')
  const args = listItems(tool.Arguments)
  assert.equal(args.length, 1, 'judge must take exactly one argument')
  assert.equal(args[0][0], 'verdict')
  assert.deepEqual(payloadOf(args[0][1]).values, ['PERFECT', 'REVISE'])
})

test('REVIEW_001_receipt_does_not_echo_the_verdict', () => {
  // The success receipt is a fixed sentence. It carries no verdict value, so a
  // reviewer cannot learn "what the system recorded" from the tool result — the
  // judgement is the model's own creation, not an echoed state.
  const received = providerResources.readText(providerLanguage.english, 'tool/judge/received')
  const description = providerResources.readText(providerLanguage.english, 'tool/judge/description')

  assert.equal(received, 'Your judgment has been received.')
  assert.equal(/PERFECT|REVISE/.test(received), false, 'receipt must not echo the verdict literal')
  assert.match(description, /does not echo the verdict/i)
  assert.match(description, /does not mutate source/i)
})
