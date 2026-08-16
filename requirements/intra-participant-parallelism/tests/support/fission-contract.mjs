import { caseOf, payloadOf, sessionId } from '../../../verification-system/tests/support/domain.mjs'

const Model = await import('../../../../dist/Execution/Fission/Model.js')
const Admission = await import('../../../../dist/Execution/Fission/Admission.js')

export const parseFissionPrompt = (text) => {
  const parsed = Model.FissionPrompt_parse(text)
  if (caseOf(parsed) !== 'Ok') throw new Error(`expected valid fission prompt, got ${caseOf(parsed)}`)
  return payloadOf(parsed)
}

export const fissionLaneContract = {
  started: (index, session, prompt) => new Admission.FissionStartedLane(index, sessionId(session), prompt),
  startup: (laneCount, laneIndex, prompt, workRecord) =>
    Admission.FissionStartup_render(laneCount, new Model.FissionLanePrompt(laneIndex, prompt), workRecord),
  view: (lane) => ({
    index: lane.Index,
    session: lane.SessionId,
    prompt: lane.Prompt,
    hasAgentId: 'AgentId' in lane,
    hasHandle: 'Handle' in lane,
    hasParent: 'Parent' in lane,
  }),
}
