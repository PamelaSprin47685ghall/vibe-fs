namespace Wanxiangshu.Sphinx

module SphinxEventTypes =
    let PluginSetBound = "sphinx/plugin-set-bound"
    let ObservationAccepted = "sphinx/observation-accepted"
    let AnswerCommitted = "sphinx/answer-committed"
    let LegacyObservation = "sphinx-legacy/observation@1"
    let GenericInquiry = "sphinx-generic/inquiry@1"

    let all =
        [ PluginSetBound
          ObservationAccepted
          AnswerCommitted
          LegacyObservation
          GenericInquiry ]

    let isSphinxEvent eventType = all |> List.contains eventType
