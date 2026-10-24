namespace Wanxiangshu.Execution.Fission.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Fission.FissionLaunch
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Host.Ports
open Wanxiangshu.OpenCode
open Wanxiangshu.Foundation.HostSession
open Wanxiangshu.Context.Scope
open Wanxiangshu.OpenCode.ToolHostCodec
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Execution.Tool

/// OpenCode adapter for Fission (HNI fission lane): decodes the `fission` tool
/// call into a FissionLaunchRequest, implements FissionHostPort over the
/// per-session ToolRuntimeScope, and encodes FissionLaunch's outcome as the
/// OpenCode tool result or the JS-sandbox receipt. No product decision lives here.
module FissionTool =

    /// The product policy of the tool — its prose vocabulary, its prose
    /// rendering and its office entitlement — lives in the product lane
    /// (`FissionToolPolicy`). This adapter owns only the wire encoding.

    type FissionReceipt =
        { GroupId: string
          LaneCount: int
          Lanes: (int * string) list }

    let private ptyCompletionOf (item: PtyJoinItem) : FissionPtyCompletion =
        match item with
        | PtyJoinItem.PtyExited exit ->
            { PtyId = exit.PtyId
              Status = FissionPtyStatus.Exited
              Outcome = exit.Outcome
              Code = None
              Message = None }
        | PtyJoinItem.PtyFailed failure ->
            { PtyId = failure.PtyId
              Status = FissionPtyStatus.Failed
              Outcome = failure.Outcome
              Code = Some failure.Code
              Message = Some failure.Message }
        | PtyJoinItem.PtyAborted aborted ->
            { PtyId = aborted.PtyId
              Status = FissionPtyStatus.Aborted
              Outcome = aborted.Outcome
              Code = Some aborted.Code
              Message = Some aborted.Message }

    let private ownerRunsOf (runtime: HostForkRuntime) : FissionOwnerRuns =
        { OutstandingAgentRuns = runtime.SnapshotOutstandingAgentRuns
          OutstandingPtyRuns = runtime.SnapshotOutstandingPtyRuns
          SubscribePtyCompletion =
            fun listener -> runtime.SubscribePtyCompletion(fun item -> listener (ptyCompletionOf item)) }

    let private registerLane (scope: ToolRuntimeScope) laneId physicalParent directory =
        scope.RegisterPhysicalParent(laneId, physicalParent)

        directory
        |> Option.iter (fun path -> scope.RegisterDirectory(SessionId.value laneId, path))

    let private readOwnerView (snapshot: ISessionSnapshotPort) (sessionId: SessionId) : Task<string option> =
        task {
            match! snapshot.GetMessages sessionId with
            | Error _ -> return None
            | Ok messages ->
                let filtered = Wanxiangshu.Context.Scope.SnapshotScope.filterSnapshot messages
                let toml = Wanxiangshu.Context.Scope.SessionView.renderJsWithTable "context" filtered
                return Some toml
        }

    let private captureOwnerView (snapshotOpt: ISessionSnapshotPort option) (sessionId: SessionId) : Task<string option> =
        match snapshotOpt with
        | None -> Task.FromResult None
        | Some snapshot -> readOwnerView snapshot sessionId

    let private hostPort (scope: ToolRuntimeScope) (ctx: HostToolContext) : FissionHostPort =
        { Sessions = scope.Sessions
          Events = scope.EventPort
          DirectoryFor = fun sessionId -> scope.DirectoryFor(SessionId.value sessionId)
          WorkspaceDirectory = scope.WorkspaceDirectory
          LogicalOwnerFor = scope.LogicalOwnerFor
          OwnerRuns = fun () -> scope.RuntimeFor ctx |> Result.map ownerRunsOf
          RegisterLane = registerLane scope
          LaneStarted = scope.RunStarted
          OwnerView = Some(captureOwnerView scope.Snapshot) }

    let private requestOf (ctx: HostToolContext) prompts : FissionLaunchRequest =
        { CallerSessionId = ctx.SessionId
          ToolCallId = ctx.ToolCallId
          ProviderRun = ctx.ProviderRunId
          Prompts = prompts }

    /// The wire mapping from a launch rejection to the semantic prose path
    /// the refusal is reported under. The paths themselves are product
    /// policy (`FissionToolPolicy.Path`).
    let private toolResultPath =
        function
        | FissionLaunchRejection.NotSubsession
        | FissionLaunchRejection.AdmissionRejected FissionRejectReason.InvalidOrigin ->
            FissionToolPolicy.Path.InvalidOrigin
        | FissionLaunchRejection.LanePromptsInvalid -> FissionToolPolicy.Path.TooFew
        | FissionLaunchRejection.CallerIsLane
        | FissionLaunchRejection.OwnerAlreadyFissioned
        | FissionLaunchRejection.AdmissionRejected FissionRejectReason.AlreadyFissioned ->
            FissionToolPolicy.Path.AlreadyActive
        | FissionLaunchRejection.AdmissionRejected FissionRejectReason.CapacityExceeded ->
            FissionToolPolicy.Path.Capacity
        | FissionLaunchRejection.MissingCaller
        | FissionLaunchRejection.ParentLookupFailed _
        | FissionLaunchRejection.PromptsRejected _
        | FissionLaunchRejection.ContextUnavailable
        | FissionLaunchRejection.AuthorityUnavailable
        | FissionLaunchRejection.RoleNotPermitted
        | FissionLaunchRejection.ObservationUnavailable
        | FissionLaunchRejection.AdmissionRejected _ -> FissionToolPolicy.Path.Unavailable

    let private consequence language path =
        ToolResultText.tomlObjectWithInstructions (FissionToolPolicy.instructions language path) []

    let private execute (scope: ToolRuntimeScope) (args: HostToolArguments) (ctx: HostToolContext) =
        task {
            let language = ProviderLanguageBinding.forSessionText ctx.SessionId

            let! launched =
                FissionLaunch.launch (hostPort scope ctx) scope.Journal (requestOf ctx (args.Texts "prompts"))

            match launched with
            | Error rejection -> return consequence language (toolResultPath rejection)
            | Ok group ->
                return
                    ToolResultText.tomlObject
                        [ "status", ToolResultText.TString "fissioned"
                          "lane_count", ToolResultText.TInt group.LaneCount ]
        }


    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "fission"
          Description = FissionToolPolicy.describe (ProviderLanguageBinding.readGlobalPreference ())
          Arguments = [ "prompts", ToolHostCodec.stringArraySchema factory ]
          Execute = execute scope }

    let private isLaneStartFailure (message: string) =
        message.StartsWith("lane create failed:", StringComparison.Ordinal)
        || message.StartsWith("lane start failed:", StringComparison.Ordinal)

    let private receiptCode =
        function
        | FissionLaunchRejection.MissingCaller
        | FissionLaunchRejection.ContextUnavailable
        | FissionLaunchRejection.AuthorityUnavailable -> "context-unavailable"
        | FissionLaunchRejection.LanePromptsInvalid -> "invalid-prompt"
        | FissionLaunchRejection.CallerIsLane
        | FissionLaunchRejection.OwnerAlreadyFissioned
        | FissionLaunchRejection.ObservationUnavailable
        | FissionLaunchRejection.AdmissionRejected FissionRejectReason.AlreadyFissioned -> "not-ready"
        | FissionLaunchRejection.AdmissionRejected(FissionRejectReason.RuntimeUnavailable message) when
            isLaneStartFailure message
            ->
            "lane-start-failed"
        | FissionLaunchRejection.ParentLookupFailed _
        | FissionLaunchRejection.NotSubsession
        | FissionLaunchRejection.PromptsRejected _
        | FissionLaunchRejection.RoleNotPermitted
        | FissionLaunchRejection.AdmissionRejected _ -> "fission-unavailable"

    let private decodeLanesCharge (args: HostToolArguments) (charge: string) : string list =
        match args.OptionalNonNegativeInteger "lanes" with
        | Ok(Some laneCount) when laneCount >= 2 && not (String.IsNullOrWhiteSpace charge) ->
            List.replicate laneCount charge
        | _ -> []

    let private decodePromptsForReceipt (args: HostToolArguments) : string list =
        let explicit = args.Texts "prompts"

        if not (List.isEmpty explicit) then
            explicit
        else
            decodeLanesCharge args (args.Text "charge")

    let private receiptOf (group: FissionLaunched) : FissionReceipt =
        { GroupId = group.GroupId
          LaneCount = group.LaneCount
          Lanes = group.Lanes |> List.map (fun lane -> lane.Index, SessionId.value lane.SessionId) }

    let executeReceipt
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        (args: HostToolArguments)
        : Task<Result<FissionReceipt, string>> =
        task {
            let! launched =
                FissionLaunch.launch
                    (hostPort scope context)
                    scope.Journal
                    (requestOf context (decodePromptsForReceipt args))

            return launched |> Result.map receiptOf |> Result.mapError receiptCode
        }
