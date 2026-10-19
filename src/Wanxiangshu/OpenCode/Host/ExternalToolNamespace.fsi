namespace Wanxiangshu.OpenCode

/// Host-declared external tool namespaces (capability-enforcement-007).
///
/// One module owns the answer to "which external integration namespaces does
/// the live Host config declare". The provider-visible projection reads the set
/// of ONE config application; the runtime admission gate reads the process-wide
/// declared set, which is monotone — see `declare`.
[<RequireQualifiedAccess>]
module ExternalToolNamespace =
    /// The enabled `mcp` server names this config application declares.
    val resolve: config: obj -> string list

    /// The provider-visible permission keys one resolved set projects to.
    val patternsOf: namespaces: string list -> string list

    /// Declare namespaces for this process. Monotone: a namespace already
    /// declared is never withdrawn.
    val declare: namespaces: string list -> unit

    /// Whether a concrete tool name sits inside a declared namespace.
    val matches: toolName: string -> bool
