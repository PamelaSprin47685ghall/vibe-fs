function dominates(left, right) {
  const leftValue = left.value ?? 0
  const rightValue = right.value ?? 0
  const leftCost = Number(left.cost) || 1
  const rightCost = Number(right.cost) || 1
  return leftValue >= rightValue && leftCost <= rightCost && (leftValue > rightValue || leftCost < rightCost)
}

export function paretoRepresentative(actions) {
  if (!actions.length) return null
  const survivors = []
  for (const candidate of actions) {
    if (survivors.some((row) => dominates(row, candidate))) continue
    const next = survivors.filter((row) => !dominates(candidate, row))
    next.push(candidate)
    survivors.splice(0, survivors.length, ...next)
  }
  return survivors.sort((left, right) => (right.value ?? 0) - (left.value ?? 0))[0] ?? actions[0]
}

export function groupEquivalenceClasses(actions) {
  const classes = new Map()
  for (const action of actions) {
    const classKey = action.equivalenceClass ?? action.semanticKey
    const bucket = classes.get(classKey) ?? []
    bucket.push(action)
    classes.set(classKey, bucket)
  }
  return classes
}

export function orderFactors(hypotheses) {
  return [...hypotheses].sort((left, right) => {
    const leftScore = (right.posterior ?? right.prior ?? 0) - (left.posterior ?? left.prior ?? 0)
    if (leftScore !== 0) return leftScore
    return String(left.semanticKey).localeCompare(String(right.semanticKey))
  })
}

export function contractRepresentation(actions) {
  const classes = groupEquivalenceClasses(actions)
  const pivots = []
  const compressed = []
  for (const [classKey, members] of classes) {
    const representative = paretoRepresentative(members)
    if (!representative) continue
    pivots.push(representative.semanticKey)
    compressed.push({
      ...representative,
      equivalenceClass: classKey,
      epistemicPivot: representative.semanticKey,
    })
  }
  return {
    compressed,
    classes: Object.fromEntries(
      [...classes.entries()].map(([key, members]) => [key, members.map((row) => row.semanticKey)]),
    ),
    pivots,
  }
}

export function optimizeRepresentation(state) {
  const { compressed, classes, pivots } = contractRepresentation(state.A)
  const factors = orderFactors(state.B?.hypotheses ?? [])
  return {
    ...state,
    A: compressed.length ? compressed : state.A,
    represent: {
      classes,
      pivots,
      factors: factors.map((row) => row.semanticKey),
    },
  }
}
