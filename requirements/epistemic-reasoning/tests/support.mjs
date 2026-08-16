import {
  createStore as createStoreSurface,
  start as startSurface,
  resume as resumeSurface,
  state as stateSurface,
  close as closeSurface,
  serverName,
  permissionKey,
  relativeServerEntry,
  isTool,
  localCommand,
  fixtureCommand,
  libraryNames,
  phase0MethodNames,
  paretoFrontier as paretoFrontierSurface,
  mctsRun,
  mctsUct,
  solveGraph as solveGraphSurface,
} from '../../../dist/Sphinx/Surface.js'

export const createStore = () => createStoreSurface()
export const start = (store, question) => startSurface(store, question)
export const resume = (store, handle, observation) => resumeSurface(store, handle, observation)
export const state = (store, handle) => stateSurface(store, handle)
export const close = (store, handle) => closeSurface(store, handle)
export const mapOfEntries = (entries) => Object.fromEntries(entries)

export { serverName, permissionKey, relativeServerEntry, isTool, localCommand, fixtureCommand }

export const library = libraryNames()
export const phase0Names = phase0MethodNames()
export const paretoFrontier = (actions) => paretoFrontierSurface(actions)
export const run = (iterations, model) => mctsRun(iterations, model)
export const uct = (parentVisits, exploration, node) => mctsUct(parentVisits, exploration, node)
export const solveGraph = (problem) => solveGraphSurface(problem)

export const assessWhy = (store, handle) =>
  resume(store, handle, {
    type: 'SemanticAssessment',
    forms: { Why: 0.8, How: 0.2 },
    facets: { causal: 0.9, explanatory: 1 },
  })

export const assessPolar = (store, handle) =>
  resume(store, handle, {
    type: 'SemanticAssessment',
    forms: { Polar: 0.95, Other: 0.05 },
    facets: { predictive: 1, evidence: 0.8 },
  })
