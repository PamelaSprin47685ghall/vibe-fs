namespace Wanxiangshu.Sphinx.V2.Plugins

module Surface =
    val listOfItems: string list -> string list
    val nodeStats: int -> float -> float -> string -> int -> NodeStats
    val uctOf: int -> float -> float -> float -> NodeStats -> float
    val meanOf: NodeStats -> float option
    val varianceOf: NodeStats -> float option
    val stateKeyOf: string -> string list -> int -> string
    val allChildrenSeen: Map<string, NodeStats> -> string list -> bool
