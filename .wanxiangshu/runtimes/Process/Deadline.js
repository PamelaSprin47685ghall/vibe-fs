
import { Union } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { union_type, class_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { op_Subtraction, add } from "../fable_modules/fable-library-js.5.6.0/DateOffset.js";
import { compare } from "../fable_modules/fable-library-js.5.6.0/Date.js";

export class Deadline extends Union {
    constructor(expiresAt) {
        super();
        this.tag = 0;
        this.fields = [expiresAt];
    }
    cases() {
        return ["Deadline"];
    }
}

export function Deadline_$reflection() {
    return union_type("Wanxiangshu.Next.Process.Deadline", [], Deadline, () => [[["expiresAt", class_type("System.DateTimeOffset")]]]);
}

export function DeadlineModule_ofBudget(now, budget) {
    return new Deadline(add(now, budget));
}

export function DeadlineModule_remaining(clock, _arg) {
    const rem = op_Subtraction(_arg.fields[0], clock());
    if (rem < 0) {
        return 0;
    }
    else {
        return rem;
    }
}

export function DeadlineModule_isExpired(clock, _arg) {
    return compare(clock(), _arg.fields[0]) >= 0;
}

