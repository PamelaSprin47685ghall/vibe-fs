namespace Wanxiangshu.OpenCode

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation

[<AllowNullLiteral>]
type internal ExecutionPermitLibrary =
    abstract issue: identity: obj * credit: int64 -> obj
    abstract target: permit: obj * fence: int64 -> obj
    abstract commit: permit: obj * fence: int64 * observed: obj -> string
    abstract releaseBeforeProvider: permit: obj * fence: int64 * observed: obj -> string
    abstract releasePhysical: sessionId: string * physicalUserMessageId: string -> string
    abstract lifecycle: permit: obj * fence: int64 -> obj
    abstract committed: sessionId: string * physicalUserMessageId: string -> obj

module internal ExecutionPermitBinding =
    [<Import("ExecutionPermits", "wanxiangli/permit")>]
    let private permitsClass: obj = jsNative

    [<Emit("new $0()")>]
    let private construct (_cls: obj) : ExecutionPermitLibrary = jsNative

    [<Emit("new WeakMap()")>]
    let projections () : obj = jsNative

    [<Emit("$0.get($1) ?? null")>]
    let projection (_cache: obj) (_permit: obj) : obj = jsNative

    [<Emit("$0.set($1, $2)")>]
    let remember (_cache: obj) (_permit: obj) (_lease: ExecutionAdmissionLease) : unit = jsNative

    let create () = construct permitsClass

    let identity (value: ExecutionAdmissionExactIdentity) : obj =
        createObj
            [ "sessionId" ==> value.SessionId
              "physicalUserMessageId" ==> value.PhysicalUserMessageId
              "role" ==> Roles.roleLabel value.Role
              "participant" ==> value.Participant
              "target"
              ==> createObj [ "model" ==> value.Target.Model; "reasoning" ==> value.Target.Reasoning ] ]

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
        if unbox<bool> (value?ok: obj) then
            Ok(value?value: obj)
        else
            Error(rejection (unbox<string> (value?rejection: obj)))

    let target (value: obj) : ModelRoutingTarget =
        { Model = unbox<string> (value?model: obj)
          Reasoning = unbox<string> (value?reasoning: obj) }

type internal ExecutionCapacityOwner(counters: CapacityTransitionCounters) =
    let permits = ExecutionPermitBinding.create ()
    let projections = ExecutionPermitBinding.projections ()

    let permitOf (lease: ExecutionAdmissionLease) : Result<obj, ExecutionAdmissionRejection> =
        let permit =
            if isNull (box lease) then
                Error ExecutionAdmissionRejection.UnknownLease
            else
                Ok lease.Owner

        permit
        |> Result.bind (fun raw ->
            match ExecutionPermitBinding.projection projections raw with
            | null -> Error ExecutionAdmissionRejection.WrongFence
            | projected when obj.ReferenceEquals(projected, box lease) -> Ok raw
            | _ -> Error ExecutionAdmissionRejection.StaleLease)

    let fenceOf (lease: ExecutionAdmissionLease) : int64 =
        if isNull (box lease) then
            0L
        else
            CapacityFence.value lease.Fence

    let record outcome =
        outcome |> ExecutionPermitBinding.outcome |> counters.Record

    let handleFor identity capacityCredit (permit: obj) =
        match ExecutionPermitBinding.projection projections permit with
        | null ->
            let lease =
                ExecutionAdmissionLease.Create(
                    permit,
                    capacityCredit,
                    CapacityLeaseId.ofValue (unbox<int64> (permit?leaseId: obj)),
                    CapacityFence.ofValue (unbox<int64> (permit?fence: obj)),
                    identity
                )

            ExecutionPermitBinding.remember projections permit lease
            lease
        | lease -> unbox<ExecutionAdmissionLease> lease

    let requiredProjection permit =
        match ExecutionPermitBinding.projection projections permit with
        | null -> invalidOp "execution-model-routing: committed permit has no typed projection"
        | lease -> unbox<ExecutionAdmissionLease> lease

    member _.Issue(identity: ExecutionAdmissionExactIdentity, capacityCredit: CapacityCreditId) =
        permits.issue (ExecutionPermitBinding.identity identity, CapacityCreditId.value capacityCredit)
        |> handleFor identity capacityCredit

    member _.Target(lease: ExecutionAdmissionLease) =
        permitOf lease
        |> Result.bind (fun raw -> permits.target (raw, fenceOf lease) |> ExecutionPermitBinding.result)
        |> Result.map ExecutionPermitBinding.target

    member _.Commit(lease: ExecutionAdmissionLease, observed: ExecutionAdmissionExactIdentity) =
        permitOf lease
        |> Result.map (fun raw -> permits.commit (raw, fenceOf lease, ExecutionPermitBinding.identity observed))
        |> Result.defaultValue "StaleFence"
        |> record

    member _.ReleaseBeforeProvider(lease: ExecutionAdmissionLease, observed: ExecutionAdmissionExactIdentity) =
        permitOf lease
        |> Result.map (fun raw ->
            permits.releaseBeforeProvider (raw, fenceOf lease, ExecutionPermitBinding.identity observed))
        |> Result.defaultValue "StaleFence"
        |> record

    member _.ReleasePhysical(sessionId: string, physicalUserMessageId: string) =
        permits.releasePhysical (sessionId, physicalUserMessageId) |> record

    member _.LifecycleName(lease: ExecutionAdmissionLease) =
        permitOf lease
        |> Result.bind (fun raw -> permits.lifecycle (raw, fenceOf lease) |> ExecutionPermitBinding.result)
        |> Result.map unbox<string>

    member _.TryReadCommittedLease(sessionId: string, physicalUserMessageId: string) =
        permits.committed (sessionId, physicalUserMessageId)
        |> Option.ofObj
        |> Option.map requiredProjection
