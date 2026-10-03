using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Novolis.Storage.Unit.Repositories;

/// <summary>In-process Azure Table REST stand-in so repository tests do not need Docker.</summary>
internal sealed class AzureTableMemoryHandler : HttpMessageHandler
{
    public bool DelayRequests { get; set; }

    private readonly Dictionary<string, Dictionary<string, string>> _tables = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _containers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> _blobs = new(StringComparer.Ordinal);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (DelayRequests)
            await Task.Yield();

        var uri = request.RequestUri ?? throw new InvalidOperationException("Missing request URI.");
        var path = Uri.UnescapeDataString(uri.AbsolutePath);
        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var query = ParseQuery(uri.Query);

        if (uri.Host.Contains(".blob.", StringComparison.OrdinalIgnoreCase))
            return await BlobAsync(request, path, query, cancellationToken).ConfigureAwait(false);

        if (request.Method == HttpMethod.Post && path.EndsWith("/Tables", StringComparison.OrdinalIgnoreCase))
            return CreateTable(body);

        if (request.Method == HttpMethod.Post && TryParseEntityCollection(path, out var insertTable))
            return InsertEntity(insertTable, body);

        if (TryParseEntity(path, out var table, out var partition, out var row))
            return Entity(request.Method, table, partition, row, body);

        if (TryParseCollection(path, out table))
            return Query(table, query);

        return Error(HttpStatusCode.BadRequest, "Unsupported", request.Method + " " + uri + " " + body);
    }

    private HttpResponseMessage InsertEntity(string table, string body)
    {
        using var document = JsonDocument.Parse(body);
        var partition = document.RootElement.GetProperty("PartitionKey").GetString()!;
        var row = document.RootElement.GetProperty("RowKey").GetString()!;
        var rows = Rows(table);
        var key = partition + "\n" + row;
        if (rows.ContainsKey(key))
            return Error(HttpStatusCode.Conflict, "EntityAlreadyExists", "The entity already exists.");

        rows.Add(key, body);
        return Json(HttpStatusCode.Created, "{}");
    }

    private async Task<HttpResponseMessage> BlobAsync(
        HttpRequestMessage request,
        string path,
        IReadOnlyDictionary<string, string> query,
        CancellationToken cancellationToken)
    {
        var separator = path.IndexOf('/', 1);
        var container = separator < 0 ? path[1..] : path[1..separator];
        var blob = separator < 0 ? string.Empty : path[(separator + 1)..];
        var key = container + "/" + blob;
        if (string.Equals(request.Method.Method, "PUT", StringComparison.OrdinalIgnoreCase)
            && query.ContainsKey("restype"))
        {
            var created = _containers.Add(container);
            return new HttpResponseMessage(created ? HttpStatusCode.Created : HttpStatusCode.Conflict);
        }

        if (request.Method == HttpMethod.Head)
        {
            var exists = blob.Length == 0
                ? _containers.Contains(container)
                : _blobs.ContainsKey(key);
            return exists
                ? BlobResponse(HttpStatusCode.OK, Array.Empty<byte>(), key)
                : BlobMissing();
        }

        if (request.Method == HttpMethod.Put)
        {
            var bytes = request.Content is null
                ? Array.Empty<byte>()
                : await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            _containers.Add(container);
            _blobs[key] = bytes;
            return BlobResponse(HttpStatusCode.Created, Array.Empty<byte>(), key);
        }

        if (request.Method == HttpMethod.Get
            && query.TryGetValue("comp", out var component)
            && string.Equals(component, "list", StringComparison.OrdinalIgnoreCase))
        {
            return BlobList(container);
        }

        if (request.Method == HttpMethod.Get)
            return _blobs.TryGetValue(key, out var stored)
                ? BlobResponse(HttpStatusCode.OK, stored, key)
                : BlobMissing();

        if (request.Method == HttpMethod.Delete)
        {
            return _blobs.Remove(key)
                ? BlobResponse(HttpStatusCode.Accepted, Array.Empty<byte>(), key)
                : BlobMissing();
        }

        return Error(HttpStatusCode.BadRequest, "Unsupported", request.Method.Method);
    }

    private HttpResponseMessage BlobList(string container)
    {
        var names = _blobs.Keys
            .Where(key => key.StartsWith(container + "/", StringComparison.Ordinal))
            .Select(key => key[(container.Length + 1)..])
            .Order(StringComparer.Ordinal)
            .ToArray();
        var xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?><EnumerationResults><Blobs>"
            + string.Join(
                string.Empty,
                names.Select(name =>
                    "<Blob><Name>" + System.Security.SecurityElement.Escape(name) + "</Name>"
                    + "<Properties><Last-Modified>" + DateTimeOffset.UtcNow.ToString("R", CultureInfo.InvariantCulture)
                    + "</Last-Modified><Etag>\\\"memory\\\"</Etag><Content-Length>0</Content-Length>"
                    + "<Content-Type>application/octet-stream</Content-Type><BlobType>BlockBlob</BlobType>"
                    + "</Properties></Blob>"))
            + "</Blobs><NextMarker /></EnumerationResults>";
        return Text(HttpStatusCode.OK, xml, "application/xml");
    }

    private static HttpResponseMessage BlobResponse(
        HttpStatusCode status,
        byte[] bytes,
        string key)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new PersistentBytesContent(bytes, "application/octet-stream"),
        };
        response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"" + key.GetHashCode().ToString("X") + "\"");
        return response;
    }

    private HttpResponseMessage CreateTable(string body)
    {
        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("TableName", out var name))
            throw new InvalidOperationException("Create table body: " + body);

        var table = name.GetString() ?? throw new InvalidOperationException("Create table body: " + body);
        if (!_tables.ContainsKey(table))
            _tables.Add(table, new Dictionary<string, string>(StringComparer.Ordinal));

        return Json(HttpStatusCode.Created, "{\"TableName\":\"" + table + "\"}");
    }

    private HttpResponseMessage Entity(HttpMethod method, string table, string partition, string row, string body)
    {
        var rows = Rows(table);
        var key = partition + "\n" + row;
        if (method == HttpMethod.Put || method == HttpMethod.Post)
        {
            rows[key] = body;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        if (method == HttpMethod.Get)
            return rows.TryGetValue(key, out var stored)
                ? EntityJson(HttpStatusCode.OK, stored, key)
                : Missing();

        if (method == HttpMethod.Delete)
        {
            if (!rows.Remove(key))
                return Missing();

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        return Error(HttpStatusCode.BadRequest, "Unsupported", method.Method);
    }

    private HttpResponseMessage Query(string table, IReadOnlyDictionary<string, string> query)
    {
        var rows = Rows(table);
        var filter = query.TryGetValue("$filter", out var text) ? text : null;
        var top = query.TryGetValue("$top", out var topText) && int.TryParse(topText, out var parsed) ? parsed : int.MaxValue;
        query.TryGetValue("NextPartitionKey", out var nextPartition);
        query.TryGetValue("NextRowKey", out var nextRow);
        var matched = rows.Values
            .Select(ReadRow)
            .Where(row => FilterParser.Matches(filter, row.Values))
            .OrderBy(row => row.PartitionKey, StringComparer.Ordinal)
            .ThenBy(row => row.RowKey, StringComparer.Ordinal)
            .ToList();
        var start = 0;
        if (!string.IsNullOrEmpty(nextRow))
        {
            start = matched.FindIndex(row => row.PartitionKey == nextPartition && row.RowKey == nextRow);
            if (start < 0)
                start = matched.Count;
        }

        var page = matched.Skip(start).Take(top).ToList();
        var payload = "{\"odata.metadata\":\"https://novolis.table.core.windows.net/$metadata#" + table + "\",\"value\":["
            + string.Join(',', page.Select(row => row.Json)) + "]}";
        var response = Json(HttpStatusCode.OK, payload);
        var next = start + page.Count;
        if (next < matched.Count)
        {
            response.Headers.TryAddWithoutValidation("x-ms-continuation-NextPartitionKey", matched[next].PartitionKey);
            response.Headers.TryAddWithoutValidation("x-ms-continuation-NextRowKey", matched[next].RowKey);
        }

        return response;
    }

    private Dictionary<string, string> Rows(string table)
    {
        if (!_tables.TryGetValue(table, out var rows))
        {
            rows = new Dictionary<string, string>(StringComparer.Ordinal);
            _tables.Add(table, rows);
        }

        return rows;
    }

    private static StoredRow ReadRow(string json)
    {
        using var document = JsonDocument.Parse(json);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Name.Contains('@', StringComparison.Ordinal))
                continue;

            var typeName = document.RootElement.TryGetProperty(property.Name + "@odata.type", out var type)
                ? type.GetString()
                : null;
            values[property.Name] = ReadValue(property.Value, typeName);
        }

        return new StoredRow(
            values.TryGetValue("PartitionKey", out var partition) ? Convert.ToString(partition, CultureInfo.InvariantCulture) ?? string.Empty : string.Empty,
            values.TryGetValue("RowKey", out var row) ? Convert.ToString(row, CultureInfo.InvariantCulture) ?? string.Empty : string.Empty,
            json,
            values);
    }

    private static object? ReadValue(JsonElement value, string? type) => type switch
    {
        "Edm.Guid" => value.GetGuid(),
        "Edm.DateTime" => DateTimeOffset.Parse(value.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        "Edm.Int64" => value.GetInt64(),
        "Edm.Double" => value.GetDouble(),
        _ => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => value.TryGetInt64(out var number) ? number : value.GetDouble(),
            JsonValueKind.Null => null,
            _ => value.GetRawText(),
        },
    };

    private static bool TryParseEntity(string path, out string table, out string partition, out string row)
    {
        table = string.Empty;
        partition = string.Empty;
        row = string.Empty;
        var open = path.IndexOf('(');
        if (open <= 1 || path[^1] != ')' || path.EndsWith("()", StringComparison.Ordinal))
            return false;

        table = path[1..open];
        var inside = path[(open + 1)..^1];
        const string partitionPrefix = "PartitionKey='";
        const string rowPrefix = "',RowKey='";
        if (!inside.StartsWith(partitionPrefix, StringComparison.Ordinal) || !inside.EndsWith('\''))
            return false;

        var rowAt = inside.IndexOf(rowPrefix, StringComparison.Ordinal);
        if (rowAt < 0)
            return false;

        partition = inside[partitionPrefix.Length..rowAt].Replace("''", "'", StringComparison.Ordinal);
        row = inside[(rowAt + rowPrefix.Length)..^1].Replace("''", "'", StringComparison.Ordinal);
        return true;
    }

    private static bool TryParseCollection(string path, out string table)
    {
        table = string.Empty;
        if (!path.EndsWith("()", StringComparison.Ordinal))
            return false;

        table = path[1..^2];
        return table.Length > 0 && !table.Contains('/', StringComparison.Ordinal);
    }

    private static bool TryParseEntityCollection(string path, out string table)
    {
        table = path.Length > 1 ? path[1..] : string.Empty;
        return table.Length > 0
            && !table.Contains('/', StringComparison.Ordinal)
            && !table.Equals("Tables", StringComparison.OrdinalIgnoreCase)
            && !table.EndsWith(')');
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(query))
            return result;

        var text = query[0] == '?' ? query[1..] : query;
        foreach (var part in text.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var split = part.IndexOf('=');
            var key = Uri.UnescapeDataString(split < 0 ? part : part[..split]);
            var value = split < 0 ? string.Empty : Uri.UnescapeDataString(part[(split + 1)..]);
            result[key] = value;
        }

        return result;
    }

    private static HttpResponseMessage Missing() =>
        Error(HttpStatusCode.NotFound, "ResourceNotFound", "The specified resource does not exist.");

    private static HttpResponseMessage BlobMissing() =>
        Text(
            HttpStatusCode.NotFound,
            "<?xml version=\"1.0\" encoding=\"utf-8\"?><Error><Code>BlobNotFound</Code><Message>The specified blob does not exist.</Message></Error>",
            "application/xml");

    private static HttpResponseMessage Error(HttpStatusCode status, string code, string message) =>
        Json(status, "{\"odata.error\":{\"code\":\"" + code + "\",\"message\":{\"lang\":\"en-US\",\"value\":\"" + message + "\"}}}");

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new PersistentJsonContent(body),
        };
        return response;
    }

    private static HttpResponseMessage EntityJson(
        HttpStatusCode status,
        string body,
        string key)
    {
        var etag = "\"" + key.GetHashCode().ToString("X") + "\"";
        var json = body.TrimEnd();
        json = json.EndsWith('}')
            ? json[..^1] + ",\"odata.etag\":\"" + etag.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"}"
            : body;
        var response = Json(status, json);
        response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue(etag);
        return response;
    }

    private static HttpResponseMessage Text(HttpStatusCode status, string body, string contentType)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new PersistentBytesContent(Encoding.UTF8.GetBytes(body), contentType),
        };
        return response;
    }

    /// <summary>
    /// The Tables client reads the body after the transport disposes the response content.
    /// A normal <see cref="StringContent"/> closes its stream at that point.
    /// </summary>
    private sealed class PersistentJsonContent : HttpContent
    {
        private readonly byte[] _bytes;

        public PersistentJsonContent(string body)
        {
            _bytes = Encoding.UTF8.GetBytes(body);
            Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json;odata=minimalmetadata");
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(_bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = _bytes.Length;
            return true;
        }

        protected override Task<Stream> CreateContentReadStreamAsync() =>
            Task.FromResult<Stream>(new PersistentStream(_bytes));
    }

    private sealed class PersistentBytesContent : HttpContent
    {
        private readonly byte[] _bytes;

        public PersistentBytesContent(byte[] bytes, string contentType)
        {
            _bytes = bytes;
            Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType);
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(_bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = _bytes.Length;
            return true;
        }

        protected override Task<Stream> CreateContentReadStreamAsync() =>
            Task.FromResult<Stream>(new PersistentStream(_bytes));
    }

    private sealed class PersistentStream : MemoryStream
    {
        public PersistentStream(byte[] bytes)
            : base(bytes, writable: false)
        {
        }

        protected override void Dispose(bool disposing)
        {
        }
    }

    private sealed record StoredRow(string PartitionKey, string RowKey, string Json, Dictionary<string, object?> Values);

    private sealed class FilterParser(string text, IReadOnlyDictionary<string, object?> values)
    {
        private int _index;

        public static bool Matches(string? filter, IReadOnlyDictionary<string, object?> row)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            var parser = new FilterParser(filter, row);
            var result = parser.ParseOr();
            parser.Skip();
            if (parser._index != filter.Length)
                throw new InvalidOperationException("Unparsed filter: " + filter);

            return result;
        }

        private bool ParseOr()
        {
            var left = ParseAnd();
            while (TryKeyword("or"))
            {
                var right = ParseAnd();
                left = left || right;
            }

            return left;
        }

        private bool ParseAnd()
        {
            var left = ParseNot();
            while (TryKeyword("and"))
            {
                var right = ParseNot();
                left = left && right;
            }

            return left;
        }

        private bool ParseNot()
        {
            if (!TryKeyword("not"))
                return ParsePrimary();

            return !ParsePrimary();
        }

        private bool ParsePrimary()
        {
            Skip();
            if (!TryChar('('))
                return ParseComparison();

            var inner = ParseOr();
            Expect(')');
            return inner;
        }

        private bool ParseComparison()
        {
            var left = ParseOperand();
            var op = ParseOperator();
            var right = ParseOperand();
            var comparison = Compare(Resolve(left), Resolve(right));
            return op switch
            {
                "eq" => comparison == 0,
                "ne" => comparison != 0,
                "gt" => comparison > 0,
                "ge" => comparison >= 0,
                "lt" => comparison < 0,
                "le" => comparison <= 0,
                _ => throw new InvalidOperationException(op),
            };
        }

        private Operand ParseOperand()
        {
            Skip();
            if (Peek() == '\'')
                return Operand.Literal(ReadQuoted());

            if (StartsWithWord("guid") && CharAfter("guid") == '\'')
            {
                MatchWord("guid");
                return Operand.Literal(Guid.Parse(ReadQuoted()));
            }

            if (StartsWithWord("datetime") && CharAfter("datetime") == '\'')
            {
                MatchWord("datetime");
                return Operand.Literal(DateTimeOffset.Parse(ReadQuoted(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
            }

            if (MatchWord("true"))
                return Operand.Literal(true);

            if (MatchWord("false"))
                return Operand.Literal(false);

            if (Peek() == '-' || char.IsAsciiDigit(Peek()))
                return Operand.Literal(ReadNumber());

            return Operand.Property(ReadIdentifier());
        }

        private string ParseOperator()
        {
            Skip();
            foreach (var op in new[] { "ge", "le", "eq", "ne", "gt", "lt" })
            {
                if (MatchWord(op))
                    return op;
            }

            throw new InvalidOperationException("Operator at " + _index + " in " + text);
        }

        private object? Resolve(Operand operand) =>
            operand.IsProperty
                ? values.TryGetValue(operand.Text ?? string.Empty, out var found) ? found : null
                : operand.Value;

        private static int Compare(object? left, object? right)
        {
            if (left is null && right is null)
                return 0;

            if (left is null)
                return -1;

            if (right is null)
                return 1;

            if (left is bool leftBool && right is bool rightBool)
                return leftBool.CompareTo(rightBool);

            if (IsNumber(left) && IsNumber(right))
                return Convert.ToDecimal(left, CultureInfo.InvariantCulture).CompareTo(Convert.ToDecimal(right, CultureInfo.InvariantCulture));

            if (left is Guid leftGuid && right is Guid rightGuid)
                return leftGuid.CompareTo(rightGuid);

            if (left is DateTimeOffset leftDate && right is DateTimeOffset rightDate)
                return leftDate.CompareTo(rightDate);

            return string.Compare(
                Convert.ToString(left, CultureInfo.InvariantCulture),
                Convert.ToString(right, CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
        }

        private static bool IsNumber(object value) => value is byte or sbyte or short or ushort or int or uint or long or float or double or decimal;

        private string ReadQuoted()
        {
            Expect('\'');
            var builder = new StringBuilder();
            while (_index < text.Length)
            {
                var character = text[_index++];
                if (character != '\'')
                {
                    builder.Append(character);
                    continue;
                }

                if (_index < text.Length && text[_index] == '\'')
                {
                    builder.Append('\'');
                    _index++;
                    continue;
                }

                return builder.ToString();
            }

            throw new InvalidOperationException("Unclosed string in " + text);
        }

        private object ReadNumber()
        {
            var start = _index;
            if (Peek() == '-')
                _index++;

            var dot = false;
            while (_index < text.Length && (char.IsAsciiDigit(text[_index]) || (!dot && text[_index] == '.')))
            {
                if (text[_index] == '.')
                    dot = true;

                _index++;
            }

            var number = text[start.._index];
            return dot
                ? double.Parse(number, CultureInfo.InvariantCulture)
                : long.Parse(number, CultureInfo.InvariantCulture);
        }

        private string ReadIdentifier()
        {
            var start = _index;
            if (_index >= text.Length || !char.IsAsciiLetter(text[_index]))
                throw new InvalidOperationException("Expected value at " + _index + " in " + text);

            while (_index < text.Length && (char.IsAsciiLetterOrDigit(text[_index]) || text[_index] == '_'))
                _index++;

            return text[start.._index];
        }

        private bool TryKeyword(string word)
        {
            var mark = _index;
            Skip();
            if (MatchWord(word))
                return true;

            _index = mark;
            return false;
        }

        private bool StartsWithWord(string word)
        {
            var mark = _index;
            var matched = MatchWord(word);
            _index = mark;
            return matched;
        }

        private char CharAfter(string word) =>
            _index + word.Length < text.Length ? text[_index + word.Length] : '\0';

        private bool MatchWord(string word)
        {
            if (_index + word.Length > text.Length)
                return false;

            if (string.Compare(text, _index, word, 0, word.Length, StringComparison.Ordinal) != 0)
                return false;

            var after = _index + word.Length;
            if (after < text.Length && char.IsAsciiLetterOrDigit(text[after]))
                return false;

            _index = after;
            return true;
        }

        private bool TryChar(char character)
        {
            if (Peek() != character)
                return false;

            _index++;
            return true;
        }

        private void Expect(char character)
        {
            if (!TryChar(character))
                throw new InvalidOperationException("Expected " + character + " at " + _index + " in " + text);
        }

        private char Peek()
        {
            Skip();
            return _index < text.Length ? text[_index] : '\0';
        }

        private void Skip()
        {
            while (_index < text.Length && char.IsWhiteSpace(text[_index]))
                _index++;
        }

        private readonly struct Operand
        {
            private Operand(bool isProperty, object? value, string? text)
            {
                IsProperty = isProperty;
                Value = value;
                Text = text;
            }

            public bool IsProperty { get; }

            public object? Value { get; }

            public string? Text { get; }

            public static Operand Literal(object? value) => new(false, value, null);

            public static Operand Property(string name) => new(true, null, name);
        }
    }
}
