import {
  caseOf,
  resultOf,
  toList,
} from '../../../verification-system/tests/support/domain.mjs'

const { Ports, resolveAndCommit } = await import('../../../../dist/Execution/Delegation/ChildRecoveryWorkflow.js')

export const childRecoveryWorkflow = {
  ports: ({
    journal,
    parent,
    snapshotResult,
    agentId,
    handle,
    child,
    role,
    targetAgent,
    observations = [],
    pulse,
    clock,
  }) =>
    new Ports(
      journal,
      parent,
      { GetMessages: () => Promise.resolve(snapshotResult) },
      agentId,
      handle,
      child,
      role,
      targetAgent,
      toList(observations),
      pulse,
      clock,
    ),

  resolve: async (ports) => {
    const result = resultOf(await resolveAndCommit(ports))
    return result.ok
      ? { ok: true, resolution: caseOf(result.value) }
      : { ok: false, error: result.error }
  },
}
