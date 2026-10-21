// WHAT[host-boundary-034] gate ⑩/⑪ adapter-thinness: the baseline write is refused on any violation,
// the port purity scope and the neutral-port vocabulary are discovered from the sources, a per-file
// product-reference growth is reported even when the aggregate total stays flat, and an adapter file that
// is not in the baseline is allowed while the totals stay within the baseline (moves and splits).
import test from 'node:test'

{
const { default: assert } = await import('node:assert/strict')
const { mkdtempSync, mkdirSync, rmSync, writeFileSync, readFileSync } = await import('node:fs')
const { tmpdir } = await import('node:os')
const { join, dirname } = await import('node:path')
const { fileURLToPath } = await import('node:url')
const { spawnSync } = await import('node:child_process')

const ROOT = dirname(fileURLToPath(import.meta.url))
const REPO = join(ROOT, '../../..')
const GATE = join(REPO, 'scripts/checks/adapter-thinness.mjs')
const {
  scanAdapterThinness,
  measure,
  portTypeNames,
  isPortFile,
  baselineWritePlan,
  applyBaselineWrite,
  scanPortFamilies,
} = await import(GATE)

const fixture = () => {
  const dir = mkdtempSync(join(tmpdir(), 'adapter-thinness-'))
  const files = new Map()
  const write = (rel, text) => {
    const abs = join(dir, rel)
    mkdirSync(dirname(abs), { recursive: true })
    writeFileSync(abs, text)
    files.set(rel, text)
    return rel
  }
  const read = (rel) => {
    if (files.has(rel)) return files.get(rel)
    return readFileSync(join(dir, rel), 'utf8')
  }
  return { dir, write, read, files, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

// (a) a violation refuses the baseline write; the baseline file on disk stays byte-identical.
test('WHAT[host-boundary-034] a violation refuses the baseline write and leaves the baseline file untouched', () => {
  const fx = fixture()
  try {
    const baselinePath = join(fx.dir, 'adapter-thinness-baseline.json')
    const baselineText = JSON.stringify({
      _note: 'fixture baseline',
      productReference: { 'src/Wanxiangshu/OpenCode/Adapter.fs': 1 },
      withoutPort: ['src/Wanxiangshu/OpenCode/Adapter.fs'],
    }, null, 2) + '\n'
    writeFileSync(baselinePath, baselineText)
    const before = readFileSync(baselinePath)

    fx.write('src/Wanxiangshu/OpenCode/Adapter.fs', [
      'namespace Wanxiangshu.OpenCode',
      'open Wanxiangshu.Execution.Delegation',
      'open Wanxiangshu.Interaction.Dispatch',
      '',
    ].join('\n'))

    const baseline = JSON.parse(baselineText)
    const { violations } = scanAdapterThinness([...fx.files.keys()], fx.read, { baseline, root: fx.dir })

    assert.ok(violations.length > 0, 'the planted growth must be a violation')
    assert.ok(violations.some((v) => v.gate === 'adapter-thinness'))

    const plan = baselineWritePlan(violations, measure([...fx.files.keys()], fx.read, fx.dir), baseline)
    assert.equal(plan.allowed, false)
    assert.match(plan.reason, /refusing to write/)
    assert.equal(plan.content, null)

    let written = null
    const result = applyBaselineWrite({
      violations,
      measurement: measure([...fx.files.keys()], fx.read, fx.dir),
      prior: baseline,
      write: (content) => { written = content },
    })
    assert.equal(result.written, false)
    assert.equal(written, null, 'the write callback must not be reached on a violation')
    assert.equal(readFileSync(baselinePath).equals(before), true, 'baseline bytes must be unchanged')
  } finally {
    fx.cleanup()
  }
})

// (b) a port family with a name that was never hardcoded is picked up by the purity scope, and its
// signature contributes the neutral-port type names.
test('WHAT[host-boundary-034] a new port family is discovered from the sources and covered by the purity scope', () => {
  const fx = fixture()
  try {
    fx.write('src/Wanxiangshu/Host/Ports/NeutralFission.fs', [
      'namespace Wanxiangshu.Host.Ports',
      'open Wanxiangshu.Execution.Delegation',
      '',
    ].join('\n'))
    fx.write('src/Wanxiangshu/Host/Ports/NeutralFission.fsi', [
      'namespace Wanxiangshu.Host.Ports',
      '',
      'type FissionPort =',
      '    abstract member Fission: unit -> unit',
      '',
    ].join('\n'))

    const files = [...fx.files.keys()]
    assert.equal(isPortFile('src/Wanxiangshu/Host/Ports/NeutralFission.fs'), true)
    assert.equal(isPortFile('src/Wanxiangshu/Host/Ports/NeutralFission.fsi'), true)

    const names = portTypeNames(fx.read, files, fx.dir)
    assert.ok(names.has('FissionPort'), 'the new signature must contribute its type name')

    const { violations } = scanAdapterThinness(files, fx.read, {
      baseline: { productReference: {}, withoutPort: [] },
      root: fx.dir,
    })
    const purity = violations.filter((v) => v.gate === 'port-purity')
    assert.equal(purity.length, 1, 'exactly the new port file violates purity')
    assert.match(purity[0].message, /NeutralFission\.fs/)
    assert.match(purity[0].message, /Execution/)
  } finally {
    fx.cleanup()
  }
})

// (c) a per-file growth is reported even when the aggregate total does not grow.
test('WHAT[host-boundary-034] a per-file product-reference growth is reported even when the total is flat', () => {
  const fx = fixture()
  try {
    fx.write('src/Wanxiangshu/OpenCode/Grown.fs', [
      'namespace Wanxiangshu.OpenCode',
      'open Wanxiangshu.Execution.Delegation',
      'open Wanxiangshu.Interaction.Dispatch',
      'open Wanxiangshu.Mission.Work',
      '',
    ].join('\n'))
    fx.write('src/Wanxiangshu/OpenCode/Shrunk.fs', [
      'namespace Wanxiangshu.OpenCode',
      'open Wanxiangshu.Execution.Delegation',
      '',
    ].join('\n'))

    const baseline = {
      productReference: {
        'src/Wanxiangshu/OpenCode/Grown.fs': 2,
        'src/Wanxiangshu/OpenCode/Shrunk.fs': 2,
      },
      withoutPort: ['src/Wanxiangshu/OpenCode/Grown.fs', 'src/Wanxiangshu/OpenCode/Shrunk.fs'],
    }
    const files = [...fx.files.keys()]
    const now = measure(files, fx.read, fx.dir)
    assert.equal(now.productReferenceTotal, 4, 'the aggregate total is unchanged')

    const { violations } = scanAdapterThinness(files, fx.read, { baseline, root: fx.dir })
    assert.equal(violations.length, 1, 'exactly one per-file violation')
    assert.equal(violations[0].gate, 'adapter-thinness')
    assert.match(violations[0].message, /Grown\.fs: product-domain references rose 2 -> 3/)
  } finally {
    fx.cleanup()
  }
})

// (d) an adapter file that is NOT in the baseline is allowed while the totals stay within the baseline:
// a lane that moves or splits a baselined adapter file never has to edit the baseline.
test('WHAT[host-boundary-034] an adapter file that is not in the baseline is allowed while the totals stay within the baseline', () => {
  const fx = fixture()
  try {
    // A baselined file is split into two: one part keeps the old name and shrinks, the other part is
    // a new file. The total is unchanged, so no baseline edit is needed.
    fx.write('src/Wanxiangshu/OpenCode/Moved.fs', [
      'namespace Wanxiangshu.OpenCode',
      'open Wanxiangshu.Execution.Delegation',
      'open Wanxiangshu.Interaction.Dispatch',
      '',
    ].join('\n'))
    fx.write('src/Wanxiangshu/OpenCode/Old.fs', [
      'namespace Wanxiangshu.OpenCode',
      'open Wanxiangshu.Execution.Delegation',
      '',
    ].join('\n'))

    const baseline = {
      productReference: { 'src/Wanxiangshu/OpenCode/Old.fs': 3 },
      withoutPort: ['src/Wanxiangshu/OpenCode/Old.fs', 'src/Wanxiangshu/OpenCode/Moved.fs'],
    }
    const files = [...fx.files.keys()]
    const now = measure(files, fx.read, fx.dir)
    assert.equal(now.productReferenceTotal, 3, 'the aggregate total is unchanged')

    const { violations } = scanAdapterThinness(files, fx.read, { baseline, root: fx.dir })
    assert.equal(violations.length, 0, `a move or split must not need a baseline edit: ${JSON.stringify(violations)}`)
  } finally {
    fx.cleanup()
  }
})

// (e) the CLI refuses to write the baseline on a planted regression and leaves the file byte-identical.
test('WHAT[host-boundary-034] the CLI refuses --write-baseline on a regression and leaves the baseline byte-identical', () => {
  const fx = fixture()
  try {
    mkdirSync(join(fx.dir, 'scripts/checks'), { recursive: true })
    const baselinePath = join(fx.dir, 'scripts/checks/adapter-thinness-baseline.json')
    const baselineText = JSON.stringify({
      _note: 'fixture baseline',
      productReference: { 'src/Wanxiangshu/OpenCode/Adapter.fs': 1 },
      withoutPort: ['src/Wanxiangshu/OpenCode/Adapter.fs'],
    }, null, 2) + '\n'
    writeFileSync(baselinePath, baselineText)
    const before = readFileSync(baselinePath)

    fx.write('src/Wanxiangshu/OpenCode/Adapter.fs', [
      'namespace Wanxiangshu.OpenCode',
      'open Wanxiangshu.Execution.Delegation',
      'open Wanxiangshu.Interaction.Dispatch',
      '',
    ].join('\n'))

    const run = spawnSync(process.execPath, [GATE, '--write-baseline'], { cwd: fx.dir, encoding: 'utf8' })
    assert.notEqual(run.status, 0, 'the CLI must exit non-zero')
    assert.match(run.stderr, /refusing to write/)
    assert.equal(readFileSync(baselinePath).equals(before), true, 'baseline bytes must be unchanged')
  } finally {
    fx.cleanup()
  }
})

// (f) port-family integrity: a family that claims no reserved id, a family that claims an id another
// family already holds, and a lane file whose base name collides with a neutral family are all
// reported (HNI-11 drift D-06 / D-07). The mirror generator hard-errors on a family without a
// marker and reads the family by file name, so both are checked here rather than by a failing build.
test('WHAT[host-boundary-034] a port family without a marker, a duplicated id and a colliding file name are reported', () => {
  const fx = fixture()
  try {
    fx.write('src/Wanxiangshu/Host/Ports/Tools.fsi', [
      'namespace Wanxiangshu.Host.Ports',
      '/// HNI P-04: product tools contributed to a host.',
      'type ToolInvocation = { Name: string }',
      '',
    ].join('\n'))
    fx.write('src/Wanxiangshu/Host/Ports/Unmarked.fsi', [
      'namespace Wanxiangshu.Host.Ports',
      '/// A family that never declared its reserved id.',
      'type Unmarked = { X: int }',
      '',
    ].join('\n'))
    fx.write('src/Wanxiangshu/Host/Ports/Duplicate.fsi', [
      'namespace Wanxiangshu.Host.Ports',
      '/// HNI P-04: a second family claiming an id that is already taken.',
      'type Duplicate = { Y: int }',
      '',
    ].join('\n'))
    // The tools lane's own file: same base name as the neutral P-04 family, different directory.
    fx.write('src/Wanxiangshu/Host/Tools.fsi', [
      'namespace Wanxiangshu.Host',
      '/// P-04 tool contribution port.',
      'type HostToolContext = { SessionId: string }',
      '',
    ].join('\n'))

    const files = [...fx.files.keys()]
    const { violations, families } = scanPortFamilies(files, fx.read, { root: fx.dir })
    const messages = violations.map((v) => v.message)

    assert.ok(
      messages.some((m) => /Unmarked\.fsi: declares no reserved port id/.test(m)),
      `a family without an HNI P-xx marker must be reported: ${JSON.stringify(messages)}`,
    )
    assert.ok(
      messages.some((m) => /reserved port id P-04 is already claimed/.test(m)),
      `a duplicated reserved id must be reported: ${JSON.stringify(messages)}`,
    )
    assert.ok(
      messages.some((m) => /Host\/Tools\.fsi: base name 'Tools' is already used/.test(m)),
      `a file name that collides with a neutral family must be reported: ${JSON.stringify(messages)}`,
    )
    assert.ok(violations.every((v) => v.gate === 'port-family'))

    // The one family that is well-formed is reported with its id.
    assert.deepEqual(families, [['P-04', 'src/Wanxiangshu/Host/Ports/Tools.fsi']])
  } finally {
    fx.cleanup()
  }
})
}
