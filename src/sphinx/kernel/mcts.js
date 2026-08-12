export function createMctsState() {
  return { nodes: {}, rollouts: 0, transpositions: 0 }
}

export function uctScore(node, parentVisits, exploration = 1.414) {
  if (!node || node.visits <= 0) return Number.POSITIVE_INFINITY
  const exploitation = node.valueSum / node.visits
  const explorationTerm = exploration * Math.sqrt(Math.log(Math.max(1, parentVisits)) / node.visits)
  return exploitation + explorationTerm
}

export function puctScore(node, parentVisits, prior = 0.5, exploration = 1.414) {
  if (!node || node.visits <= 0) return Number.POSITIVE_INFINITY
  const q = node.valueSum / node.visits
  const u = exploration * prior * Math.sqrt(parentVisits) / (1 + node.visits)
  return q + u
}

export function selectMctsNode(state) {
  const nodes = Object.values(state.mcts?.nodes ?? {})
  if (!nodes.length) return null
  const parentVisits = nodes.reduce((sum, node) => sum + node.visits, 0) + 1
  return [...nodes]
    .map((node) => ({
      ...node,
      uct: uctScore(node, parentVisits),
      puct: puctScore(node, parentVisits, node.prior ?? 0.5),
    }))
    .sort((left, right) => right.uct - left.uct)[0]
}

export function backupMctsValue(state, semanticKey, reward) {
  const mcts = state.mcts ?? createMctsState()
  const nodes = { ...mcts.nodes }
  const key = semanticKey
  const prev = nodes[key] ?? {
    semanticKey: key,
    visits: 0,
    valueSum: 0,
    prior: 0.5,
    transposition: key,
  }
  nodes[key] = {
    ...prev,
    visits: prev.visits + 1,
    valueSum: prev.valueSum + reward,
    lastReward: reward,
  }
  return {
    ...state,
    mcts: {
      ...mcts,
      nodes,
      rollouts: (mcts.rollouts ?? 0) + 1,
      transpositions: Object.keys(nodes).length,
    },
  }
}

export function rolloutValue(action, state) {
  const value = typeof action.value === 'number' ? action.value : 0
  const cost = Number(action.cost) || 1
  return value - cost * 0.05
}

export function syncMcts(state) {
  const mcts = state.mcts ?? createMctsState()
  const nodes = { ...mcts.nodes }
  const posteriors = new Map(
    (state.B?.hypotheses ?? []).map((hypothesis) => [
      hypothesis.semanticKey,
      hypothesis.posterior ?? hypothesis.prior ?? 0.5,
    ]),
  )

  for (const action of state.A) {
    if (action.kind !== 'candidate') continue
    const key = action.semanticKey
    const prev = nodes[key] ?? {
      semanticKey: key,
      visits: 0,
      valueSum: 0,
      prior: posteriors.get(key) ?? 0.5,
      transposition: key,
    }
    const sample = rolloutValue(action, state)
    nodes[key] = {
      ...prev,
      prior: posteriors.get(key) ?? prev.prior,
      sampledValue: sample,
      transposition: key,
    }
  }

  return {
    ...state,
    mcts: {
      ...mcts,
      nodes,
      transpositions: Object.keys(nodes).length,
    },
  }
}

export function degenerateMctsSelection(actions, rewards, rollouts = 12) {
  const nodes = Object.fromEntries(
    actions.map((action) => [
      action.id,
      { semanticKey: action.id, visits: 0, valueSum: 0, prior: 0.5, transposition: action.id },
    ]),
  )
  let state = { mcts: { nodes, rollouts: 0, transpositions: actions.length } }
  for (let step = 0; step < rollouts; step += 1) {
    const selected = selectMctsNode(state)
    const reward = rewards[selected.semanticKey] ?? 0
    state = backupMctsValue(state, selected.semanticKey, reward)
  }
  return selectMctsNode(state)
}
