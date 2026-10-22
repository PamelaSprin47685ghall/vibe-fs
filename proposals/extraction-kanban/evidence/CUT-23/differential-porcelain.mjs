/**
 * CUT-23 ①: Git porcelain readers, old host code vs a candidate.
 * Usage: node differential-porcelain.mjs <base-dist> <candidate-dist> [cases]
 * Drives WorktreeCommands.list / listBranches / rebaseInProgress of both dists with
 * the same fake Git runner over a seeded corpus of LF porcelain outputs and compares the
 * results. A CRLF corpus is reported separately: the old reader split on CR too.
 */
import path from 'node:path'
import { mkdtempSync, mkdirSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { pathToFileURL } from 'node:url'

const load = async (dist) => import(pathToFileURL(path.resolve(dist, 'Git/WorktreeResource.js')).href)
const [baseDist, candDist, n = '20000'] = process.argv.slice(2)
const base = await load(baseDist)
const cand = await load(candDist)
const listOf = (m) => m.WorktreeCommands_list ?? m.WorktreeCommands_list_Z
let seed = 0x2323
const rnd = (k) => (((seed = (Math.imul(seed, 1103515245) + 12345) & 0x7fffffff) >>> 16) % k)
const pick = (xs) => xs[rnd(xs.length)]

function listing() {
  const records = []
  for (let i = 0, k = rnd(5); i < k; i++) {
    const r = []
    if (rnd(8) !== 0) r.push('worktree ' + pick(['/repo', '/repo/.wt/j' + i, '/tmp/a b', ' /pad ']))
    r.push('HEAD ' + '0123456789abcdef'.repeat(2) + '01234567')
    const b = rnd(4)
    if (b === 0) r.push('detached')
    else if (b === 1) r.push('bare')
    else r.push('branch ' + pick(['refs/heads/main', 'refs/heads/manager/j' + i, 'refs/heads/feature/x', 'refs/heads/manager/']))
    if (rnd(5) === 0) r.push('locked reason')
    if (rnd(6) === 0) r.push('prunable gitdir file points to non-existent location')
    records.push(r.join('\n'))
  }
  return records.join('\n\n') + pick(['', '\n', '\n\n'])
}

function branchList() {
  const lines = []
  for (let i = 0, k = rnd(6); i < k; i++) lines.push(pick(['  ', '* ', '+ ', '']) + pick(['main', 'manager/j' + i, 'manager/a+b', 'x']) + pick(['', ' ']))
  return lines.join(pick(['\n', '\n\n'])) + pick(['', '\n'])
}

const runnerFor = (stdout, code = 0) => () => Promise.resolve([code, stdout, ''])
const show = (r) => JSON.stringify(r, (k, v) => (v && typeof v === 'object' && 'fields' in v && 'tag' in v ? { tag: v.tag, fields: v.fields } : v))

let compared = 0, differing = 0, crlfDiffering = 0
const samples = []
for (let i = 0; i < Number(n); i++) {
  const out = listing()
  const a = show(await listOf(base)(runnerFor(out), '/repo', undefined))
  const b = show(await listOf(cand)(runnerFor(out), '/repo', undefined))
  compared++
  if (a !== b) { differing++; if (samples.length < 5) samples.push({ out, a, b }) }
  const crlf = out.replaceAll('\n', '\r\n')
  const c = show(await listOf(cand)(runnerFor(crlf), '/repo', undefined))
  if (c !== b) crlfDiffering++
  const bl = branchList()
  const x = show(await base.WorktreeCommands_listBranches(runnerFor(bl), '/repo', undefined))
  const y = show(await cand.WorktreeCommands_listBranches(runnerFor(bl), '/repo', undefined))
  compared++
  if (x !== y) { differing++; if (samples.length < 5) samples.push({ bl, x, y }) }
}

// CRLF counterexample on the old reader (record closed after its `worktree` line).
const crlfCase = 'worktree /repo\r\nHEAD 1\r\nbranch refs/heads/main\r\n\r\nworktree /repo/.wt/j1\r\nHEAD 2\r\nbranch refs/heads/manager/j1\r\n'
const crlf = {
  base: JSON.parse(show(await listOf(base)(runnerFor(crlfCase), '/repo', undefined))),
  candidate: JSON.parse(show(await listOf(cand)(runnerFor(crlfCase), '/repo', undefined))),
}

// rebaseInProgress: candidate only (the old one was private in two copies); real dirs.
let rebase = null
if (cand.WorktreeCommands_rebaseInProgress) {
  const d = mkdtempSync(path.join(tmpdir(), 'cut23-'))
  mkdirSync(path.join(d, 'rebase-merge'))
  const probe = (merge, apply) => (cmd) => Promise.resolve([...cmd.Arguments].at(-1) === 'rebase-merge' ? merge : apply)
  rebase = {
    mergeDirExists: await cand.WorktreeCommands_rebaseInProgress(probe([0, path.join(d, 'rebase-merge') + '\n', ''], [0, path.join(d, 'rebase-apply'), '']), d),
    neitherExists: await cand.WorktreeCommands_rebaseInProgress(probe([0, path.join(d, 'nope'), ''], [0, path.join(d, 'rebase-apply'), '']), d),
    revParseFailed: await cand.WorktreeCommands_rebaseInProgress(probe([128, path.join(d, 'rebase-merge'), ''], [128, '', '']), d),
  }
  rmSync(d, { recursive: true, force: true })
}

console.log(JSON.stringify({ compared, differing, crlfCandidateDiffersFromLf: crlfDiffering, crlf, rebase, samples }, null, 2))
if (differing !== 0) process.exit(1)
