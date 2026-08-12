import { rootInformationGain } from './value.js'

export function createSearchState() {
  return { bestG: {}, closed: {}, frontier: [], reopenCount: 0, exploreSteps: 0 }
}

export class PriorityQueue {
  constructor(compare) {
    this.compare = compare
    this.heap = []
  }

  size() {
    return this.heap.length
  }

  peek() {
    return this.heap[0]
  }

  push(item) {
    this.heap.push(item)
    this.bubbleUp(this.heap.length - 1)
  }

  pop() {
    if (this.heap.length === 0) return undefined
    const top = this.heap[0]
    const last = this.heap.pop()
    if (this.heap.length > 0) {
      this.heap[0] = last
      this.bubbleDown(0)
    }
    return top
  }

  toSortedArray() {
    const copy = [...this.heap]
    const out = []
    const queue = new PriorityQueue(this.compare)
    queue.heap = copy
    while (queue.size() > 0) out.push(queue.pop())
    return out
  }

  bubbleUp(index) {
    while (index > 0) {
      const parent = Math.floor((index - 1) / 2)
      if (this.compare(this.heap[index], this.heap[parent]) <= 0) break
      ;[this.heap[index], this.heap[parent]] = [this.heap[parent], this.heap[index]]
      index = parent
    }
  }

  bubbleDown(index) {
    const length = this.heap.length
    while (true) {
      const left = index * 2 + 1
      const right = left + 1
      let best = index
      if (left < length && this.compare(this.heap[left], this.heap[best]) > 0) best = left
      if (right < length && this.compare(this.heap[right], this.heap[best]) > 0) best = right
      if (best === index) break
      ;[this.heap[index], this.heap[best]] = [this.heap[best], this.heap[index]]
      index = best
    }
  }
}

export function frontierScore(action, state) {
  const g = Number(action.cost) || 1
  const value = typeof action.value === 'number' ? action.value : rootInformationGain(action, state)
  return value - g * 0.1
}

export function graphAstarScore(action, state, heuristic = null) {
  const g = Number(action.cost) || 0
  const h =
    typeof heuristic === 'number'
      ? heuristic
      : typeof action.heuristic === 'number'
        ? action.heuristic
        : Math.max(0, 1 - rootInformationGain(action, state))
  return g + h
}

export function graphAstarExpandOrder(actions, getG, getH) {
  const queue = new PriorityQueue((left, right) => left.priority - right.priority)
  for (const action of actions) {
    const g = getG(action)
    const h = getH(action)
    queue.push({ action, priority: -(g + h), g, h })
  }
  return queue.toSortedArray().map((row) => row.action)
}

export function reopenOnBeliefShift(state, previousMass, epsilon = 0.05) {
  const search = state.search ?? createSearchState()
  if (Math.abs(state.B.evidenceMass - previousMass) <= epsilon) return state
  return {
    ...state,
    search: {
      ...search,
      closed: {},
      reopenCount: (search.reopenCount ?? 0) + 1,
    },
  }
}

export function syncSearchFrontier(state) {
  const search = state.search ?? createSearchState()
  const bestG = { ...search.bestG }
  const closed = { ...search.closed }
  let reopenCount = search.reopenCount ?? 0
  const queue = new PriorityQueue((left, right) => left.f - right.f)

  for (const action of state.A) {
    if (action.kind !== 'candidate') continue
    const key = action.semanticKey
    if (!key) continue
    const g = Number(action.cost) || 1
    const prevG = bestG[key]
    if (prevG !== undefined && g > prevG) continue
    if (prevG !== undefined && g < prevG && closed[key]) {
      delete closed[key]
      reopenCount += 1
    }
    bestG[key] = prevG === undefined ? g : Math.min(prevG, g)
    if (!closed[key]) {
      queue.push({
        key,
        f: frontierScore(action, state),
        g,
        actionId: action.id,
        method: action.method,
        rootGain: rootInformationGain(action, state),
      })
    }
  }

  const frontier = queue.toSortedArray()
  return { ...state, search: { ...search, bestG, closed, frontier, reopenCount } }
}

export function markExplored(state, semanticKey) {
  if (!semanticKey) return state
  const search = state.search ?? createSearchState()
  return {
    ...state,
    search: {
      ...search,
      closed: { ...search.closed, [semanticKey]: true },
      exploreSteps: (search.exploreSteps ?? 0) + 1,
    },
  }
}

export function topFrontierAction(state) {
  return state.search?.frontier?.[0] ?? null
}

export function orderActionsByFrontier(state, actions) {
  const rank = new Map((state.search?.frontier ?? []).map((row, index) => [row.actionId, index]))
  return [...actions].sort(
    (left, right) => (rank.get(left.id) ?? 999) - (rank.get(right.id) ?? 999),
  )
}
