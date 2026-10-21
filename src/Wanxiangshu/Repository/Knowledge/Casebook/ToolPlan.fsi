namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks

/// HNI-09: the product decisions behind the Casebook tool contribution.
module CasebookToolPlan =
    val isEnabled: workspaceRoot: string -> bool
    val commonDir: workspaceRoot: string -> Task<string>
