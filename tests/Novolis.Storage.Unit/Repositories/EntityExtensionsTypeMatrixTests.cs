using System.Globalization;
using System.Reflection;
using Novolis.Storage.Abstractions;
using Novolis.Storage.Sqlite;

namespace Novolis.Storage.Tests.Repositories;

public sealed class EntityExtensionsTypeMatrixTests
{
    sealed class MatrixEntity : IHasId
    {
        public Guid Id { get; set; }
        public int IntVal { get; set; }
        public int? NullableInt { get; set; }
        public long LongVal { get; set; }
        public long? NullableLong { get; set; }
        public short ShortVal { get; set; }
        public short? NullableShort { get; set; }
        public byte ByteVal { get; set; }
        public byte? NullableByte { get; set; }
        public uint UIntVal { get; set; }
        public uint? NullableUInt { get; set; }
        public ulong ULongVal { get; set; }
        public ulong? NullableULong { get; set; }
        public ushort UShortVal { get; set; }
        public ushort? NullableUShort { get; set; }
        public sbyte SByteVal { get; set; }
        public sbyte? NullableSByte { get; set; }
        public char CharVal { get; set; }
        public char? NullableChar { get; set; }
        public string Text { get; set; } = string.Empty;
        public Guid GuidVal { get; set; }
        public Guid? NullableGuid { get; set; }
        public DateTime DateTimeVal { get; set; }
        public DateTime? NullableDateTime { get; set; }
        public bool BoolVal { get; set; }
        public bool? NullableBool { get; set; }
        public decimal DecimalVal { get; set; }
        public decimal? NullableDecimal { get; set; }
        public TimeSpan TimeSpanVal { get; set; }
        public TimeSpan? NullableTimeSpan { get; set; }
        public DateTimeOffset OffsetVal { get; set; }
        public DateTimeOffset? NullableOffset { get; set; }
        public byte[] Bytes { get; set; } = [];
    }

    [Test]
    public async Task SetPropertyValue_covers_scalar_type_matrix()
    {
        var entity = new MatrixEntity { Id = Guid.NewGuid() };
        var utc = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var offset = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.FromHours(2));
        var duration = new TimeSpan(1, 2, 3);
        var guid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var payload = new byte[] { 1, 2, 3 };

        Set(entity, nameof(MatrixEntity.IntVal), typeof(int), 42);
        Set(entity, nameof(MatrixEntity.NullableInt), typeof(int?), 7);
        Set(entity, nameof(MatrixEntity.LongVal), typeof(long), 9_000_000_000L);
        Set(entity, nameof(MatrixEntity.NullableLong), typeof(long?), 8L);
        Set(entity, nameof(MatrixEntity.ShortVal), typeof(short), (short)9);
        Set(entity, nameof(MatrixEntity.NullableShort), typeof(short?), (short)3);
        Set(entity, nameof(MatrixEntity.ByteVal), typeof(byte), (byte)5);
        Set(entity, nameof(MatrixEntity.NullableByte), typeof(byte?), (byte)2);
        Set(entity, nameof(MatrixEntity.UIntVal), typeof(uint), 11U);
        Set(entity, nameof(MatrixEntity.NullableUInt), typeof(uint?), 4U);
        Set(entity, nameof(MatrixEntity.ULongVal), typeof(ulong), 22UL);
        Set(entity, nameof(MatrixEntity.NullableULong), typeof(ulong?), 6UL);
        Set(entity, nameof(MatrixEntity.UShortVal), typeof(ushort), (ushort)13);
        Set(entity, nameof(MatrixEntity.NullableUShort), typeof(ushort?), (ushort)1);
        Set(entity, nameof(MatrixEntity.SByteVal), typeof(sbyte), (sbyte)-4);
        Set(entity, nameof(MatrixEntity.NullableSByte), typeof(sbyte?), (sbyte)-2);
        Set(entity, nameof(MatrixEntity.CharVal), typeof(char), 'Z');
        Set(entity, nameof(MatrixEntity.NullableChar), typeof(char?), 'Q');
        Set(entity, nameof(MatrixEntity.Text), typeof(string), "typed");
        Set(entity, nameof(MatrixEntity.GuidVal), typeof(Guid), guid.ToString());
        Set(entity, nameof(MatrixEntity.NullableGuid), typeof(Guid?), guid.ToString());
        Set(entity, nameof(MatrixEntity.DateTimeVal), typeof(DateTime), utc);
        Set(entity, nameof(MatrixEntity.NullableDateTime), typeof(DateTime?), utc);
        Set(entity, nameof(MatrixEntity.BoolVal), typeof(bool), true);
        Set(entity, nameof(MatrixEntity.NullableBool), typeof(bool?), false);
        Set(entity, nameof(MatrixEntity.DecimalVal), typeof(decimal), 12345);
        Set(entity, nameof(MatrixEntity.NullableDecimal), typeof(decimal?), 67890);
        Set(entity, nameof(MatrixEntity.TimeSpanVal), typeof(TimeSpan), duration.ToString());
        Set(entity, nameof(MatrixEntity.NullableTimeSpan), typeof(TimeSpan?), duration.ToString());
        Set(entity, nameof(MatrixEntity.OffsetVal), typeof(DateTimeOffset), offset.ToString("o", CultureInfo.InvariantCulture));
        Set(entity, nameof(MatrixEntity.NullableOffset), typeof(DateTimeOffset?), offset.ToString("o", CultureInfo.InvariantCulture));
        Set(entity, nameof(MatrixEntity.Bytes), typeof(byte[]), payload);

        await Assert.That(entity.IntVal).IsEqualTo(42);
        await Assert.That(entity.NullableInt).IsEqualTo(7);
        await Assert.That(entity.LongVal).IsEqualTo(9_000_000_000L);
        await Assert.That(entity.Text).IsEqualTo("typed");
        await Assert.That(entity.GuidVal).IsEqualTo(guid);
        await Assert.That(entity.NullableGuid).IsEqualTo(guid);
        await Assert.That(entity.DecimalVal).IsEqualTo(12345);
        await Assert.That(entity.NullableDecimal).IsEqualTo(67890);
        await Assert.That(entity.TimeSpanVal).IsEqualTo(duration);
        await Assert.That(entity.NullableTimeSpan).IsEqualTo(duration);
        await Assert.That(entity.OffsetVal).IsEqualTo(offset);
        await Assert.That(entity.NullableOffset).IsEqualTo(offset);
        await Assert.That(entity.Bytes[0]).IsEqualTo((byte)1);
        await Assert.That(entity.Bytes.Length).IsEqualTo(3);
    }

    [Test]
    public async Task SetPropertyValue_nullable_parsers_accept_invalid_as_null()
    {
        var entity = new MatrixEntity { Id = Guid.NewGuid() };
        Set(entity, nameof(MatrixEntity.NullableGuid), typeof(Guid?), "not-a-guid");
        Set(entity, nameof(MatrixEntity.NullableTimeSpan), typeof(TimeSpan?), "not-a-timespan");
        Set(entity, nameof(MatrixEntity.NullableOffset), typeof(DateTimeOffset?), "not-an-offset");

        await Assert.That(entity.NullableGuid).IsNull();
        await Assert.That(entity.NullableTimeSpan).IsNull();
        await Assert.That(entity.NullableOffset).IsNull();
    }

    static void Set(MatrixEntity entity, string name, Type fieldType, object value)
    {
        var property = typeof(MatrixEntity).GetProperty(name, BindingFlags.Public | BindingFlags.Instance)!;
        entity.SetPropertyValue(fieldType, property, value);
    }
}
