namespace Wanxiangshu.Mission.Relay.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

module RelayNarrativeTransform =
    val apply:
        journal: AgentJournal option ->
        interruptAttempt: (SessionId -> Task<unit>) ->
        sessionId: string option ->
        outObj: obj ->
            Task<unit>
