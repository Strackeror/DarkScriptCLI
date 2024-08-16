class ConditionType {
  /**
   * @param {IfFunc | undefined} cond
   * @param {SkipFunc | undefined} skip
   * @param {Endfunc} end
   * @param {GotoFunc | undefined} goto
   * @param {WaitFunc | undefined} wait
   */
  constructor(cond, skip, end, goto, wait) {
    /** @type {IfFunc | undefined} */
    this.If = cond;
    /** @type {SkipFunc | undefined} */
    this.Skip = skip;
    /** @type {Endfunc} */
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
    this.args[index] = on;
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
      this.negator.is_true = !this.negator.is_true;
      this.args[this.negator.index] = this.negator.is_true
        ? this.negator.on
        : this.negator.off;
      return this;
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

  /** @type {(num: number) => Condition} */
  Eq(num) {
    this.condition.args[this.rhs] = num;
    return this.condition.withNegator(
      ComparisonType.Equal,
      ComparisonType.NotEqual,
      this.comparison
    );
  }

  /** @type {(num: number) => Condition} */
  NEq(num) {
    this.condition.args[this.rhs] = num;
    return this.condition.withNegator(
      ComparisonType.NotEqual,
      ComparisonType.Equal,
      this.comparison
    );
  }
  /** @type {(num: number) => Condition} */
  Gt(num) {
    this.condition.args[this.rhs] = num;
    return this.condition.withNegator(
      ComparisonType.Greater,
      ComparisonType.LessOrEqual,
      this.comparison
    );
  }
  /** @type {(num: number) => Condition} */
  Lt(num) {
    this.condition.args[this.rhs] = num;
    return this.condition.withNegator(
      ComparisonType.Less,
      ComparisonType.GreaterOrEqual,
      this.comparison
    );
  }
  /** @type {(num: number) => Condition} */
  GtE(num) {
    this.condition.args[this.rhs] = num;
    return this.condition.withNegator(
      ComparisonType.GreaterOrEqual,
      ComparisonType.Less,
      this.comparison
    );
  }
  /** @type {(num: number) => Condition} */
  LtE(num) {
    this.condition.args[this.rhs] = num;
    return this.condition.withNegator(
      ComparisonType.LessOrEqual,
      ComparisonType.Greater,
      this.comparison
    );
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
    _ReserveSkip(target);
    cond.type.Skip(99, ...cond.args);
  } else if (cond.type.If) SkipToIf(target, cond.Get());
  else throw new Error("Can't skip for condition");
}

/** @type {(target: string | number) => void} */
function SkipTo(target) {
  SkipToIf(target, Always());
}

/** @type {(target: string) => void} */
function NamedLabel(target) {
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

var Else = undefined;
/** @type {(...args: [Condition, () => void, ...any[]]) => void} */
function If(...args) {
  /** @type {Condition | undefined} */
  let condition;
  /** @type {() => void} */
  let body;
  /** @type {any[]} */
  let rest = args;

  let endId = _Event().nextSkipId++;
  while (rest.length >= 2) {
    [condition, body, ...rest] = rest;

    if (!condition) {
      body();
      break;
    }

    let id = _Event().nextSkipId++;
    SkipToIf(id, Not(condition));
    body();
    if (rest.length > 0) SkipToIf(endId, Always());
    _FillSkip(id);
  }

  if (rest.length > 0) {
    throw new Error("Invalid If condition");
  }
  _FillSkip(endId);
}

const L0 = 0;
const L1 = 1;
const L2 = 2;
const L3 = 3;
const L4 = 4;
const L5 = 5;
const L6 = 6;
const L7 = 7;
const L8 = 8;
const L9 = 9;
const L10 = 10;
const L11 = 11;
const L12 = 12;
const L13 = 13;
const L14 = 14;
const L15 = 15;
const L16 = 16;
const L17 = 17;
const L18 = 18;
const L19 = 19;
const L20 = 20;