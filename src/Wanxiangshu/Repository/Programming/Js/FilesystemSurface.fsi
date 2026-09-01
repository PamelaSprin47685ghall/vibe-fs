namespace Wanxiangshu.Repository.Programming.Js

/// JS-native filesystem owner boundary. Paths, anchor declarations, listings
/// and mutation plans are plain data; Node filesystem effects remain behind the
/// production adapters.
[<RequireQualifiedAccess>]
module JsFilesystemSurface =

    val readUtf8: path: string -> obj
    val glob: root: string -> pattern: string -> obj
    val findAnchor: textValue: string -> declaration: obj -> occurrence: int -> obj
    val requireUnique: textValue: string -> declaration: obj -> obj
    val grep: root: string -> declaration: obj -> pattern: string -> obj
    val commitPlan: root: string -> plan: obj array -> obj
    val rollbackPlan: root: string -> plan: obj array -> unit
