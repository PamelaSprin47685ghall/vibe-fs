namespace Wanxiangshu.OpenCode.Host.RequirementGrounding

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

module RequirementGroundingTransform =
    val source: string
    val toolName: string
    val cursorSeparator: string
    val isGroundingRead: raw: obj -> bool

    val stableCallId:
        sessionId: string ->
        workspace: string ->
        packageName: string ->
        digest: string ->
        ordinal: int64 ->
        index: int ->
            string

    val cursorResult: path: string -> resultBytes: string -> string

    val tryProject:
        journal: AgentJournal -> sessionId: string -> rawMessages: obj list -> Task<Result<obj list, string>>

    val projectOrTerminate:
        journal: AgentJournal option ->
        workspaceDirectory: string option ->
        terminateSession: (SessionId -> string -> Task<Result<unit, string>>) ->
        projectionSessionIdOpt: string option ->
        outObj: obj ->
            Task<unit>
