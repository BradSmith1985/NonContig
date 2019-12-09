# NonContig
A set of classes for working with sequences of bytes that are not contiguously allocated.

## NcByteCollection
A collection class implementing `IList<byte>` which allows both sequential and random access to individual bytes or byte ranges. Internally, the data is stored as a linked list of 4KB blocks, designed to avoid the `OutOfMemoryException` that can occur when trying to allocate large contiguous arrays in .NET.

The class offers several methods for copying data to and from byte arrays, unmanaged memory and `Stream` objects. It also implements `ICloneable` for duplicating the data. Both binary and XML serialization are supported.

## NcByteStream
A `Stream`-derived class that uses an `NcByteCollection` as its backing store. Designed as a replacement for `MemoryStream` in situations where the length is large or varies greatly.

More information here: https://www.brad-smith.info/blog/archives/954
