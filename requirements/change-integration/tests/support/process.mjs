export const exited = (exitCode, stdout = '', stderr = '') => ({
  kind: 'Observed', ending: { kind: 'Exited', exitCode },
  stopRequested: null, output: { kind: 'InMemory', stdout, stderr }, fault: null,
})

export const responseReceipt = response => Array.isArray(response) ? exited(...response) : response

export const absentState = command => exited(0,
  command.args[0] === 'rev-parse' && command.args[1] === '--git-path'
    ? `/nonexistent-wanxiang-test-state/${command.args[2]}\n` : '')
