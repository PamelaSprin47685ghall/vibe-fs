namespace Wanxiangshu.Participant.Cognition

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module CognitiveJournalAdapter =
    val port: journal: AgentJournal option -> CognitiveJournalPort
