namespace Wanxiangshu.OpenCode

module NodeFs =
    val readFileSync: path: string * encoding: string -> string
    val writeFileSync: path: string * data: string * encoding: string -> unit
    val existsSync: path: string -> bool
    val statSync: path: string -> obj
    val readdirSync: path: string -> obj
    val renameSync: source: string * destination: string -> unit
    val rmSync: path: string * options: obj -> unit
    val cpSync: source: string * destination: string * options: obj -> unit
