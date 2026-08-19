// action-affordance — calling-time act contracts (PROMPT-020/021, ARCH-006/007).
//
// What this file proves:
//   1. High-risk verbs carry a local contract answering the five questions
//      (act / fit / tempting nearby act NOT performed / success consequence /
//      argument meaning), pinned by TOOL_DESCRIPTION_ANCHORS in both locales.
//   2. Tool descriptions are caller-facing boundary mirrors (PROMPT-021):
//      the confusable adjacent act is named, not implied.
//   3. Action names express semantic acts; distinct semantics get distinct
//      names (commission ≠ fork; ARCH-006/007).
//   4. A successful return establishes a bounded consequence, not more.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

import { TOOL_DESCRIPTION_ANCHORS } from '../../../scripts/checks/semantic-anchors.mjs'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const LOCALES = ['en', 'zh-CN']

/** Gate C high-risk minimum set (ARCH-016): every name must be anchored. */
const HIGH_RISK_TOOLS = Object.freeze([
  'commission',
  'establish-behavior',
  'fork',
  'inspect',
  'query-shell',
  'repair-behavior',
  'run',
])

const readTool = (tool, locale) => read(`resources/provider/tool/${tool}/description/${locale}.md`)

test('WHAT[ACTION-AFFORDANCE-002] AA_prompt_020_high_risk_verbs_have_semantic_anchor_catalog', () => {
  for (const tool of HIGH_RISK_TOOLS) {
    assert.ok(tool in TOOL_DESCRIPTION_ANCHORS, `Gate C high-risk minimum set must include ${tool}`)
  }
  assert.equal(
    Object.getOwnPropertyNames(TOOL_DESCRIPTION_ANCHORS).length,
    HIGH_RISK_TOOLS.length,
    'Gate C anchor catalog must contain exactly the high-risk minimum set',
  )
})

test('WHAT[ACTION-AFFORDANCE-001] AA_prompt_020_tool_descriptions_carry_contract_anchors_in_both_locales', () => {
  for (const tool of HIGH_RISK_TOOLS) {
    const anchors = TOOL_DESCRIPTION_ANCHORS[tool]
    assert.ok(Array.isArray(anchors) && anchors.length > 0, `${tool} must carry at least one anchor`)
    for (const locale of LOCALES) {
      const text = readTool(tool, locale)
      for (const { id, en, zh } of anchors) {
        const re = locale === 'en' ? en : zh
        assert.match(text, re, `${tool}/${locale}.md missing anchor ${id}`)
      }
    }
  }
})

test('WHAT[ACTION-AFFORDANCE-001] AA_assume_contract_answers_act_fit_boundary_return_and_argument', () => {
  for (const locale of LOCALES) {
    const description = readTool('assume', locale)
    const argument = read(`resources/provider/tool/assume/arg-assumption/${locale}.md`)

    assert.match(description, /钉成当前工作假设|Pin a judgment/i, 'act must be explicit')
    assert.match(description, /先抽象|Abstract first/i, 'fit must be explicit')
    assert.match(description, /不是求证|not verification/i, 'nearby act not performed must be explicit')
    assert.match(description, /不是.*证明|does not establish.*proven/is, 'successful return must stay bounded')
    assert.match(argument, /当前判断|current judgment/i, 'argument semantics must be explicit')
  }
})

test('WHAT[ACTION-AFFORDANCE-003] AA_prompt_020_inspect_contract_names_the_not_performed_act', () => {
  for (const locale of LOCALES) {
    const text = readTool('inspect', locale)
    assert.match(text, /read-only in the causal sense|因果意义上是只读的/i, 'causal read-only must be explicit')
    assert.match(
      text,
      /does not implement or repair code|不会实现或修复代码|不实现或修复代码/i,
      'the tempting adjacent act must be named and refused',
    )
  }
})

test('WHAT[ACTION-AFFORDANCE-012] AA_prompt_020_inspect_caller_forbidden_charge_is_named', () => {
  for (const locale of LOCALES) {
    const text = readTool('inspect', locale)
    assert.match(text, /Do not use inspect to ask for code changes|不要用 inspect 请求代码修改/i)
  }
})

test('WHAT[ACTION-AFFORDANCE-004] AA_prompt_020_repair_behavior_contract_defines_mechanical', () => {
  for (const locale of LOCALES) {
    const text = readTool('repair-behavior', locale)
    assert.match(text, /meaning is already decided|含义已经被决定|已经决定.*含义/i, 'mechanical = decided meaning')
    assert.match(
      text,
      /Do not treat the returned WorkRecord as proof that the repair passes|不要把返回的 WorkRecord 当作[^。]*通过的证明/i,
      'returned record must not be claimed as passing proof',
    )
  }
})

test('WHAT[ACTION-AFFORDANCE-005] AA_prompt_020_establish_behavior_contract_separates_mutation_from_execution', () => {
  for (const locale of LOCALES) {
    const text = readTool('establish-behavior', locale)
    assert.match(text, /Coder writes source|Coder[^。]{0,24}(?:写入|修改|写|改变) source|托付 Coder/i)
    assert.match(text, /not execution evidence|不是执行证据|不运行这些测试/i)
  }
})

test('WHAT[ACTION-AFFORDANCE-006] AA_prompt_020_run_contract_grounds_command_as_act_with_bounded_consequence', () => {
  for (const locale of LOCALES) {
    const text = readTool('run', locale)
    assert.match(text, /command is an act|命令是一种行动|command 是一次行动|命令是一次行动/i)
    assert.match(text, /economic commitments|经济承诺|不是运行时预测/i)
    const queryShell = readTool('query-shell', locale)
    assert.match(queryShell, /This is observation, not execution|这是观察，不是执行/i)
    assert.match(queryShell, /Not appropriate:|不适宜：/i)
  }
})

test('WHAT[ACTION-AFFORDANCE-013] AA_prompt_020_success_returns_establish_bounded_consequence', () => {
  const inspectEn = readTool('inspect', 'en')
  assert.match(inspectEn, /The returned WorkRecord is evidence from a witness\.\nIt is not a mutation and it is not behavioral execution evidence\./i)
  const commissionEn = readTool('commission', 'en')
  assert.match(commissionEn, /A successful return establishes that the named road has taken the charge\./)
  assert.match(commissionEn, /It does not establish that the destination has been reached\./)
})

test('WHAT[ACTION-AFFORDANCE-011] AA_prompt_021_callers_see_the_boundary_mirror_not_just_callee_role_law', () => {
  const inspect = readTool('inspect', 'en')
  assert.match(inspect, /read-only in the causal sense/i, 'caller-facing description must mirror the causal boundary')
  assert.match(inspect, /Do not use inspect to ask for code changes/i, 'caller must be told the forbidden request shape')
  const establish = readTool('establish-behavior', 'en')
  assert.match(establish, /Coder completion is not execution evidence|does not run those tests/i)
})

test('WHAT[ACTION-AFFORDANCE-007] AA_arch_006_007_distinct_semantics_have_distinct_names', () => {
  const fork = readTool('fork', 'en')
  const commission = readTool('commission', 'en')
  assert.match(fork, /another office within this mission/i)
  assert.match(commission, /independent road/i)
})

test('WHAT[ACTION-AFFORDANCE-008] AA_arch_007_same_tool_name_means_same_contract', () => {
  const commission = readTool('commission', 'en')
  assert.match(commission, /This is not fork/i, 'commission must name the confusable nearby act it does NOT perform')
  assert.match(commission, /not position in a\s*lifecycle/i, 'commission is not a lifecycle stage')
  assert.match(commission, /not size of labor/i)
})

test('WHAT[ACTION-AFFORDANCE-010] AA_prompt_020_fork_contract_answers_whom_work_is_entrusted_to', () => {
  const fork = readTool('fork', 'en')
  assert.match(fork, /Choose the office by the consequence you need/i)
  for (const office of [/Coder \/ Engineer/, /Scout \/ Investigator/, /Technician \/ Operator/, /Navigator \/ Researcher/, /Analyst \/ Inquirer/]) {
    assert.match(fork, office, 'each of the five offices must be named with its entitled consequence')
  }
})

test('WHAT[ACTION-AFFORDANCE-009] AA_prompt_020_fork_calling_names_differ_in_persona_not_authority', () => {
  const fork = readTool('fork', 'en')
  assert.match(fork, /The two calling names belonging to one office differ in persona and reasoning depth,\nnot in the office's authority\./i)
})
