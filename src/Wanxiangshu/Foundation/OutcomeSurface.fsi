namespace Wanxiangshu.Foundation

module OutcomeSurface =
    val sendOutcomeKinds: unit -> string array
    val sessionErrorKinds: unit -> string array
    val isValidAgentRunResult: terminalText: string -> bool
