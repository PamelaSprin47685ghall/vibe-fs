import assert from 'node:assert/strict'
import { readdirSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import {
  TaskResultCE_taskResult,
  TaskResultBuilder__Run_2ABB9ADE,
  TaskResultBuilder__Delay_2ABB9ADE,
  TaskResultBuilder__Return_1505,
  TaskResultBuilder__Bind_476BAC93,
  TaskResultBuilder__TryFinally_Z478A6D38,
  TaskResultBuilder__TryWith_Z1A5064FB,
  TaskResultBuilder__Using_30F76102,
  TaskResultBuilder__While_Z49657FF9,
  TaskResultCE_ofTask,
} from '../../../dist/Foundation/TaskResult.js'
import { FSharpResult$2 } from '../../../dist/fable_modules/fable-library-js.5.13.0/Result.js'
import { TaskResultListSurface_traverseM } from '../../../dist/Foundation/FsToolkitFableCompat.js'

const builder = TaskResultCE_taskResult
const run = (delayed) => TaskResultBuilder__Run_2ABB9ADE(builder, delayed)
const delay = (fn) => TaskResultBuilder__Delay_2ABB9ADE(builder, fn)
const ret = (v) => TaskResultBuilder__Return_1505(builder, v)
const bind = (t, next) => TaskResultBuilder__Bind_476BAC93(builder, t, next)

test('WHAT[STRUCTURED-WORKFLOW-004] TaskResult CE executes success, error short-circuit, and typed ofTask lifting', async () => {
  // 1. Success execution
  const successOutcome = await run(delay(() => ret('success-val')))
  assert.equal(successOutcome.tag, 0)
  assert.equal(successOutcome.fields[0], 'success-val')

  // 2. Error short-circuit (next step never executed)
  let nextRan = false
  const errorInput = Promise.resolve(new FSharpResult$2(1, ['early-failure']))
  const shortCircuitOutcome = await run(delay(() => bind(errorInput, (v) => {
    nextRan = true
    return ret(v)
  })))
  assert.equal(shortCircuitOutcome.tag, 1)
  assert.equal(shortCircuitOutcome.fields[0], 'early-failure')
  assert.equal(nextRan, false, 'error must short-circuit workflow execution')

  // 3. ofTask lifting lifts plain Task/Promise to Task<Result<T, 'e>>
  const lifted = await TaskResultCE_ofTask(Promise.resolve(42))
  assert.equal(lifted.tag, 0)
  assert.equal(lifted.fields[0], 42)
})

test('WHAT[STRUCTURED-WORKFLOW-004] TaskResult CE executes resource disposal, TryFinally compensation, and TryWith exception mapping', async () => {
  // 1. TryFinally executes compensation on success and on throw
  let finallyCleaned = false
  const finallyOk = await run(delay(() => TaskResultBuilder__TryFinally_Z478A6D38(builder, () => ret('ok'), () => {
    finallyCleaned = true
  })))
  assert.equal(finallyOk.tag, 0)
  assert.equal(finallyCleaned, true)

  let throwCleaned = false
  await assert.rejects(async () => {
    await run(delay(() => TaskResultBuilder__TryFinally_Z478A6D38(builder, () => {
      throw new Error('boom')
    }, () => {
      throwCleaned = true
    })))
  }, /boom/)
  assert.equal(throwCleaned, true)

  // 2. Using disposes resource via IDisposable pattern
  let disposed = false
  const resource = { Dispose() { disposed = true } }
  const used = await run(delay(() => TaskResultBuilder__Using_30F76102(builder, resource, () => ret('used-val'))))
  assert.equal(used.tag, 0)
  assert.equal(used.fields[0], 'used-val')
  assert.equal(disposed, true)

  // 3. TryWith catches and maps exceptions to Result
  const caught = await run(delay(() => TaskResultBuilder__TryWith_Z1A5064FB(builder, () => {
    throw new Error('caught-exception')
  }, (ex) => ret(ex.message))))
  assert.equal(caught.tag, 0)
  assert.equal(caught.fields[0], 'caught-exception')

  // 4. While loop execution
  let iteration = 0
  const whileResult = await run(delay(() => TaskResultBuilder__While_Z49657FF9(builder, () => iteration < 3, () => bind(ret(iteration), () => {
    iteration += 1
    return ret(iteration)
  }))))
  assert.equal(whileResult.tag, 0)
  assert.equal(iteration, 3)
})

test('WHAT[STRUCTURED-WORKFLOW-004] Fable async Result plumbing provides sequential short-circuiting traversal', async () => {
  const traversedOk = await TaskResultListSurface_traverseM((x) => Promise.resolve(x > 0), [1, 2, 3])
  assert.deepEqual(traversedOk, ['Ok', 1, 2, 3])

  const traversedErr = await TaskResultListSurface_traverseM((x) => Promise.resolve(x !== 2), [1, 2, 3])
  assert.deepEqual(traversedErr, ['Error', 2])
})
