// WHAT[host-boundary-034] Gate 11/12/13 adapter boundaries: port purity forbids
// concrete Host SDK clients, file handles, mutable registries, and adapter/product
// namespaces; a legitimate adapter with domain references passes; port families
// and namespace tiers maintain integrity.
import test from 'node:test'

{
const { default: assert } = await import('node:assert/strict')
const { mkdtempSync, mkdirSync, rmSync, writeFileSync, readFileSync } = await import('node:fs')
const { tmpdir } = await import('node:os')
const { join, dirname, relative } = await import('node:path')
const { fileURLToPath } = await import('node:url')

const ROOT = dirname(fileURLToPath(import.meta.url))
const REPO = join(ROOT, '../../..')
const GATE = join(REPO, 'scripts/checks/adapter-thinness.mjs')
const { walk } = await import(join(REPO, 'scripts/lib/walk.mjs'))
const {
  scanAdapterThinness,
  portTypeNames,
  isPortFile,
  scanPortFamilies,
  scanNamespaceTiers,
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

// (a) Enhanced Gate 11: neutral port signatures must not carry concrete Host SDK clients,
// file handles, or mutable registries.
test('WHAT[host-boundary-034] neutral port signatures carrying Host SDK clients, file handles, or mutable registries are rejected', () => {
  const fx = fixture()
  try {
    // 1. Concrete Host SDK client handle in port
    fx.write('src/Wanxiangshu/Host/Ports/LeakingClient.fs', [
      'namespace Wanxiangshu.Host.Ports',
      'type LeakingClientPort =',
      '    member _.DoWork(client: obj) = client.session.get()',
      '',
    ].join('\n'))

    // 2. Concrete file handle in port
    fx.write('src/Wanxiangshu/Host/Ports/LeakingFile.fs', [
      'namespace Wanxiangshu.Host.Ports',
      'open System.IO',
      'type LeakingFilePort =',
      '    member _.ReadFile(path: string) = File.ReadAllText(path)',
      '',
    ].join('\n'))

    // 3. Mutable registry in port
    fx.write('src/Wanxiangshu/Host/Ports/LeakingRegistry.fs', [
      'namespace Wanxiangshu.Host.Ports',
      'type LeakingRegistryPort =',
      '    val mutable table: Dictionary<string, obj>',
      '',
    ].join('\n'))

    const files = [...fx.files.keys()]
    const { violations } = scanAdapterThinness(files, fx.read, { root: fx.dir })
    const purityViolations = violations.filter((v) => v.gate === 'port-purity')

    assert.ok(purityViolations.length >= 3, `all three planted leaks must be reported: ${JSON.stringify(purityViolations)}`)
    assert.ok(purityViolations.some((v) => /LeakingClient\.fs.*Host SDK client/.test(v.message)))
    assert.ok(purityViolations.some((v) => /LeakingFile\.fs.*file handle/.test(v.message)))
    assert.ok(purityViolations.some((v) => /LeakingRegistry\.fs.*mutable registry/.test(v.message)))
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

    const { violations } = scanAdapterThinness(files, fx.read, { root: fx.dir })
    const purity = violations.filter((v) => v.gate === 'port-purity')
    assert.equal(purity.length, 1, 'exactly the new port file violates purity')
    assert.match(purity[0].message, /NeutralFission\.fs/)
    assert.match(purity[0].message, /Execution/)
  } finally {
    fx.cleanup()
  }
})

// (c) A legitimate adapter with multiple domain namespace references but clear responsibility
// passes without being blocked by an import count ratchet.
test('WHAT[host-boundary-034] a legitimate adapter with multiple product domain references passes without count ratchet rejection', () => {
  const fx = fixture()
  try {
    // Valid clean neutral port
    fx.write('src/Wanxiangshu/Host/Ports/CleanPort.fsi', [
      'namespace Wanxiangshu.Host.Ports',
      '/// HNI P-99: clean port.',
      'type CleanPort =',
      '    abstract member Execute: unit -> unit',
      '',
    ].join('\n'))
    fx.write('src/Wanxiangshu/Host/Ports/CleanPort.fs', [
      'namespace Wanxiangshu.Host.Ports',
      'type CleanPort =',
      '    interface CleanPort with',
      '        member _.Execute() = ()',
      '',
    ].join('\n'))

    // Adapter referencing multiple product domains cleanly
    fx.write('src/Wanxiangshu/OpenCode/RichAdapter.fs', [
      'namespace Wanxiangshu.OpenCode',
      'open Wanxiangshu.Execution.Delegation',
      'open Wanxiangshu.Interaction.Dispatch',
      'open Wanxiangshu.Mission.Work',
      'open Wanxiangshu.Context.Prefix',
      '',
      'type RichAdapter = { Name: string }',
      '',
    ].join('\n'))

    const files = [...fx.files.keys()]
    const { violations } = scanAdapterThinness(files, fx.read, { root: fx.dir })
    assert.deepEqual(violations, [], `a rich adapter with clear responsibilities must pass: ${JSON.stringify(violations)}`)
  } finally {
    fx.cleanup()
  }
})

// (d) port-family integrity: a family that claims no reserved id, a family that claims an id another
// family already holds, and a lane file whose base name collides with a neutral family are all
// reported (HNI-11 drift D-06 / D-07).
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

// (e) namespace-tier integrity: a tier-A file with the wrong namespace, a tier-B file with the
// wrong namespace and a tier namespace claimed directly under Host/ are all reported (HNI-11
// drift D-05). The tree is read from the sources, so a family another lane lands is covered
// without editing the gate.
test('WHAT[host-boundary-034] a wrong tier namespace and a misplaced tier namespace are reported', () => {
  const fx = fixture()
  try {
    fx.write('src/Wanxiangshu/Host/Ports/Tools.fs', [
      'namespace Wanxiangshu.Host',
      'type ToolInvocation = { Name: string }',
      '',
    ].join('\n'))
    fx.write('src/Wanxiangshu/Host/Domain/PairGuidance.fs', [
      'namespace Wanxiangshu.Host',
      'type PairGuidanceLedger = { History: string list }',
      '',
    ].join('\n'))
    fx.write('src/Wanxiangshu/Host/Stray.fs', [
      'namespace Wanxiangshu.Host.Ports',
      'type Stray = { X: int }',
      '',
    ].join('\n'))

    const files = [...fx.files.keys()]
    const { violations } = scanNamespaceTiers(files, fx.read, { root: fx.dir })
    const messages = violations.map((v) => v.message)

    assert.ok(
      messages.some((m) => /Host\/Ports\/Tools\.fs: a tier-A port file must declare 'Wanxiangshu\.Host\.Ports'/.test(m)),
      `a tier-A file with the wrong namespace must be reported: ${JSON.stringify(messages)}`,
    )
    assert.ok(
      messages.some((m) => /Host\/Domain\/PairGuidance\.fs: a tier-B domain seam must declare 'Wanxiangshu\.Host\.Domain'/.test(m)),
      `a tier-B file with the wrong namespace must be reported: ${JSON.stringify(messages)}`,
    )
    assert.ok(
      messages.some((m) => /Host\/Stray\.fs: declares 'Wanxiangshu\.Host\.Ports' but sits outside/.test(m)),
      `a tier namespace claimed directly under Host/ must be reported: ${JSON.stringify(messages)}`,
    )
  } finally {
    fx.cleanup()
  }
})

// (f) the real tree passes Gate 11/12/13: port purity, port families, and namespace tiers.
test('WHAT[host-boundary-034] the repository tree passes the port-purity and namespace-tier gates', () => {
  const files = walk(join(REPO, 'src/Wanxiangshu'), ['.fs', '.fsi'])
    .map((p) => relative(REPO, p).replace(/\\/g, '/'))
    .sort()
  const read = (p) => readFileSync(join(REPO, p), 'utf8')
  const { violations } = scanAdapterThinness(files, read, { root: REPO })
  assert.deepEqual(violations, [], `the tree must pass port purity, port family, and namespace tier checks: ${JSON.stringify(violations)}`)
})
}
