namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation

/// The slice of the `wanxiangli/permit` exact execution permit owner (CUT-10)
/// this host owner reads. Issue, the Pending → Committed → Released lifecycle,
/// the two mutually exclusive releases, exact identity checks and the
/// outcome classification are decided there; this adapter keeps the F#
/// lease handle, maps outcomes and records the shared transition counters.
type internal ExecutionPermitLibrary =
    abstract issue: identity: obj * credit: float -> obj
    abstract target: permit: obj * fence: float -> obj
    abstract commit: permit: obj * fence: float * observed: obj -> string
    abstract releaseBeforeProvider: permit: obj * fence: float * observed: obj -> string
    abstract releasePhysical: sessionId: string * physicalUserMessageId: string -> string
    abstract lifecycle: permit: obj * fence: float -> obj
    abstract committed: sessionId: string * physicalUserMessageId: string -> obj

module internal ExecutionPermitBinding =
    [<Import("ExecutionPermits", "wanxiangli/permit")>]
    let private permitsClass: obj = jsNative

    [<Emit("new $0()")>]
    let private construct (cls: obj) : ExecutionPermitLibrary = jsNative

    let create () = construct permitsClass

    let identity (value: ExecutionAdmissionExactIdentity) : obj =
        createObj
            [ "sessionId" ==> value.SessionId
              "physicalUserMessageId" ==> value.PhysicalUserMessageId
              "role" ==> Roles.roleLabel value.Role
              "participant" ==> value.Participant
              "target" ==> createObj [ "model" ==> value.Target.Model; "reasoning" ==> value.Target.Reasoning ] ]

    let outcome (value: string) =
        match value with
        | "Applied" -> CapacityTransitionOutcome.Applied
        | "AlreadyApplied" -> CapacityTransitionOutcome.AlreadyApplied
        | "StaleFence" -> CapacityTransitionOutcome.StaleFence
        | "Conflict" -> CapacityTransitionOutcome.Conflict
        | other -> invalidOp (sprintf "execution-model-routing: unknown permit outcome '%s'" other)

    let rejection (value: string) =
        match value with
        | "UnknownLease" -> ExecutionAdmissionRejection.UnknownLease
        | "WrongFence" -> ExecutionAdmissionRejection.WrongFence
        | "StaleLease" -> ExecutionAdmissionRejection.StaleLease
        | "WrongSession" -> ExecutionAdmissionRejection.WrongSession
        | "WrongPhysicalUserMessage" -> ExecutionAdmissionRejection.WrongPhysicalUserMessage
        | "WrongRole" -> ExecutionAdmissionRejection.WrongRole
        | "WrongParticipant" -> ExecutionAdmissionRejection.WrongParticipant
        | "WrongTarget" -> ExecutionAdmissionRejection.WrongTarget
        | "IllegalTransition" -> ExecutionAdmissionRejection.IllegalTransition
        | "OppositeTerminalConflict" -> ExecutionAdmissionRejection.OppositeTerminalConflict
        | other -> invalidOp (sprintf "execution-model-routing: unknown permit rejection '%s'" other)

    let result (value: obj) : Result<obj, ExecutionAdmissionRejection> =
        if unbox<bool> value?ok then
            Ok(value?value: obj)
        else
            Error(rejection (unbox<string> value?rejection))

type internal ExecutionCapacityOwner(counters: CapacityTransitionCounters) =
    let permits = ExecutionPermitBinding.create ()
    // The F# handle of each session's current permit; Owner is the library permit.
    let leaseBySession = Dictionary<string, ExecutionAdmissionLease>()
    // DSL-MUTABLE: resource — host handle identity, kept in step with the library permit
    let mutable nextLeaseId = CapacityLeaseId.initial
    let mutable nextFence = CapacityFence.initial

    let permitOf (lease: ExecutionAdmissionLease) : obj =
        if isNull (box lease) then null else lease.Owner

    let fenceOf (lease: ExecutionAdmissionLease) : float =
        if isNull (box lease) then 0.0 else float (CapacityFence.value lease.Fence)

    let record (outcome: string) =
        outcome |> ExecutionPermitBinding.outcome |> counters.Record

    let handleFor (identity: ExecutionAdmissionExactIdentity) (capacityCredit: CapacityCreditId) (permit: obj) =
        match leaseBySession.TryGetValue identity.SessionId with
        | true, lease when obj.ReferenceEquals(lease.Owner, permit) -> lease
        | _ ->
            nextLeaseId <- CapacityLeaseId.next nextLeaseId
            nextFence <- CapacityFence.next nextFence

            if float (CapacityFence.value nextFence) <> unbox<float> permit?fence then
                invalidOp "execution-model-routing: host lease fence diverged from the exact permit owner"

            let lease =
                ExecutionAdmissionLease.Create(permit, capacityCredit, nextLeaseId, nextFence, identity)

            leaseBySession.[identity.SessionId] <- lease
            lease

    member _.Issue(identity: ExecutionAdmissionExactIdentity, capacityCredit: CapacityCreditId) =
        permits.issue (ExecutionPermitBinding.identity identity, float (CapacityCreditId.value capacityCredit))
        |> handleFor identity capacityCredit

    member _.Target(lease: ExecutionAdmissionLease) =
        permits.target (permitOf lease, fenceOf lease)
        |> ExecutionPermitBinding.result
        |> Result.map (fun _ -> lease.Identity.Target)

    member _.Commit(lease: ExecutionAdmissionLease, observed: ExecutionAdmissionExactIdentity) =
        permits.commit (permitOf lease, fenceOf lease, ExecutionPermitBinding.identity observed)
        |> record

    member _.ReleaseBeforeProvider(lease: ExecutionAdmissionLease, observed: ExecutionAdmissionExactIdentity) =
        permits.releaseBeforeProvider (permitOf lease, fenceOf lease, ExecutionPermitBinding.identity observed)
        |> record

    member _.ReleasePhysical(sessionId: string, physicalUserMessageId: string) =
        permits.releasePhysical (sessionId, physicalUserMessageId) |> record

    member _.LifecycleName(lease: ExecutionAdmissionLease) =
        permits.lifecycle (permitOf lease, fenceOf lease)
        |> ExecutionPermitBinding.result
        |> Result.map unbox<string>

    /// Read-only exact committed lease query; a pending, releasing, released,
    /// absent or wrong-physical lease is not an executable resource.
    member _.TryReadCommittedLease(sessionId: string, physicalUserMessageId: string) =
        let permit = permits.committed (sessionId, physicalUserMessageId)

        if isNull permit then
            None
        else
            match leaseBySession.TryGetValue sessionId with
            | true, lease when obj.ReferenceEquals(lease.Owner, permit) -> Some lease
            | _ -> invalidOp "execution-model-routing: committed permit has no host lease handle"
