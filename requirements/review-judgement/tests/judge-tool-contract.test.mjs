// REVIEW-JUDGEMENT-001 (REVIEW-001): the public judgement tool contract.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as provider from '../../../dist/Participant/Provider/LanguageSurface.js'
import * as judge from '../../../dist/Mission/Review/OpenCode/JudgeSurface.js'

const parse = (value) => judge.parse(value)

test('WHAT[REVIEW-JUDGEMENT-001] REVIEW_001_verdict_schema_allows_only_the_verdict_argument', () => {
  const schema = JSON.parse(judge.schemaJson)
  assert.deepEqual(Object.keys(schema.properties).sort(), ['verdict'])
  assert.deepEqual(schema.properties.verdict.enum, ['PERFECT', 'REVISE'])
  assert.equal(schema.properties.verdict.type, 'string')
  assert.deepEqual(schema.required, ['verdict'])
  assert.equal(schema.additionalProperties, false)
})

test('WHAT[REVIEW-JUDGEMENT-001] REVIEW_001_verdict_parse_is_exact_perfect_or_revise', () => {
  assert.deepEqual(parse('PERFECT'), { ok: true, value: 'Perfect' })
  assert.deepEqual(parse('REVISE'), { ok: true, value: 'Revise' })
  for (const garbage of ['APPROVE', 'revise', 'REVISE ', 'perfect', 'PASS', '', 'judgement']) {
    assert.equal(parse(garbage).ok, false, `'${garbage}' must be refused`)
    assert.equal(parse(garbage).error, 'verdict must be exactly PERFECT or REVISE')
  }
})

test('WHAT[REVIEW-JUDGEMENT-001] REVIEW_001_tool_spec_exposes_judge_with_a_single_verdict_argument', () => {
  const contract = judge.contract('English')
  assert.equal(contract.name, 'judge')
  assert.equal(contract.arguments.length, 1)
  assert.equal(contract.arguments[0].name, 'verdict')
  assert.deepEqual(contract.arguments[0].values, ['PERFECT', 'REVISE'])
})

test('WHAT[REVIEW-JUDGEMENT-001] REVIEW_001_reviewer_provider_instructions_name_judge_never_the_removed_verdict_tool', () => {
  const paths = ['runtime/reviewer-verdict-required', 'lifecycle/magic-todo/process-reviewer-preamble', 'lifecycle/host-review/opening']
  for (const language of ['English', 'SimplifiedChinese']) {
    for (const path of paths) {
      const text = provider.readText(language, path)
      assert.match(text, /judge/)
      assert.doesNotMatch(text, /verdict tool|verdict 工具/i)
    }
  }
})

test('WHAT[REVIEW-JUDGEMENT-001] REVIEW_001_receipt_does_not_echo_the_verdict', () => {
  const received = judge.receipt('English')
  const description = provider.readText('English', 'tool/judge/description')
  assert.equal(received, 'Your judgment has been received, please conclude the conversation.')
  assert.equal(/PERFECT|REVISE/.test(received), false)
  assert.match(description, /does not echo the verdict/i)
  assert.match(description, /does not mutate source/i)
})

test('WHAT[REVIEW-JUDGEMENT-001] REVIEW_001_already_judged_receipt_prompts_to_conclude', () => {
  const zh = judge.alreadyJudged('SimplifiedChinese')
  const en = judge.alreadyJudged('English')
  assert.equal(zh, '你已经做出过判断了，现在请你结束对话。')
  assert.equal(en, 'You have already made a judgment, please conclude the conversation.')
  assert.equal(/PERFECT|REVISE/.test(zh), false)
  assert.equal(/PERFECT|REVISE/.test(en), false)
})
