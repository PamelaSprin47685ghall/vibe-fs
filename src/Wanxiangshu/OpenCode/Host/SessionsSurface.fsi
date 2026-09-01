namespace Wanxiangshu.OpenCode

open System.Threading.Tasks

module SessionsSurface =
    val familyRoot: parents: obj -> session: string -> string
    val physicalParents: parents: obj -> children: obj -> string array
    val interruptAttemptAdapterProbe: unit -> Task<obj>
    val interruptRejectedAdapterProbe: unit -> Task<obj>
