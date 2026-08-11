// Structural enforcer-rulebook-gate (folder SSOT) + optional constitution headings.
import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import {
  ENFORCER_REQUIRED_HEADINGS,
  EXPECTED_RULE_COUNT,
  MAIN_REQUIRED_HEADINGS,
  checkEnforcerRubric,
  checkMainRubric,
  hasHeading,
  missingHeadings,
  parseCliArgs,
  scanRulebook,
  scanRepoRulebook,
} from '../../../scripts/checks/enforcer-rulebook-gate.mjs'

const ENFORCER_BODY = `# sample-tip — Enforcer

## Definition
X is the anti-pattern.

## Trigger When
Fire when X appears.

## Do Not Trigger When
Skip near-misses.

## Nudge
Stop doing X.
`

const MAIN_BODY = `# sample-tip — Main

## What To Do Now
Fix X now.

## Repair Strategy
Undo the damage.

## Verification
Prove X is gone.

## Done When
No X remains.
`

const writeTip = (root, name, enforcerText, mainText) => {
  const tip = join(root, name)
  mkdirSync(tip, { recursive: true })
  writeFileSync(join(tip, 'enforcer.md'), enforcerText, 'utf8')
  writeFileSync(join(tip, 'main.md'), mainText, 'utf8')
}

test('enforcer_rulebook_gate_repo_is_green', () => {
  const result = scanRepoRulebook()
  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
  assert.equal(result.count, EXPECTED_RULE_COUNT)
})

test('enforcer_rulebook_gate_repo_is_green_with_requireHeadings', () => {
  // All 120 tips carry constitution headings (ConstA/B/C); check.mjs enables the flag.
  const result = scanRepoRulebook(process.cwd(), { requireHeadings: true })
  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
  assert.equal(result.count, EXPECTED_RULE_COUNT)
})

test('enforcer_rulebook_gate_rejects_third_file_and_catalog', () => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-rulebook-gate-'))
  try {
    writeTip(root, 'sample-tip', '# sample-tip — Enforcer\n\nbody\n', '# sample-tip — Main\n\nbody\n')
    writeFileSync(join(root, 'sample-tip', 'extra.txt'), 'nope\n', 'utf8')
    writeFileSync(join(root, 'catalog.json'), '{}\n', 'utf8')

    const result = scanRulebook(root, { expectedCount: 1 })
    assert.equal(result.ok, false)
    const codes = result.violations.map((v) => v.code)
    assert.ok(codes.includes('extra-entry'), codes.join(','))
    assert.ok(codes.includes('catalog-json-forbidden'), codes.join(','))
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})

test('enforcer_rulebook_hasHeading_matches_exact_atx_h2', () => {
  assert.equal(hasHeading(ENFORCER_BODY, 'Definition'), true)
  assert.equal(hasHeading(ENFORCER_BODY, 'Trigger When'), true)
  assert.equal(hasHeading(ENFORCER_BODY, 'Nudge'), true)
  assert.equal(hasHeading(MAIN_BODY, 'What To Do Now'), true)
  assert.equal(hasHeading(MAIN_BODY, 'Verification'), true)
  assert.equal(hasHeading(MAIN_BODY, 'Done When'), true)
  // body mention is not a heading
  assert.equal(hasHeading('Definition is important\n', 'Definition'), false)
  // wrong level
  assert.equal(hasHeading('# Definition\n', 'Definition'), false)
})

test('enforcer_rulebook_missingHeadings_lists_absent', () => {
  const hits = missingHeadings('# t\n\n## Definition\n', ENFORCER_REQUIRED_HEADINGS, 'x/enforcer.md')
  assert.equal(hits.length, 2)
  assert.ok(hits.every((v) => v.code === 'missing-heading'))
  assert.ok(hits.some((v) => v.detail?.includes('Trigger When')))
  assert.ok(hits.some((v) => v.detail?.includes('Nudge')))
})

test('enforcer_rulebook_requireHeadings_default_false', () => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-rulebook-headings-off-'))
  try {
    // Title only — no constitution body headings.
    writeTip(
      root,
      'sample-tip',
      '# sample-tip — Enforcer\n\nbody only\n',
      '# sample-tip — Main\n\nbody only\n',
    )
    const off = scanRulebook(root, { expectedCount: 1 })
    assert.equal(off.ok, true, JSON.stringify(off.violations, null, 2))

    const explicitFalse = scanRulebook(root, { expectedCount: 1, requireHeadings: false })
    assert.equal(explicitFalse.ok, true)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})

test('enforcer_rulebook_requireHeadings_true_enforces_constitution', () => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-rulebook-headings-on-'))
  try {
    writeTip(
      root,
      'good-tip',
      ENFORCER_BODY,
      MAIN_BODY,
    )
    writeTip(
      root,
      'bad-tip',
      '# bad-tip — Enforcer\n\n## Definition\nonly one heading\n',
      '# bad-tip — Main\n\n## What To Do Now\nonly one heading\n',
    )

    const result = scanRulebook(root, { expectedCount: 2, requireHeadings: true })
    assert.equal(result.ok, false)
    const missing = result.violations.filter((v) => v.code === 'missing-heading')
    assert.ok(missing.length >= 4, JSON.stringify(missing, null, 2))
    assert.ok(missing.some((v) => v.path?.includes('bad-tip/enforcer.md') && v.detail?.includes('Trigger When')))
    assert.ok(missing.some((v) => v.path?.includes('bad-tip/enforcer.md') && v.detail?.includes('Nudge')))
    assert.ok(missing.some((v) => v.path?.includes('bad-tip/main.md') && v.detail?.includes('Verification')))
    assert.ok(missing.some((v) => v.path?.includes('bad-tip/main.md') && v.detail?.includes('Done When')))
    // good-tip must not contribute missing-heading
    assert.equal(
      missing.filter((v) => v.path?.includes('good-tip/')).length,
      0,
      JSON.stringify(missing, null, 2),
    )
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})

test('enforcer_rulebook_requireHeadings_true_green_when_complete', () => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-rulebook-headings-green-'))
  try {
    writeTip(root, 'complete-tip', ENFORCER_BODY, MAIN_BODY)
    const result = scanRulebook(root, { expectedCount: 1, requireHeadings: true })
    assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})

test('enforcer_rulebook_parseCliArgs_require_headings_flag', () => {
  assert.deepEqual(parseCliArgs([]), { requireHeadings: false, requireRubric: false })
  assert.deepEqual(parseCliArgs(['--require-headings']), {
    requireHeadings: true,
    requireRubric: false,
  })
  assert.deepEqual(parseCliArgs(['--require-headings=true']), {
    requireHeadings: true,
    requireRubric: false,
  })
  assert.deepEqual(parseCliArgs(['--require-headings=false']), {
    requireHeadings: false,
    requireRubric: false,
  })
  assert.deepEqual(parseCliArgs(['--require-headings', '--no-require-headings']), {
    requireHeadings: false,
    requireRubric: false,
  })
})

test('enforcer_rulebook_parseCliArgs_strict_and_require_rubric', () => {
  assert.deepEqual(parseCliArgs(['--strict']), { requireHeadings: false, requireRubric: true })
  assert.deepEqual(parseCliArgs(['--require-rubric']), {
    requireHeadings: false,
    requireRubric: true,
  })
  assert.deepEqual(parseCliArgs(['--require-headings', '--strict']), {
    requireHeadings: true,
    requireRubric: true,
  })
  assert.deepEqual(parseCliArgs(['--strict', '--no-strict']), {
    requireHeadings: false,
    requireRubric: false,
  })
  assert.deepEqual(parseCliArgs(['--require-rubric=false']), {
    requireHeadings: false,
    requireRubric: false,
  })
})

test('enforcer_rulebook_documents_heading_constants', () => {
  assert.deepEqual([...ENFORCER_REQUIRED_HEADINGS], ['Definition', 'Trigger When', 'Nudge'])
  assert.deepEqual([...MAIN_REQUIRED_HEADINGS], ['What To Do Now', 'Verification', 'Done When'])
})

const ENFORCER_RUBRIC_OK = `# sample-tip — Enforcer

## Do Not Trigger When
- skip local-only change
- skip when evidence is missing
- skip when a sibling rule owns it

## Distinguish From
\`blind-edit\` vs resources/enforcer/guessed-not-verified: different causal stage.

## Examples
- positive / 正例: mutation before locating the owner
- near-miss / 近邻: read the owner then edit
- counterexample / 反例: truly local verified change
`

const MAIN_RUBRIC_OK = `# sample-tip — Main

## Decision Branches
If the value is internal-only, wrap at the module boundary.
If it crosses a public API, introduce a named type at the contract.

## Common Wrong Fixes
- rename parameters only
- add another boolean flag
- catch-and-ignore at the call site

## Verification
Prove the invariant holds: sibling concepts with the same primitive must not substitute.

## Done When
The boundary carries domain identity.
`

test('enforcer_rulebook_checkEnforcerRubric_green_on_complete', () => {
  assert.deepEqual(checkEnforcerRubric(ENFORCER_RUBRIC_OK), [])
})

test('enforcer_rulebook_checkEnforcerRubric_reports_structural_gaps', () => {
  const hits = checkEnforcerRubric('# t\n\n## Definition\nonly prose\n')
  const codes = hits.map((v) => v.code)
  assert.ok(codes.includes('rubric-do-not-trigger-count'), codes.join(','))
  assert.ok(codes.includes('rubric-distinguish-siblings'), codes.join(','))
  assert.ok(codes.includes('rubric-examples-positive'), codes.join(','))
  assert.ok(codes.includes('rubric-examples-near-miss'), codes.join(','))
  assert.ok(codes.includes('rubric-examples-counterexample'), codes.join(','))
})

test('enforcer_rulebook_checkEnforcerRubric_accepts_chinese_example_markers', () => {
  const text = `# t

## Do Not Trigger When
- a
- b
- c

## Distinguish From
See foo-bar and baz-qux.

## Examples
正例 one. 近邻 two. 反例 three.
`
  assert.deepEqual(checkEnforcerRubric(text), [])
})

test('enforcer_rulebook_checkMainRubric_green_on_complete', () => {
  assert.deepEqual(checkMainRubric(MAIN_RUBRIC_OK), [])
})

test('enforcer_rulebook_checkMainRubric_reports_structural_gaps', () => {
  const hits = checkMainRubric('# t\n\n## What To Do Now\nfix it\n\n## Verification\nrun tests\n')
  const codes = hits.map((v) => v.code)
  assert.ok(codes.includes('rubric-wrong-fixes-count'), codes.join(','))
  assert.ok(codes.includes('rubric-decision-branches-count'), codes.join(','))
  assert.ok(codes.includes('rubric-verification-invariant'), codes.join(','))
  assert.ok(codes.includes('rubric-done-when'), codes.join(','))
})

test('enforcer_rulebook_requireRubric_default_false', () => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-rulebook-rubric-off-'))
  try {
    writeTip(root, 'sample-tip', ENFORCER_BODY, MAIN_BODY)
    const off = scanRulebook(root, { expectedCount: 1, requireHeadings: true })
    assert.equal(off.ok, true, JSON.stringify(off.violations, null, 2))
    assert.equal(off.violations.some((v) => String(v.code).startsWith('rubric-')), false)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})

test('enforcer_rulebook_requireRubric_true_enforces_structural_rubric', () => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-rulebook-rubric-on-'))
  try {
    writeTip(root, 'good-tip', ENFORCER_RUBRIC_OK, MAIN_RUBRIC_OK)
    writeTip(root, 'bad-tip', ENFORCER_BODY, MAIN_BODY)

    const result = scanRulebook(root, { expectedCount: 2, requireRubric: true })
    assert.equal(result.ok, false)
    const rubric = result.violations.filter((v) => String(v.code).startsWith('rubric-'))
    assert.ok(rubric.length >= 1, JSON.stringify(result.violations, null, 2))
    assert.ok(rubric.some((v) => v.path?.includes('bad-tip/')))
    assert.equal(
      rubric.filter((v) => v.path?.includes('good-tip/')).length,
      0,
      JSON.stringify(rubric, null, 2),
    )
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})
