/**
 * G9 / Playbook §24.4 session-ownership matrix ratchet.
 *
 * Pins scripts/checks/session-ownership-ratchet.mjs: every managed session kind
 * must answer owner / reusable / cancel / retire / handle / companion /
 * crashReconcile / evidencePath. Fail closed on missing kind, empty field, or
 * missing evidence file. AttachmentKind token checks stay required.
 */
import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import test from 'node:test'
import {
  MATRIX_FIELDS,
  REQUIRED_ATTACHMENT_TOKENS,
  REQUIRED_KINDS,
  SESSION_OWNERSHIP_MATRIX_REL,
  SESSION_OWNERSHIP_REL,
  loadMatrixFile,
  missingAttachmentTokens,
  relatedEvidenceToken,
  scanMatrix,
  scanRepo,
  scanSessionOwnership,
} from '../../../scripts/checks/session-ownership-ratchet.mjs'

const ROOT = new URL('../../../', import.meta.url).pathname

const makeFixture = () => {
  const dir = mkdtempSync(join(tmpdir(), 'session-ownership-'))
  return {
    dir,
    write: (rel, text) => {
      const file = join(dir, rel)
      mkdirSync(dirname(file), { recursive: true })
      writeFileSync(file, text)
    },
    dispose: () => rmSync(dir, { recursive: true, force: true }),
  }
}

const filledRow = (kind, overrides = {}) => {
  const token = relatedEvidenceToken(kind)
  const evidencePath = overrides.evidencePath ?? `src/${kind.replaceAll(' ', '-')}.fs`
  return {
    owner: `owner of ${kind}`,
    reusable: `reusable for ${kind}`,
    cancel: `cancel of ${kind}`,
    retire: `retire of ${kind}`,
    handle: `handle of ${kind}`,
    companion: `companion of ${kind}`,
    crashReconcile: `crash reconcile of ${kind}`,
    evidencePath,
    ...overrides,
    _token: token,
  }
}

const filledMatrix = (kindOverrides = {}, evidenceFor = {}) => {
  const kinds = {}
  for (const kind of REQUIRED_KINDS) {
    const row = filledRow(kind, kindOverrides[kind] ?? {})
    delete row._token
    kinds[kind] = row
    if (evidenceFor[kind] === undefined) {
      evidenceFor[kind] = relatedEvidenceToken(kind)
    }
  }
  return { kinds, evidenceFor }
}

const writeEvidence = (fx, kinds, evidenceFor) => {
  for (const kind of Object.keys(kinds)) {
    const rel = kinds[kind].evidencePath
    if (typeof rel !== 'string' || rel.length === 0) continue
    const token = evidenceFor[kind] ?? relatedEvidenceToken(kind)
    fx.write(rel, `${token}\n`)
  }
}

test('WHAT[SESSION-ONTOLOGY-001] session_ownership_ratchet_documents_closed_kind_set', () => {
  assert.deepEqual([...REQUIRED_KINDS], [
    'Companion',
    'SyncInspector',
    'SyncCoder',
    'Bookkeeper',
    'hidden Reviewer',
    'StrengthReplica',
    'fork agent',
    'Distiller child',
  ])
  assert.equal(relatedEvidenceToken('hidden Reviewer'), 'Reviewer')
  assert.equal(relatedEvidenceToken('fork agent'), 'Fork')
  assert.equal(relatedEvidenceToken('Distiller child'), 'Distiller')
  assert.equal(relatedEvidenceToken('Bookkeeper'), 'Bookkeeper')
  assert.equal(SESSION_OWNERSHIP_MATRIX_REL, 'scripts/checks/session-ownership-matrix.json')
})

test('WHAT[SESSION-ONTOLOGY-002] session_ownership_ratchet_questionnaire_requires_owner_field', () => {
  assert.deepEqual([...MATRIX_FIELDS], [
    'owner',
    'reusable',
    'cancel',
    'retire',
    'handle',
    'companion',
    'crashReconcile',
    'evidencePath',
  ])
})

test('WHAT[SESSION-ONTOLOGY-011] session_ownership_ratchet_attachment_tokens_include_strength_replica', () => {
  assert.deepEqual([...REQUIRED_ATTACHMENT_TOKENS], [
    'Companion',
    'SyncInspector',
    'SyncCoder',
    'Bookkeeper',
    'StrengthReplica',
  ])
})

test('WHAT[SESSION-ONTOLOGY-001] session_ownership_attachment_tokens_require_surface', () => {
  const good = `
type AttachmentKind =
    | Companion
    | SyncInspector
    | SyncCoder
    | Bookkeeper of transactionId: string
    | StrengthReplica
`
  assert.equal(scanSessionOwnership(good).ok, true)
  assert.equal(missingAttachmentTokens(good).length, 0)

  const missing = scanSessionOwnership('type AttachmentKind =\n    | Companion\n')
  assert.equal(missing.ok, false)
  assert.ok(missing.missing.includes('SyncInspector'))
  assert.ok(missing.missing.includes('Bookkeeper'))
  assert.ok(missing.missing.includes('StrengthReplica'))
})

test('WHAT[SESSION-ONTOLOGY-002] session_ownership_matrix_green_fixture', (t) => {
  const fx = makeFixture()
  t.after(fx.dispose)
  const { kinds, evidenceFor } = filledMatrix()
  writeEvidence(fx, kinds, evidenceFor)
  const result = scanMatrix({ kinds }, { repoRoot: fx.dir })
  assert.equal(result.ok, true, JSON.stringify(result.failures, null, 2))
  assert.equal(result.failures.length, 0)
})

test('WHAT[SESSION-ONTOLOGY-011] session_ownership_matrix_strength_replica_row_answers_owner', () => {
  const loaded = loadMatrixFile(join(ROOT, SESSION_OWNERSHIP_MATRIX_REL))
  assert.equal(loaded.ok, true)
  const row = loaded.matrix.kinds.StrengthReplica
  assert.equal(typeof row.owner, 'string')
  assert.ok(row.owner.trim().length > 0)
  assert.match(row.owner, /At most one active replica per owner/)
})

test('WHAT[SESSION-ONTOLOGY-014] session_ownership_matrix_missing_kind_fails_closed', (t) => {
  const fx = makeFixture()
  t.after(fx.dispose)
  const { kinds, evidenceFor } = filledMatrix()
  delete kinds.Bookkeeper
  writeEvidence(fx, kinds, evidenceFor)
  const result = scanMatrix({ kinds }, { repoRoot: fx.dir })
  assert.equal(result.ok, false)
  assert.ok(result.failures.some((f) => f.code === 'missing-kind' && f.kind === 'Bookkeeper'))
})

test('WHAT[SESSION-ONTOLOGY-002] session_ownership_matrix_empty_field_fails_closed', (t) => {
  const fx = makeFixture()
  t.after(fx.dispose)
  const { kinds, evidenceFor } = filledMatrix({
    Companion: { cancel: '   ' },
  })
  writeEvidence(fx, kinds, evidenceFor)
  const result = scanMatrix({ kinds }, { repoRoot: fx.dir })
  assert.equal(result.ok, false)
  assert.ok(
    result.failures.some(
      (f) => f.code === 'empty-field' && f.kind === 'Companion' && f.field === 'cancel',
    ),
  )
})

test('WHAT[SESSION-ONTOLOGY-014] session_ownership_matrix_missing_evidence_file_fails_closed', (t) => {
  const fx = makeFixture()
  t.after(fx.dispose)
  const { kinds, evidenceFor } = filledMatrix()
  writeEvidence(fx, kinds, evidenceFor)
  kinds['fork agent'].evidencePath = 'src/DoesNotExist.fs'
  const result = scanMatrix({ kinds }, { repoRoot: fx.dir })
  assert.equal(result.ok, false)
  assert.ok(
    result.failures.some(
      (f) => f.code === 'missing-evidence-file' && f.kind === 'fork agent',
    ),
  )
})

test('WHAT[SESSION-ONTOLOGY-011] session_ownership_matrix_evidence_without_token_fails_closed', (t) => {
  const fx = makeFixture()
  t.after(fx.dispose)
  const { kinds, evidenceFor } = filledMatrix()
  evidenceFor.StrengthReplica = 'UnrelatedToken'
  writeEvidence(fx, kinds, evidenceFor)
  const result = scanMatrix({ kinds }, { repoRoot: fx.dir })
  assert.equal(result.ok, false)
  assert.ok(
    result.failures.some(
      (f) =>
        f.code === 'missing-evidence-token' &&
        f.kind === 'StrengthReplica' &&
        f.token === 'StrengthReplica',
    ),
  )
})

test('WHAT[SESSION-ONTOLOGY-002] session_ownership_matrix_rejects_special_pleading', (t) => {
  const fx = makeFixture()
  t.after(fx.dispose)
  const { kinds, evidenceFor } = filledMatrix({
    Bookkeeper: { owner: 'this one is special because G6' },
  })
  writeEvidence(fx, kinds, evidenceFor)
  const result = scanMatrix({ kinds }, { repoRoot: fx.dir })
  assert.equal(result.ok, false)
  assert.ok(result.failures.some((f) => f.code === 'special-pleading' && f.kind === 'Bookkeeper'))
})

test('WHAT[SESSION-ONTOLOGY-014] session_ownership_matrix_rejects_unexpected_kind', (t) => {
  const fx = makeFixture()
  t.after(fx.dispose)
  const { kinds, evidenceFor } = filledMatrix()
  kinds.Teacher = filledRow('Teacher', { evidencePath: 'src/Teacher.fs' })
  delete kinds.Teacher._token
  writeEvidence(fx, kinds, evidenceFor)
  const result = scanMatrix({ kinds }, { repoRoot: fx.dir })
  assert.equal(result.ok, false)
  assert.ok(result.failures.some((f) => f.code === 'unexpected-kind' && f.kind === 'Teacher'))
})

test('WHAT[SESSION-ONTOLOGY-014] session_ownership_matrix_invalid_document_fails_closed', () => {
  assert.equal(scanMatrix(null).ok, false)
  assert.equal(scanMatrix([]).ok, false)
  assert.ok(scanMatrix({}).failures.some((f) => f.code === 'invalid-matrix'))
})

test('WHAT[SESSION-ONTOLOGY-014] session_ownership_repo_scan_is_green', () => {
  const result = scanRepo(ROOT)
  assert.equal(result.ok, true, JSON.stringify({
    attachment: result.attachment,
    matrix: result.matrix,
  }, null, 2))
  assert.equal(result.matrixPath, SESSION_OWNERSHIP_MATRIX_REL)
  assert.equal(result.attachment.ok, true)
  assert.equal(result.matrix.failures.length, 0)

  const loaded = loadMatrixFile(join(ROOT, SESSION_OWNERSHIP_MATRIX_REL))
  assert.equal(loaded.ok, true)
  for (const kind of REQUIRED_KINDS) {
    assert.ok(loaded.matrix.kinds[kind], kind)
    for (const field of MATRIX_FIELDS) {
      assert.equal(typeof loaded.matrix.kinds[kind][field], 'string', `${kind}.${field}`)
      assert.ok(loaded.matrix.kinds[kind][field].trim().length > 0, `${kind}.${field}`)
    }
  }
  assert.equal(
    loaded.matrix.kinds.Bookkeeper.evidencePath,
    'src/Wanxiangshu/Repository/Knowledge/Casebook/BookkeeperRuntime.fs',
  )
})
