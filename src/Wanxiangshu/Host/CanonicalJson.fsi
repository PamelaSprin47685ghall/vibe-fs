namespace Wanxiangshu.Host

module CanonicalJson =
    val canonicalJson: value: obj -> string
    val equal: left: obj -> right: obj -> bool
    val withoutKeys: keys: string array -> value: obj -> obj
