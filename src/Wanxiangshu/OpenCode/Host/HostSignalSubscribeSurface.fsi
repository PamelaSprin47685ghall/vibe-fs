namespace Wanxiangshu.OpenCode

open System.Threading.Tasks

module HostSignalSubscribeSurface =
    val trySubscribe: input: obj -> onSignalEvent: obj -> Task<obj>
