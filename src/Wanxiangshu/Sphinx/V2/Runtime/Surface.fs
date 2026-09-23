namespace Wanxiangshu.Sphinx.V2.Runtime

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Sphinx.V2.Core
open Wanxiangshu.Sphinx.V2.Plugins
open Wanxiangshu.Sphinx.V2.Plugins

/// The JS-native surface for the decision loop. Pure: no store, no clock, no model call.
///
/// A caller supplies plain records: planId, scopeId, location (NaN for none), rank
/// (-1 for never compared), and a kind string naming the estimate kind. `Rank` alone
/// decides — id order never does.
module Surface =

    /// The plain shape the surface reads; a JS caller builds one with an object literal.
    type PlainEstimate =
        { PlanId: string
          ScopeId: string
          Location: float
          Rank: int
          Kind: string }

    /// A rank present at all? A JS `null` coerced to zero would place an unestimated
    /// plan first, which is exactly the failure this guards against.
    [<Emit("$0?.Rank !== null && $0?.Rank !== undefined")>]
    let private rankPresent (candidate: obj) : bool = jsNative

    [<Emit("Number($0?.Rank)")>]
    let private rankOf (candidate: obj) : float = jsNative

    /// `Rank` may be absent, which means 'never compared'. Reading a missing rank as
    /// zero would place an unestimated plan first.
    let private plainOf (candidate: obj) : PlainEstimate =
        let present = rankPresent candidate
        let rankValue = rankOf candidate

        { PlanId = string candidate?PlanId
          ScopeId = string candidate?ScopeId
          Location = float candidate?Location
          Rank = if present then int rankValue else -1
          Kind = string candidate?Kind }

    /// Ranked within one scope. Unestimated plans are never placed, so a missing rank
    /// stays missing rather than becoming a rank of zero.
    let decisionRank (scopeId: string) (candidates: obj list) : obj array =
        candidates
        |> List.ofSeq
        |> List.map plainOf
        |> List.filter (fun item -> item.Rank >= 0 && item.ScopeId = scopeId)
        |> List.sortBy (fun item -> item.Rank)
        |> List.map (fun item ->
            box
                {| PlanId = item.PlanId
                   ScopeId = item.ScopeId
                   Rank = item.Rank
                   Kind = item.Kind |})
        |> List.toArray

    /// Whether the set can support a numeric comparison. A provisional order is a real
    /// answer with a real limitation: usable for a first decision, never for a numeric
    /// comparison.
    let decisionSupportsNumeric (candidates: obj list) : bool =
        let kinds =
            candidates |> List.ofSeq |> List.map plainOf |> List.map (fun item -> item.Kind)

        let modeled = kinds |> List.filter (fun kind -> kind.StartsWith "model-estimate")

        List.length modeled = List.length kinds

    /// Selects the highest-ranked plan in one scope and returns why. The estimate kind
    /// survives selection so a degraded ordering stays labelled.
    let decisionSelect (scopeId: string) (candidates: obj list) : obj =
        let ranked =
            candidates
            |> List.ofSeq
            |> List.map plainOf
            |> List.filter (fun item -> item.Rank >= 0 && item.ScopeId = scopeId)
            |> List.sortBy (fun item -> item.Rank)
            |> List.map (fun item ->
                ({ PlanId = PlanId.create item.PlanId
                   ScopeId = item.ScopeId
                   Kind = EstimateKind.ModelEstimate(item.Kind, "surface")
                   Location =
                     if System.Double.IsNaN item.Location then
                         None
                     else
                         Some item.Location
                   Rank = Some item.Rank }
                : PlanEstimate))

        let chosen =
            Decision.choose scopeId "highest-estimate" "1" (Map.empty) (Map.empty) [] [] [] ranked []

        match chosen with
        | Ok outcome ->
            let selected = outcome.Selected

            box
                {| SelectedPlanId = PlanId.value selected.PlanId
                   SelectedRank = selected.Rank
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

    /// The one action each crash window may take. Windows whose action is pure never
    /// bill; windows that still have real work to do may.
    let recoveryAction (window: string) : string =
        match window with
        | "DispatchPending" -> "dispatch"
        | "ReceiptPending" -> "reconcile-by-intent"
        | "RunningUnmarked" -> "reconcile-by-intent"
        | "ResultPending" -> "accept-if-valid"
        | "InterpretationPending" -> "interpret"
        | "CancelPending" -> "await-terminal"
        | "CommitPending" -> "commit-or-render"
        | _ -> "unknown"

    let recoveryMaySpend (window: string) : bool =
        match window with
        | "DispatchPending"
        | "CommitPending" -> true
        | _ -> false
