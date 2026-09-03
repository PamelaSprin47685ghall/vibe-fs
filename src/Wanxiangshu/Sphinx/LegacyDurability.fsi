namespace Wanxiangshu.Sphinx

module LegacyDurability =
    val observationType: string
    val streamFor: handle: string -> string
    val envelopeId: handle: string -> revision: int -> string
    val encodeObservation: handle: string -> tool: string -> args: obj -> revision: int -> obj
    val decodeObservation: envelope: obj -> obj
    val replayObservations: store: SessionStore -> raws: obj array -> obj
