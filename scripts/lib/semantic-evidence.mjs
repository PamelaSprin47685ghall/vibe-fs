import { isAbsolute, join, relative, resolve } from 'node:path'

const norm = (value) => String(value).replace(/\\/g, '/')
const exactProofKeys = 'path,title,what_id'

const finding = (entry, reason) => ({
  code: 'invalid-semantic-evidence-metadata',
  path: entry?.path,
  message: `${entry?.path ?? ''}: semantic-evidence ${reason}`,
})

export function validateSemanticEvidenceProof(entry, traceGraph, repositoryRoot) {
  if (entry?.kind !== 'semantic-evidence') return null

  const proof = entry.proof
  if (
    proof === null
    || typeof proof !== 'object'
    || Array.isArray(proof)
    || Object.keys(proof).sort().join(',') !== exactProofKeys
    || typeof proof.path !== 'string'
    || typeof proof.title !== 'string'
    || typeof proof.what_id !== 'string'
  ) return finding(entry, 'needs exact proof {path,title,what_id}')

  const segments = norm(proof.path).split('/')
  const expectedPrefix = `requirements/${entry.owner}/tests/`
  const normalizedPath = norm(proof.path)
  const absolutePath = resolve(repositoryRoot, normalizedPath)
  const relativePath = norm(relative(repositoryRoot, absolutePath))
  if (
    proof.path !== normalizedPath
    || isAbsolute(proof.path)
    || segments.includes('..')
    || segments.includes('.')
    || relativePath === '..'
    || relativePath.startsWith('../')
    || relativePath !== normalizedPath
    || !normalizedPath.startsWith(expectedPrefix)
    || !normalizedPath.endsWith('.test.mjs')
  ) return finding(entry, 'proof path must be one exact owner test inside the repository')

  const law = `WHAT[${proof.what_id}]`
  if (entry.law !== law || !/^WHAT\[[A-Z][A-Z0-9-]*-\d{3}\]$/.test(law)) {
    return finding(entry, 'law and proof what_id must be the same canonical WHAT')
  }

  const definition = traceGraph?.whats?.get(proof.what_id)
  if (!definition || definition.package !== entry.owner) {
    return finding(entry, 'WHAT must have one definition owned by the contract owner')
  }

  const expectedHow = resolve(repositoryRoot, 'requirements', entry.owner, 'HOW.md')
  const graphPath = (value) => isAbsolute(value) ? resolve(value) : resolve(repositoryRoot, value)
  const matches = (traceGraph?.proofEdges ?? []).filter((edge) =>
    edge.state === 'active'
    && !edge.reason
    && edge.whatId === proof.what_id
    && graphPath(edge.file) === absolutePath
    && edge.title === proof.title
    && graphPath(edge.proofFile) === expectedHow)

  return matches.length === 1
    ? null
    : finding(entry, 'proof must resolve to one active, unrejected requirement-trace edge')
}

export function validatedSemanticEvidenceContracts(contracts, traceGraph, repositoryRoot) {
  const valid = []
  const findings = []
  for (const entry of contracts ?? []) {
    const invalid = validateSemanticEvidenceProof(entry, traceGraph, repositoryRoot)
    if (invalid) findings.push(invalid)
    else valid.push(entry)
  }
  return { contracts: valid, findings }
}
