namespace Wanxiangshu.Sphinx

module SphinxEventTypes =
    val PluginSetBound: string
    val ObservationAccepted: string
    val AnswerCommitted: string
    val LegacyObservation: string
    val GenericInquiry: string
    val all: string list
    val isSphinxEvent: eventType: string -> bool
