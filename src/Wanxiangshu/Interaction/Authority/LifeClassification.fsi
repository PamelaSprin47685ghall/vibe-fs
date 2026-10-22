namespace Wanxiangshu.Interaction.Authority

/// The closed set of closure kinds and the agent-owner life labels, mirroring
/// the canonical law in `wanxiangli/src/authority/ClosureLaw.fs`.
[<RequireQualifiedAccess>]
module AuthorityLifeClassification =
    /// The closed set of expected closure kinds.
    val closureKindLabels: string list

    /// The closed set of agent-owner life labels.
    val agentOwnerLifeLabels: string list

    /// The root kind label of a closure kind: "HumanRoot" | "AgentOwnerRoot".
    val rootKindOfKind: closureKind: string -> string

    /// The closure witness kind a closure of this kind must carry.
    val witnessKindOf: closureKind: string -> string option

    /// The closure kind this root acceptance expects, mirroring
    /// `ClosureLaw.expectedKind` exactly. None when the inputs cannot determine
    /// a unique kind.
    val expectedClosureKind: rootKindLabel: string -> isManager: bool -> agentOwnerLife: string option -> string option

    /// The closure kind and the agent-owner life of a root acceptance.
    ///
    /// The life is the one the producer proved; it is never invented here. An
    /// agent-owner root whose life the producer could not prove yields no
    /// kind at all, and so admits no closure.
    val deriveClosureKind:
        rootKindLabel: string -> isManager: bool -> agentOwnerLife: string option -> (string * string option) option

    /// A recorded kind must match the proven lifecycle; missing proof and mismatches are rejected.
    val recordedClosureKind:
        rootKindLabel: string -> isManager: bool -> agentOwnerLife: string option -> recorded: string option -> string
