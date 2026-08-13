namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity

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

    let private tipFullText (lang: ProviderLanguage) (tipName: string) (mainText: string) : string =
        let body = if isNull mainText then "" else mainText.Trim()

        if body.Length = 0 then
            tipIdentityText tipName
        else
            let heading =
                match lang with
                | ProviderLanguage.English -> "# Enforcer Tip"
                | ProviderLanguage.SimplifiedChinese -> "# Enforcer Tip（规则提示）"

            sprintf "%s\ntip = \"%s\"\n\n%s" heading tipName body

    let private tryOwnerMainSession (journal: AgentJournal) (mainOrBloggerSession: SessionId) : SessionId option =
        let associations = (AgentJournal.snapshot journal).AgentProjections.Associations

        match SessionAssociationProjection.tryMainSessionOf mainOrBloggerSession associations with
        | Some owner -> Some owner
        | None ->
            // Already a main / work session (or unassociated): treat as main session id.
            match SessionAssociationProjection.tryBloggerOf mainOrBloggerSession associations with
            | Some _ -> Some mainOrBloggerSession
            | None when SessionAssociationProjection.isCompanion mainOrBloggerSession associations -> None
            | None -> Some mainOrBloggerSession

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

    /// Resolve Main tip guidance for the auto-injected marker tip half.
    ///
    /// First Full delivery of a tip in this Main session → main.md body (+ name header).
    /// Subsequent → compact `tip: <name>` identity only. Decision is TipDeliveryProjection
    /// (TipGuidanceDelivered fold), not process-local memory.
    ///
    /// `mainOrBloggerSession` may be the Main session id (SpikePlugin) or the Blogger
    /// satellite id; owner is resolved through SessionAssociation.
    let resolveTipGuidance
        (journal: AgentJournal)
        (mainOrBloggerSession: SessionId)
        : Task<TipGuidance option> =
        task {
            match tryOwnerMainSession journal mainOrBloggerSession with
            | None -> return None
            | Some mainSessionId ->
                match latestOwnerTipField journal mainSessionId with
                | None -> return None
                | Some field ->
                    let lang =
                        SessionProviderLanguage.tryGet mainSessionId
                        |> Option.defaultValue ProviderLanguage.English

                    match EnforcerCatalog.tryFindByField field (RuntimeResources.enforcerRulesFor lang) with
                    | None -> return None
                    | Some rule ->
                        let tipName =
                            if not (isNull rule.Name) && rule.Name.Trim().Length > 0 then
                                rule.Name.Trim()
                            else
                                field.Trim()

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

    /// Latest tip guidance text for Main marker (Full or Identity). None when no tip.
    let latestTipGuidance (journal: AgentJournal) (mainOrBloggerSession: SessionId) : Task<string option> =
        task {
            let! guidance = resolveTipGuidance journal mainOrBloggerSession
            return guidance |> Option.map (fun g -> g.Text)
        }

    /// Backward-compatible alias: same as latestTipGuidance (Full/Identity, not Nudge-only).
    let latestTipNudge (journal: AgentJournal) (mainOrBloggerSession: SessionId) : Task<string option> =
        latestTipGuidance journal mainOrBloggerSession
