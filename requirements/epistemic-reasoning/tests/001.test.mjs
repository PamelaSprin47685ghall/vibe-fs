import assert from 'node:assert/strict'
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs'
import { dirname, join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

// This package is SUPERSEDED by `sphinx-v2` (see `../sphinx-v2/SUPERSEDES.md`).
// Its 36 propositions described the Sphinx kernel that shipped before the
// 2026-09-23 clean-break; they are retained as historical record and design
// provenance, and are not acceptance obligations for the current tree.
//
// The executable proof for the Sphinx surface now lives in `../sphinx-v2/tests/`,
// which covers the same laws under the new ids. This test keeps the historical
// package honest: it asserts the supersede record exists, is complete enough to
// map every old proposition, and does not claim the old kernel still runs.

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const read = (path) => readFileSync(join(ROOT, path), 'utf8')

const oldWhat = read('requirements/epistemic-reasoning/WHAT.md')
const supersedes = read('requirements/sphinx-v2/SUPERSEDES.md')

const oldPropositionIds = () => {
  const ids = []
  for (const match of oldWhat.matchAll(/^## \[(\d{3})\]/gm)) ids.push(match[1])
  return ids
}

test('WHAT[epistemic-reasoning-001] superseded package keeps its WHAT and WHY documents', () => {
  assert.ok(oldWhat.includes('SUPERSEDED'), 'WHAT.md must state that this package is superseded')
  assert.ok(read('requirements/epistemic-reasoning/WHY.md').includes('SUPERSEDED'), 'WHY.md must state the same')
})

test('WHAT[epistemic-reasoning-001] every superseded proposition has a recorded disposition', () => {
  // The old numbering runs 001..036. Each must appear in the supersede mapping as
  // either a replacement target or an explicit exit from the default build.
  const ids = oldPropositionIds()
  assert.ok(ids.length >= 36, `expected at least 36 historical propositions, found ${ids.length}`)

  for (const id of ids) {
    const referenced = supersedes.includes(`-${id}`) || supersedes.includes(` ${id} `)
    assert.ok(referenced, `proposition ${id} has no recorded disposition in sphinx-v2/SUPERSEDES.md`)
  }
})

test('WHAT[epistemic-reasoning-001] the old kernel is absent from the production sources', () => {
  // The clean-break removed the whole legacy tree. Nothing in Sphinx/V2 may
  // re-introduce the old types by name.
  const walk = (dir) =>
    readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
      const full = join(dir, entry.name)
      if (entry.isDirectory()) return walk(full)
      return entry.name.endsWith('.fs') ? [full] : []
    })

  const v2Sources = walk(join(ROOT, 'src/Wanxiangshu/Sphinx/V2'))

  const forbidden = [
    'SolverMode',
    'EpistemicState',
    'SessionStore',
    'LegacyPlugin',
    'GecInquiry',
    'GecStore',
    'GecSurface',
    'expectedRootGain',
    'gatewayGain',
    'expectTurns',
    'TurnPrice',
  ]

  for (const source of v2Sources) {
    const text = readFileSync(source, 'utf8')
    for (const token of forbidden) {
      assert.ok(!text.includes(token), `${relative(ROOT, source)} reintroduces the retired ${token}`)
    }
  }
})
