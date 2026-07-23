
import { Flow_run, Flow_create } from "../Kernel/Flow.js";
import { task } from "../fable_modules/fable-library-js.5.6.0/TaskBuilder.js";
import { DeadlineModule_ofBudget } from "./Deadline.js";
import { utcNow } from "../fable_modules/fable-library-js.5.6.0/DateOffset.js";
import { Command } from "./Command.js";
import { ProcessSpawn_spawn } from "./ProcessHandle.js";
import { FSharpResult$2 } from "../fable_modules/fable-library-js.5.6.0/Result.js";
import { Operators_IsNull } from "../fable_modules/fable-library-js.5.6.0/FSharp.Core.js";
import { toString } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { ProcessError } from "./ProcessTypes.js";

export function execute(cmd) {
    return Flow_create((ctx, ct) => {
        const builder$0040 = task();
        return builder$0040.Run(builder$0040.Delay(() => {
            let effectiveCmd;
            const matchValue_1 = ctx.DefaultTimeout;
            if (cmd.Deadline == null) {
                if (matchValue_1 == null) {
                    effectiveCmd = cmd;
                }
                else {
                    const budget = matchValue_1;
                    effectiveCmd = (new Command(cmd.FileName, cmd.Arguments, cmd.WorkingDirectory, cmd.Environment, cmd.Stdin, DeadlineModule_ofBudget(utcNow(), budget), cmd.PtyOptions));
                }
            }
            else {
                effectiveCmd = cmd;
            }
            return builder$0040.Bind(ProcessSpawn_spawn(effectiveCmd, ctx, ct), (_arg) => {
                const spawnResult = _arg;
                return (spawnResult.tag === 0) ? builder$0040.Using(spawnResult.fields[0], (_arg_1) => builder$0040.ReturnFrom(Flow_run(ctx, ct, _arg_1.RunToCompletion()))) : builder$0040.Return(new FSharpResult$2(1, [spawnResult.fields[0]]));
            });
        }));
    });
}

export function runFlow(ctx, ct, flow) {
    const builder$0040 = task();
    return builder$0040.Run(builder$0040.Delay(() => builder$0040.TryWith(builder$0040.Delay(() => builder$0040.ReturnFrom(Flow_run(ctx, ct, flow))), (_arg) => {
        const ex = _arg;
        const msg = Operators_IsNull(ex) ? "" : toString(ex);
        return (((msg.indexOf("cancel") >= 0) ? true : (msg.indexOf("Cancel") >= 0)) ? true : (msg.indexOf("Operation") >= 0)) ? builder$0040.Return(new FSharpResult$2(1, [new ProcessError(1, ["Operation cancelled"])])) : builder$0040.Return(new FSharpResult$2(1, [new ProcessError(3, [msg])]));
    })));
}

