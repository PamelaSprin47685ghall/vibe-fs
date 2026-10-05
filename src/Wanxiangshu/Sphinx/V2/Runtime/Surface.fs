namespace Wanxiangshu.Sphinx.V2.Runtime

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Sphinx.V2.Core
open Wanxiangshu.Sphinx.V2.Plugins

/// The JS-native surface for the decision loop. Pure: no store, no clock, no model call.
///
/// A caller supplies plain records: planId, scopeId, location (NaN for none), rank
/// (-1 for never compared), and a kind string naming the estimate kind. Only usable
/// estimates participate; their rank decides, never their id order.
module Surface =

    let private contributionKind =
        function
        | "model-estimate" -> ContributionKind.ModelEstimate("model-estimate", "surface")
        | "ordinal-only" -> ContributionKind.OrdinalOnly
        | "single-response-provisional" -> ContributionKind.SingleResponseProvisional
        | _ -> ContributionKind.Unestimated

    let private estimateKind =
        function
        | ContributionKind.ModelEstimate(modelRef, approximation) -> EstimateKind.ModelEstimate(modelRef, approximation)
        | ContributionKind.OrdinalOnly -> EstimateKind.OrdinalOnly
        | ContributionKind.SingleResponseProvisional -> EstimateKind.SingleResponseProvisional
        | ContributionKind.Unestimated -> EstimateKind.Unestimated

    let private kindName =
        function
        | EstimateKind.ModelEstimate _ -> "model-estimate"
        | EstimateKind.OrdinalOnly -> "ordinal-only"
        | EstimateKind.SingleResponseProvisional -> "single-response-provisional"
        | EstimateKind.Unestimated -> "unestimated"

    /// A rank present at all? A JS `null` coerced to zero would place an unestimated
    /// plan first, which is exactly the failure this guards against.
    [<Emit("$0?.Rank !== null && $0?.Rank !== undefined")>]
    let private rankPresent (candidate: obj) : bool = jsNative

    [<Emit("Number($0?.Rank)")>]
    let private rankOf (candidate: obj) : float = jsNative

    /// `Rank` may be absent, which means 'never compared'. Reading a missing rank as
    /// zero would place an unestimated plan first.
    let private plainOf (candidate: obj) : ContributionEstimate =
        let present = rankPresent candidate
        let rankValue = rankOf candidate
        let rank = if present then int rankValue else -1
        let location = float candidate?Location

        { PlanId = string candidate?PlanId
          ScopeId = string candidate?ScopeId
          Location = if Double.IsNaN location then None else Some location
          Rank = if rank >= 0 then Some rank else None
          Kind = contributionKind (string candidate?Kind) }

    /// Ranked within one scope. Unestimated plans are never placed, so a missing rank
    /// stays missing rather than becoming a rank of zero.
    let decisionRank (scopeId: string) (candidates: obj list) : obj array =
        candidates
        |> List.ofSeq
        |> List.map plainOf
        |> List.filter (fun item -> item.ScopeId = scopeId && DecisionModel.usable item)
        |> List.sortBy (fun item -> defaultArg item.Rank System.Int32.MaxValue)
        |> List.map (fun item ->
            box
                {| PlanId = item.PlanId
                   ScopeId = item.ScopeId
                   Rank = item.Rank
                   Kind = item.Kind |> estimateKind |> kindName |})
        |> List.toArray

    /// Whether the set can support a numeric comparison. A provisional order is a real
    /// answer with a real limitation: usable for a first decision, never for a numeric
    /// comparison.
    let decisionSupportsNumeric (candidates: obj list) : bool =
        candidates
        |> List.ofSeq
        |> List.map plainOf
        |> DecisionModel.supportsNumericComparison

    /// Selects the highest-ranked plan in one scope and returns why. The estimate kind
    /// survives selection so a degraded ordering stays labelled.
    let decisionSelect (scopeId: string) (candidates: obj list) : obj =
        let estimates =
            candidates
            |> List.ofSeq
            |> List.map plainOf
            |> List.filter (fun item -> item.ScopeId = scopeId)
            |> List.map (fun item ->
                ({ PlanId = PlanId.create item.PlanId
                   ScopeId = item.ScopeId
                   Kind = estimateKind item.Kind
                   Location = item.Location
                   Rank = item.Rank }
                : PlanEstimate))

        let chosen =
            Decision.choose scopeId "highest-estimate" "1" (Map.empty) (Map.empty) [] [] [] estimates []

        match chosen with
        | Ok outcome ->
            let selected = outcome.Selected

            box
                {| SelectedPlanId = PlanId.value selected.PlanId
                   SelectedRank = selected.Rank
                   SelectedKind = kindName selected.Kind
                   Alternatives = outcome.Alternatives |> List.map (fun plan -> PlanId.value plan.PlanId) |}
        | Error fault -> box {| error = fault.Code |}

    let decisionExclude (planId: string) (reason: string) : obj =
        let excluded = Decision.exclude (PlanId.create planId) reason

        box
            {| PlanId = PlanId.value excluded.PlanId
               Reason = excluded.Reason |}

    /// Classify a state into the next action, reported as plain values. A caller reads
    /// the outcome name and reason without touching the DU representation, so the
    /// boundary gate can hold.
    let classifyOutcome (state: InquiryState) : obj =
        let outcome = Driver.classify state

        let name () =
            match outcome with
            | AdvanceOutcome.AwaitingResults _ -> "awaiting-results"
            | AdvanceOutcome.InputRequired _ -> "input-required"
            | AdvanceOutcome.NoRunnalbeWork _ -> "no-runnable-work"
            | AdvanceOutcome.Terminal _ -> "terminal"
            | AdvanceOutcome.RefinementPending _ -> "refinement-pending"

        let detail () =
            match outcome with
            | AdvanceOutcome.AwaitingResults count -> string count
            | AdvanceOutcome.InputRequired authorization -> authorization
            | AdvanceOutcome.NoRunnalbeWork reason -> reason
            | AdvanceOutcome.Terminal status -> status
            | AdvanceOutcome.RefinementPending remaining -> string remaining

        box
            {| Outcome = name ()
               Detail = detail () |}

    // --- Default profile ---------------------------------------------------------

    /// The declared default. Real models and real quotas have no implicit default: they
    /// come from an authorized Host/provider configuration.
    let profileDefault () : DefaultProfile = Profile.defaultProfile

    let profileValidate (profile: DefaultProfile) : Result<DefaultProfile, ProfileError> = Profile.validate profile

    let profileConfigInput () : string =
        Profile.configHashInput Profile.defaultProfile

    let profileClaimsIndependence (profile: DefaultProfile) : bool = Profile.claimsIndependence profile

    /// A copy of the declared default with one engineering field replaced, so a test can
    /// build an inadmissible profile without hand-assembling the whole record.
    let profileWith (changes: obj) : DefaultProfile =
        let declared = Profile.defaultProfile

        let changed name fallback =
            let value = changes?(name)
            let present = (value: obj) <> null

            match present with
            | true -> box value
            | false -> box fallback

        { declared with
            MaxActivePlanCards = int (unbox<float> (changed "MaxActivePlanCards" (float declared.MaxActivePlanCards)))
            FitMaxIterations = int (unbox<float> (changed "FitMaxIterations" (float declared.FitMaxIterations)))
            FitGradientTolerance = unbox<float> (changed "FitGradientTolerance" declared.FitGradientTolerance)
            ThetaL2 = unbox<float> (changed "ThetaL2" declared.ThetaL2)
            ExecutionMode = unbox<ExecutionMode> (changed "ExecutionMode" declared.ExecutionMode) }

    // --- Stop ---------------------------------------------------------------------

    let stopRanked () : obj = box StopReason.ModelRankedStop

    let stopOrdinal () : obj = box StopReason.OrdinalStop

    let stopResourceLimited () : obj = box StopReason.ResourceLimited

    let stopNoPlan () : obj = box StopReason.NoExecutablePlan

    let stopCancelled () : obj = box StopReason.UserCancelled

    let stopCertified (modelRef: string) : obj =
        box (StopReason.CertifiedWithinModel modelRef)

    let stopReasonName (reason: obj) : string =
        Stop.reasonName (unbox<StopReason> reason)

    let stopIsModelRelative (reason: obj) : bool =
        Stop.isModelRelative (unbox<StopReason> reason)

    // --- Provider usage ------------------------------------------------------------

    /// One Host observation, as the provider adapter reports it. `UsageUnresolved` is
    /// distinct from zero usage: the first means the Host did not tell us, the second
    /// means nothing was consumed.
    let providerOutcome
        (text: string)
        (inputTokens: int64)
        (outputTokens: int64)
        (calls: int64)
        (usageUnresolved: bool)
        : Wanxiangshu.Sphinx.V2.Plugins.ProviderOutcome =
        { Text = text
          InputTokens = inputTokens
          OutputTokens = outputTokens
          Calls = calls
          MoneyMinor = 0L
          UsageUnresolved = usageUnresolved
          PhysicalRunRef = Some "run-ref" }

    let providerUsageUnresolved (outcome: Wanxiangshu.Sphinx.V2.Plugins.ProviderOutcome) : bool =
        Wanxiangshu.Sphinx.V2.Plugins.ProviderAdapter.keepsReservation outcome

    let providerUsageCounts (outcome: Wanxiangshu.Sphinx.V2.Plugins.ProviderOutcome) : int64 * int64 * int64 =
        Wanxiangshu.Sphinx.V2.Plugins.ProviderAdapter.usageCountsOf outcome

    // --- Recovery -----------------------------------------------------------------

    let private crashWindow (window: string) : CrashWindow option =
        match window with
        | "DispatchPending" -> Some CrashWindow.DispatchPending
        | "ReceiptPending" -> Some CrashWindow.ReceiptPending
        | "RunningUnmarked" -> Some CrashWindow.RunningUnmarked
        | "ResultPending" -> Some CrashWindow.ResultPending
        | "InterpretationPending" -> Some CrashWindow.InterpretationPending
        | "CancelPending" -> Some CrashWindow.CancelPending
        | "CommitPending" -> Some CrashWindow.CommitPending
        | _ -> None

    let recoveryAction (window: string) : string =
        crashWindow window
        |> Option.map (fun current -> (Recovery.reconcile current).Action)
        |> Option.defaultValue "unknown"

    let recoveryMaySpend (window: string) : bool =
        crashWindow window
        |> Option.map (Recovery.reconcile >> Recovery.maySpend)
        |> Option.defaultValue false
