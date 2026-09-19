import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, rmSync, unlinkSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import path from 'node:path'
import test from 'node:test'
import { Worker } from 'node:worker_threads'
import { encode } from 'gpt-tokenizer/encoding/o200k_base'
import * as loopDetector from '../../../dist/Execution/Session/LoopDetectorSurface.js'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'
import {
  deriveLoopDetectorEnvelope,
  encodeParallel,
  envelopeBounds,
  loadLoopDetectorRepositoryCorpusV1,
} from '../../../scripts/lib/derive-loop-detector-envelope.mjs'
import {
  loopDetectorRepositoryInputFiles,
} from '../../../scripts/lib/loop-detector-repository-corpus.mjs'

const close = (actual, expected, tolerance = 1e-9) =>
  assert.ok(Math.abs(actual - expected) <= tolerance, `${actual} != ${expected}`)

const lowerQuantileProbability = 0.025

const upperQuantileProbability = 1.0

const centralProbability = upperQuantileProbability - lowerQuantileProbability

const referenceScore = (text) => {
  const lastSeen = new Map()
  let weightedDistinctTokens = loopDetector.normalWeightedDistinctCount
  let step = 0

  for (const token of encode(text)) {
    step += 1
    const previous = lastSeen.get(token)
    weightedDistinctTokens =
      loopDetector.lambda * weightedDistinctTokens +
      1 -
      (previous === undefined ? 0 : loopDetector.lambda ** (step - previous))
    lastSeen.set(token, step)
  }

  return { weightedDistinctTokens, step }
}

test('WHAT[degeneration-guard-004] LOOP_004_repository_corpus_contains_normal_source_documents_only', () => {
  const files = loopDetectorRepositoryInputFiles().map((file) => file.replaceAll('\\', '/'))

  assert.ok(files.every(path.isAbsolute), 'selector must return filesystem paths')
  assert.ok(files.some((file) => file.endsWith('/src/Wanxiangshu/Execution/Session/LoopDetector.fs')))
  assert.ok(files.some((file) => file.endsWith('/requirements/degeneration-guard/WHAT.md')))
  assert.ok(!files.some((file) => file.endsWith('/package-lock.json')))
  assert.ok(!files.some((file) => file.endsWith('/scripts/checks/semantic-owners.json')))
  assert.ok(!files.some((file) => file.endsWith('/docs/index.html')))
})

test('WHAT[degeneration-guard-004] LOOP_004_repository_corpus_excludes_tracked_paths_deleted_from_the_worktree', () => {
  const root = mkdtempSync(path.join(tmpdir(), 'wanxiangshu-loop-selector-'))

  try {
    execFileSync('git', ['init', '-q'], { cwd: root })
    writeFileSync(path.join(root, 'alive.md'), 'alive\n')
    writeFileSync(path.join(root, 'deleted.md'), 'deleted\n')
    execFileSync('git', ['add', 'alive.md', 'deleted.md'], { cwd: root })
    unlinkSync(path.join(root, 'deleted.md'))

    assert.deepEqual(
      loopDetectorRepositoryInputFiles(root).map((file) => path.basename(file)),
      ['alive.md'],
    )
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})

test('WHAT[degeneration-guard-004] LOOP_004_runtime_envelope_reads_every_selected_repository_input_through_the_tracking_reader', async () => {
  const reads = []
  const bytes = new Map([
    ['a.md', Buffer.from('first source\n')],
    ['b.fs', Buffer.from('module Second\nlet value = 2\n')],
  ])
  const derived = await deriveLoopDetectorEnvelope('/fixture-root', {
    selectInputFiles: () => [...bytes.keys()].reverse().map((repositoryPath) => `/fixture-root/${repositoryPath}`),
    readFile: (file) => {
      const repositoryPath = file.replace('/fixture-root/', '')
      reads.push(repositoryPath)
      return bytes.get(repositoryPath)
    },
  })

  assert.deepEqual(reads, ['a.md', 'b.fs'])
  assert.deepEqual(derived.selectedInputs.map(({ path }) => path), ['a.md', 'b.fs'])
  assert.ok(derived.selectedInputs.every(({ blob_digest: blobDigest }) => /^sha256:[0-9a-f]{64}$/.test(blobDigest)))

  assert.throws(() => loadLoopDetectorRepositoryCorpusV1('/fixture-root', {
    selectInputFiles: () => ['/outside-root/secret.md'],
    readFile: () => Buffer.from('must not be read'),
  }), { code: 'generated-selected-input-outside-root' })
})

{
const {
  writeLoopDetectorEnvelopeArtifact,
} = await import('../../../scripts/lib/derive-loop-detector-envelope.mjs')

const empiricalQuantile = (values, probability) => {
  const rank = Math.ceil(probability * values.length)
  const index = Math.min(values.length - 1, rank - 1)
  return Float64Array.from(values).sort()[index]
}

const referenceEnvelope = (tokens, lambda, initialValue) => {
  const lastSeen = new Map()
  let weightedDistinctTokens = initialValue
  let sum = 0
  const trajectory = new Float64Array(tokens.length)

  for (let index = 0; index < tokens.length; index += 1) {
    const step = index + 1
    const token = tokens[index]
    const previous = lastSeen.get(token)
    weightedDistinctTokens =
      lambda * weightedDistinctTokens +
      1 -
      (previous === undefined ? 0 : lambda ** (step - previous))
    lastSeen.set(token, step)
    trajectory[index] = weightedDistinctTokens
    sum += weightedDistinctTokens
  }

  return {
    mean: sum / tokens.length,
    minimum: empiricalQuantile(trajectory, lowerQuantileProbability),
    maximum: empiricalQuantile(trajectory, upperQuantileProbability),
  }
}

integrationTest('WHAT[degeneration-guard-004] LOOP_004_runtime_envelope_is_freshly_derived_from_the_current_repository_without_numeric_snapshots', async () => {
  let generatedBytes = null
  const derived = await writeLoopDetectorEnvelopeArtifact(undefined, {
    writeArtifact: (_target, bytes) => { generatedBytes = bytes },
  })

  assert.ok(Buffer.isBuffer(generatedBytes) && generatedBytes.length > 0)
  assert.equal(derived.halfLife, 256)
  close(derived.centralProbability, 0.975)
  close(derived.lowerQuantileProbability, 0.025)
  close(derived.upperQuantileProbability, 1.0)
  close(loopDetector.halfLife, derived.halfLife)
  close(loopDetector.lambda, derived.lambda)
  close(loopDetector.normalWeightedDistinctCount, derived.normalPrior)
  close(loopDetector.centralProbability, derived.centralProbability)
  close(loopDetector.lowerQuantileProbability, derived.lowerQuantileProbability)
  close(loopDetector.upperQuantileProbability, derived.upperQuantileProbability)
  close(loopDetector.minimumWeightedDistinctCount, derived.minimum)
  close(loopDetector.maximumWeightedDistinctCount, derived.maximum)

  const tokens = encode(loadLoopDetectorRepositoryCorpusV1().texts.join('\n'))
  const reference = referenceEnvelope(tokens, derived.lambda, derived.normalPrior)
  close(reference.mean, derived.normalPrior)
  close(reference.minimum, derived.minimum)
  close(reference.maximum, derived.maximum)
})
}
