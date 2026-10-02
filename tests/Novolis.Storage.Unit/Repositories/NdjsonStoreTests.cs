using System.Text;
using Novolis.Storage.Ndjson;
using TUnit.Core;

namespace Novolis.Storage.Tests.Repositories;

public sealed class NdjsonStoreTests
{
    [Test]
    public async Task Append_and_read_preserve_record_order()
    {
        var root = Directory.CreateTempSubdirectory("novolis-storage-ndjson-");
        try
        {
            using var store = new NdjsonStore(Path.Combine(root.FullName, "events.ndjson"));
            await store.AppendAsync(new TestRecord(1, "first"));
            await store.AppendAsync(new TestRecord(2, "second"));

            var records = new List<TestRecord>();
            await foreach (var record in store.ReadAsync<TestRecord>())
                records.Add(record);

            await Assert.That(records).Count().IsEqualTo(2);
            await Assert.That(records[0].Id).IsEqualTo(1);
            await Assert.That(records[1].Message).IsEqualTo("second");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Read_skips_malformed_and_incomplete_physical_lines()
    {
        var root = Directory.CreateTempSubdirectory("novolis-storage-ndjson-");
        var path = Path.Combine(root.FullName, "events.ndjson");
        try
        {
            await File.WriteAllTextAsync(
                path,
                "{\"id\":1,\"message\":\"first\"}\nnot-json\n{\"id\":2,\"message\":\"second\"}\n{\"id\":3",
                Encoding.UTF8);

            using var store = new NdjsonStore(path);
            var records = new List<TestRecord>();
            await foreach (var record in store.ReadAsync<TestRecord>())
                records.Add(record);

            await Assert.That(records).Count().IsEqualTo(2);
            await Assert.That(records[0].Id).IsEqualTo(1);
            await Assert.That(records[1].Id).IsEqualTo(2);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Replace_atomically_writes_the_new_sequence()
    {
        var root = Directory.CreateTempSubdirectory("novolis-storage-ndjson-");
        try
        {
            using var store = new NdjsonStore(Path.Combine(root.FullName, "events.ndjson"));
            store.Append(new TestRecord(1, "old"));
            await store.ReplaceAsync(
            [
                new TestRecord(2, "new"),
                new TestRecord(3, "newer"),
            ]);

            var records = new List<TestRecord>();
            await foreach (var record in store.ReadAsync<TestRecord>())
                records.Add(record);

            await Assert.That(records.Select(record => record.Id)).IsEquivalentTo([2, 3]);
            await Assert.That(
                    Directory.EnumerateFiles(root.FullName, "*.tmp", SearchOption.TopDirectoryOnly))
                .IsEmpty();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    private sealed record TestRecord(int Id, string Message);
}
