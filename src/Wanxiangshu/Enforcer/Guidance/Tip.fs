namespace Wanxiangshu.Enforcer.Guidance

open Wanxiangshu.Persistence.Journal.JournalOutcome
open System
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Execution.Session
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Enforcer
open Wanxiangshu.Participant.Provider
open Wanxiangshu.OpenCode

/// Tip guidance body for Main auto-injected marker (without pair-programming trailer).
[<RequireQualifiedAccess>]
type TipGuidance =
    {
        TipName: string
        Presentation: TipPresentation
        /// Marker tip half only (Full = name header + authored tip body; Identity = tip: name).
        Text: string
    }

/// Current Main tip guidance: which tip text should the Main see (ENFORCER-*)?
/// Only answers that question — no continuation parking, no blog commit, no repair.
module EnforcerTipGuidance =

    let private tipIdentityText (tipName: string) : string = sprintf "tip: %s" tipName

    let private tipHeading (lang: ProviderLanguage) =
        match lang with
        | ProviderLanguage.English -> "# Enforcer Tip"
        | ProviderLanguage.SimplifiedChinese -> "# Enforcer Tip（规则提示）"

    let private tipFullText (lang: ProviderLanguage) (tipName: string) (body: string) : string =
        let body = if isNull body then "" else body.Trim()

        if body.Length = 0 then
            tipIdentityText tipName
        else
            sprintf "%s\ntip = \"%s\"\n\n%s" (tipHeading lang) tipName body

    let private tryOwnerMainSession (journal: AgentJournal) (mainOrBloggerSession: SessionId) : SessionId option =
        let associations = (AgentJournal.snapshot journal).AgentProjections.Associations

        match SessionAssociationProjection.tryFind mainOrBloggerSession associations with
        | Some { Kind = ManagedSessionKind.WorkSession } -> Some mainOrBloggerSession
        | Some { Kind = ManagedSessionKind.SatelliteSession(owner, SatelliteKind.Companion) } -> Some owner
        | None -> None

    let private isTipIdentityOnly
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (occurrence: string)
        (tipName: string)
        : bool =
        match
            (AgentJournal.snapshot journal).AgentProjections.Sessions
            |> Map.tryFind mainSessionId
        with
        | None -> false
        | Some session ->
            session.TipDelivery
            |> Option.map (TipDeliveryProjection.isIdentityOnly occurrence tipName)
            |> Option.defaultValue false

    /// Record that Full tip guidance was injected for this Main session (restart-safe).
    /// The occurrence identity (RecentTip CycleId) makes the delivery frontier
    /// per-occurrence: a new occurrence of the same TipName gets its own first Full.
    let private recordFullTipDelivered
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (tipName: string)
        (occurrence: string)
        : Task<unit> =
        let fact =
            HostFact.TipGuidanceDelivered
                {| SessionId = mainSessionId
                   TipName = tipName
                   OccurrenceId = Some occurrence
                   Presentation = TipPresentation.Full |}

        task {
            match! AgentJournal.appendAgent (StreamId.Session mainSessionId) None fact journal with
            | Ok _ -> ()
            | Error failure ->
                // Delivery still proceeds with computed Full text; durability failure is
                // visible via diagnostic so a later Identity-only cannot silently strand.
                Diagnostic.emit
                    "tip-guidance-delivery-append-failed"
                    [ "session_id", SessionId.value mainSessionId
                      "tip", tipName
                      "occurrence", occurrence
                      "result", JournalAppendFailure.describe failure ]
        }

    /// Read the authored tip body for one committed cycle and verify its digest
    /// (Algorithm R2). None when the record is missing, carries no tip-text ref,
    /// or the read fails / digest mismatches — never a guessed body.
    let private tryReadTipBody (journal: AgentJournal) (record: EnforcementCycleRecord) : Task<string option> =
        task {
            match record.TipTextRef, record.TipTextDigest with
            | Some tipRef, Some tipDigest ->
                match! journal.Writer.BlobWriter.Read tipRef with
                | Ok text when HostDigest.sha256Hex text = BlobDigest.value tipDigest -> return Some text
                | Ok _ ->
                    Diagnostic.emit
                        "tip-guidance-digest-mismatch"
                        [ "tip", record.TipName; "cycle", ProviderRunIdentity.value record.ProviderRun ]

                    return None
                | Error error ->
                    Diagnostic.emit
                        "tip-guidance-blob-read-failed"
                        [ "tip", record.TipName
                          "cycle", ProviderRunIdentity.value record.ProviderRun
                          "result", error ]

                    return None
            | _ -> return None
        }

    let private guidanceForRecord
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (lang: ProviderLanguage)
        (occurrence: string)
        (record: EnforcementCycleRecord)
        : Task<TipGuidance option> =
        task {
            let tipName = record.TipName

            if isNull tipName || tipName.Trim().Length = 0 then
                return None
            elif isTipIdentityOnly journal mainSessionId occurrence tipName then
                let guidance: TipGuidance =
                    { TipName = tipName
                      Presentation = TipPresentation.IdentityOnly
                      Text = tipIdentityText tipName }

                return Some guidance
            else
                let! body = tryReadTipBody journal record

                match body with
                | None ->
                    // G5: an occurrence without a recoverable authored body is
                    // identity-only; the frontier is never advanced by a fake Full.
                    let guidance: TipGuidance =
                        { TipName = tipName
                          Presentation = TipPresentation.IdentityOnly
                          Text = tipIdentityText tipName }

                    return Some guidance
                | Some text ->
                    let fullText = tipFullText lang tipName text
                    do! recordFullTipDelivered journal mainSessionId tipName occurrence

                    let guidance: TipGuidance =
                        { TipName = tipName
                          Presentation = TipPresentation.Full
                          Text = fullText }

                    return Some guidance
        }

    let private guidanceForOwner (journal: AgentJournal) (mainSessionId: SessionId) : Task<TipGuidance option> =
        task {
            match
                (AgentJournal.snapshot journal).AgentProjections.Sessions
                |> Map.tryFind mainSessionId
            with
            | None -> return None
            | Some session ->
                match session.Enforcement with
                | None -> return None
                | Some enforcement ->
                    match EnforcementProjection.recentTips enforcement |> List.tryLast with
                    | None -> return None
                    | Some tip ->
                        let run = ProviderRunIdentity.create tip.CycleId

                        match EnforcementProjection.tryFindByProviderRun run enforcement with
                        | None -> return None
                        | Some record ->
                            let lang = SessionProviderLanguage.languageOf mainSessionId
                            return! guidanceForRecord journal mainSessionId lang tip.CycleId record
        }

    /// Resolve Main tip guidance for the auto-injected marker tip half.
    ///
    /// First Full delivery of a tip occurrence in this Main session → authored
    /// tip body read back from its blob (+ name header). A new occurrence of the
    /// same TipName gets its own first Full; a replayed occurrence → compact
    /// `tip: <name>` identity only. After ContextReanchored the full text is
    /// restored once without a new first delivery. Decision is
    /// TipDeliveryProjection (TipGuidanceDelivered fold), not process-local memory.
    /// An occurrence without a recoverable authored body is identity-only (G5).
    ///
    /// `mainOrBloggerSession` may be the Main session id (SpikePlugin) or the Blogger
    /// satellite id; owner is resolved through SessionAssociation.
    let resolveTipGuidance (journal: AgentJournal) (mainOrBloggerSession: SessionId) : Task<TipGuidance option> =
        task {
            match tryOwnerMainSession journal mainOrBloggerSession with
            | None -> return None
            | Some mainSessionId -> return! guidanceForOwner journal mainSessionId
        }

    /// Latest tip guidance text for Main marker (Full or Identity). None when no tip.
    let latestTipGuidance (journal: AgentJournal) (mainOrBloggerSession: SessionId) : Task<string option> =
        task {
            let! guidance = resolveTipGuidance journal mainOrBloggerSession
            return guidance |> Option.map (fun g -> g.Text)
        }

    /// Backward-compatible alias: same as latestTipGuidance (Full/Identity, not Nudge-only).
    let latestTipNudge (journal: AgentJournal) (mainOrBloggerSession: SessionId) : Task<string option> =
        latestTipGuidance journal mainOrBloggerSession
