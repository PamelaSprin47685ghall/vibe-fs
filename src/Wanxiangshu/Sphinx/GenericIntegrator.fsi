namespace Wanxiangshu.Sphinx

/// WHAT[EPI-019]: pure Current fold for generic Sphinx inquiries. No IO, no
/// clock, no codec: the spine decodes durable envelopes into
/// GenericEnvelopeInput and this module only folds them into per-inquiry
/// cursors with an unbroken revision chain.
[<RequireQualifiedAccess>]
module GenericIntegrator =
    type GenericCursor =
        { Revision: int
          Cancelled: bool
          Question: string
          Profile: string
          ExecutionMode: string
          PluginsJson: string
          BudgetJson: string
          ResultsJson: string list }

    type GenericEnvelopeInput =
        | GenericStarted of
            inquiry: string *
            revision: int *
            question: string *
            profile: string *
            executionMode: string *
            pluginsJson: string *
            budgetJson: string
        | GenericSubmitted of inquiry: string * revision: int * expectedRevision: int * resultsJson: string
        | GenericCancelled of inquiry: string * revision: int

    type SphinxGenericCurrent = Map<string, GenericCursor>

    val empty: SphinxGenericCurrent

    val applyOne: current: SphinxGenericCurrent -> input: GenericEnvelopeInput -> Result<SphinxGenericCurrent, string>
