import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, rmSync, unlinkSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import path from 'node:path'
import test from 'node:test'

import {
  deriveLoopDetectorEnvelope,
  loadLoopDetectorRepositoryCorpusV1,
} from '../../../scripts/lib/derive-loop-detector-envelope.mjs'
import {
  loopDetectorRepositoryInputFiles,
} from '../../../scripts/lib/loop-detector-repository-corpus.mjs'

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
