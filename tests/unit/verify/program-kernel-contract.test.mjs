// tests/unit/verify/program-kernel-contract.test.mjs — M1 RED: generic Program kernel contract.
//
// spec/14 FLOW-002/003/007: business programs are closed ASTs (data), each DSL has
// exactly one production/simulation/trace interpreter, and the ONLY shared surface
// is the minimal Program mechanism + cancellation model + resource scope + test
// tooling. That shared kernel must exist as `Kernel/Program.fs` (the generic
// `Program<'instruction,'result>` with Pure/Suspend) and `Kernel/TraceInterpreter.fs`
// (the shared `trace` walker), compiled by Wanxiangshu.fsproj and exported from the
// test facade as `programKernel`.
//
// RED: none of these exist yet. Static source assertions only — never import dist,
// never import domain.mjs, so this file cannot break any other test.
import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'

const ROOT = new URL('../../../', import.meta.url).pathname
const FSPROJ = join(ROOT, 'src/Wanxiangshu/Wanxiangshu.fsproj')
const PROGRAM_FS = join(ROOT, 'src/Wanxiangshu/Kernel/Program.fs')
const TRACE_INTERPRETER_FS = join(ROOT, 'src/Wanxiangshu/Kernel/TraceInterpreter.fs')
const DOMAIN_FACADE = join(ROOT, 'tests/unit/support/domain.mjs')

test('PROGRAM_KERNEL_fsproj_compiles_both_kernel_files', () => {
  const fsproj = readFileSync(FSPROJ, 'utf8')

  // F# compilation order is load-bearing: the TraceInterpreter walks the Program
  // type, so Program.fs must be compiled before TraceInterpreter.fs.
  assert.ok(
    fsproj.includes('<Compile Include="Kernel/Program.fs"/>'),
    'Wanxiangshu.fsproj must compile Kernel/Program.fs (generic Program kernel)',
  )
  assert.ok(
    fsproj.includes('<Compile Include="Kernel/TraceInterpreter.fs"/>'),
    'Wanxiangshu.fsproj must compile Kernel/TraceInterpreter.fs (shared trace walker)',
  )
  const programIndex = fsproj.indexOf('<Compile Include="Kernel/Program.fs"/>')
  const traceIndex = fsproj.indexOf('<Compile Include="Kernel/TraceInterpreter.fs"/>')
  assert.ok(
    programIndex >= 0 && traceIndex > programIndex,
    'Kernel/Program.fs must be compiled before Kernel/TraceInterpreter.fs',
  )
})

test('PROGRAM_KERNEL_Program_fs_defines_generic_program_with_pure_and_suspend', () => {
  assert.ok(existsSync(PROGRAM_FS), 'src/Wanxiangshu/Kernel/Program.fs must exist (FLOW-007 shared minimal mechanism)')
  const source = readFileSync(PROGRAM_FS, 'utf8')

  // FLOW-002: `Program<'instruction,'result>` is a closed instruction AST, not a
  // coroutine. Spaces around the comma are free; the two type parameters are not.
  assert.match(
    source,
    /type\s+Program<'instruction\s*,\s*'result>\s*=/,
    'Kernel/Program.fs must define `type Program<\'instruction, \'result>`',
  )
  assert.match(source, /^\s*\| Pure of/m, 'Program must carry a Pure case (pure value)')
  assert.match(source, /^\s*\| Suspend of/m, 'Program must carry a Suspend case (instruction + continuation)')
})

test('PROGRAM_KERNEL_TraceInterpreter_fs_defines_trace', () => {
  assert.ok(
    existsSync(TRACE_INTERPRETER_FS),
    'src/Wanxiangshu/Kernel/TraceInterpreter.fs must exist (FLOW-003 shared trace interpreter)',
  )
  const source = readFileSync(TRACE_INTERPRETER_FS, 'utf8')

  assert.match(
    source,
    /let\s+(?:rec\s+)?trace\s*\(/,
    'Kernel/TraceInterpreter.fs must define a `trace` function over the generic Program',
  )
})

test('PROGRAM_KERNEL_domain_facade_exports_programKernel', () => {
  const facade = readFileSync(DOMAIN_FACADE, 'utf8')

  // VERIFY-008: Fable output shape is confined to domain.mjs; the contract surface
  // a test may import is a named namespace export there. Static check only — the
  // facade itself must not be imported by this RED test.
  assert.match(
    facade,
    /export\s+const\s+programKernel\s*=/,
    'tests/unit/support/domain.mjs must export the `programKernel` namespace',
  )
})
