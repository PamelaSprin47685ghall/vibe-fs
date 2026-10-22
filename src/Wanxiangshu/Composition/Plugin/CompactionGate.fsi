namespace Wanxiangshu.Composition.Plugin

open Wanxiangshu.Host.Ports

module CompactionGate =
    val create: unit -> ICompactionGate
