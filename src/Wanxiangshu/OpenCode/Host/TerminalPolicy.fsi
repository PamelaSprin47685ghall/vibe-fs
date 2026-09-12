namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

module TerminalPolicy =
    val sessionDead: port: TerminalPolicyPort option -> sessionId: SessionId -> bool

    val isTopLevelManager:
        sessionParents: Dictionary<string, string> -> journal: AgentJournal option -> sessionKey: string -> bool

    val outstandingBackground:
        port: TerminalPolicyPort option ->
        hasLivePty: (string -> bool) ->
        role: Role option ->
        sessionId: SessionId ->
            bool
