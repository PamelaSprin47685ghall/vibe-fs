namespace Wanxiangshu.Persistence.EventStore

open System.Threading.Tasks

module GitObjectDatabase =
    val writeBlob: objectsDir: string -> content: byte[] -> Task<string>
    val writeTree: objectsDir: string -> entries: TreeEntry list -> Task<string>
    val tryReadObject: objectsDir: string -> oid: string -> Task<byte[] option>
    val tryReadTree: objectsDir: string -> oid: string -> Task<TreeEntry list option>
    val tryReadRef: gitDir: string -> refName: string -> Task<string option>

    val compareAndSwapRef:
        gitDir: string -> refName: string -> expectedOld: string option -> newOid: string -> Task<bool>
