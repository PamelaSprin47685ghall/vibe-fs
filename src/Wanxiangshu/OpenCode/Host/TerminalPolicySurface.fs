namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

module TerminalPolicySurface =
    let private roleOf value =
        match Roles.tryParseRole value with
        | Some role -> Some role
        | None -> None

    let sessionDeadWithoutJournal (sessionId: string) =
        TerminalPolicy.sessionDead None (SessionId.create sessionId)

    let outstandingWithoutJournal (role: string) (hasLivePty: bool) (sessionId: string) =
        TerminalPolicy.outstandingBackground None (fun _ -> hasLivePty) (roleOf role) (SessionId.create sessionId)
