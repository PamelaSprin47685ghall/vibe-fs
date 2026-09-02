namespace Wanxiangshu.Git.Hook

/// DURABLE-CONVERGENCE-008. Product startup only ENSURES the Git hook membrane.
/// Actual full bidirectional convergence runs later in an independent Git-hook
/// process through resources/git/wanxiang-hook.mjs + HookSync.
[<RequireQualifiedAccess>]
module HookDispatcher =

    [<Literal>]
    val SyncActiveEnv: string = "WANXIANG_GIT_SYNC_ACTIVE"

    [<Literal>]
    val OwnershipMarker: string = "wanxiang-hook-dispatcher"

    [<Literal>]
    val IncompleteDiagnosis: string = "Git integration incomplete"

    type HookKind =
        | ReferenceTransaction
        | PrePush

    type HookInstallVerdict =
        | Installed
        | AlreadyOwned
        | ForeignHook of path: string
        | DiagnoseIncomplete of reason: string

    val classifyExistingHook: existingBody: string option -> HookInstallVerdict
    val installOrDiagnose: hooksDir: string -> kind: HookKind -> shimBody: string -> HookInstallVerdict
    val shimHeaderComment: string

    /// Startup-only ensure. There is intentionally no fetch/pull/push here.
    /// Both installed hooks later launch the same standalone FULL converge path.
    val ensure: workspace: string -> Result<unit, string>
