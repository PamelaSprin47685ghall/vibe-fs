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
open Wanxiangshu.Resources
open Wanxiangshu.Participant.Provider
open Wanxiangshu.OpenCode

/// Tip guidance body for Main auto-injected marker (without pair-programming trailer).
[<RequireQualifiedAccess>]
type TipGuidance =
    {
        TipName: string
        Presentation: TipPresentation
        /// Marker tip half only (Full = name header + main.md; Identity = tip: name).
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

    let private tipFullText (lang: ProviderLanguage) (tipName: string) (mainText: string) : string =
        let body = if isNull mainText then "" else mainText.Trim()

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

    let private latestOwnerTip (journal: AgentJournal) (mainSessionId: SessionId) : RecentTip option =
        match
            (AgentJournal.snapshot journal).AgentProjections.Sessions
            |> Map.tryFind mainSessionId
        with
        | None -> None
        | Some session ->
            session.Enforcement
            |> Option.map EnforcementProjection.recentTips
            |> Option.defaultValue []
            |> List.tryLast

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

    let private tipNameOf (field: string) (rule: EnforcerRule) =
        if not (isNull rule.Name) && rule.Name.Trim().Length > 0 then
            rule.Name.Trim()
        else
            field.Trim()

    let private guidanceForRule
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (lang: ProviderLanguage)
        (occurrence: string)
        (field: string)
        (rule: EnforcerRule)
        : Task<TipGuidance option> =
        task {
            let tipName = tipNameOf field rule

            if tipName.Length = 0 then
                return None
            elif isTipIdentityOnly journal mainSessionId occurrence tipName then
                let guidance: TipGuidance =
                    { TipName = tipName
                      Presentation = TipPresentation.IdentityOnly
                      Text = tipIdentityText tipName }

                return Some guidance
            else
                let text = tipFullText lang tipName rule.MainText
                do! recordFullTipDelivered journal mainSessionId tipName occurrence

                let guidance: TipGuidance =
                    { TipName = tipName
                      Presentation = TipPresentation.Full
                      Text = text }

                return Some guidance
        }

    let private guidanceForField
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (occurrence: string)
        (field: string)
        : Task<TipGuidance option> =
        task {
            let lang = SessionProviderLanguage.languageOf mainSessionId

            match EnforcerCatalog.tryFindByField field (RuntimeResources.enforcerRulesFor lang) with
            | None -> return None
            | Some rule -> return! guidanceForRule journal mainSessionId lang occurrence field rule
        }

    let private guidanceForOwner (journal: AgentJournal) (mainSessionId: SessionId) : Task<TipGuidance option> =
        task {
            match latestOwnerTip journal mainSessionId with
            | None -> return None
            | Some tip -> return! guidanceForField journal mainSessionId tip.CycleId tip.FieldName
        }

    /// Resolve Main tip guidance for the auto-injected marker tip half.
    ///
    /// First Full delivery of a tip occurrence in this Main session → main.md body
    /// (+ name header). A new occurrence of the same TipName gets its own first
    /// Full; a replayed occurrence → compact `tip: <name>` identity only. After
    /// ContextReanchored the full text is restored once without a new first
    /// delivery. Decision is TipDeliveryProjection (TipGuidanceDelivered fold),
    /// not process-local memory.
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
