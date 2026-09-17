namespace Wanxiangshu.Host.Contract

module ToolResultBound =
    val HostMaxLines: int
    val HostMaxBytes: int
    val Marker: string
    val MarkerBytes: int
    val ContentMaxLines: int
    val ContentMaxBytes: int
    val fitsInWindow: text: string -> bool
    val bound: text: string -> string
