namespace Wanxiangshu.Requirement.Grounding

module GroundingCatalog =
    type ScopeRule = { Include: bool; Pattern: string }

    type PackageDescriptor =
        { Name: string
          Root: string
          Rules: ScopeRule list }

    val canonicalWorkspace: workspace: string -> string
    val workspaceRelativePath: workspace: string -> path: string -> string option
    val discover: workspace: string -> PackageDescriptor list
    val resolve: workspace: string -> path: string -> PackageDescriptor list
    val materialize: workspace: string -> packageName: string -> GroundingSnapshot
    val snapshotsForPaths: workspace: string -> paths: string list -> GroundingSnapshot list
