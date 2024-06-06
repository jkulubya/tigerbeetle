using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace TigerBeetle.Tests;

[TestClass]
public class UInt128Tests
{
    [TestMethod]
    public void GuidConversion()
    {
        Guid guid = Guid.Parse("A945C62A-4CC7-425B-B44A-893577632902");
        UInt128 value = guid.ToUInt128();

        Assert.AreEqual(value, guid.ToUInt128());
        Assert.AreEqual(guid, value.ToGuid());
    }

    [TestMethod]
    public void GuidMaxConversion()
    {
        Guid guid = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        UInt128 value = guid.ToUInt128();

        Assert.AreEqual(value, guid.ToUInt128());
        Assert.AreEqual(guid, value.ToGuid());
    }

    [TestMethod]
    public void ArrayConversion()
    {
        byte[] array = new byte[16] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f, 0x10 };
        UInt128 value = array.ToUInt128();

        Assert.IsTrue(value.ToArray().SequenceEqual(array));
        Assert.IsTrue(array.SequenceEqual(value.ToArray()));
        Assert.IsTrue(value.Equals(array.ToUInt128()));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullArrayConversion()
    {
        byte[] array = null!;
        _ = array.ToUInt128();
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void EmptyArrayConversion()
    {
        byte[] array = new byte[0];
        _ = array.ToUInt128();
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void InvalidArrayConversion()
    {
        // Expected ArgumentException.
        byte[] array = new byte[17];
        _ = array.ToUInt128();
    }

    [TestMethod]
    public void BigIntegerConversion()
    {
        var checkConvertion = (BigInteger bigInteger) =>
        {
            UInt128 uint128 = bigInteger.ToUInt128();

            Assert.AreEqual(uint128, bigInteger.ToUInt128());
            Assert.AreEqual(bigInteger, uint128.ToBigInteger());
            Assert.IsTrue(uint128.Equals(bigInteger.ToUInt128()));
        };

        checkConvertion(BigInteger.Parse("0"));
        checkConvertion(BigInteger.Parse("1"));
        checkConvertion(BigInteger.Parse("123456789012345678901234567890123456789"));
        checkConvertion(new BigInteger(uint.MaxValue));
        checkConvertion(new BigInteger(ulong.MaxValue));
    }

    [TestMethod]
    [ExpectedException(typeof(OverflowException))]
    public void BigIntegerNegative()
    {
        // Expected OverflowException.
        _ = BigInteger.MinusOne.ToUInt128();
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void BigIntegerExceedU128()
    {
        // Expected ArgumentOutOfRangeException.
        BigInteger bigInteger = BigInteger.Parse("9999999999999999999999999999999999999999");
        _ = bigInteger.ToUInt128();
    }

    [TestMethod]
    public void LittleEndian()
    {
        var expected = new byte[16] {86, 52, 18, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0};

        Assert.IsTrue(expected.SequenceEqual(expected.ToUInt128().ToArray()));
        Assert.IsTrue(expected.SequenceEqual(BigInteger.Parse("123456", NumberStyles.HexNumber).ToUInt128().ToArray()));
        // `bigEndian: true` signals to the Guid constructor to store the bytes as-is 
        Assert.IsTrue(expected.SequenceEqual(new Guid(expected, bigEndian: true).ToUInt128().ToArray()));
        Assert.IsTrue(expected.SequenceEqual(new UInt128(0, 0x123456).ToArray()));
    }

    [TestMethod]
    public unsafe void EndToEndTigerBeetleSimulationViaGuid()
    {
        // "00000001-0001-4000-AA00-000000000000" 
        var idSourcedExternally = Guid.Parse("00000001-0001-4000-AA00-000000000000"); // e.g. guid from other database
        var bytesStoredInTB = new byte[]
            {0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x40, 0x00, 0xAA, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};

        // Parse TB result (TB client reads by overlaying dotnet types on raw bytes)
        UInt128 parsed = UInt128.Zero;
        bytesStoredInTB.CopyTo(new Span<byte>(&parsed, 16));

        // Round-trip via GUID
        var roundTripped = parsed.ToGuid().ToUInt128();

        // Send bytes to TB (TB client send raw bytes of dotnet types over the wire) 
        var roundTrippedBytes = new Span<byte>(&roundTripped, 16);

        var externalIdUInt128 = idSourcedExternally.ToUInt128();
        var externalIdBytes = new Span<byte>(&externalIdUInt128, 16);

        Assert.IsTrue(roundTrippedBytes.SequenceEqual(bytesStoredInTB));
        Assert.IsTrue(externalIdBytes.SequenceEqual(bytesStoredInTB));
    }

    [TestMethod]
    public void IDCreation()
    {
        var verifier = () =>
        {
            UInt128 idA = ID.Create();
            for (int i = 0; i < 1_000_000; i++)
            {
                if (i % 1_000 == 0)
                {
                    Thread.Sleep(1);
                }

                UInt128 idB = ID.Create();
                Assert.IsTrue(idB.CompareTo(idA) > 0);
                idA = idB;
            }
        };

        // Verify monotonic IDs locally.
        verifier();

        // Verify monotonic IDs across multiple threads.
        var concurrency = 10;
        var startBarrier = new Barrier(concurrency);
        Parallel.For(0, concurrency, (_, _) =>
        {
            startBarrier.SignalAndWait();
            verifier();
        });
    }
}
