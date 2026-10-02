namespace Wanxiangshu.OpenCode.Host

module ProtocolArgumentVault =

    type FieldOwnership =
        { ReviewContract: bool
          InvestigationEstimate: bool }

    /// One snapshot of the protocol fields, verbatim from the tool arguments.
    type Snapshot =
        { Contract: obj option
          ReadonlyRounds: obj option
          SelfNote: obj option }

    /// Process-local, in-memory snapshot store. Never persisted.
    type Vault

    val create: unit -> Vault

    val record: vault: Vault -> sessionId: string -> callId: string -> snapshot: Snapshot -> unit

    val tryFind: vault: Vault -> sessionId: string -> callId: string -> Snapshot option

    val clear: vault: Vault -> unit

    /// Read only owned protocol fields; unowned fields are not inspected.
    val snapshotOfArguments: ownership: FieldOwnership -> args: obj -> Snapshot option

    /// Pure merge decision: business arguments verbatim, protocol fields
    /// appended when missing or different; same reference when nothing changes.
    val restoreArguments: snapshot: Snapshot -> current: obj -> obj
