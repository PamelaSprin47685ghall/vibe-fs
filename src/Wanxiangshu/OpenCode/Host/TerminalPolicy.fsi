namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

module TerminalPolicy =
    val sessionDead: journal: AgentJournal option -> sessionId: SessionId -> bool

    val isTopLevelManager:
        sessionParents: Dictionary<string, string> -> journal: AgentJournal option -> sessionKey: string -> bool

    val outstandingBackground:
        journal: AgentJournal option ->
        hasLivePty: (string -> bool) ->
        role: Role option ->
        sessionId: SessionId ->
            bool
