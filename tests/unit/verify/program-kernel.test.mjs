// tests/unit/verify/program-kernel.test.mjs — M1 GREEN behavior for Program kernel.
//
// Consumes only the programKernel facade (VERIFY-008). No Fable internals.
import assert from 'node:assert/strict'
import test from 'node:test'
import { programKernel } from '../support/domain.mjs'

const { pure, suspend, bind, map, trace, program: builder } = programKernel

test('PROGRAM_KERNEL_trace_pure', () => {
  assert.deepEqual(trace(pure('ok')), [[], 'ok'])
})

test('PROGRAM_KERNEL_trace_suspend', () => {
  assert.deepEqual(
    trace(suspend('read', () => pure('done'))),
    [['read'], 'done'],
  )
})

test('PROGRAM_KERNEL_trace_bind', () => {
  assert.deepEqual(
    trace(bind(suspend('a', () => pure(1)), (x) => pure(x + 1))),
    [['a'], 2],
  )
})

test('PROGRAM_KERNEL_trace_map', () => {
  assert.deepEqual(
    trace(map(suspend('a', () => pure(1)), (x) => x + 1)),
    [['a'], 2],
  )
})

test('PROGRAM_KERNEL_trace_builder_bind', () => {
  // JS cannot run F# CE syntax; builder.Bind/Return is the same surface the CE uses.
  const step = suspend('a', () => pure(1))
  const program =
    typeof builder.Bind === 'function'
      ? builder.Bind(step, (x) => builder.Return(x + 1))
      : bind(step, (x) => pure(x + 1))
  assert.deepEqual(trace(program), [['a'], 2])
})
