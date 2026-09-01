namespace Wanxiangshu.Requirement.Grounding

module Surface =
    val discoverPackages: workspace: string -> string array
    val resolvePackages: workspace: string -> path: string -> string array
    val materializePackage: workspace: string -> packageName: string -> obj
