namespace Wanxiangshu.Git

module GitSubject =
    [<Literal>]
    val Executable: string = "git"

    val execIn: directory: string -> arguments: string array -> string
    val revParseGitCommonDir: workspace: string -> string
    val diffHeadBinary: directory: string -> string
    val lsFilesUntracked: directory: string -> string
    val revParseHeadTree: directory: string -> string
