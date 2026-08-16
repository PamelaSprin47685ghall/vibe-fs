import {
  listItems,
  resultOf,
  toList,
} from '../../../verification-system/tests/support/domain.mjs'

const Model = await import('../../../../dist/Repository/Knowledge/Casebook/Model.js')
const Workflow = await import('../../../../dist/Repository/Knowledge/Casebook/Workflow.js')

export const casebookContract = {
  normalizedCount: (observations) => listItems(Model.Observations_normalize(toList(observations))).length,
  finalize: async (store, record) => resultOf(await Workflow.CasebookWorkflow_finalizeCase(store, record)),
  archive: async (store, record) => resultOf(await Workflow.CasebookWorkflow_archiveInspectorResult(store, record)),
}
