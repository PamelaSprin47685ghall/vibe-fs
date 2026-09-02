namespace Wanxiangshu.OpenCode.Host

open System.Threading.Tasks

module RequirementGroundingSurface =
    val createJournal: directory: string -> Task<obj>
    val disposeJournal: journal: obj -> unit
    val requestPaths: journal: obj -> workspace: string -> sessionId: string -> paths: string array -> Task<obj>
    val mutationDecision: journal: obj -> workspace: string -> sessionId: string -> paths: string array -> Task<obj>
    val weakMutationObservation: journal: obj -> workspace: string -> sessionId: string -> path: string -> Task<bool>

    val observationDecision:
        journal: obj ->
        workspace: string ->
        sessionId: string ->
        toolName: string ->
        args: obj ->
        output: obj ->
            Task<obj>

    val projectWithJournal: journal: obj -> sessionId: string -> rawMessages: obj array -> Task<obj>
    val groundedIdentities: journal: obj -> sessionId: string -> string array
    val pendingPackages: journal: obj -> sessionId: string -> string array

    val appendContextReanchored:
        journal: obj ->
        sessionId: string ->
        previousEpoch: int64 ->
        nextEpoch: int64 ->
        observedRun: string ->
            Task<obj>

    val source: string
    val cursorSeparator: string
    val isGroundingRead: raw: obj -> bool
