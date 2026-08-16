namespace Wanxiangshu.OpenCode

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// JS-native terminal admission boundary. The durable journal remains an opaque
/// host/session-owned resource; policy returns only booleans and child snapshots.
module TerminalPolicySurface =
    let sessionDead (journal: obj) (sessionId: string) : bool =
        let typed = if isNull journal then None else Some(journal :?> AgentJournal)
        TerminalPolicy.sessionDead typed (SessionId.create sessionId)

    let tryLinkedChild (journal: obj) (sessionId: string) : obj =
        let typed = if isNull journal then None else Some(journal :?> AgentJournal)
        TerminalPolicy.tryLinkedChild typed sessionId
        |> Option.map (fun record -> box {| targetAgent = record.TargetAgent; child = SessionId.value record.ChildSessionId |})
        |> Option.defaultValue null

    let isLinkedChild (journal: obj) (sessionId: string) : bool =
        let typed = if isNull journal then None else Some(journal :?> AgentJournal)
        TerminalPolicy.isLinkedChild typed sessionId

    let outstandingBackground (journal: obj) (role: string) (sessionId: string) (hasLivePty: string -> bool) : bool =
        let typed = if isNull journal then None else Some(journal :?> AgentJournal)
        let parsed = Roles.tryParseRole role
        TerminalPolicy.outstandingBackground typed hasLivePty parsed (SessionId.create sessionId)
