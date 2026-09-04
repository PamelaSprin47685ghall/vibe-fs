namespace Wanxiangshu.Mission.Obligation.Todo

open System.Threading.Tasks
open Wanxiangshu.Persistence.Journal

/// JS-native effect-shell owner for the Magic Todo membrane.
/// Journal, snapshot, and review ports stay opaque resources; only durable
/// receipts and provider-visible outcomes cross the boundary.
type MagicTodoPreparedHandle =
    private new: bridge: MagicTodoMembrane.PreparedBridge -> MagicTodoPreparedHandle
    member internal Bridge: MagicTodoMembrane.PreparedBridge
    static member Create: bridge: MagicTodoMembrane.PreparedBridge -> MagicTodoPreparedHandle

[<RequireQualifiedAccess>]
module MagicTodoMembraneSurface =
    val prepare:
        handle: JournalHandle ->
        sessionId: string ->
        callId: string ->
        inputCanonical: string ->
        providerInputDigest: string ->
        planComplete: bool ->
        obligations: obj array ->
        state: obj ->
            Task<obj>

    val accept:
        handle: JournalHandle ->
        prepared: MagicTodoPreparedHandle ->
        physicalEvidence: string ->
        observedInputDigest: string ->
        observedOutputDigest: string ->
            Task<obj>

    val appendFact: handle: JournalHandle -> sessionId: string -> factJson: string -> Task<obj>

    val snapshot: handle: JournalHandle -> incumbencyId: string -> obj

    val openIncumbency: handle: JournalHandle -> sessionId: string -> incumbencyId: string -> Task<obj>

    val openLife: handle: JournalHandle -> sessionId: string -> lifeId: string -> Task<obj>

    /// Real Host Before -> controlled builtin executor -> After workflow. Only
    /// successful return from the supplied physical executor reaches After;
    /// no PhysicalSuccessEvidence value crosses this boundary.
    val executeHostSuccess:
        handle: JournalHandle ->
        rawMessages: obj array ->
        sessionId: string ->
        incumbencyId: string ->
        callId: string ->
        args: obj ->
        executor: obj ->
            Task<obj>
