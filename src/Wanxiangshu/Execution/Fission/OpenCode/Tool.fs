namespace Wanxiangshu.Execution.Fission.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Fission.FissionLaunch
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host.Ports
open Wanxiangshu.OpenCode
open Wanxiangshu.OpenCode.ToolHostCodec
open Wanxiangshu.Participant.Provider

/// OpenCode adapter for Fission (HNI fission lane): decodes the `fission` tool
/// call into a FissionLaunchRequest, implements FissionHostPort over the
/// per-session ToolRuntimeScope, and encodes FissionLaunch's outcome as the
/// OpenCode tool result or the JS-sandbox receipt. No product decision lives here.
module FissionTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let Description = "tool/fission/description"

        [<Literal>]
        let TooFew = "tool/fission/too-few"

        [<Literal>]
        let InvalidOrigin = "tool/fission/invalid-origin"

        [<Literal>]
        let Capacity = "tool/fission/capacity"

        [<Literal>]
        let AlreadyActive = "tool/fission/already-active"

        [<Literal>]
        let Unavailable = "tool/fission/unavailable"

        [<Literal>]
        let Started = "tool/fission/started"

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

    let private hostPort (scope: ToolRuntimeScope) (ctx: HostToolContext) : FissionHostPort =
        { Sessions = scope.Sessions
          Events = scope.EventPort
          DirectoryFor = fun sessionId -> scope.DirectoryFor(SessionId.value sessionId)
          WorkspaceDirectory = scope.WorkspaceDirectory
          LogicalOwnerFor = scope.LogicalOwnerFor
          OwnerRuns = fun () -> scope.RuntimeFor ctx |> Result.map ownerRunsOf
          RegisterLane = registerLane scope
          LaneStarted = scope.RunStarted }

    let private requestOf (ctx: HostToolContext) prompts : FissionLaunchRequest =
        { CallerSessionId = ctx.SessionId
          ToolCallId = ctx.ToolCallId
          ProviderRun = ctx.ProviderRunId
          Prompts = prompts }

    let private consequence language path =
        tomlObjectWithInstructions (ProviderProse.instructionLines language path Map.empty) []

    let private toolResultPath =
        function
        | FissionLaunchRejection.NotSubsession
        | FissionLaunchRejection.AdmissionRejected FissionRejectReason.InvalidOrigin -> Path.InvalidOrigin
        | FissionLaunchRejection.LanePromptsInvalid -> Path.TooFew
        | FissionLaunchRejection.CallerIsLane
        | FissionLaunchRejection.OwnerAlreadyFissioned
        | FissionLaunchRejection.AdmissionRejected FissionRejectReason.AlreadyFissioned -> Path.AlreadyActive
        | FissionLaunchRejection.AdmissionRejected FissionRejectReason.CapacityExceeded -> Path.Capacity
        | FissionLaunchRejection.MissingCaller
        | FissionLaunchRejection.ParentLookupFailed _
        | FissionLaunchRejection.PromptsRejected _
        | FissionLaunchRejection.ContextUnavailable
        | FissionLaunchRejection.AuthorityUnavailable
        | FissionLaunchRejection.RoleNotPermitted
        | FissionLaunchRejection.ObservationUnavailable
        | FissionLaunchRejection.AdmissionRejected _ -> Path.Unavailable

    let private execute (scope: ToolRuntimeScope) (args: HostToolArguments) (ctx: HostToolContext) =
        task {
            let language = ProviderLanguageBinding.forSessionText ctx.SessionId

            let! launched =
                FissionLaunch.launch (hostPort scope ctx) scope.Journal (requestOf ctx (args.Texts "prompts"))

            match launched with
            | Error rejection -> return consequence language (toolResultPath rejection)
            | Ok group -> return tomlObject [ "status", TString "fissioned"; "lane_count", TInt group.LaneCount ]
        }

    let admission: ToolAdmission =
        ToolAdmission.OfficeRole(fun _ r -> OfficeCapability.isAllowed r ToolPermission.Fission)

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "fission"
          Description =
            ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) Path.Description Map.empty
          Arguments = [ "prompts", ToolHostCodec.stringArraySchema factory ]
          Admission = admission
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
