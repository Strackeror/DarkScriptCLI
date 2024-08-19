class ConditionType {
  /**
   * @param {IfFunc | undefined} cond
   * @param {SkipFunc | undefined} skip
   * @param {Endfunc | undefined} end
   * @param {GotoFunc | undefined} goto
   * @param {WaitFunc | undefined} wait
   */
  constructor(cond, skip, end, goto, wait) {
    /** @type {IfFunc | undefined} */
    this.If = cond;
    /** @type {SkipFunc | undefined} */
    this.Skip = skip;
    /** @type {Endfunc | undefined} */
    this.End = end;
    /** @type {GotoFunc | undefined} */
    this.Goto = goto;
    /** @type {WaitFunc | undefined} */
    this.Wait = wait;
  }
}

/** @typedef {{on: unknown, off: unknown, is_true: boolean, index: number}} Negator */

class Condition {
  /** @type {Negator | undefined} */
  negator = undefined;

  /** @type {ConditionGroup} */
  condGroup = 0;

  /**
   * @param {ConditionType} type
   * @param {...any} args
   */
  constructor(type, ...args) {
    /** @type {ConditionType} */
    this.type = type;
    /** @type {any[]} */
    this.args = args;
  }

  name() {
    return this.type.constructor?.name;
  }

  /** @type {(on: unknown, off:unknown, index: number) => Condition} */
  withNegator(on, off, index) {
    this.negator = { on, off, index, is_true: true };
    return this;
  }

  /** @type {(group: ConditionGroup) => Condition} */
  withCond(group) {
    this.condGroup = group;
    return this;
  }

  /** @type {-1} */
  static OR = -1;
  /** @type {1} */
  static AND = 1;

  /** @type {(type: 1 | 0| -1) => ConditionGroup} */
  nextCondGroup(type) {
    if (type == 0)
      return this.condGroup != 0 ? this.condGroup : _Event().andIndex++;
    if (type < 0)
      return this.condGroup < 0 ? this.condGroup : -_Event().orIndex++;
    else return this.condGroup > 0 ? this.condGroup : _Event().andIndex++;
  }

  /** @type {() => Condition} */
  Not() {
    if (this.negator) {
      var negated = new Condition(this.type, ...this.args);
      var negator = { ...this.negator };
      negator.is_true = !negator.is_true;
      negated.args[negator.index] = negator.is_true ? negator.on : negator.off;
      negated.negator = negator;
      return negated;
    }

    if (this.type.If) {
      var condGroup = this.nextCondGroup(0);
      this.type.If(condGroup, ...this.args);
      return Not(CondGroup(condGroup));
    }

    throw new Error(`Cannot negate condition`);
  }
  /** @type {(direction?: -1 | 0 | 1) => Condition} */
  Get(direction = 0) {
    var condGroup = this.nextCondGroup(direction);
    if (this.type.If) {
      if (condGroup != this.condGroup) this.type.If(condGroup, ...this.args);
      return CondGroup(condGroup).withCond(condGroup);
    } else throw new Error(`No cond function`);
  }

  /** @type {(cond: Condition) => Condition} */
  And(cond) {
    if (this.type.If && cond.type.If) {
      var condGroup = this.nextCondGroup(Condition.AND);
      if (condGroup != this.condGroup) this.type.If(condGroup, ...this.args);
      cond.type.If(condGroup, ...cond.args);
      return CondGroup(condGroup).withCond(condGroup);
    }
    throw new Error(`No cond function`);
  }

  /** @type {(cond: Condition) => Condition} */
  Or(cond) {
    if (this.type.If && cond.type.If) {
      var condGroup = this.nextCondGroup(Condition.OR);
      if (condGroup != this.condGroup) this.type.If(condGroup, ...this.args);
      cond.type.If(condGroup, ...cond.args);
      return CondGroup(condGroup).withCond(condGroup);
    }
    throw new Error(`No cond function`);
  }

  /** @type {Condition} */
  get Passed() {
    var next = this.nextCondGroup(0);
    if (next != this.condGroup) {
      if (this.type.If) this.type.If(next, ...this.args);
      else throw new Error("No cond function");
    }
    return CompiledConditionGroup(next);
  }

  static New() {
    return new Unconditional();
  }
}

class Unconditional extends Condition {
  constructor() {
    super(CondAlways);
    CondAlways.If = () => {};
  }

  Not() {
    return this;
  }

  /** @type {(c: Condition) => Condition} */
  And(cond) {
    return cond.Get(1);
  }

  /** @type {(c: Condition) => Condition} */
  Or(cond) {
    return cond.Get(-1);
  }
}

class Comparable {
  /** @type {Condition} */
  condition;

  // /** @type {number} */
  // lhs;

  /** @type {number} */
  rhs;

  /** @type {number} */
  comparison;

  /**
   * @param {Condition} cond
   * param {number} lhs
   * @param {number} comparison
   * @param {number} rhs
   */
  constructor(cond, comparison, rhs) {
    this.condition = cond;
    this.rhs = rhs;
    this.comparison = comparison;
  }

  /** @type {(type: ComparisonType) => ComparisonType} */
  static OppositeComparison(type) {
    switch (type) {
      case ComparisonType.Equal:
        return ComparisonType.NotEqual;
      case ComparisonType.NotEqual:
        return ComparisonType.Equal;
      case ComparisonType.Greater:
        return ComparisonType.LessOrEqual;
      case ComparisonType.Less:
        return ComparisonType.GreaterOrEqual;
      case ComparisonType.GreaterOrEqual:
        return ComparisonType.Less;
      case ComparisonType.LessOrEqual:
        return ComparisonType.Greater;
    }
  }

  /** @type {(num: number, comparison: ComparisonType) => Condition} */
  _Compare(num, comparison) {
    this.condition.args[this.rhs] = num;
    this.condition.args[this.comparison] = comparison;
    return this.condition.withNegator(
      comparison,
      Comparable.OppositeComparison(comparison),
      this.comparison
    );
  }

  /** @type {(num: number) => Condition} */
  Eq(num) {
    return this._Compare(num, ComparisonType.Equal);
  }
  /** @type {(num: number) => Condition} */
  NEq(num) {
    return this._Compare(num, ComparisonType.NotEqual);
  }
  /** @type {(num: number) => Condition} */
  Gt(num) {
    return this._Compare(num, ComparisonType.Greater);
  }
  /** @type {(num: number) => Condition} */
  Lt(num) {
    return this._Compare(num, ComparisonType.Less);
  }
  /** @type {(num: number) => Condition} */
  GtE(num) {
    return this._Compare(num, ComparisonType.GreaterOrEqual);
  }
  /** @type {(num: number) => Condition} */
  LtE(num) {
    return this._Compare(num, ComparisonType.LessOrEqual);
  }
}

/** @type {(c: Comparable) => Condition} */
Number.prototype.Eq = function (cond) {
  return cond.Eq(Number(this));
};
/** @type {(c: Comparable) => Condition} */
Number.prototype.NEq = function (cond) {
  return cond.NEq(Number(this));
};
/** @type {(c: Comparable) => Condition} */
Number.prototype.Gt = function (cond) {
  return cond.LtE(Number(this));
};
/** @type {(c: Comparable) => Condition} */
Number.prototype.Lt = function (cond) {
  return cond.GtE(Number(this));
};
/** @type {(c: Comparable) => Condition} */
Number.prototype.GtE = function (cond) {
  return cond.Lt(Number(this));
};
/** @type {(c: Comparable) => Condition} */
Number.prototype.LtE = function (cond) {
  return cond.Gt(Number(this));
};

/** @param {Condition} cond */
function WaitFor(cond) {
  if (cond.type.If) cond.type.If(ConditionGroup.MAIN, ...cond.args);
  else if (cond.type.Wait) cond.type.Wait(...cond.args);
  else throw new Error(`No Wait function for condition`);
}

/** @param {Condition} cond */
function EndIf(cond, endType = EventEndType.End) {
  if (cond.type.End) cond.type.End(endType, ...cond.args);
  else if (cond.type.If) EndIf(cond.Get(), endType);
  else throw new Error(`No End function for condition`);
}

/** @param {Condition} cond */
function RestartIf(cond) {
  return EndIf(cond, EventEndType.Restart);
}

/** @type {(target: Label, cond: Condition) => void} */
function GotoIf(target, cond) {
  if (cond.type.Goto) cond.type.Goto(target, ...cond.args);
  else if (cond.type.If) GotoIf(target, cond.Get());
  else throw new Error("Can't goto for condition");
}

/** @type {(target: Label) => void} */
function Goto(target) {
  GotoIf(target, Always());
}

/** @type {(target: string | number, cond: Condition) => void} */
function SkipToIf(target, cond) {
  if (cond.type.Skip) {
    cond.type.Skip(99, ...cond.args);
    _ReserveSkip(target);
  } else if (cond.type.If) SkipToIf(target, cond.Get());
  else throw new Error("Can't skip for condition");
}

/** @type {(target: string | number) => void} */
function SkipTo(target) {
  SkipToIf(target, Always());
}

/** @type {(target: string) => void} */
function SkipLabel(target) {
  _FillSkip(target);
}
/** @type {(lines: number, cond: Condition) => void} */
function SkipIf(lines, cond) {
  if (cond.type.Skip) cond.type.Skip(lines, ...cond.args);
  else if (cond.type.If) SkipIf(lines, cond.Get());
  else throw new Error("Can't skip for condition");
}

/** @type {(lines: number) => void} */
function Skip(lines) {
  SkipIf(lines, Always());
}

function RestartEvent() {
  RestartIf(Always());
}

function EndEvent() {
  EndIf(Always());
}

/** @type {(cond: Condition) => Condition} */
function Not(cond) {
  return cond.Not();
}

/** @type {(cond: Condition) => [Condition, number]} */
function VirtualGotoIf(cond) {
  if (cond.type.Skip || cond.type.Goto) {
    Skip(0);
    let index = _Event().instructionIndex - 1;
    return [cond, index];
  } else if (cond.type.If) return VirtualGotoIf(cond.Get());
  else throw new Error("Can't virtual goto for condition");
}

/** @param {[Condition, number][]} sources */
function VirtualLabel(sources) {
  let currentIndex = _Event().instructionIndex;
  let gotos = [];
  for (let [cond, index] of sources) {
    _Event().replaceInstruction = index;
    if (currentIndex - index < 100) {
      SkipIf(currentIndex - index - 1, cond);
    } else {
      GotoIf(99, cond);
      gotos.push(index);
    }
  }
  if (!gotos.length) return;
  if (!_Event().virtualLabels[currentIndex]) {
    _Event().virtualLabels[currentIndex] = { sources: [] };
  }
  _Event().virtualLabels[currentIndex]?.sources.push(...gotos);
}

var Else = undefined;
/** @type {(...args: [Condition, () => void, ...any[]]) => void} */
function If(...args) {
  /** @type {Condition | undefined} */
  let condition;
  /** @type {() => void} */
  let body;
  /** @type {any[]} */
  let rest = args;

  let gotos = [];
  while (rest.length >= 2) {
    [condition, body, ...rest] = rest;

    if (!condition) {
      body();
      break;
    }

    let virtualCond = VirtualGotoIf(Not(condition));
    body();
    if (rest.length > 0) gotos.push(VirtualGotoIf(Always()));
    VirtualLabel([virtualCond]);
  }
  if (gotos) VirtualLabel(gotos);
  if (rest.length > 0) throw new Error("Invalid If condition");
}
