namespace Wanxiangshu.Persistence.Journal

module RuntimePath =
    val gitCommonDir: workspace: string -> string
    val forWorkspace: workspace: string -> string
    val forCommonDir: commonDirectory: string -> string
