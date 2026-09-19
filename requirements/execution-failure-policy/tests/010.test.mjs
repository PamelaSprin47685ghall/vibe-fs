import assert from 'node:assert/strict'
import test from 'node:test'
import {
  INVENTORY_REL,
  scanFatalInventory,
  scanFatalSites,
} from '../../../scripts/checks/fatal-inventory-gate.mjs'

const file = (path, text) => ({ path, text })

const row = (overrides) => ({
  id: 'F99',
  ownerRequirement: 'execution-failure-policy',
  sourceSymbol: 'Example/Module.fs :: exampleSymbol',
  operation: 'example-fatal-op',
  triggerBranch: 'test branch',
  inputSource: 'test input',
  exactIdentity: 'test identity',
  phase: 'test phase',
  assertedInvariant: 'test invariant',
  commitDisposition: 'test disposition',
  scopeOfEffect: 'test scope',
  evidenceType: 'test evidence',
  formalTestId: null,
  status: 'Open',
  ...overrides,
})

test('WHAT[execution-failure-policy-010] scanner finds direct FatalProcess and Diagnostic call sites', () => {
  const { sites } = scanFatalSites([
    file('src/Wanxiangshu/A.fs', 'let a () =\n    FatalProcess.trip "a-op" detail\n'),
    file('src/Wanxiangshu/B.fs', 'let b () =\n    Diagnostic.fatal "b-op" [ "result", r ]\n'),
    file('src/Wanxiangshu/C.fs', 'let c () =\n    FatalProcess.kill ()\n'),
  ])
  assert.deepEqual(sites.map((s) => s.operation), ['a-op', 'b-op', null])
  assert.deepEqual(sites.map((s) => s.path), [
    'src/Wanxiangshu/A.fs',
    'src/Wanxiangshu/B.fs',
    'src/Wanxiangshu/C.fs',
  ])
})

test('WHAT[execution-failure-policy-010] scanner ignores comments, strings, and point-free bindings', () => {
  const { sites } = scanFatalSites([
    file(
      'src/Wanxiangshu/D.fs',
      '// FatalProcess.trip "comment-op" must not match\nlet s = "Diagnostic.fatal \\"string-op\\""\nlet TripFatal = FatalProcess.trip\n',
    ),
  ])
  assert.deepEqual(sites, [])
})

test('WHAT[execution-failure-policy-010] scanner resolves a point-free alias one hop to its use', () => {
  const { sites, aliases } = scanFatalSites([
    file('src/Wanxiangshu/Runtime.fs', 'let deps = { TripFatal = FatalProcess.trip }\n'),
    file('src/Wanxiangshu/Workflow.fs', 'let w () =\n    deps.TripFatal "w-op" detail\n'),
  ])
  assert.equal(aliases.get('TripFatal'), 'src/Wanxiangshu/Runtime.fs')
  assert.equal(sites.length, 1)
  assert.equal(sites[0].path, 'src/Wanxiangshu/Workflow.fs')
  assert.equal(sites[0].via, 'TripFatal')
  assert.equal(sites[0].operation, 'w-op')
})

test('WHAT[execution-failure-policy-010] scanner resolves a ReportFatalDiagnostic forwarder one hop', () => {
  const { sites, aliases } = scanFatalSites([
    file(
      'src/Wanxiangshu/Port.fs',
      'member _.ReportFatalDiagnostic(operation, fields) =\n    FatalProcess.trip operation fields\n',
    ),
    file('src/Wanxiangshu/Consumer.fs', 'let c port =\n    port.ReportFatalDiagnostic("c-op", [])\n'),
  ])
  assert.equal(aliases.get('ReportFatalDiagnostic'), 'src/Wanxiangshu/Port.fs')
  const consumer = sites.find((s) => s.path === 'src/Wanxiangshu/Consumer.fs')
  assert.ok(consumer, 'consumer invocation through the forwarder is a scanned site')
  assert.equal(consumer.via, 'ReportFatalDiagnostic')
})

test('WHAT[execution-failure-policy-010] gate fails on a fatal call site with no inventory row', () => {
  const files = [
    file('src/Wanxiangshu/New.fs', 'let n () =\n    FatalProcess.trip "brand-new-op" x\n'),
    file('src/Wanxiangshu/Other/Mod.fs', 'let s () =\n    FatalProcess.trip "other-op" x\n'),
  ]
  const violations = scanFatalInventory(files, [row({ id: 'F98', sourceSymbol: 'Other/Mod.fs :: s', operation: 'other-op' })])
  assert.equal(violations.length, 1)
  assert.equal(violations[0].code, 'fatal-entry-unregistered')
  assert.match(violations[0].detail, /brand-new-op/)
})

test('WHAT[execution-failure-policy-010] gate fails on an alias invocation with no inventory row', () => {
  const files = [
    file('src/Wanxiangshu/R.fs', 'let t = { TripFatal = FatalProcess.trip }\n'),
    file('src/Wanxiangshu/W.fs', 'let w () =\n    deps.TripFatal "sneaky-op" d\n'),
  ]
  const violations = scanFatalInventory(files, [])
  assert.ok(violations.some((v) => v.code === 'fatal-entry-unregistered' && /TripFatal/.test(v.detail)))
})

test('WHAT[execution-failure-policy-010] gate fails when the row operation left its source file', () => {
  const files = [file('src/Wanxiangshu/Moved.fs', 'let m () = ()\n')]
  const violations = scanFatalInventory(files, [
    row({ id: 'F97', sourceSymbol: 'Moved.fs :: m', operation: 'moved-op' }),
  ])
  assert.ok(violations.some((v) => v.code === 'fatal-inventory-stale-symbol' && /F97/.test(v.detail)))
})

test('WHAT[execution-failure-policy-010] gate fails when FixedWithRegression names a missing test', () => {
  const files = [file('src/Wanxiangshu/S.fs', 'let s () =\n    FatalProcess.trip "s-op" x\n')]
  const entries = [
    row({
      id: 'F96',
      sourceSymbol: 'S.fs :: s',
      operation: 's-op',
      status: 'FixedWithRegression',
      formalTestId: 'requirements/ghost/tests/missing.test.mjs::WHAT[execution-failure-policy-010] ghost',
    }),
  ]
  const violations = scanFatalInventory(files, entries, () => false)
  assert.ok(
    violations.some((v) => v.code === 'fatal-inventory-missing-regression-test' && /F96/.test(v.detail)),
  )
})

test('WHAT[execution-failure-policy-010] gate passes FixedWithRegression when the named test exists', () => {
  const files = [file('src/Wanxiangshu/S.fs', 'let s () =\n    FatalProcess.trip "s-op" x\n')]
  const entries = [
    row({
      id: 'F96',
      sourceSymbol: 'S.fs :: s',
      operation: 's-op',
      status: 'FixedWithRegression',
      formalTestId: 'requirements/real/tests/there.test.mjs::WHAT[execution-failure-policy-010] there',
    }),
  ]
  const violations = scanFatalInventory(files, entries, (rel) => rel === 'requirements/real/tests/there.test.mjs')
  assert.deepEqual(violations, [])
})

test('WHAT[execution-failure-policy-010] gate passes when every site has a row and no row is stale', () => {
  const files = [file('src/Wanxiangshu/S.fs', 'let s () =\n    FatalProcess.trip "s-op" x\n')]
  const violations = scanFatalInventory(files, [row({ id: 'F96', sourceSymbol: 'S.fs :: s', operation: 's-op' })])
  assert.deepEqual(violations, [])
})

test('WHAT[execution-failure-policy-010] retired-fuse migration anchored on its regression test stays green', () => {
  // A dead optional fuse replaced by a typed return: no live fatal subject
  // in the file, but the FixedWithRegression test guards the absence.
  const files = [file('src/Wanxiangshu/Cut.fs', 'let c () =\n    Error reason\n')]
  const entries = [
    row({
      id: 'C99',
      sourceSymbol: 'Cut.fs :: c',
      operation: 'dynamic (retired fuse; absence guarded by regression test)',
      status: 'FixedWithRegression',
      formalTestId: 'requirements/real/tests/there.test.mjs::WHAT[execution-failure-policy-010] no optional fuse remains',
    }),
  ]
  const violations = scanFatalInventory(files, entries, (rel) => rel === 'requirements/real/tests/there.test.mjs')
  assert.deepEqual(violations, [])
})

test('WHAT[execution-failure-policy-010] retired-fuse migration without its regression test still fails', () => {
  const files = [file('src/Wanxiangshu/Cut.fs', 'let c () =\n    Error reason\n')]
  const entries = [
    row({
      id: 'C99',
      sourceSymbol: 'Cut.fs :: c',
      operation: 'dynamic (retired fuse; absence guarded by regression test)',
      status: 'FixedWithRegression',
      formalTestId: 'requirements/ghost/tests/missing.test.mjs::WHAT[execution-failure-policy-010] ghost',
    }),
  ]
  const violations = scanFatalInventory(files, entries, () => false)
  assert.ok(
    violations.some((v) => v.code === 'fatal-inventory-missing-regression-test' && /C99/.test(v.detail)),
  )
})
