namespace LibMatrix.Homeservers.ImplementationDetails.Synapse.Models.Filters;

public class SynapseAdminLocalRoomQueryFilter {
    public StringFilter RoomId { get; set; } = new();
    public StringFilter Name { get; set; } = new();
    public StringFilter CanonicalAlias { get; set; } = new();
    public StringFilter Version { get; set; } = new();
    public StringFilter Creator { get; set; } = new();
    public StringFilter Encryption { get; set; } = new();
    public StringFilter JoinRules { get; set; } = new();
    public StringFilter GuestAccess { get; set; } = new();
    public StringFilter HistoryVisibility { get; set; } = new();
    public StringFilter RoomType { get; set; } = new();
    public StringFilter Topic { get; set; } = new();

    public IntFilter JoinedMembers { get; set; } = new() {
        GreaterThan = 0,
        LessThan = int.MaxValue
    };

    public IntFilter JoinedLocalMembers { get; set; } = new() {
        GreaterThan = 0,
        LessThan = int.MaxValue
    };

    public IntFilter StateEvents { get; set; } = new() {
        GreaterThan = 0,
        LessThan = int.MaxValue
    };

    public BoolFilter Federation { get; set; } = new();
    public BoolFilter Public { get; set; } = new();
    public BoolFilter Tombstone { get; set; } = new();
}

public class OptionalFilter {
    public bool Enabled { get; set; }
}

public class StringFilter : OptionalFilter {
    public bool CheckValueContains { get; set; }
    public string? ValueContains { get; set; }

    public bool CheckValueEquals { get; set; }
    public string? ValueEquals { get; set; }

    public bool Matches(string? value, StringComparison comparison = StringComparison.Ordinal) {
        if (!Enabled) return true;

        if (CheckValueEquals) {
            if (!string.Equals(value, ValueEquals, comparison)) return false;
        }

        if (CheckValueContains && ValueContains != null) {
            if (value != null && !value.Contains(ValueContains, comparison)) return false;
        }

        return true;
    }
}

public class IntFilter : OptionalFilter {
    public bool CheckGreaterThan { get; set; }
    public int GreaterThan { get; set; }
    public bool CheckLessThan { get; set; }
    public int LessThan { get; set; }

    public bool Matches(int value) {
        if (!Enabled) return true;

        if (CheckGreaterThan) {
            if (value <= GreaterThan) return false;
        }

        if (CheckLessThan) {
            if (value >= LessThan) return false;
        }

        return true;
    }
}

public class BoolFilter : OptionalFilter {
    public bool Value { get; set; }

    public bool Matches(bool value) {
        if (!Enabled) return true;
        return value == Value;
    }
}