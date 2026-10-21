namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation.HostSession

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal

/// HOST-013：永久 pair-programming auto-injected pairs。
module PairProgrammingThoughtTransform =

    [<RequireQualifiedAccess>]
    type CursorPresentationOwner = | PairGuidance

    /// HOST-013 English canonical used by tests; production loads via session language.
    val text: string

    /// Provider id on a Host message (`info.providerID` or `info.model.providerID`).
    val providerIdOfMessage: rawMsg: obj -> string option

    /// Most recent provider id on the transcript (assistant `providerID` or user `model.providerID`).
    val providerIdFromMessages: rawMessages: obj list -> string option

    /// HOST-013 borrows the Host-owned skill wire. Empty name is injection-only;
    /// real non-empty skill names remain ordinary executable Host skills.
    val toolName: string

    /// HOST-013：marker 身份仅按 `info.source`。
    val isPairProgrammingThought: rawMsg: obj -> bool

    /// Active empty-name skill loads are reserved for synthetic injection only.
    val reprimandText: lang: ProviderLanguage option -> string

    val cursorGuidanceSeparator: string

    val stripCursorSuffixesWithJournal:
        journal: AgentJournal ->
        sessionId: SessionId ->
        owner: CursorPresentationOwner ->
        suffixTexts: string list ->
        rawMessages: obj list ->
            Task<Result<obj list, string>>

    val internal gapsAroundAddress:
        gapCtor: (TranscriptMessageAddress -> TranscriptGap) ->
        message: obj ->
        errorMsg: string ->
            Result<TranscriptGap * TranscriptGap, string>

    val decideCurrentPlacement: realMessages: obj list -> Result<(TranscriptGap * TranscriptGap) option, string>

    val applyAnchorView: sessionId: string -> messages: obj list -> obj list * string option

    val maybeInjectGuideline:
        journal: AgentJournal option ->
        projectionSessionIdOpt: string option ->
        sessionStartedAt: DateTimeOffset option ->
        clock: IClockPort ->
        terminateSession: (SessionId -> string -> Task<Result<unit, string>>) ->
        language: ProviderLanguage ->
        restartGuidance: string option ->
        markRestartGuidanceDelivered: (unit -> unit) ->
        outObj: obj ->
            Task

    val tryInject:
        journal: AgentJournal option ->
        sessionId: string option ->
        markerText: string ->
        rawMessages: obj list ->
            Task<Result<obj list, string>>
