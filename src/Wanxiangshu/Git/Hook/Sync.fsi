namespace Wanxiangshu.Git.Hook

open System.Threading.Tasks

/// Standalone hook-process entry functions. They depend only on `.git/wanxiang`,
/// Git transport and WriterStreamSync — never PluginHost/WorkspaceEventStore/
/// CanonicalIntegrator. The compiled module is invoked by resources/git/wanxiang-hook.mjs.
[<RequireQualifiedAccess>]
module HookSync =
    /// pre-push receives remote-name and remote-url from Git. The remote URL is
    /// intentionally irrelevant: Git itself owns remote resolution/auth.
    val runPrePush: remote: string -> Task<string option>

    /// reference-transaction `committed` is also FULL bidirectional convergence.
    /// The observed root only skips discovery of the first remote snapshot.
    val runReferenceTransaction: state: string -> stdinText: string -> Task<string option>
