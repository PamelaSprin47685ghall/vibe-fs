namespace Wanxiangshu.OpenCode

open Fable.Core

module NodeFs =
    [<Import("readFileSync", "fs")>]
    let private readFileSyncPhysical (path: string, encoding: string) : string = jsNative

    [<Import("writeFileSync", "fs")>]
    let private writeFileSyncPhysical (path: string, data: string, encoding: string) : unit = jsNative

    [<Import("existsSync", "fs")>]
    let private existsSyncPhysical (path: string) : bool = jsNative

    [<Import("statSync", "fs")>]
    let private statSyncPhysical (path: string) : obj = jsNative

    [<Import("readdirSync", "fs")>]
    let private readdirSyncPhysical (path: string) : obj = jsNative

    [<Import("renameSync", "fs")>]
    let private renameSyncPhysical (source: string, destination: string) : unit = jsNative

    [<Import("rmSync", "fs")>]
    let private rmSyncPhysical (path: string, options: obj) : unit = jsNative

    [<Import("cpSync", "fs")>]
    let private cpSyncPhysical (source: string, destination: string, options: obj) : unit = jsNative

    let readFileSync (path: string, encoding: string) = readFileSyncPhysical (path, encoding)

    let writeFileSync (path: string, data: string, encoding: string) =
        writeFileSyncPhysical (path, data, encoding)

    let existsSync (path: string) = existsSyncPhysical path
    let statSync (path: string) = statSyncPhysical path
    let readdirSync (path: string) = readdirSyncPhysical path

    let renameSync (source: string, destination: string) =
        renameSyncPhysical (source, destination)

    let rmSync (path: string, options: obj) = rmSyncPhysical (path, options)

    let cpSync (source: string, destination: string, options: obj) =
        cpSyncPhysical (source, destination, options)
