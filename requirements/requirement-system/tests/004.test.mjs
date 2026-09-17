import assert from 'node:assert/strict'
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')

const REQUIREMENTS = join(ROOT, 'requirements')

const INDEX_FILE = join(ROOT, 'requirements/INDEX.md')

const TREE_ENTRY = join(REQUIREMENTS, 'README.md')

const read = (path) => readFileSync(path, 'utf8')

const REQUIRED_DOCS = ['WHY.md', 'WHAT.md', 'HOW.md']

const packageNamesFromTreeEntry = () => {
  const text = read(TREE_ENTRY)
  const names = [...text.matchAll(/\]\(([a-z][a-z0-9-]*)(?:\/(?:WHAT|WHY|README)\.md|\/)?\)/g)].map((m) => m[1])
  return [...new Set(names)]
}

const packageNamesFromIndexTables = () => {
  const text = read(INDEX_FILE)
  const names = []
  for (const line of text.split('\n')) {
    if (!line.startsWith('| ')) continue
    const name = /`([a-z][a-z0-9-]*)`/.exec(line)?.[1]
    if (name) names.push(name)
  }
  return [...new Set(names)]
}

const dependencySkeleton = () => {
  const text = read(INDEX_FILE)
  const heading = text.indexOf('# 依赖骨架')
  assert.ok(heading >= 0, 'INDEX.md must contain a "# 依赖骨架" section')
  const fenceStart = text.indexOf('```', heading)
  const fenceEnd = text.indexOf('```', fenceStart + 3)
  assert.ok(fenceStart >= 0 && fenceEnd >= 0, 'dependency skeleton must live in a fenced code block')
  const block = text.slice(fenceStart + 3, fenceEnd)
  const edges = new Map() // pkg -> Set<dep>
  for (const line of block.split('\n')) {
    const match = /^([a-z][a-z0-9-]*)\s*→\s*(.+)$/.exec(line.trim())
    if (!match) continue
    const [pkg, rhs] = [match[1], match[2]]
    const deps = new Set()
    for (const name of packageNamesFromIndexTables()) {
      if (name === pkg) continue
      if (new RegExp(`\\b${name}\\b`).test(rhs)) deps.add(name)
    }
    edges.set(pkg, deps)
  }
  return edges
}

const DEPENDS_ON_TRIGGER = /^\s*(?:#+\s*|\*\*\s*)?DEPENDS\s+ON\b/i

const declaredDependencies = (pkg, docRel, allNames) => {
  const filePath = join(REQUIREMENTS, pkg, docRel)
  if (!existsSync(filePath)) return new Set()
  const text = read(filePath)
  const declared = new Set()
  let collecting = false
  for (const line of text.split('\n')) {
    if (collecting) {
      if (line.trim() === '' || /^\s*#/.test(line)) collecting = false
      else collectNames(line, allNames, pkg, declared)
      continue
    }
    if (DEPENDS_ON_TRIGGER.test(line)) {
      collecting = true
      collectNames(line, allNames, pkg, declared)
    }
  }
  return declared
}

const collectNames = (line, allNames, self, out) => {
  for (const name of allNames) {
    if (name === self) continue
    if (new RegExp(`\\b${name}\\b`).test(line)) out.add(name)
  }
}

const landingFileTokens = (row) => {
  const cells = row.split('|').map((cell) => cell.trim())
  const landing = cells[2] ?? ''
  return [...landing.matchAll(/(?:requirements\/|tests\/|scripts\/)[\w./-]+\.(?:test\.mjs|mjs)/g)].map(
    (m) => m[0],
  )
}

const resolveLanding = (pkg, token) => {
  const repo = join(ROOT, token)
  if (existsSync(repo)) return repo
  if (token.startsWith('tests/')) {
    const local = join(REQUIREMENTS, pkg, token)
    if (existsSync(local)) return local
  }
  return repo
}

const docFailures = (pkg) => {
  const failures = []
  for (const doc of REQUIRED_DOCS) {
    if (!existsSync(join(REQUIREMENTS, pkg, doc))) failures.push(`${pkg}: missing ${doc}`)
  }
  return failures
}

const proofFailures = (pkg) => {
  const failures = []
  const howText = read(join(REQUIREMENTS, pkg, 'HOW.md'))
  for (const line of howText.split('\n')) {
    if (!line.startsWith('|')) continue
    for (const token of landingFileTokens(line)) {
      const resolved = resolveLanding(pkg, token)
      if (!existsSync(resolved)) {
        failures.push(`${pkg}: HOW landing file missing: ${token}`)
      }
    }
  }
  return failures
}

const depFailures = (pkg, allNames, skeleton) => {
  const failures = []
  for (const doc of ['WHY.md', 'WHAT.md', 'HOW.md']) {
    const declared = declaredDependencies(pkg, doc, allNames)
    const allowed = skeleton.get(pkg) ?? new Set()
    for (const dep of declared) {
      if (!allowed.has(dep)) {
        failures.push(`${pkg}: ${doc} declares DEPENDS ON ${dep}, but the INDEX skeleton has no such edge (allowed: ${[...allowed].join(', ') || '无'})`)
      }
    }
  }
  return failures
}

test('WHAT[REQUIREMENT-SYSTEM-004] declared proof rows name live landing files', () => {
  const dirs = readdirSync(REQUIREMENTS)
    .filter((entry) => statSync(join(REQUIREMENTS, entry)).isDirectory())
    .sort()
  const migrated = dirs.filter((pkg) => REQUIRED_DOCS.every((doc) => existsSync(join(REQUIREMENTS, pkg, doc))))

  assert.ok(migrated.length >= 2, 'requirement-system and verification-system must be structurally complete now')

  const failures = []
  for (const pkg of migrated) {
    failures.push(...proofFailures(pkg))
  }
  assert.deepEqual(
    failures,
    [],
    'declared proof rows must name live landing files:\n' + failures.join('\n'),
  )
})
