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

test('WHAT[REPOSITORY-INVESTIGATION-004] INVESTIGATE_inspector_role_law_has_evidence_funnel_and_stop_rule', () => {
  for (const locale of LOCALES) {
    const law = readLaw('role/inspector', locale)
    assert.match(law, /cheapest adequate observation|最便宜的充分观察/, `${locale} cheapest adequate observation`)
    assert.match(
      law,
      /If the first cheap observation ends the investigation, stop|第一次便宜的观察已经结束调查，就停下|第一个便宜的观察.*结束调查.*停止/,
      `${locale} stop when sufficient`,
    )
    assert.match(law, /before the evidence becomes a verdict|在证据变成.*verdict.*之前停下/, `${locale} evidence is not verdict`)
  }
})
