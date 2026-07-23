
import { Record } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { record_type, class_type, option_type, list_type, string_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { Deadline_$reflection } from "./Deadline.js";
import { PtyOptions_$reflection } from "./ProcessTypes.js";

export class Command extends Record {
    constructor(FileName, Arguments, WorkingDirectory, Environment, Stdin, Deadline, PtyOptions) {
        super();
        this.FileName = FileName;
        this.Arguments = Arguments;
        this.WorkingDirectory = WorkingDirectory;
        this.Environment = Environment;
        this.Stdin = Stdin;
        this.Deadline = Deadline;
        this.PtyOptions = PtyOptions;
    }
}

export function Command_$reflection() {
    return record_type("Wanxiangshu.Next.Process.Command", [], Command, () => [["FileName", string_type], ["Arguments", list_type(string_type)], ["WorkingDirectory", option_type(string_type)], ["Environment", option_type(class_type("Microsoft.FSharp.Collections.FSharpMap`2", [string_type, string_type]))], ["Stdin", option_type(string_type)], ["Deadline", option_type(Deadline_$reflection())], ["PtyOptions", option_type(PtyOptions_$reflection())]]);
}

