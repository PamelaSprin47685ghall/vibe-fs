import { ofArray as listOfArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'
import {
  SessionStore_$ctor,
  SessionStore__Resume_433E080,
  SessionStore__Start_Z721C83C5,
  SessionStore__TryState_Z721C83C5,
} from '../../../dist/Sphinx/Session.js'
import { close } from '../../../dist/Sphinx/Closure.js'
import {
  serverName,
  permissionKey,
  relativeServerEntry,
  isTool,
  localCommand,
  fixtureCommand,
} from '../../../dist/Sphinx/Mcp.js'
import { create as createMcpServer } from '../../../dist/Sphinx/McpServer.js'
import { library, phase0Names } from '../../../dist/Sphinx/Methodology.js'
import { paretoFrontier } from '../../../dist/Sphinx/Representation.js'
import { Model, run, uct } from '../../../dist/Sphinx/MonteCarlo.js'
import { MonteCarloNode } from '../../../dist/Sphinx/RuntimeTypes.js'
import { AStarProblem, GraphEdge, solveGraph } from '../../../dist/Sphinx/Search.js'

export { close }
export { serverName, permissionKey, relativeServerEntry, isTool, localCommand, fixtureCommand }
export { createMcpServer }
export { library, phase0Names }
export { paretoFrontier }
export { Model, run, uct, MonteCarloNode }
export { AStarProblem, GraphEdge, solveGraph }

export const createStore = () => SessionStore_$ctor()
export const start = (store, question) => SessionStore__Start_Z721C83C5(store, question)
export const resume = (store, handle, observation) =>
  SessionStore__Resume_433E080(store, handle, observation)
export const state = (store, handle) => SessionStore__TryState_Z721C83C5(store, handle)
export const fsharpList = (items) => listOfArray(items)

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
