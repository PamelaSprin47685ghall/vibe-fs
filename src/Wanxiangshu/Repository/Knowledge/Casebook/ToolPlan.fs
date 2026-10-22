namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks
open Wanxiangshu.Persistence.Journal

/// HNI-09: the product decisions behind the Casebook tool contribution.
///
/// The adapter (`Repository/Knowledge/Casebook/OpenCode/Tools.fs`) only wires
/// host tool shapes around them: it must not decide whether the feature
/// contributes, nor where the durable Casebook journal lives.
module CasebookToolPlan =

    /// The feature contributes its tools only in a workspace that carries the
    /// Casebook marker directory.
    let isEnabled (workspaceRoot: string) : bool = CasebookFeature.isEnabled workspaceRoot

    /// The durable common directory of a workspace: the state root the Casebook
    /// journal lives in. Product-owned path resolution — the adapter only hands
    /// the resolved directory to the host store binding.
    let commonDir (workspaceRoot: string) : Task<string> = RuntimePath.stateRoot workspaceRoot
