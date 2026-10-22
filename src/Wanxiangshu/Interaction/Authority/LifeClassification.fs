namespace Wanxiangshu.Interaction.Authority

/// The closed set of closure kinds and the agent-owner life labels, mirroring
/// the canonical law in `wanxiangli/src/authority/ClosureLaw.fs`.
///
/// The host records the expected closure kind of an accepted authority root
/// atomically with the acceptance (interaction-authority-018 / C1 closure), so
/// the law is mirrored here as a pure module rather than reached through the
/// JavaScript bridge: the bridge only answers for already-rooted lifecycles,
/// while the host must be able to derive the kind *before* the root exists.
[<RequireQualifiedAccess>]
module AuthorityLifeClassification =
    /// The closed set of expected closure kinds.
    let closureKindLabels =
        [ "HumanRootManagerLife"
          "HumanRootManagedRun"
          "AgentOwnerChildWork"
          "AgentOwnerAttachedWork"
          "AgentOwnerInternalLeaf" ]

    /// The closed set of agent-owner life labels.
    let agentOwnerLifeLabels = [ "Child"; "Attached"; "InternalLeaf" ]

    /// The root kind label of a closure kind: "HumanRoot" | "AgentOwnerRoot".
    let rootKindOfKind (closureKind: string) : string =
        match closureKind with
        | "HumanRootManagerLife"
        | "HumanRootManagedRun" -> "HumanRoot"
        | "AgentOwnerChildWork"
        | "AgentOwnerAttachedWork"
        | "AgentOwnerInternalLeaf" -> "AgentOwnerRoot"
        | other -> invalidArg "closureKind" (sprintf "unknown closure kind '%s'" other)

    /// The closure witness kind a closure of this kind must carry.
    ///
    /// A Manager life closes with a life witness; every other closure kind
    /// closes with a work witness.
    let witnessKindOf (closureKind: string) : string option =
        match closureKind with
        | "HumanRootManagerLife" -> Some "LifeCompleted"
        | "HumanRootManagedRun"
        | "AgentOwnerChildWork"
        | "AgentOwnerAttachedWork"
        | "AgentOwnerInternalLeaf" -> Some "WorkCompleted"
        | _ -> None

    /// The closure kind this root acceptance expects, mirroring
    /// `ClosureLaw.expectedKind` exactly.
    ///
    /// Returns None when the inputs cannot determine a unique kind — in
    /// particular an agent-owner root without a life, which must fail closed
    /// rather than close under a guessed kind.
    let expectedClosureKind (rootKindLabel: string) (isManager: bool) (agentOwnerLife: string option) : string option =
        match rootKindLabel, agentOwnerLife with
        | "HumanRoot", None when isManager -> Some "HumanRootManagerLife"
        | "HumanRoot", None -> Some "HumanRootManagedRun"
        | "HumanRoot", Some _ -> None
        | "AgentOwnerRoot", Some "Child" -> Some "AgentOwnerChildWork"
        | "AgentOwnerRoot", Some "Attached" -> Some "AgentOwnerAttachedWork"
        | "AgentOwnerRoot", Some "InternalLeaf" -> Some "AgentOwnerInternalLeaf"
        | "AgentOwnerRoot", None -> None
        | _ -> None

    /// The closure kind and the agent-owner life of a root acceptance.
    ///
    /// The life is the one the producer proved; it is never invented here.
    /// An agent-owner root whose life the producer could not prove yields no
    /// kind at all, and so admits no closure — the alternative would be a
    /// closure kind guessed from the root's shape, which is exactly the
    /// inference WHAT-018 forbids.
    ///
    /// Returns None when the root kind is unknown, or when an agent-owner
    /// root has no proven life.
    let deriveClosureKind
        (rootKindLabel: string)
        (isManager: bool)
        (agentOwnerLife: string option)
        : (string * string option) option =
        match rootKindLabel with
        | "HumanRoot" ->
            expectedClosureKind rootKindLabel isManager agentOwnerLife
            |> Option.map (fun kind -> (kind, None))
        | "AgentOwnerRoot" ->
            expectedClosureKind rootKindLabel isManager agentOwnerLife
            |> Option.map (fun kind -> (kind, agentOwnerLife))
        | _ -> None

    /// A recorded kind must match the lifecycle the acceptance proves.
    let recordedClosureKind
        (rootKindLabel: string)
        (isManager: bool)
        (agentOwnerLife: string option)
        (recorded: string option)
        : string =
        let derived =
            deriveClosureKind rootKindLabel isManager agentOwnerLife
            |> Option.map fst
            |> Option.defaultWith (fun () ->
                invalidArg "agentOwnerLife" "the authority root has no proven closure lifecycle")

        match recorded with
        | None -> derived
        | Some kind when kind = derived -> kind
        | Some _ -> invalidArg "ExpectedClosureKind" "the recorded closure kind does not match the proven lifecycle"
