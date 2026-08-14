// requirements/review-judgement/tests/discrimination-fixtures.test.mjs
//
// REVIEW-JUDGEMENT-002..007/009/010 (REVIEW-011 + Role Law + Examiner's Ledger):
// PERFECT/REVISE discrimination semantics. The judgement itself is performed by
// a model, so the executable contract here is the authoritative prose: each
// fixture encodes one scenario category (e.g. "minor typo" vs "material
// defect") and asserts that the Role Law / Examiner's Ledger actually decide it
// in the required direction — in BOTH languages. A fixture reddens when the
// decision disappears from the authoritative text, which is exactly the failure
// PROOF-MAP warns about ("cannot rely on prompt anchors alone"): these are
// per-sentence contract assertions, not loose regex presence checks.

import assert from 'node:assert/strict'
import test from 'node:test'
import { providerLanguage, providerResources } from '../../../tests/unit/support/domain.mjs'

const roleLaw = providerResources.readText(providerLanguage.english, 'role/reviewer')
const ledger = providerResources.readText(providerLanguage.english, 'library/reviewer/quality-ledger')
const roleLawZh = providerResources.readText(providerLanguage.simplifiedChinese, 'role/reviewer')
const ledgerZh = providerResources.readText(providerLanguage.simplifiedChinese, 'library/reviewer/quality-ledger')

/** Collapse newlines so line-wrapped prose matches the single-line phrase. */
const flat = (text) => text.replace(/\s+/g, ' ').trim()

const enText = flat(`${roleLaw}\n${ledger}`)
const enZh = flat(roleLawZh)
const zhLedgerText = flat(ledgerZh)

/** Assert that every required phrase appears in the EN contract text. */
const en = (fixture, ...phrases) => {
  for (const phrase of phrases) {
    assert.ok(enText.includes(flat(phrase)), `${fixture}: missing EN phrase: ${phrase}`)
  }
}

/** Assert that every required phrase appears in the ZH Role Law. */
const zh = (fixture, ...phrases) => {
  for (const phrase of phrases) {
    assert.ok(enZh.includes(flat(phrase)), `${fixture}: missing ZH phrase: ${phrase}`)
  }
}

test('REVIEW_011_acceptance_and_rejection_must_both_be_earned', () => {
  en(
    'earned-both-ways',
    'Acceptance must be earned. Rejection must also be earned.',
    'Rejection must also be earned.',
  )
  zh('earned-both-ways', 'Acceptance 必须被赢得。', 'Rejection 同样必须被赢得。')
})

test('REVIEW_011_discrimination_is_the_craft_not_rejection_theatre', () => {
  en('discrimination-not-rejection-theatre', 'Your purpose is discrimination, not rejection.', 'Rejection is not a pose of rigor.')
  zh('discrimination-not-rejection-theatre', '你的目的，是作出有区分力的判断，而不是追求拒绝。', '拒绝不是严谨的姿态。')
})

test('REVIEW_011_judgement_is_against_the_obligation_not_the_reviewer_mood', () => {
  en(
    'judged-against-obligation-not-mood',
    'Judge the work that exists, by the obligation that exists, with the evidence that exists.',
    'They are not moods, and they are not measures of your severity.',
  )
  zh('judged-against-obligation-not-mood', '根据真实存在的 obligation，使用真实存在的 evidence，判断真实存在的工作。', '它们不是情绪，也不是你严厉程度的量尺。')
})

test('REVIEW_011_blocking_vs_nonblocking_workmanship_are_distinguished', () => {
  en(
    'blocking-vs-nonblocking',
    'Blocking workmanship withholds acceptance.',
    'Non-blocking workmanship does not withhold acceptance.',
    'It is still a truthful observation.',
  )
  zh('blocking-vs-nonblocking', '阻断性做工扣住 acceptance。', '非阻断性做工不扣住 acceptance。')
})

test('REVIEW_011_a_minor_typo_never_purchases_revise', () => {
  en(
    'minor-typo-never-purchases-revise',
    'A minor typo, a clumsy name, a rough edge that does not touch the entrusted result',
    'They do not purchase REVISE merely so the review looks rigorous.',
    'Do not reject merely to demonstrate caution.',
  )
  zh('minor-typo-never-purchases-revise', '一个无关紧要的笔误、一个笨拙的命名、一处并不触碰 entrusted result 的毛边', '它们不能仅仅为了让审查显得严谨，就买到 REVISE。')
})

test('REVIEW_011_perfect_does_not_silence_a_true_minor_observation', () => {
  en(
    'perfect-does-not-silence-minor',
    'Suppressing a non-blocking observation because the verdict is PERFECT is also false.',
    'Acceptance can be earned while small truths remain speakable.',
  )
  zh('perfect-does-not-silence-minor', '因为 verdict 是 PERFECT 就压制非阻断性观察，同样是虚假的。')
})

test('REVIEW_011_materiality_traces_consequence_not_edit_size', () => {
  en(
    'materiality-traces-consequence-not-size',
    'Size of edit and materiality of consequence are different quantities.',
    'A missing await may be a tiny edit and a severe defect.',
    'Small is not harmless. Large is not important. Trace the consequence.',
    'Do not invent materiality to justify taste.',
  )
})

test('REVIEW_011_perfect_is_not_literal_flawlessness', () => {
  en('perfect-not-flawless', 'It does not mean literal flawlessness.', 'It does not mean you imagined every possible future failure.')
  zh('perfect-not-flawless', '它并不意味着字面上的毫无瑕疵。')
})

test('REVIEW_011_rejection_must_purchase_a_materially_better_or_more_truthful_result', () => {
  en(
    'rejection-purchases',
    'It must purchase something: a materially better result, a more truthful account of what was delivered, or the repair of a concrete defect that matters to the entrusted result.',
    'Reject when a material obligation is unmet, a material claim lacks the evidence it requires, or the work contains a concrete defect that matters to the entrusted result.',
  )
  zh('rejection-purchases', '拒绝必须买到东西：一个实质上更好的结果、对已交付之物更真实的陈述，或对一个真正影响 entrusted result 的具体缺陷的修复。')
})

test('REVIEW_011_acceptance_does_not_require_omniscience', () => {
  en(
    'acceptance-not-omniscience',
    'Omniscience is not the standard. Proportionate discrimination is.',
    'Accept because proportionate inquiry has left no material ground for withholding acceptance',
  )
  zh('acceptance-not-omniscience', '全知不是标准。', '相称的区分才是。')
})

test('REVIEW_011_evidence_must_be_proportional_to_the_claim', () => {
  en(
    'evidence-proportional-to-claim',
    'Evidence must be proportional to the claim.',
    'A passing test proves what that test can distinguish and nothing more.',
    'Do not demand a trial\'s worth of proof for a sentence that only notes a rough edge.',
  )
  zh('evidence-proportional-to-claim', '证据必须与主张相称。', '一个通过的测试，只能证明这个测试本身有能力区分的事情，不能证明更多。')
})

test('REVIEW_011_unresolved_uncertainty_is_preserved_not_laundered_into_a_verdict', () => {
  en(
    'uncertainty-preserved-not-laundered',
    'preserve that uncertainty in your judgment.',
    'Do not launder unresolved material doubt into either PERFECT or REVISE by rhetoric alone.',
  )
  zh('uncertainty-preserved-not-laundered', '在 judgment 中保留这种不确定性。', '不要单靠修辞，把尚未解决的重要疑虑洗成 PERFECT 或 REVISE。')
})

test('REVIEW_011_the_ledger_is_a_judgement_direction_not_a_checklist', () => {
  en(
    'ledger-not-checklist',
    'The entries are not eight boxes to mark Pass.',
    'Walk the whole Ledger in thought. Speak only where there is something worth saying.',
    'Neither is a checklist whose boxes can replace judgment.',
  )
  assert.ok(zhLedgerText.includes('这些 entries 不是八个需要逐一勾选 Pass 的方框'), 'ledger-not-checklist: missing ZH phrase')
  assert.ok(
    zhLedgerText.includes('不要为了证明 taste 合理而发明 materiality'),
    'materiality-traces-consequence-not-size: missing ZH phrase',
  )
})

test('REVIEW_011_no_fixed_report_schema_or_eight_heading_template', () => {
  en(
    'no-fixed-report-schema',
    'It does not prescribe a report format.',
    'It does not require eight headings in every review.',
    'A short review may be complete.',
  )
})

test('REVIEW_011_the_wound_must_be_clear_enough_to_purchase_the_repair', () => {
  en(
    'wound-clear-enough-to-purchase',
    'make the wound clear enough that repairing it purchases that better or more truthful result.',
    'A clear wound does not become clearer when surrounded by imaginary bruises.',
  )
  zh('wound-clear-enough-to-purchase', '把真正的伤口说清楚，使修复它能够买到那个更好或更真实的结果。', '一个清晰的伤口，不会因为周围再画上一圈想象出来的淤青而变得更清晰。')
})

test('REVIEW_011_no_invented_obligations_to_look_careful', () => {
  en(
    'no-invented-obligations',
    'Do not invent a requirement, risk, boundary, test, or hypothetical world that the actual obligation does not need.',
    'Inventing obligations in order to look careful is not judgment.',
  )
  zh('no-invented-obligations', '不要发明真实 obligation 并不需要的 requirement、risk、boundary、test 或 hypothetical world。', '为了显得仔细而发明要求，不是判断。')
})

test('REVIEW_011_judgement_does_not_reward_confidence_or_punish_unfamiliarity', () => {
  en(
    'no-reward-for-confidence',
    'Do not reward confidence. Do not punish unfamiliarity.',
    'Do not reject merely because you would have written the code differently.',
    'Do not accept merely because the implementation is polished.',
  )
})

test('REVIEW_011_novelty_and_style_preference_are_not_defects_by_themselves', () => {
  en(
    'novelty-and-preference-not-defect',
    'But novelty is not a defect.',
    'A stylistic preference is not a defect merely because you can describe it.',
    'Do not invent materiality to justify taste.',
  )
})

test('REVIEW_011_a_match_is_an_observation_a_defect_is_a_judgement', () => {
  en(
    'match-is-observation-defect-is-judgement',
    'A match is an observation. A defect is your judgment about what that observation means for the work.',
    'A work record is evidence. A test result is evidence.',
    'None of these, alone, is judgment.',
  )
  zh('match-is-observation-defect-is-judgement', '一次 match 是观察。', 'Defect 则是你对“这个观察对当前工作意味着什么”的判断。')
})

test('REVIEW_011_a_lens_may_narrow_sight_but_not_responsibility', () => {
  en(
    'lens-narrows-sight-not-responsibility',
    'A lens may narrow sight. It may not narrow responsibility.',
    'The user\'s real requirement remains the measure.',
  )
})
