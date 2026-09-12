namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

module TerminalPolicy =
    val sessionDead: port: TerminalPolicyPort option -> sessionId: SessionId -> bool

    val isTopLevelManager:
        sessionParents: Dictionary<string, string> -> port: TerminalPolicyPort option -> sessionKey: string -> bool

    val outstandingBackground:
        port: TerminalPolicyPort option ->
        hasLivePty: (string -> bool) ->
        role: Role option ->
        sessionId: SessionId ->
            bool
