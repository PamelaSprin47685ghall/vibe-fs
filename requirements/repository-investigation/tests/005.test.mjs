// requirements/repository-investigation/tests/investigation-resource-laws.test.mjs
//
// Package-owned prose-law oracle: the repository-claim evidence contract must be
// stated in the provider-facing laws that an Inspector actually consumes.
// repository-investigation OWNS the acquisition contract (real observation,
// locatability, causal read-only, cheapest adequate observation, low-trust
// warm-start hints); this test pins those laws into the shipped resources so the
// contract cannot silently drift while the implementation stays green.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const here = dirname(fileURLToPath(import.meta.url))
const providerRoot = join(here, '../../../resources/provider')
const readLaw = (semanticPath, locale) => readFileSync(join(providerRoot, semanticPath, `${locale}.md`), 'utf8')

// Every assertion below must hold in BOTH locales: the provider-facing evidence
// contract is language-invariant (PROMPT-017 invariant face).
const LOCALES = ['en', 'zh-CN']

test('WHAT[REPOSITORY-INVESTIGATION-005] INVESTIGATE_inspect_law_pins_causal_readonly_witness_not_editor', () => {
  for (const locale of LOCALES) {
    const law = readLaw('tool/inspect/description', locale)
    assert.match(law, /read-only in the causal sense|在因果意义上是只读的/, `${locale} causal readonly`)
    assert.match(law, /does not modify files|不会修改文件/, `${locale} no file mutation`)
    assert.match(law, /does not implement or repair code|不会实现或修复代码/, `${locale} no implement/repair`)
    assert.match(
      law,
      /make the project run[\s\S]{0,80}behavioral evidence|让项目运行起来以制造新的行为证据/,
      `${locale} no behavioral execution`,
    )
    assert.match(law, /evidence from a witness|witness 提供的 evidence/, `${locale} witness`)
    assert.match(law, /not a mutation|不是 mutation/, `${locale} not a mutation`)
  }
})

test('WHAT[REPOSITORY-INVESTIGATION-005] INVESTIGATE_query_shell_law_is_observation_not_execution_and_inspector_only', () => {
  for (const locale of LOCALES) {
    const law = readLaw('tool/query-shell/description', locale)
    assert.match(law, /This is observation, not execution|这是观察，不是执行/, `${locale} observation not execution`)
    assert.match(law, /Inspector-only|仅供 Inspector 使用/, `${locale} inspector only`)
    assert.match(law, /git status/, `${locale} static query examples`)
    // The negative list must keep execution-shaped commands out of observation.
    assert.match(law, /build/, `${locale} forbids build`)
    assert.match(law, /test/, `${locale} forbids test`)
  }
})

test('WHAT[REPOSITORY-INVESTIGATION-005] INVESTIGATE_inspector_role_law_pins_observe_without_changing', () => {
  for (const locale of LOCALES) {
    const law = readLaw('role/inspector', locale)
    assert.match(law, /Observe without changing|不要为了观察而改变它/, `${locale} observe without changing`)
  }
})
