namespace Wanxiangshu.Enforcer.Guidance

open Wanxiangshu.OpenCode
open Wanxiangshu.Composition.Durable

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open Wanxiangshu.Host
open Wanxiangshu.Resources
open Wanxiangshu.Execution.Session
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity

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

    let private asMainIfNotCompanion (associations: Map<SessionId, SessionAssociation>) (sessionId: SessionId) =
        if SessionAssociationProjection.isCompanion sessionId associations then
            None
        else
            Some sessionId

    let private tryOwnerMainSession (journal: AgentJournal) (mainOrBloggerSession: SessionId) : SessionId option =
        let associations = (AgentJournal.snapshot journal).AgentProjections.Associations

        match SessionAssociationProjection.tryMainSessionOf mainOrBloggerSession associations with
        | Some owner -> Some owner
        | None ->
            // Already a main / work session (or unassociated): treat as main session id.
            SessionAssociationProjection.tryBloggerOf mainOrBloggerSession associations
            |> Option.map (fun _ -> mainOrBloggerSession)
            |> Option.orElseWith (fun () -> asMainIfNotCompanion associations mainOrBloggerSession)

    let private latestOwnerTipField (journal: AgentJournal) (mainSessionId: SessionId) : string option =
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
            |> Option.map (fun tip -> tip.FieldName)

    let private hasFullTipDelivered (journal: AgentJournal) (mainSessionId: SessionId) (tipName: string) : bool =
        match
            (AgentJournal.snapshot journal).AgentProjections.Sessions
            |> Map.tryFind mainSessionId
        with
        | None -> false
        | Some session ->
            session.TipDelivery
            |> Option.map (TipDeliveryProjection.hasFullDelivered tipName)
            |> Option.defaultValue false

    /// Record that Full tip guidance was injected for this Main session (restart-safe).
    let private recordFullTipDelivered
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (tipName: string)
        : Task<unit> =
        let fact =
            HostFact.TipGuidanceDelivered
                {| SessionId = mainSessionId
                   TipName = tipName
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
        (field: string)
        (rule: EnforcerRule)
        : Task<TipGuidance option> =
        task {
            let tipName = tipNameOf field rule

            if tipName.Length = 0 then
                return None
            elif hasFullTipDelivered journal mainSessionId tipName then
                let guidance: TipGuidance =
                    { TipName = tipName
                      Presentation = TipPresentation.IdentityOnly
                      Text = tipIdentityText tipName }

                return Some guidance
            else
                let text = tipFullText lang tipName rule.MainText
                do! recordFullTipDelivered journal mainSessionId tipName

                let guidance: TipGuidance =
                    { TipName = tipName
                      Presentation = TipPresentation.Full
                      Text = text }

                return Some guidance
        }

    let private guidanceForField
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (field: string)
        : Task<TipGuidance option> =
        task {
            let lang =
                SessionProviderLanguage.tryGet mainSessionId
                |> Option.defaultValue ProviderLanguage.English

            match EnforcerCatalog.tryFindByField field (RuntimeResources.enforcerRulesFor lang) with
            | None -> return None
            | Some rule -> return! guidanceForRule journal mainSessionId lang field rule
        }

    let private guidanceForOwner
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        : Task<TipGuidance option> =
        task {
            match latestOwnerTipField journal mainSessionId with
            | None -> return None
            | Some field -> return! guidanceForField journal mainSessionId field
        }

    /// Resolve Main tip guidance for the auto-injected marker tip half.
    ///
    /// First Full delivery of a tip in this Main session → main.md body (+ name header).
    /// Subsequent → compact `tip: <name>` identity only. Decision is TipDeliveryProjection
    /// (TipGuidanceDelivered fold), not process-local memory.
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
