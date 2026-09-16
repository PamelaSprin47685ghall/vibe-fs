import assert from 'node:assert/strict'
import test from 'node:test'
import {
  HostForkRuntime__SubscribePtyCompletion_6A484C48 as subscribe,
  HostForkRuntime__notifyPtyObservers_3E8F9322 as notify,
} from '../../../dist/Execution/Delegation/Fork/Host/Runtime.js'

test('WHAT[DELEG-019] HOST_PTY_completion_observers_receive_each_physical_completion_once_until_disposed', () => {
  const runtime = { gate: {}, ptyCompletionObservers: [] }
  const first = []
  const second = []
  const firstSubscription = subscribe(runtime, (item) => first.push(item))
  subscribe(runtime, (item) => second.push(item))

  notify(runtime, 'completed-1')
  firstSubscription.Dispose()
  notify(runtime, 'completed-2')

  assert.deepEqual(first, ['completed-1'])
  assert.deepEqual(second, ['completed-1', 'completed-2'])
})
