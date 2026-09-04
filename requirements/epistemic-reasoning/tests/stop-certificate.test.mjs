import test from 'node:test'
import assert from 'node:assert/strict'

import { gecSurface } from '../../../dist/Sphinx/GecSurface.js'

const posterior = { approve: 0.68, reject: 0.32 }

test('WHAT[EPI-029] certificate-bounds-guarantee-to-tested-framing-family-only', async () => {
  const result = await gecSurface.stopCertificate({
    testedFramings: ['neutral', 'reverse-wording'],
    decisionPosterior: { ...posterior },
    checksSoFar: 1,
    alpha: 0.05,
  })
  assert.equal(result.ok, true)
  assert.deepEqual(result.certificate.testedFamily, ['neutral', 'reverse-wording'])
  const scopeText = `${result.certificate.scope} ${result.certificate.guarantee}`
  assert.match(result.certificate.scope, /tested/i)
  assert.ok(!/universal/i.test(scopeText))
  assert.ok(!/all wording/i.test(scopeText))
  assert.ok(!result.certificate.testedFamily.includes('leading-question'))
})

test('WHAT[EPI-029] sequential-error-control-tightens-with-repeated-checks', async () => {
  const base = {
    testedFramings: ['neutral', 'reverse-wording'],
    decisionPosterior: { ...posterior },
    alpha: 0.05,
  }
  const early = await gecSurface.stopCertificate({ ...base, checksSoFar: 1 })
  const late = await gecSurface.stopCertificate({ ...base, checksSoFar: 5 })
  assert.equal(early.ok, true)
  assert.equal(late.ok, true)
  assert.ok(late.certificate.sequentialAlpha < early.certificate.sequentialAlpha)
  assert.ok(early.certificate.sequentialError.cumulativeError <= 0.05 + 1e-12)
  assert.ok(late.certificate.sequentialError.cumulativeError <= 0.05 + 1e-12)
  assert.match(late.certificate.sequentialError.method, /bonferroni/i)
})

test('WHAT[EPI-029] stable-minority-mode-returns-decision-distribution-not-single-winner', async () => {
  const result = await gecSurface.stopCertificate({
    testedFramings: ['neutral', 'reverse-wording'],
    decisionPosterior: { ...posterior },
    framingStability: { approve: [0.66, 0.7], reject: [0.3, 0.34] },
    minorityStable: true,
    checksSoFar: 2,
    alpha: 0.05,
  })
  assert.equal(result.ok, true)
  assert.ok(result.decision.kind !== 'single-winner')
  assert.ok(!('winner' in result.decision))
  assert.equal(result.decision.modes.length, 2)
  const minority = result.decision.modes.find((mode) => mode.decision === 'reject')
  assert.ok(minority)
  assert.ok(Math.abs(minority.mass - 0.32) < 1e-12)
  assert.deepEqual(result.decision.minorityModes, [{ decision: 'reject', mass: 0.32 }])
})

test('WHAT[EPI-029] caller-supplied-coverage-and-minority-thresholds-bind', async () => {
  const base = {
    testedFramings: ['neutral', 'reverse-wording'],
    decisionPosterior: { ...posterior },
    checksSoFar: 1,
    alpha: 0.05,
  }
  const custom = await gecSurface.stopCertificate({ ...base, requiredCoverage: 0.9, minorityThreshold: 0.4 })
  assert.equal(custom.ok, true)
  assert.ok(Math.abs(custom.certificate.requiredCoverage - 0.9) < 1e-12)
  assert.ok(Math.abs(custom.certificate.minorityThreshold - 0.4) < 1e-12)

  const fallback = await gecSurface.stopCertificate(base)
  assert.equal(fallback.ok, true)
  assert.ok(Math.abs(fallback.certificate.requiredCoverage - 0.5) < 1e-12)
  assert.ok(Math.abs(fallback.certificate.minorityThreshold - 0.05) < 1e-12)
})

test('WHAT[EPI-029] caller-evidence-fires-stop-when-all-checks-pass', async () => {
  const result = await gecSurface.stopCertificate({
    testedFramings: ['neutral', 'reverse-wording'],
    decisionPosterior: { ...posterior },
    framingStability: { approve: [0.66, 0.7], reject: [0.3, 0.34] },
    checksSoFar: 1,
    alpha: 0.05,
    evidence: 25,
  })
  assert.equal(result.ok, true)
  assert.deepEqual(
    result.certificate.testedFamily,
    ['neutral', 'reverse-wording'],
  )
  assert.equal(result.certificate.checks.length, 4)
  for (const check of result.certificate.checks) {
    assert.equal(check.passed, true)
  }
  assert.equal(result.recommendation, 'stop')
})

test('WHAT[EPI-029] conservative-upper-voc-blocks-stopping-on-point-estimate-alone', async () => {
  const result = await gecSurface.stopCertificate({
    testedFramings: ['neutral', 'reverse-wording'],
    decisionPosterior: { ...posterior },
    checksSoFar: 2,
    alpha: 0.05,
    voc: { point: 0.01, upper: 0.5, threshold: 0.1 },
  })
  assert.equal(result.ok, true)
  assert.ok(result.voc.upper >= result.voc.point)
  assert.ok(Math.abs(result.voc.point - 0.01) < 1e-12)
  assert.ok(Math.abs(result.voc.upper - 0.5) < 1e-12)
  assert.equal(result.recommendation, 'continue')
})
