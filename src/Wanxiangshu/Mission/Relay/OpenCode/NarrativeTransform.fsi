namespace Wanxiangshu.Mission.Relay.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Persistence.Journal

module RelayNarrativeTransform =
    val apply: journal: AgentJournal option -> sessionId: string option -> outObj: obj -> Task<unit>
