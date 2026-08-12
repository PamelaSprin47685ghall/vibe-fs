export function canonicalAnswer(state, stopReason) {
  const strands = state.A.filter((a) => a.kind === 'candidate').map((a) => ({
    method: a.method,
    label: a.label,
    semanticKey: a.semanticKey,
    value: a.value,
  }))
  const evidence = state.E.map((item) => ({
    type: item.type,
    semanticKey: item.semanticKey ?? null,
    exogenous: Boolean(item.exogenous),
  }))
  return {
    question: state.rootQuestion,
    contract: state.R,
    activatedMethods: [...state.activatedMethods],
    evidenceMass: state.B.evidenceMass,
    strands,
    synthesis: state.synthesis,
    evidence,
    stopReason,
    yieldsUsed: state.C.yieldsUsed,
  }
}
