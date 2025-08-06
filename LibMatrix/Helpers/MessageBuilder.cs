using LibMatrix.EventTypes.Spec;

namespace LibMatrix.Helpers;

public class MessageBuilder(string msgType = "m.text", string format = "org.matrix.custom.html") {
    private RoomMessageEventContent Content { get; set; } = new() {
        MessageType = msgType,
        Format = format
    };

    public RoomMessageEventContent Build() => Content;

    public MessageBuilder WithBody(string body, string? formattedBody = null) {
        Content.Body += body;
        Content.FormattedBody += formattedBody ?? body;
        return this;
    }

    public MessageBuilder WithHtmlTag(string tag, string body, Dictionary<string, string>? attributes = null) {
        Content.Body += body;
        Content.FormattedBody += $"<{tag}";
        if (attributes != null)
            foreach (var (key, value) in attributes)
                Content.FormattedBody += $" {key}=\"{value}\"";
        Content.FormattedBody += $">{body}</{tag}>";
        return this;
    }

    public MessageBuilder WithHtmlTag(string tag, Action<MessageBuilder> bodyBuilder, Dictionary<string, string>? attributes = null) {
        Content.FormattedBody += $"<{tag}";
        if (attributes != null)
            foreach (var (key, value) in attributes)
                Content.FormattedBody += $" {key}=\"{value}\"";
        Content.FormattedBody += ">";
        bodyBuilder(this);
        Content.FormattedBody += $"</{tag}>";
        return this;
    }

    public static string GetColoredBody(string color, string body) {
        return $"<font color=\"{color}\">{body}</font>";
    }

    public MessageBuilder WithColoredBody(string color, string body) {
        Content.Body += body;
        Content.FormattedBody += $"<font color=\"{color}\">{body}</font>";
        return this;
    }

    public MessageBuilder WithColoredBody(string color, Action<MessageBuilder> bodyBuilder) {
        Content.FormattedBody += $"<font color=\"{color}\">";
        bodyBuilder(this);
        Content.FormattedBody += "</font>";
        return this;
    }

    public MessageBuilder WithCustomEmoji(string mxcUri, string name) {
        Content.Body += $"{{{name}}}";
        Content.FormattedBody += $"<img data-mx-emoticon height=\"32\" src=\"{mxcUri}\" alt=\"{name}\" title=\"{name}\" />";
        return this;
    }

    public MessageBuilder WithRainbowString(string text, byte skip = 1, int offset = 0, double lengthFactor = 255.0, bool useLength = true) {
        if (useLength) {
            lengthFactor = text.Length;
        }

        // HslaColorInterpolator interpolator = new((0, 255, 128, 255), (255, 255, 128, 255));
        // // RainbowEnumerator enumerator = new(skip, offset, lengthFactor);
        // for (int i = 0; i < text.Length; i++) {
        //     // var (r, g, b) = enumerator.Next();
        //     // var (r,g,b,a) = interpolator.Interpolate((i+offset) * skip, lengthFactor).ToRgba();
        //     // Console.WriteLine($"RBA: {r} {g} {b} {a}");
        //     // Content.FormattedBody += $"<font color=\"#{r:X2}{g:X2}{b:X2}\">{text[i]}</font>";
        // }
        return this;
    }

    public MessageBuilder WithCodeBlock(string code, string language = "plaintext") {
        Content.Body += code;
        Content.FormattedBody += $"<pre><code class=\"language-{language}\">{code}</code></pre>";
        return this;
    }

    public MessageBuilder WithCollapsibleSection(string title, string body) {
        Content.Body += body;
        Content.FormattedBody += $"<details><summary>{title}</summary>{body}</details>";
        return this;
    }

    public MessageBuilder WithCollapsibleSection(string title, Action<MessageBuilder> bodyBuilder) {
        Content.FormattedBody += $"<details><summary>{title}</summary>";
        bodyBuilder(this);
        Content.FormattedBody += "</details>";
        return this;
    }

    public MessageBuilder WithMention(string id, string? displayName = null, string[]? vias = null, bool useIdInPlainText = false, bool useLinkInPlainText = false) {
        if (!useLinkInPlainText) Content.Body += $"@{(useIdInPlainText ? id : displayName ?? id)}";
        else {
            Content.Body += $"https://matrix.to/#/{id}";
            if (vias is { Length: > 0 }) Content.Body += $"?via={string.Join("&via=", vias)}";
        }

        Content.FormattedBody += $"<a href=\"https://matrix.to/#/{id}\">{displayName ?? id}</a>";
        if (id == "@room") {
            Content.Mentions ??= new();
            Content.Mentions.Room = true;
        }
        else if (id.StartsWith('@')) {
            Content.Mentions ??= new();
            Content.Mentions.Users ??= new();
            Content.Mentions.Users.Add(id);
        }

        return this;
    }

    public MessageBuilder WithRoomMention() {
        // Legacy push rules support
        Content.Body += "@room";
        Content.FormattedBody += "@room";
        Content.Mentions ??= new();
        Content.Mentions.Room = true;
        return this;
    }

    public MessageBuilder WithNewline() {
        Content.Body += "\n";
        Content.FormattedBody += "<br>";
        return this;
    }

    public MessageBuilder WithTable(Action<TableBuilder> tableBuilder) {
        var tb = new TableBuilder(this);
        WithHtmlTag("table", msb => tableBuilder(tb));
        return this;
    }

    public class TableBuilder(MessageBuilder msb) {
        public TableBuilder WithTitle(string title, int colspan) {
            msb.Content.Body += title + "\n";
            msb.Content.FormattedBody += $"<thead><tr><th colspan=\"{colspan}\">{title}</th></tr></thead><br/>";
            return this;
        }

        public TableBuilder WithRow(Action<RowBuilder> rowBuilder) {
            var rb = new RowBuilder(msb);
            msb.WithHtmlTag("tr", msb => rowBuilder(rb)).WithBody("\n");
            return this;
        }

        public class RowBuilder(MessageBuilder msb) {
            public RowBuilder WithCell(string content, Dictionary<string, string>? attributes = null) {
                msb.Content.Body += content + "\n";
                msb.Content.FormattedBody += $"<td>{content}</td>\t";
                return this;
            }
        }
    }
}