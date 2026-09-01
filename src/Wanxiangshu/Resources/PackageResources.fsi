namespace Wanxiangshu.Resources

module PackageResources =
    val readText: relativeResourcePath: string -> string
    val exists: relativeResourcePath: string -> bool
    val listChildDirectoryNames: relativeDir: string -> string list
