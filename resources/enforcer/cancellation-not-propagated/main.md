# cancellation-not-propagated — Main

Tip already selected by Enforcer. Next step: apply the nudge.

## Why

A cancellation token or abort signal stops at an outer layer while inner network, process, tool, or child work continues.

## What to do

Cancellation does not reach owned work. Propagate the cancellation signal through every resource boundary.

## Reference

Family F, enforcement-f05, ordinal 55.
