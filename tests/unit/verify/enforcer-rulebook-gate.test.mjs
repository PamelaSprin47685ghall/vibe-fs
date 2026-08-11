// Structural enforcer-rulebook-gate (folder SSOT) + optional constitution headings.
import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, mkdirSync, writeFileSync, rmSync, readFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  ENFORCER_REQUIRED_HEADINGS,
  EXPECTED_RULE_COUNT,
  HUMAN_ONLY_RUBRIC_ITEMS,
  MAIN_REQUIRED_HEADINGS,
  NEW_RUBRIC_CODES,
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

## Definition
A sample-tip is editing a representation before the root-cause owner is known.

## Trigger When
Fire when implementation changes begin before locating the owner and tracing the causal path.

## Do Not Trigger When
- skip local-only change
- skip when evidence is missing
- skip when a sibling rule owns it

## Distinguish From
\`blind-edit\` vs resources/enforcer/guessed-not-verified: different causal stage.
Tie-break: if mutation starts without an ownership map, this rule owns the case.

## Examples
- positive / 正例: mutation before locating the owner
- near-miss / 近邻: read the owner then edit
- counterexample / 反例: truly local verified change
`

const MAIN_RUBRIC_OK = `# sample-tip — Main

## What To Do Now
Repair only at the root-cause owner; who owns the violated invariant is the legal edit site.

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

## Definition
精确说明该反模式的根因：在未定位 owner 前就开始改代码。

## Trigger When
当实现改动在定位 owner 之前就开始时触发。

## Do Not Trigger When
- a
- b
- c

## Distinguish From
See foo-bar and baz-qux. 若相似, foo-bar 更早。

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

const replaceSection = (src, heading, body) =>
  src.replace(new RegExp(`## ${heading}\\n[\\s\\S]*?(?=\\n## |$)`), `## ${heading}\n${body}\n`)

const codesOf = (hits) => hits.map((v) => v.code)

test('enforcer_rulebook_checkEnforcerRubric_tie_break_required', () => {
  const text = replaceSection(
    ENFORCER_RUBRIC_OK,
    'Distinguish From',
    '`blind-edit` vs resources/enforcer/guessed-not-verified: different causal stage.',
  )
  const codes = codesOf(checkEnforcerRubric(text))
  assert.ok(codes.includes('rubric-distinguish-tie-break'), codes.join(','))
  assert.equal(codes.includes('rubric-distinguish-siblings'), false)
})

test('enforcer_rulebook_checkEnforcerRubric_root_cause_required', () => {
  const text = ENFORCER_RUBRIC_OK.replace(/root-cause/g, 'causal')
  const codes = codesOf(checkEnforcerRubric(text))
  assert.ok(codes.includes('rubric-root-cause'), codes.join(','))
})

test('enforcer_rulebook_checkEnforcerRubric_remediation_drift', () => {
  const withNow = `${ENFORCER_RUBRIC_OK}\n## What To Do Now\nFix it in the owner.\n`
  assert.ok(codesOf(checkEnforcerRubric(withNow)).includes('rubric-remediation-drift'))
  const withFixes = `${ENFORCER_RUBRIC_OK}\n## Common Wrong Fixes\n- add a flag\n`
  assert.ok(codesOf(checkEnforcerRubric(withFixes)).includes('rubric-remediation-drift'))
})

test('enforcer_rulebook_checkEnforcerRubric_trigger_must_be_semantic', () => {
  const globOnly = replaceSection(
    ENFORCER_RUBRIC_OK,
    'Trigger When',
    '- `*.js`\n- `*.ts`\n- `**/*.mjs`\n',
  )
  assert.ok(codesOf(checkEnforcerRubric(globOnly)).includes('rubric-trigger-semantic'))

  const empty = replaceSection(ENFORCER_RUBRIC_OK, 'Trigger When', '   \n')
  assert.ok(codesOf(checkEnforcerRubric(empty)).includes('rubric-trigger-semantic'))
})

test('enforcer_rulebook_checkEnforcerRubric_definition_nonempty_not_title', () => {
  const empty = replaceSection(ENFORCER_RUBRIC_OK, 'Definition', '   \n')
  assert.ok(codesOf(checkEnforcerRubric(empty)).includes('rubric-definition'))

  const titleOnly = replaceSection(ENFORCER_RUBRIC_OK, 'Definition', 'sample-tip — Enforcer\n')
  assert.ok(codesOf(checkEnforcerRubric(titleOnly)).includes('rubric-definition'))

  const nameOnly = replaceSection(ENFORCER_RUBRIC_OK, 'Definition', 'sample-tip\n')
  assert.ok(codesOf(checkEnforcerRubric(nameOnly)).includes('rubric-definition'))
})

test('enforcer_rulebook_checkMainRubric_owner_root_required', () => {
  const text = MAIN_RUBRIC_OK.replace('root-cause owner; who owns', 'the current file; repair')
  const codes = codesOf(checkMainRubric(text))
  assert.ok(codes.includes('rubric-owner-root'), codes.join(','))
})

test('enforcer_rulebook_checkMainRubric_authority_not_exceeded', () => {
  const overreach = `${MAIN_RUBRIC_OK}\nChange the Rulebook gates to allow this exception.\n`
  assert.ok(codesOf(checkMainRubric(overreach)).includes('rubric-authority-overreach'))

  const playbook = `${MAIN_RUBRIC_OK}\nEdit Playbook gates before shipping.\n`
  assert.ok(codesOf(checkMainRubric(playbook)).includes('rubric-authority-overreach'))

  const negated = `${MAIN_RUBRIC_OK}\nDo not change Rulebook gates or architecture gates.\n`
  assert.deepEqual(
    checkMainRubric(negated).filter((v) => v.code === 'rubric-authority-overreach'),
    [],
  )
})

test('enforcer_rulebook_checkMainRubric_no_reclassification', () => {
  const text = `${MAIN_RUBRIC_OK}\n## Definition\nRe-state the anti-pattern here.\n`
  assert.ok(codesOf(checkMainRubric(text)).includes('rubric-reclassification'))

  const detection = `${MAIN_RUBRIC_OK}\n## Detection\nDecide again whether this rule fires.\n`
  assert.ok(codesOf(checkMainRubric(detection)).includes('rubric-reclassification'))
})

test('enforcer_rulebook_checkMainRubric_scope_not_expanded', () => {
  const rewrite = `${MAIN_RUBRIC_OK}\nNext, rewrite the system around this tip.\n`
  assert.ok(codesOf(checkMainRubric(rewrite)).includes('rubric-scope-expansion'))

  const unrelated = `${MAIN_RUBRIC_OK}\nAlso clean unrelated modules while you are here.\n`
  assert.ok(codesOf(checkMainRubric(unrelated)).includes('rubric-scope-expansion'))

  const negated = `${MAIN_RUBRIC_OK}\nDo not rewrite the system or touch unrelated modules.\n`
  assert.deepEqual(
    checkMainRubric(negated).filter((v) => v.code === 'rubric-scope-expansion'),
    [],
  )
})

test('enforcer_rulebook_documents_human_only_items_not_subset', () => {
  assert.deepEqual([...HUMAN_ONLY_RUBRIC_ITEMS], [
    'paired-history 120',
    'A39 pair review',
    'A40 tournament',
  ])
  assert.deepEqual([...NEW_RUBRIC_CODES], [
    'rubric-distinguish-tie-break',
    'rubric-root-cause',
    'rubric-remediation-drift',
    'rubric-trigger-semantic',
    'rubric-definition',
    'rubric-owner-root',
    'rubric-authority-overreach',
    'rubric-reclassification',
    'rubric-scope-expansion',
  ])
  const gateSrc = readFileSync(
    join(dirname(fileURLToPath(import.meta.url)), '../../../scripts/checks/enforcer-rulebook-gate.mjs'),
    'utf8',
  )
  assert.equal(gateSrc.includes('A37 subset'), false)
  assert.equal(gateSrc.includes('A38 subset'), false)
  assert.ok(gateSrc.includes('paired-history 120'))
  assert.ok(gateSrc.includes('A39 pair review'))
  assert.ok(gateSrc.includes('A40 tournament'))
})
