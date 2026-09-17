import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, rmSync, unlinkSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import path from 'node:path'
import test from 'node:test'
import { Worker } from 'node:worker_threads'
import { encode } from 'gpt-tokenizer/encoding/o200k_base'
import * as loopDetector from '../../../dist/Execution/Session/LoopDetectorSurface.js'
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

test('WHAT[DG-003] LOOP_003_fresh_detector_uses_repository_normal_prior', () => {
  const result = loopDetector.evaluate(loopDetector.create())
  assert.equal(result.state, 'Normal')
  assert.equal(result.isAnomalous, false)
  assert.equal(result.step, 0)
  close(result.weightedDistinctTokens, loopDetector.normalWeightedDistinctCount)
})

test('WHAT[DG-003] LOOP_003_push_text_is_o200k_token_based', () => {
  const text = 'const π = await repository.load("订单-42");\nreturn { ok: true, revision: 17 };'
  const expected = referenceScore(text)
  const result = loopDetector.pushText(loopDetector.create(), text)

  assert.equal(result.step, encode(text).length)
  assert.equal(result.step, expected.step)
  close(result.weightedDistinctTokens, expected.weightedDistinctTokens)
})

test('WHAT[DG-003] LOOP_EQ_serial_parallel_token_identical', async () => {
  const fixture = [
    'export class OrderProcessor {',
    '  constructor(private readonly repository: OrderRepository) {}',
    '  // comment with slash / and symbols',
    '  /// doc comment',
    '  async processOrder(orderId: string): Promise<void> {}',
    '}',
    '',
    'const message = "你好，世界！🚀";',
    '// 包含中文、Emoji 以及多行换行',
    '',
    'let count = 42;',
    '// ' + 'long text payload '.repeat(100),
  ].join('\n')

  const serialTokens = Array.from(encode(fixture))
  const parallel1Tokens = await encodeParallel(fixture, 1)
  const parallel4Tokens = await encodeParallel(fixture, 4)

  assert.deepEqual(parallel1Tokens, serialTokens)
  assert.deepEqual(parallel4Tokens, serialTokens)
})

test('WHAT[DG-003] LOOP_BOUNDS_edge_cases', () => {
  // n = 1
  assert.deepEqual(envelopeBounds([42], 0.5), { minimum: 42, maximum: 42 })
  // all-equal
  assert.deepEqual(envelopeBounds([7, 7, 7, 7, 7], 0.5), { minimum: 7, maximum: 7 })
  // extremes
  assert.deepEqual(envelopeBounds([-1e15, 0, 1e15], 0.5), { minimum: 0, maximum: 1e15 })
  // lowerP in {0.5, 0.9, 1.0}
  const samples = [10, 20, 30, 40, 50, 60, 70, 80, 90, 100]
  assert.deepEqual(envelopeBounds(samples, 0.5), { minimum: 50, maximum: 100 })
  assert.deepEqual(envelopeBounds(samples, 0.9), { minimum: 90, maximum: 100 })
  assert.deepEqual(envelopeBounds(samples, 1.0), { minimum: 100, maximum: 100 })
})

test('WHAT[DG-003] LOOP_BOUNDS_empty_and_invalid', () => {
  assert.throws(() => envelopeBounds([], 0.5), { message: 'Loop detector envelope has no samples' })
  assert.throws(() => envelopeBounds([1, 2, 3], 0), { message: 'Loop detector envelope has invalid probability' })
  assert.throws(() => envelopeBounds([1, 2, 3], -0.2), { message: 'Loop detector envelope has invalid probability' })
  assert.throws(() => envelopeBounds([1, 2, 3], 1.5), { message: 'Loop detector envelope has invalid probability' })
})

test('WHAT[DG-003] LOOP_WORKER_failure_terminates', async () => {
  const fixture = [
    'class Alpha {',
    '  run() { return 1; }',
    '}',
    'class Beta {',
    '  compute() { return 2; }',
    '}',
    'let value = 12345;',
  ].join('\n')

  const spawned = new Set()
  let count = 0
  const failingWorkerFactory = (src, opts) => {
    count += 1
    const current = count
    let worker
    if (current === 2) {
      worker = new Worker('process.exit(42)', { eval: true })
    } else {
      worker = new Worker(src, opts)
    }
    spawned.add(worker)
    return worker
  }

  await assert.rejects(
    () => encodeParallel(fixture, 4, { workerFactory: failingWorkerFactory }),
    (err) => {
      assert.match(err.message, /loop detector tokenize worker exited with 42/)
      return true
    },
  )

  await new Promise((resolve) => setTimeout(resolve, 50))
  assert.ok(spawned.size > 0, 'workers must have been spawned')
  for (const worker of spawned) {
    assert.equal(worker.threadId, -1, 'every spawned worker must be terminated')
  }
})
