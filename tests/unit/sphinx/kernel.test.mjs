import assert from 'node:assert/strict'
import test from 'node:test'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  activateMethods,
  canonicalAnswer,
  closure,
  createEpistemicState,
  deriveRootContract,
  evidenceMassWithoutExogenous,
  METHODS,
  resumeInquiry,
  semanticKeyOf,
  startInquiry,
  stopValue,
  bestActionValue,
} from '../../../src/sphinx/kernel/index.js'

const here = dirname(fileURLToPath(import.meta.url))
const root = join(here, '../../..')

test('kernel_has_no_wanxiangshu_imports', async () => {
  const { readdir, readFile } = await import('node:fs/promises')
  async function walk(dir) {
    const entries = await readdir(dir, { withFileTypes: true })
    const files = []
    for (const entry of entries) {
      const full = join(dir, entry.name)
      if (entry.isDirectory()) files.push(...(await walk(full)))
      else if (entry.name.endsWith('.js')) files.push(full)
    }
    return files
  }
  const files = await walk(join(root, 'src/sphinx'))
  assert.ok(files.length > 0)
  for (const file of files) {
    const source = await readFile(file, 'utf8')
    assert.equal(/Wanxiangshu|wanxiangshu\//i.test(source), false, file)
  }
})

test('semantic_assessment_derives_root_contract_and_activates_methods', () => {
  let { state, result } = startInquiry('花儿为什么这样红？')
  assert.equal(result.status, 'yield')
  assert.equal(result.request.type, 'SemanticAssessmentRequest')

  ;({ state, result } = resumeInquiry(state, {
    type: 'SemanticAssessment',
    forms: { Why: 0.75, How: 0.18, Other: 0.07 },
    facets: { causal: 0.84, explanatory: 0.91, predictive: 0.06 },
  }))

  assert.equal(result.status, 'yield')
  assert.equal(result.request.type, 'GenerateCandidatesRequest')
  assert.equal(state.R.primaryForm, 'Why')
  assert.equal(state.R.primaryContract, 'Explanation')
  assert.ok(state.activatedMethods.includes('Multidisciplinary'))
  assert.ok(state.activatedMethods.includes('Abduction'))
  assert.ok(state.activatedMethods.includes('Synthesis'))
  assert.ok(state.A.length >= 1)
  assert.ok(state.B.evidenceMass > 0)
})

test('closure_dedups_by_semantic_key_and_prunes_zero_novelty', () => {
  let state = createEpistemicState('why is the sky blue?')
  state = closure(
    state,
    {
      type: 'SemanticAssessment',
      forms: { Why: 1 },
      facets: { explanatory: 1, causal: 0.8 },
    },
    { exogenous: true },
  )
  state = closure(
    state,
    {
      type: 'Candidates',
      items: [
        { method: 'Abduction', text: 'Rayleigh scattering', semanticKey: 'abduction:rayleigh' },
        { method: 'Abduction', text: 'Rayleigh scattering', semanticKey: 'abduction:rayleigh' },
        { method: 'Analogy', text: 'sunset red contrast', semanticKey: 'analogy:sunset' },
      ],
    },
    { exogenous: true },
  )

  const keys = state.A.map((a) => a.semanticKey)
  assert.equal(keys.filter((k) => k === 'abduction:rayleigh').length, 1)
  assert.ok(keys.includes('analogy:sunset'))

  const massAfterFirst = state.B.evidenceMass
  const again = closure(
    state,
    {
      type: 'Candidates',
      items: [
        { method: 'Abduction', text: 'Rayleigh scattering', semanticKey: 'abduction:rayleigh' },
      ],
    },
    { exogenous: true },
  )
  assert.equal(again.B.evidenceMass, massAfterFirst)
  assert.equal(
    again.B.hypotheses.filter((h) => h.semanticKey === 'abduction:rayleigh').length,
    state.B.hypotheses.filter((h) => h.semanticKey === 'abduction:rayleigh').length,
  )
})

test('no_free_information_without_exogenous_observation', () => {
  let state = createEpistemicState('will silver rise tomorrow?')
  state = closure(
    state,
    {
      type: 'SemanticAssessment',
      forms: { Polar: 0.9, Other: 0.1 },
      facets: { predictive: 0.8, comparative: 0.5 },
    },
    { exogenous: true },
  )
  const mass = state.B.evidenceMass
  assert.ok(mass > 0)
  assert.equal(evidenceMassWithoutExogenous(state), mass)
  const replay = closure(state, null, { exogenous: false })
  assert.equal(replay.B.evidenceMass, mass)
})

test('stop_dominates_when_synthesis_present', () => {
  let state = createEpistemicState('花儿为什么这样红？')
  state = closure(
    state,
    {
      type: 'SemanticAssessment',
      forms: { Why: 0.8, How: 0.2 },
      facets: { explanatory: 0.9, causal: 0.7 },
    },
    { exogenous: true },
  )
  state = closure(
    state,
    {
      type: 'Candidates',
      items: [
        { method: 'Multidisciplinary', text: 'anthocyanin chemistry', semanticKey: 'multi:anthocyanin' },
        { method: 'Abduction', text: 'pollinator signaling', semanticKey: 'abd:pollinator' },
      ],
    },
    { exogenous: true },
  )
  state = closure(
    state,
    {
      type: 'Synthesis',
      text: 'Chemistry and ecology jointly explain the redness.',
      strands: ['anthocyanin', 'pollinator'],
    },
    { exogenous: true },
  )
  assert.ok(stopValue(state) >= bestActionValue(state))
  const answer = canonicalAnswer(state, 'stop-dominates')
  assert.equal(answer.question, '花儿为什么这样红？')
  assert.match(answer.synthesis.text, /Chemistry/)
  assert.ok(answer.strands.length >= 1)
  assert.equal(answer.stopReason, 'stop-dominates')
})

test('method_library_phase0_is_fixed', () => {
  assert.deepEqual(METHODS, [
    'Multidisciplinary',
    'Abduction',
    'Analogy',
    'Counterexample',
    'Synthesis',
  ])
  const contract = deriveRootContract({ Why: 0.7, How: 0.3 }, { explanatory: 1 })
  const blank = createEpistemicState('q')
  const state = {
    ...blank,
    R: contract,
    B: { ...blank.B, formBelief: contract.formBelief, facets: contract.facets },
  }
  const activated = activateMethods(state, 0)
  assert.ok(activated.includes('Multidisciplinary'))
})

test('semantic_key_helper_is_stable', () => {
  const key = semanticKeyOf({
    type: 'Synthesis',
    text: '  Hello   World ',
  })
  assert.equal(key, 'synthesis:hello world')
})
