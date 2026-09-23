namespace Wanxiangshu.Sphinx.V2.Plugins

module Surface =
    val listOfItems: 'a list -> 'a list
    val stringFloatMapOf: (string * float) list -> Map<string, float>
    val isOk: Result<'value, 'error> -> bool
    val isError: Result<'value, 'error> -> bool
    val okValue: Result<'value, 'error> -> 'value
    val solve: AStarProblem -> Result<SearchSnapshot, AStarFault>
    val costOf: SearchSnapshot -> float
    val pathOf: AStarProblem -> SearchSnapshot -> string list option
    val boundOf: SearchSnapshot -> float option
