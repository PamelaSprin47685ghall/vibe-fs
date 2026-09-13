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

test('WHAT[DG-004] LOOP_004_repository_corpus_contains_normal_source_documents_only', () => {
  const files = loopDetectorRepositoryInputFiles().map((file) => file.replaceAll('\\', '/'))

  assert.ok(files.every(path.isAbsolute), 'selector must return filesystem paths')
  assert.ok(files.some((file) => file.endsWith('/src/Wanxiangshu/Execution/Session/LoopDetector.fs')))
  assert.ok(files.some((file) => file.endsWith('/requirements/degeneration-guard/WHAT.md')))
  assert.ok(!files.some((file) => file.endsWith('/package-lock.json')))
  assert.ok(!files.some((file) => file.endsWith('/scripts/checks/semantic-owners.json')))
  assert.ok(!files.some((file) => file.endsWith('/docs/index.html')))
})

test('WHAT[DG-004] LOOP_004_repository_corpus_excludes_tracked_paths_deleted_from_the_worktree', () => {
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
test('WHAT[DG-004] LOOP_004_runtime_envelope_reads_every_selected_repository_input_through_the_tracking_reader', async () => {
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

test('WHAT[DG-003] LOOP_003_push_text_is_o200k_token_based', () => {
  const text = 'const π = await repository.load("订单-42");\nreturn { ok: true, revision: 17 };'
  const expected = referenceScore(text)
  const result = loopDetector.pushText(loopDetector.create(), text)

  assert.equal(result.step, encode(text).length)
  assert.equal(result.step, expected.step)
  close(result.weightedDistinctTokens, expected.weightedDistinctTokens)
})

test('WHAT[DG-001] LOOP_003_single_token_repetition_becomes_too_repetitive', () => {
  const unit = ' retry'
  assert.equal(encode(unit).length, 1, 'fixture must be one o200k token')

  const result = loopDetector.pushText(loopDetector.create(), unit.repeat(1000))
  assert.equal(result.isAnomalous, true, `weightedDistinct=${result.weightedDistinctTokens}`)
  assert.equal(result.state, 'TooRepetitive')
  assert.ok(result.weightedDistinctTokens < loopDetector.minimumWeightedDistinctCount)
})

test('WHAT[DG-001] LOOP_003_repository_like_programmatic_text_stays_normal', () => {
  const body = `
export class OrderProcessor {
  constructor(private readonly repository: OrderRepository, private readonly paymentGateway: PaymentGateway) {}
  async processOrder(orderId: string, user: UserContext): Promise<OrderResult> {
    const order = await this.repository.findById(orderId);
    if (!order) throw new EntityNotFoundError("Order", orderId);
    const authorization = await this.paymentGateway.authorize({ amount: order.totalAmount, currency: order.currency });
    if (!authorization.approved) return { success: false, reason: authorization.declineReason };
    return { success: true, order: await this.repository.finalizeOrder(orderId, authorization.transactionId) };
  }
}
`

  const result = loopDetector.pushText(loopDetector.create(), body)
  assert.equal(result.isAnomalous, false, `weightedDistinct=${result.weightedDistinctTokens}`)
  assert.equal(result.state, 'Normal')
})

test('WHAT[DG-005] LOOP_005_empty_push_is_noop', () => {
  const detector = loopDetector.create()
  const before = loopDetector.evaluate(detector)
  const after = loopDetector.pushText(detector, '')
  assert.deepEqual(after, before)
})

test('WHAT[DG-002] LOOP_009_text_and_reasoning_delta_decode_fail_closed', () => {
  assert.equal(loopDetector.tryDecodeTextDelta({ type: 'session.status' }), null)

  for (const field of ['text', 'reasoning', 'model_thought', 'thinking', 'reasoning_content']) {
    assert.deepEqual(loopDetector.tryDecodeTextDelta({
      type: 'message.part.delta',
      properties: {
        sessionID: 'ses_loop',
        messageID: 'msg_a',
        partID: 'prt_1',
        field,
        delta: 'zzzz',
      },
    }), {
      sessionId: 'ses_loop',
      messageId: 'msg_a',
      partId: 'prt_1',
      field,
      delta: 'zzzz',
    })
  }

  for (const field of ['tool', 'tool_call', 'custom_metadata']) {
    assert.equal(loopDetector.tryDecodeTextDelta({
      type: 'message.part.delta',
      properties: {
        sessionID: 'ses_loop',
        messageID: 'msg_a',
        partID: 'prt_1',
        field,
        delta: 'zzzz',
      },
    }), null)
  }
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
