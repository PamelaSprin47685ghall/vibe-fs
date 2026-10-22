// WHAT[host-boundary-035] gate ⑬ (O-3): the omp entry module holds no product decision —
// every forbidden class (prose, product import, decision control flow, mutable state,
// decision vocabulary, product resource lookup, runtime product lookup) is rejected on a
// planted violation, and the real entry module and its interface are accepted.
import test from 'node:test'

{
const { default: assert } = await import('node:assert/strict')
const { mkdtempSync, mkdirSync, writeFileSync, rmSync, readFileSync } = await import('node:fs')
const { tmpdir } = await import('node:os')
const { join, dirname } = await import('node:path')
const { fileURLToPath } = await import('node:url')

const ROOT = dirname(fileURLToPath(import.meta.url))
const REPO = join(ROOT, '../../..')
const GATE = join(REPO, 'scripts/checks/omp-entry-decision-gate.mjs')
const {
  analyzeEntryModule,
  analyzeEntrySource,
  locateEntryModule,
  selfTest,
  PLANTED_VIOLATIONS,
  ENTRY_FOLDER,
  isAllowedNamespace,
} = await import(GATE)

/** Materialize a planted entry module under a scratch repository root. */
const plantedRoot = (source) => {
  const root = mkdtempSync(join(tmpdir(), 'omp-entry-gate-'))
  const folder = join(root, ENTRY_FOLDER)
  mkdirSync(folder, { recursive: true })
  writeFileSync(join(folder, 'OmpExtension.fs'), `${source}\n`)
  return root
}

test('WHAT[host-boundary-035] the real omp entry module carries no product decision', () => {
  const located = locateEntryModule(REPO)
  assert.equal(located.ok, true, located.reason)
  const issues = analyzeEntryModule(REPO)
  assert.deepEqual(issues, [], issues.map((issue) => `${issue.path}:${issue.line} ${issue.message}`).join('\n'))
})

test('WHAT[host-boundary-035] every planted product decision is rejected by its own forbidden class', () => {
  for (const planted of PLANTED_VIOLATIONS) {
    const root = plantedRoot(planted.source)
    try {
      const issues = analyzeEntryModule(root)
      const codes = issues.map((issue) => issue.code)
      assert.ok(
        codes.includes(planted.code),
        `planted '${planted.label}' was not rejected by ${planted.code}; got ${codes.join(', ') || 'no issue'}`,
      )
    } finally {
      rmSync(root, { recursive: true, force: true })
    }
  }
})

test('WHAT[host-boundary-035] time wiring permits only the exact neutral contracts and physical factory', () => {
  for (const name of [
    'Wanxiangshu.Foundation.IClockPort',
    'Wanxiangshu.Foundation.ITimerPort',
    'Wanxiangshu.Process.NodeTiming.nodeClockPort',
    'Wanxiangshu.Process.NodeTiming.nodeTimerPort',
  ]) assert.equal(isAllowedNamespace(name), true, name)

  for (const name of [
    'Wanxiangshu.Foundation',
    'Wanxiangshu.Foundation.OtherPort',
    'Wanxiangshu.Process',
    'Wanxiangshu.Process.VirtualTiming',
    'Wanxiangshu.Process.JsCallLoop',
  ]) {
    assert.equal(isAllowedNamespace(name), false, name)
    assert.ok(analyzeEntrySource(`open ${name}\n`, 'entry.fs')
      .some(issue => issue.code === 'omp-entry-product-import'), name)
  }
})

test('WHAT[host-boundary-035] factory wiring does not permit ambient clocks timers or budgets', () => {
  for (const source of [
    'let now () = System.DateTimeOffset.UtcNow',
    'let now () = System.DateTime.UtcNow',
    '[<Emit("Date.now()")>] let now () = jsNative',
    '[<Emit("setTimeout($0, 1)")>] let schedule callback = jsNative',
  ]) assert.ok(analyzeEntrySource(source, 'entry.fs')
    .some(issue => issue.code === 'omp-entry-ambient-time'), source)

  assert.ok(analyzeEntrySource('let budget = 5000', 'entry.fs')
    .some(issue => issue.code === 'omp-entry-decision-vocabulary'))
})

test('WHAT[host-boundary-035] a planted admission decision in the real entry module is rejected', () => {
  const root = mkdtempSync(join(tmpdir(), 'omp-entry-gate-real-'))
  try {
    const folder = join(root, ENTRY_FOLDER)
    mkdirSync(folder, { recursive: true })
    const real = locateEntryModule(REPO)
    writeFileSync(join(folder, 'OmpExtension.fs'), readFileSync(real.implementation, 'utf8'))
    writeFileSync(
      join(folder, 'OmpExtension.fs'),
      '\n    let refuse (pi: obj) = if pi = null then "This turn is not admitted; please retry later." else "denied"\n',
      { flag: 'a' },
    )
    const issues = analyzeEntryModule(root)
    const codes = issues.map((issue) => issue.code)
    assert.ok(codes.includes('omp-entry-prose'), `expected prose rejection, got ${codes.join(', ') || 'no issue'}`)
    assert.ok(codes.includes('omp-entry-decision-control-flow'), `expected control-flow rejection, got ${codes.join(', ') || 'no issue'}`)
    assert.ok(codes.includes('omp-entry-decision-vocabulary'), `expected decision-vocabulary rejection, got ${codes.join(', ') || 'no issue'}`)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})

test('WHAT[host-boundary-035] the gate refuses to pass when the entry module or its export site is missing', () => {
  const root = mkdtempSync(join(tmpdir(), 'omp-entry-gate-empty-'))
  try {
    const issues = analyzeEntryModule(root)
    assert.equal(issues.length, 1)
    assert.equal(issues[0].code, 'omp-entry-missing')
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})

test('WHAT[host-boundary-035] the gate self-test proves every planted class and accepts the real module', () => {
  const tmpDir = mkdtempSync(join(tmpdir(), 'omp-entry-gate-self-'))
  try {
    const result = selfTest({ root: REPO, tmpDir })
    assert.deepEqual(result.failures, [])
    assert.equal(result.ok, true)
    assert.ok(result.lines.some((line) => line.includes('real entry module -> PASS (accepted)')))
  } finally {
    rmSync(tmpDir, { recursive: true, force: true })
  }
})
}
