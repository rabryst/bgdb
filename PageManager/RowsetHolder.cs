﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;

namespace PageManager
{
    public interface IRowsetHolder
    {
        public int[] GetIntColumn(int columnId);
        public double[] GetDoubleColumn(int columnId);
        public PagePointerOffsetPair[] GetStringPointerColumn(int columnId);
        public long[] GetPagePointerColumn(int columnId);
        public void SetColumns(int[][] intColumns, double[][] doubleColumns, PagePointerOffsetPair[][] pagePointerOffsetColumns, long[][] pagePointerColumns);
        public uint StorageSizeInBytes();
        public void Serialize(BinaryWriter content);
        public void Deserialize(BinaryReader source, uint elemCount);
        public uint GetRowCount();
        public void Merge(RowsetHolder rowsetHolder);
        public ColumnType[] GetColumnTypes();
        public void ModifyRow(int rowNumber, RowsetHolder rowsetHolder);
    }

    public struct RowHolder : IEquatable<RowHolder>
    {
        public int[] iRow;
        public double[] dRow;
        public PagePointerOffsetPair[] strPRow;
        public long[] pagePRow;

        public bool Equals([AllowNull] RowHolder other)
        {
            return Enumerable.SequenceEqual(iRow, other.iRow) &&
                Enumerable.SequenceEqual(dRow, other.dRow) &&
                Enumerable.SequenceEqual(strPRow, other.strPRow) &&
                Enumerable.SequenceEqual(pagePRow, other.pagePRow);
        }
    }

    public class RowsetHolder : IRowsetHolder, IEnumerable<RowHolder>, IEquatable<RowsetHolder>
    {
        private int[][] intColumns;
        private PagePointerOffsetPair[][] pagePointerOffsetColumns;
        private double[][] doubleColumns;
        private long[][] pagePointerColumns;
        private int[] columnIdToTypeIdMappers;
        private uint rowsetCount = 0;
        private ColumnType[] columnTypes;

        public RowsetHolder(ColumnType[] columnTypes)
        {
            int intCount = 0;
            int doubleCount = 0;
            int pagePointerOffsetCount = 0;
            int pagePointerCount = 0;
            this.columnTypes = columnTypes;

            columnIdToTypeIdMappers = new int[columnTypes.Length];

            for (int i = 0; i < columnTypes.Length; i++)
            {
                switch (columnTypes[i])
                {
                    case ColumnType.Int:
                        columnIdToTypeIdMappers[i] = intCount;
                        intCount++;
                        break;
                    case ColumnType.Double:
                        columnIdToTypeIdMappers[i] = doubleCount;
                        doubleCount++;
                        break;
                    case ColumnType.StringPointer:
                        columnIdToTypeIdMappers[i] = pagePointerOffsetCount;
                        pagePointerOffsetCount++;
                        break;
                    case ColumnType.PagePointer:
                        columnIdToTypeIdMappers[i] = pagePointerCount;
                        pagePointerCount++;
                        break;
                    default:
                        throw new UnexpectedEnumValueException<ColumnType>(columnTypes[i]);
                }
            }

            this.intColumns = new int[intCount][];
            for(int i = 0; i <  this.intColumns.Length; i++)
            {
                this.intColumns[i] = new int[0];
            }

            this.pagePointerOffsetColumns = new PagePointerOffsetPair[pagePointerOffsetCount][];
            for(int i = 0; i <  this.pagePointerOffsetColumns.Length; i++)
            {
                this.pagePointerOffsetColumns[i] = new PagePointerOffsetPair[0];
            }

            this.doubleColumns = new double[doubleCount][];
            for(int i = 0; i <  this.doubleColumns.Length; i++)
            {
                this.doubleColumns[i] = new double[0];
            }

            this.pagePointerColumns = new long[pagePointerCount][];
            for(int i = 0; i <  this.pagePointerColumns.Length; i++)
            {
                this.pagePointerColumns[i] = new long[0];
            }

            this.rowsetCount = 0;
        }

        public uint GetRowCount() => this.rowsetCount;

        public int[] GetIntColumn(int columnId)
        {
            return intColumns[columnIdToTypeIdMappers[columnId]];
        }

        public double[] GetDoubleColumn(int columnId)
        {
            return doubleColumns[columnIdToTypeIdMappers[columnId]];
        }

        public PagePointerOffsetPair[] GetStringPointerColumn(int columnId)
        {
            return pagePointerOffsetColumns[columnIdToTypeIdMappers[columnId]];
        }

        private uint VerifyColumnValidityAndGetRowCount(int[][] intColumns, double[][] doubleColumns, PagePointerOffsetPair[][] pagePointerOffsetColumns, long[][] pagePointerColumns)
        {
            if (intColumns.Length != this.intColumns.Length ||
                doubleColumns.Length != this.doubleColumns.Length ||
                pagePointerColumns.Length != this.pagePointerColumns.Length ||
                pagePointerOffsetColumns.Length != this.pagePointerOffsetColumns.Length)
            {
                throw new InvalidRowsetDefinitionException();
            }

            int rowCount = 0;
            foreach (var intColum in intColumns)
            {
                if (rowCount == 0)
                {
                    rowCount = intColum.Length;
                }

                if (intColum.Length != rowCount)
                {
                    throw new InvalidRowsetDefinitionException();
                }
            }

            foreach (var doubleColum in doubleColumns)
            {
                if (rowCount == 0)
                {
                    rowCount = doubleColum.Length;
                }

                if (doubleColum.Length != rowCount)
                {
                    throw new InvalidRowsetDefinitionException();
                }
            }

            foreach (var pagePointerColumn in pagePointerColumns)
            {
                if (rowCount == 0)
                {
                    rowCount = pagePointerColumn.Length;
                }

                if (pagePointerColumn.Length != rowCount)
                {
                    throw new InvalidRowsetDefinitionException();
                }
            }

            foreach (var pagePointerOffsetColumn in pagePointerOffsetColumns)
            {
                if (rowCount == 0)
                {
                    rowCount = pagePointerOffsetColumn.Length;
                }

                if (pagePointerOffsetColumn.Length != rowCount)
                {
                    throw new InvalidRowsetDefinitionException();
                }
            }

            return (uint)rowCount;
        }

        public void SetColumns(int[][] intColumns, double[][] doubleColumns, PagePointerOffsetPair[][] pagePointerOffsetColumns, long[][] pagePointerColumns)
        {
            this.rowsetCount = this.VerifyColumnValidityAndGetRowCount(intColumns, doubleColumns, pagePointerOffsetColumns, pagePointerColumns);

            for (int i = 0; i < pagePointerOffsetColumns.Length; i++)
            {
                this.pagePointerOffsetColumns[i] = pagePointerOffsetColumns[i];
            }

            for (int i = 0; i < intColumns.Length; i++)
            {
                this.intColumns[i] = intColumns[i];
            }

            for (int i = 0; i < doubleColumns.Length; i++)
            {
                this.doubleColumns[i] = doubleColumns[i];
            }

            for (int i = 0; i < pagePointerColumns.Length; i++)
            {
                this.pagePointerColumns[i] = pagePointerColumns[i];
            }
        }

        public uint StorageSizeInBytes()
        {
            return sizeof(int) + this.rowsetCount * (uint)(PagePointerOffsetPair.Size * this.pagePointerOffsetColumns.Length +
                sizeof(int) * this.intColumns.Length +
                sizeof(double) * this.doubleColumns.Length +
                sizeof(long) * this.pagePointerColumns.Length);
        }

        public void Serialize(BinaryWriter destination)
        {
            if (destination.BaseStream.Length < this.StorageSizeInBytes())
            {
                throw new InvalidRowsetDefinitionException();
            }

            foreach (int[] iCol in this.intColumns)
            {
                foreach (int iVal in iCol)
                {
                    destination.Write(iVal);
                }
            }

            foreach (double[] dCol in this.doubleColumns)
            {
                foreach (double dVal in dCol)
                {
                    destination.Write(dVal);
                }
            }

            foreach (long[] lCol in this.pagePointerColumns)
            {
                foreach (long lVal in lCol)
                {
                    destination.Write(lVal);
                }
            }

            foreach (PagePointerOffsetPair[] ppCol in this.pagePointerOffsetColumns)
            {
                foreach (PagePointerOffsetPair ppVal in ppCol)
                {
                    destination.Write(ppVal.OffsetInPage);
                    destination.Write(ppVal.PageId);
                }
            }
        }

        public void Deserialize(BinaryReader source, uint elemCount)
        {
            this.rowsetCount = elemCount;

            for (int i = 0; i < intColumns.Length; i++)
            {
                intColumns[i] = new int[this.rowsetCount];

                for (int j = 0; j < rowsetCount; j++)
                {
                    intColumns[i][j] = source.ReadInt32();
                }
            }

            for (int i = 0; i < doubleColumns.Length; i++)
            {
                doubleColumns[i] = new double[this.rowsetCount];

                for (int j = 0; j < rowsetCount; j++)
                {
                    doubleColumns[i][j] = source.ReadDouble();
                }
            }

            for (int i = 0; i < pagePointerColumns.Length; i++)
            {
                pagePointerColumns[i] = new long[this.rowsetCount];

                for (int j = 0; j < rowsetCount; j++)
                {
                    pagePointerColumns[i][j] = source.ReadInt64();
                }
            }

            for (int i = 0; i < pagePointerOffsetColumns.Length; i++)
            {
                pagePointerOffsetColumns[i] = new PagePointerOffsetPair[this.rowsetCount];

                for (int j = 0; j < rowsetCount; j++)
                {
                    pagePointerOffsetColumns[i][j].OffsetInPage = source.ReadInt32();
                    pagePointerOffsetColumns[i][j].PageId = source.ReadInt64();
                }
            }
        }

        public static uint CalculateSizeOfRow(ColumnType[] types)
        {
            int totalSize = 0;

            foreach (ColumnType type in types)
            {
                switch (type)
                {
                    case ColumnType.Int: totalSize += sizeof(int); break;
                    case ColumnType.Double: totalSize += sizeof(double); break;
                    case ColumnType.StringPointer: totalSize += (int)PagePointerOffsetPair.Size; break;
                    case ColumnType.PagePointer: totalSize += sizeof(long); break;
                    default:
                        throw new UnexpectedEnumValueException<ColumnType>(type);
                }
            }

            return (uint)totalSize;
        }

        public long[] GetPagePointerColumn(int columnId)
        {
            return pagePointerColumns[columnIdToTypeIdMappers[columnId]];
        }

        public void Merge(RowsetHolder rowsetHolder)
        {
            if (this.columnIdToTypeIdMappers.Length != rowsetHolder.columnIdToTypeIdMappers.Length)
            {
                throw new ArgumentException();
            }

            for (int i = 0; i < this.columnIdToTypeIdMappers.Length; i++)
            {
                if (this.columnIdToTypeIdMappers[i] != rowsetHolder.columnIdToTypeIdMappers[i])
                {
                    throw new ArgumentException();
                }
            }

            for (int i = 0; i < this.pagePointerOffsetColumns.Length; i++)
            {
                if (this.pagePointerOffsetColumns[i] == null)
                {
                    this.pagePointerOffsetColumns[i] = rowsetHolder.pagePointerOffsetColumns[i];
                }
                else
                {
                    this.pagePointerOffsetColumns[i] = this.pagePointerOffsetColumns[i].Concat(rowsetHolder.pagePointerOffsetColumns[i]).ToArray();
                }
            }

            for (int i = 0; i < this.intColumns.Length; i++)
            {
                if (this.intColumns[i] == null)
                {
                    this.intColumns[i] = rowsetHolder.intColumns[i];
                }
                else
                {
                    this.intColumns[i] = this.intColumns[i].Concat(rowsetHolder.intColumns[i]).ToArray();
                }
            }

            for (int i = 0; i < this.doubleColumns.Length; i++)
            {
                if (this.doubleColumns[i] == null)
                {
                    this.doubleColumns[i] = rowsetHolder.doubleColumns[i];
                }
                else
                {
                    this.doubleColumns[i] = this.doubleColumns[i].Concat(rowsetHolder.doubleColumns[i]).ToArray();
                }
            }

            for (int i = 0; i < this.pagePointerColumns.Length; i++)
            {
                if (this.pagePointerColumns[i] == null)
                {
                    this.pagePointerColumns[i] = rowsetHolder.pagePointerColumns[i];
                }
                else
                {
                    this.pagePointerColumns[i] = this.pagePointerColumns[i].Concat(rowsetHolder.pagePointerColumns[i]).ToArray();
                }
            }

            this.rowsetCount += rowsetHolder.rowsetCount;
        }

        public void ModifyRow(int rowNumber, RowsetHolder rowsetHolder)
        {
            if (rowsetHolder.Count() != 1)
            {
                throw new ArgumentException();
            }

            if (this.columnIdToTypeIdMappers.Length != rowsetHolder.columnIdToTypeIdMappers.Length)
            {
                throw new ArgumentException();
            }

            for (int i = 0; i < this.columnIdToTypeIdMappers.Length; i++)
            {
                if (this.columnIdToTypeIdMappers[i] != rowsetHolder.columnIdToTypeIdMappers[i])
                {
                    throw new ArgumentException();
                }
            }

            for (int i = 0; i < this.pagePointerOffsetColumns.Length; i++)
            {
                this.pagePointerOffsetColumns[i][rowNumber] = rowsetHolder.pagePointerOffsetColumns[i][0];
            }

            for (int i = 0; i < this.intColumns.Length; i++)
            {
                this.intColumns[i][rowNumber] = rowsetHolder.intColumns[i][0];
            }

            for (int i = 0; i < this.doubleColumns.Length; i++)
            {
                this.doubleColumns[i][rowNumber] = rowsetHolder.doubleColumns[i][0];
            }

            for (int i = 0; i < this.pagePointerColumns.Length; i++)
            {
                this.pagePointerColumns[i][rowNumber] = rowsetHolder.pagePointerColumns[i][0];
            }
        }

        public ColumnType[] GetColumnTypes() => this.columnTypes;

        public IEnumerator<RowHolder> GetEnumerator()
        {
            for (int i = 0; i < this.rowsetCount; i++)
            {
                RowHolder rh = new RowHolder();
                rh.iRow = new int[this.intColumns.Length];
                for (int columnNum = 0; columnNum < this.intColumns.Length; columnNum++)
                {
                    rh.iRow[columnNum] = this.intColumns[columnNum][i];
                }

                rh.dRow = new double[this.doubleColumns.Length];
                for (int columnNum = 0; columnNum < this.doubleColumns.Length; columnNum++)
                {
                    rh.dRow[columnNum] = this.doubleColumns[columnNum][i];
                }

                rh.pagePRow = new long[this.pagePointerColumns.Length];
                for (int columnNum = 0; columnNum < this.pagePointerColumns.Length; columnNum++)
                {
                    rh.pagePRow[columnNum] = this.pagePointerColumns[columnNum][i];
                }

                rh.strPRow = new PagePointerOffsetPair[this.pagePointerOffsetColumns.Length];
                for (int columnNum = 0; columnNum < this.pagePointerOffsetColumns.Length; columnNum++)
                {
                    rh.strPRow[columnNum] = this.pagePointerOffsetColumns[columnNum][i];
                }

                yield return rh;
            }
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator1();
        }

        private IEnumerator GetEnumerator1()
        {
            return this.GetEnumerator();
        }

        public bool Equals([AllowNull] RowsetHolder other)
        {
            return Enumerable.SequenceEqual(this, other);
        }
    }
}
