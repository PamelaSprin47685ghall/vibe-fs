namespace Wanxiangshu.Git

[<Struct>]
type GitTreePort = { GetTreeHash: unit -> string }

module GitSubject =
    [<Literal>]
    val Executable: string = "git"

    val execIn: directory: string -> arguments: string array -> string
    val revParseGitCommonDir: workspace: string -> string
    val diffHeadBinary: directory: string -> string
    val lsFilesUntracked: directory: string -> string
    val lsFilesUntrackedZ: directory: string -> string
    val statusPorcelainV2Z: directory: string -> string
    val lsFilesStageZ: directory: string -> string
    val hashObjectNoFilters: directory: string -> path: string -> string
    val revParseHeadTree: directory: string -> string
