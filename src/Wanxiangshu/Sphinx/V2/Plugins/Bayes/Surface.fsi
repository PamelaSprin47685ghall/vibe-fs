namespace Wanxiangshu.Sphinx.V2.Plugins

module Surface =
    val listOfItems: Hypothesis list -> Hypothesis list
    val stringFloatMapOf: (string * float) list -> Map<string, float>
    val isOk: Result<'value, 'error> -> bool
    val isError: Result<'value, 'error> -> bool
    val infer: Hypothesis list -> Factor list -> Result<Posterior, ExactFault>
    val priorOnly: Hypothesis list -> Result<Posterior, ExactFault>

    /// Reads the posterior out of a result, failing loudly when the fit refused.
    val okPosterior: Result<Posterior, ExactFault> -> Posterior
    val probabilityOf: Posterior -> string -> float
