namespace Wanxiangshu.Foundation

/// Pure glob-pattern matching extracted from the JS glob runtime.
module GlobMatch =
    type GlobPatternError = | InvalidPattern

    val compilePattern: pattern: string -> Result<obj, GlobPatternError>
    val testCompiled: regex: obj -> text: string -> bool
    val matchesPathPattern: pattern: string -> path: string -> Result<bool, GlobPatternError>
