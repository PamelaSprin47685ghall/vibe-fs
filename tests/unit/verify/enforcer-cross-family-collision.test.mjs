/**
 * A40 machine collision review: two synthetic temp dirs, not the 120-tip corpus.
 * CI of this checker must not depend on production wording.
 */
import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import {
  SCORING,
  STOPWORDS,
  collectSiblingNames,
  cosine,
  extractSection,
  jaccard,
  levenshteinRatio,
  normalizeTrigger,
  parseCliArgs,
  scanCollisions,
  scorePair,
  tokenize,
} from '../../../scripts/checks/enforcer-cross-family-collision.mjs'

const writeTip = (root, name, enforcerText) => {
  const tip = join(root, name)
  mkdirSync(tip, { recursive: true })
  writeFileSync(join(tip, 'enforcer.md'), enforcerText, 'utf8')
  writeFileSync(join(tip, 'main.md'), `# ${name} — Main\n\n## What To Do Now\nfix\n`, 'utf8')
}

const tipDoc = ({
  name,
  trigger,
  definition = 'A distinct root-cause about this anti-pattern.',
  distinguish = '',
}) => {
  const siblings = collectSiblingNames(distinguish)
  siblings.delete(name)
  return {
    name,
    path: `resources/enforcer/${name}/enforcer.md`,
    trigger,
    definition,
    siblings,
    triggerTokens: tokenize(trigger),
    definitionTokens: tokenize(definition),
    triggerNorm: normalizeTrigger(trigger),
  }
}

const EXTREME_TRIGGER =
  'Trigger when shared mutable buffer state is published before the owning contract is verified.'

const OVERLAP_TRIGGER_A =
  'Trigger when shared mutable buffer state is published before the owning contract is verified by observers.'

const OVERLAP_TRIGGER_B =
  'Trigger when shared mutable buffer records are published before the owning contract is checked by observers.'

const DISTINCT_TRIGGER_A =
  'Trigger when the compiler emits a warning for unused imports in a leaf module.'

const DISTINCT_TRIGGER_B =
  'Trigger when a background worker retries a failed network write without a backoff budget.'

const enforcerBody = (name, trigger, extra = {}) => `# ${name} — Enforcer

## Definition
${extra.definition ?? `${name} is a distinct anti-pattern whose root-cause is local to this tip.`}

## Trigger When
${trigger}

## Distinguish From
${extra.distinguish ?? 'No sibling listed here.'}

## Nudge
Stop.
`

test('enforcer_collision_documents_scoring_formula', () => {
  assert.equal(SCORING.triggerJaccardFail, 0.9)
  assert.equal(SCORING.triggerLevenshteinFail, 0.95)
  assert.equal(SCORING.warnTrigger, 0.55)
  assert.equal(SCORING.warnCombined, 0.5)
  assert.equal(SCORING.combinedTriggerWeight, 0.7)
  assert.equal(SCORING.combinedDefinitionWeight, 0.3)
  assert.equal(
    SCORING.combinedTriggerWeight + SCORING.combinedDefinitionWeight,
    1,
  )
  assert.ok(STOPWORDS.has('trigger'))
  assert.ok(STOPWORDS.has('when'))
})

test('enforcer_collision_tokenize_jaccard_cosine_levenshtein', () => {
  const a = tokenize(OVERLAP_TRIGGER_A)
  const b = tokenize(OVERLAP_TRIGGER_B)
  const j = jaccard(a, b)
  const c = cosine(a, b)
  assert.ok(j > 0.5 && j < SCORING.triggerJaccardFail, `jaccard=${j}`)
  assert.ok(c >= SCORING.warnTrigger, `cosine=${c}`)
  assert.equal(jaccard(tokenize(EXTREME_TRIGGER), tokenize(EXTREME_TRIGGER)), 1)
  assert.equal(levenshteinRatio('abc', 'abc'), 1)
  assert.ok(levenshteinRatio('abcd', 'abce') >= 0.75)
  assert.equal(jaccard(tokenize(DISTINCT_TRIGGER_A), tokenize(DISTINCT_TRIGGER_B)), 0)
})

test('enforcer_collision_extractSection_and_siblings', () => {
  const text = enforcerBody('alpha-tip', EXTREME_TRIGGER, {
    distinguish: '`beta-tip` vs resources/enforcer/gamma-tip: different stage.',
  })
  assert.match(extractSection(text, 'Trigger When'), /shared mutable buffer/)
  const sibs = collectSiblingNames(extractSection(text, 'Distinguish From'))
  assert.ok(sibs.has('beta-tip'))
  assert.ok(sibs.has('gamma-tip'))
})

test('enforcer_collision_scorePair_fail_warn_note_clean', () => {
  const extremeA = tipDoc({ name: 'alpha-tip', trigger: EXTREME_TRIGGER })
  const extremeB = tipDoc({ name: 'beta-tip', trigger: EXTREME_TRIGGER })
  const fail = scorePair(extremeA, extremeB)
  assert.equal(fail.severity, 'fail')
  assert.equal(fail.code, 'extreme-trigger-duplicate')
  assert.equal(fail.mutualSiblings, false)
  assert.ok(fail.triggerJaccard >= SCORING.triggerJaccardFail)

  const mutualA = tipDoc({
    name: 'alpha-tip',
    trigger: EXTREME_TRIGGER,
    distinguish: 'See beta-tip. Tie-break: this owns mutation.',
  })
  const mutualB = tipDoc({
    name: 'beta-tip',
    trigger: EXTREME_TRIGGER,
    distinguish: 'See alpha-tip. Tie-break: that owns mutation.',
  })
  const siblingExtreme = scorePair(mutualA, mutualB)
  assert.equal(siblingExtreme.severity, 'warn')
  assert.equal(siblingExtreme.code, 'extreme-trigger-duplicate-siblings')
  assert.equal(siblingExtreme.mutualSiblings, true)

  const overlapA = tipDoc({ name: 'alpha-tip', trigger: OVERLAP_TRIGGER_A })
  const overlapB = tipDoc({ name: 'beta-tip', trigger: OVERLAP_TRIGGER_B })
  const warn = scorePair(overlapA, overlapB)
  assert.equal(warn.severity, 'warn')
  assert.equal(warn.code, 'high-lexical-overlap')
  assert.ok(warn.triggerScore >= SCORING.warnTrigger)
  assert.ok(warn.triggerJaccard < SCORING.triggerJaccardFail)
  assert.ok(warn.triggerLevenshtein < SCORING.triggerLevenshteinFail)

  const listedA = tipDoc({
    name: 'alpha-tip',
    trigger: OVERLAP_TRIGGER_A,
    distinguish: 'Neighbor beta-tip is the sibling; tie-break by stage.',
  })
  const listedB = tipDoc({ name: 'beta-tip', trigger: OVERLAP_TRIGGER_B })
  const note = scorePair(listedA, listedB)
  assert.equal(note.severity, 'note')
  assert.equal(note.code, 'sibling-overlap')
  assert.equal(note.listedSiblings, true)

  const clean = scorePair(
    tipDoc({ name: 'alpha-tip', trigger: DISTINCT_TRIGGER_A }),
    tipDoc({ name: 'beta-tip', trigger: DISTINCT_TRIGGER_B }),
  )
  assert.equal(clean.severity, null)
  assert.equal(clean.code, null)
})

test('enforcer_collision_temp_dir_fail_closed_on_extreme_non_siblings', () => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-a40-fail-'))
  try {
    writeTip(root, 'alpha-tip', enforcerBody('alpha-tip', EXTREME_TRIGGER))
    writeTip(root, 'beta-tip', enforcerBody('beta-tip', EXTREME_TRIGGER))
    const result = scanCollisions(root)
    assert.equal(result.ok, false, JSON.stringify(result.failures, null, 2))
    assert.equal(result.count, 2)
    assert.equal(result.pairsCompared, 1)
    assert.equal(result.failures.length, 1)
    assert.equal(result.failures[0].code, 'extreme-trigger-duplicate')
    assert.equal(result.scoring.triggerJaccardFail, 0.9)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})

test('enforcer_collision_temp_dir_warns_overlap_passes_distinct', () => {
  const overlapRoot = mkdtempSync(join(tmpdir(), 'wxs-a40-warn-'))
  const distinctRoot = mkdtempSync(join(tmpdir(), 'wxs-a40-clean-'))
  try {
    writeTip(overlapRoot, 'alpha-tip', enforcerBody('alpha-tip', OVERLAP_TRIGGER_A))
    writeTip(overlapRoot, 'beta-tip', enforcerBody('beta-tip', OVERLAP_TRIGGER_B))
    const warned = scanCollisions(overlapRoot)
    assert.equal(warned.ok, true, JSON.stringify(warned.failures, null, 2))
    assert.equal(warned.warnings.length, 1)
    assert.equal(warned.warnings[0].code, 'high-lexical-overlap')
    assert.ok(warned.evidence.length >= 1)
    assert.equal(warned.evidence[0].a, 'alpha-tip')
    assert.equal(warned.evidence[0].b, 'beta-tip')

    writeTip(distinctRoot, 'alpha-tip', enforcerBody('alpha-tip', DISTINCT_TRIGGER_A))
    writeTip(distinctRoot, 'beta-tip', enforcerBody('beta-tip', DISTINCT_TRIGGER_B))
    const clean = scanCollisions(distinctRoot)
    assert.equal(clean.ok, true)
    assert.equal(clean.failures.length, 0)
    assert.equal(clean.warnings.length, 0)
    assert.equal(clean.notes.length, 0)
    assert.equal(clean.pairsCompared, 1)
    // A40 evidence still lists the pair so a clean corpus is not a silent skip.
    assert.equal(clean.evidence.length, 1)
    assert.ok(clean.evidence[0].combined < SCORING.warnCombined)
  } finally {
    rmSync(overlapRoot, { recursive: true, force: true })
    rmSync(distinctRoot, { recursive: true, force: true })
  }
})

test('enforcer_collision_parseCliArgs_root', () => {
  assert.deepEqual(parseCliArgs([]), { root: null })
  assert.deepEqual(parseCliArgs(['--root=/tmp/tips']), { root: '/tmp/tips' })
  assert.deepEqual(parseCliArgs(['--root', '/tmp/tips']), { root: '/tmp/tips' })
})

test('enforcer_collision_missing_root_is_not_ok', () => {
  const result = scanCollisions(join(tmpdir(), 'wxs-a40-missing-root-does-not-exist'))
  assert.equal(result.ok, false)
  assert.equal(result.count, 0)
  assert.ok(result.loadErrors.some((e) => e.code === 'missing-root'))
})
