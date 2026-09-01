namespace Wanxiangshu.Resources

[<RequireQualifiedAccess>]
module ProviderResourceBytes =
    val exists: relativePath: string -> bool
    val readText: relativePath: string -> string
