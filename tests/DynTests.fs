module VibeFs.Tests.DynTests

open VibeFs.Tests.Assert
open VibeFs.Kernel.Dyn

/// Verify the nullish guards treat BOTH null and a genuine JS undefined as absent.
let nullish () =
    equal "str absent → empty" "" (str (box {| |}) "missing")
    equal "str present" "v" (str (box {| x = "v" |}) "x")
    check "isNullish null" (isNullish null)
    // A genuine JS undefined (via the Emit helper), distinct from null.
    check "isNullish undefined" (isNullish undefinedValue)
    check "not isNullish real value" (not (isNullish (box 0)))
    check "opt absent → None" ((opt (box {| |}) "nope").IsNone)
