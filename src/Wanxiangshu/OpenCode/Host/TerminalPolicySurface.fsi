namespace Wanxiangshu.OpenCode

module TerminalPolicySurface =
    val sessionDeadWithoutJournal: sessionId: string -> bool
    val outstandingWithoutJournal: role: string -> hasLivePty: bool -> sessionId: string -> bool
