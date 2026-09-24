namespace Wanxiangshu.Ablation

/// Ablation decisions, made against a registry the caller supplies.
///
/// Every decision here is pure: same registry in, same answer out. The registry is
/// resolved once — by `AblationSurface` at the Host edge, or by a test that built its
/// own — and threaded down, so no decision function reads the environment.
///
/// The ambient registry is the single exception, and it exists only so a Host boundary
/// with no registry of its own to thread can still answer. A caller that holds one
/// passes it directly and never touches the cell.
[<RequireQualifiedAccess>]
module AblationGate =
    /// The registry this process answers ablation questions against.
    ///
    /// A cell, not a memo: the Host boundary installs the resolved registry once, and
    /// everything downstream reads a value instead of deriving one. Because it is a
    /// plain reference the caller controls, a test installs its own registry without
    /// reaching into the environment — which is why there is no cache key to get
    /// wrong, and no way for two callers to disagree about which profile is active.
    ///
    /// DSL-MUTABLE: resource — the one pointer to the active ablation registry.
    val useRegistry: registry: AblationRegistry -> unit

    /// The registry decisions are made against when the caller supplies none.
    ///
    /// Falls back to the environment when nothing has been installed, so a Host
    /// boundary that never calls `useRegistry` still behaves as before. The fallback
    /// is deliberately not cached: an environment change is visible on the next call,
    /// which is what keeps this a value rather than a memo.
    val registry: unit -> AblationRegistry

    /// Drop whatever registry is installed, returning to the environment fallback.
    val resetRegistry: unit -> unit

    /// The registry explicitly installed, without consulting the environment.
    val installedRegistry: unit -> AblationRegistry option

    val deniedPath: string

    val toolDenied: registry: AblationRegistry -> toolName: string -> bool
    val toolSchemaDenied: registry: AblationRegistry -> toolName: string -> bool
    val filterToolPermissionMap: registry: AblationRegistry -> permissions: Map<string, bool> -> Map<string, bool>
    val filterKnownToolNames: registry: AblationRegistry -> names: string list -> string list

    val deniedFactPath: string

    val factDenied: registry: AblationRegistry -> factTag: string -> bool

    /// Which primary agents the registry exposes.
    val primaryAgentAllowed: registry: AblationRegistry -> agentName: string -> bool

    val fissionVisible: registry: AblationRegistry -> bool

    /// Mode of one node, for callers that need the raw value rather than a yes/no.
    val modeFor: registry: AblationRegistry -> nodeId: string -> AblationMode
