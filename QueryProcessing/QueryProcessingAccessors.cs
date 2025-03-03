﻿using MetadataManager;
using PageManager;
using QueryProcessing.Exceptions;
using System;
using System.Diagnostics;
using System.Linq;

namespace QueryProcessing
{
    static class QueryProcessingAccessors
    {
        public static MetadataColumn[] MergeColumns(MetadataColumn[] left, MetadataColumn[] right)
        {
            MetadataColumn[] res = new MetadataColumn[left.Length + right.Length];
            left.CopyTo(res, 0);

            // Need to update offsets here.
            for (int i = 0; i < right.Length; i++)
            {
                res[i + left.Length] = new MetadataColumn(
                    right[i].ColumnId + left.Length, // aligning the offset to match new position.
                    right[i].TableId,
                    right[i].ColumnName,
                    right[i].ColumnType);
            }

            return res;
        }

        public static MetadataColumn GetMetadataColumn(string name, MetadataColumn[] metadataColumns)
        {
            if (name.Contains("."))
            {
                if (name.Count(c => c == '.') != 1)
                {
                    throw new InvalidColumnNameException();
                }

                // At this moment all columns should have full name.
                // So just find the column.
                foreach (MetadataColumn mc in metadataColumns)
                {
                    if (string.Equals(mc.ColumnName, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return mc;
                    }
                }

                throw new InvalidColumnNameException();
            }
            else
            {
                // ignore table name.
                // also need to insure that there is only 1 identifier with this name.
                MetadataColumn? foundMc = null;
                foreach (MetadataColumn mc in metadataColumns)
                {
                    string searchColumnName = mc.ColumnName;
                    if (searchColumnName.Contains('.'))
                    {
                        if (searchColumnName.Count(c => c == '.') != 1)
                        {
                            throw new InvalidColumnNameException();
                        }

                        searchColumnName = searchColumnName.Split('.')[1];
                    }

                    if (string.Equals(searchColumnName, name, StringComparison.OrdinalIgnoreCase))
                    {
                        if (foundMc == null)
                        {
                            foundMc = mc;
                        }
                        else
                        {
                            throw new AmbiguousColumnNameException();
                        }
                    }
                }

                if (foundMc != null)
                {
                    return foundMc.Value;
                }
                else
                {
                    throw new InvalidColumnNameException();
                }
            }
        }

        public static IComparable MetadataColumnRowsetHolderFetcher(MetadataColumn mc, RowHolder rowHolder)
        {
            // TODO: Can't use ColumnId as fetcher.
            if (mc.ColumnType.ColumnType == ColumnType.Int)
            {
                return rowHolder.GetField<int>(mc.ColumnId);
            }
            else if (mc.ColumnType.ColumnType == ColumnType.Double)
            {
                return rowHolder.GetField<double>(mc.ColumnId);
            }
            else if (mc.ColumnType.ColumnType == ColumnType.String)
            {
                // TODO: Since char[] doesn't implement IComparable need to cast it to string.
                // This is super slow...
                // Consider creating your own string type.
                return new string(rowHolder.GetStringField(mc.ColumnId));
            }
            else
            {
                Debug.Fail("Invalid column type");
                throw new InvalidProgramException("Invalid state.");
            }
        }
        public static ColumnType ValueToType(Sql.value val)
        {
            if (val.IsFloat)
            {
                return ColumnType.Double;
            }
            else if (val.IsInt)
            {
                return ColumnType.Int;
            }
            else if (val.IsString)
            {
                return ColumnType.String;
            }
            else
            {
                throw new NotImplementedException("No support for this value type");
            }
        }

        // TODO: This is just bad.
        // It is very hard to keep all type -> agg mappings.
        // Needs refactoring.
        public static void ApplyAgg(MetadataColumn mc, ref RowHolder inputRowHolder, Sql.aggType aggType, ref RowHolder stateRowHolder) 
        {
            // TODO: Can't use ColumnId as fetcher.
            if (mc.ColumnType.ColumnType == ColumnType.Int)
            {
                int inputValue = inputRowHolder.GetField<int>(mc.ColumnId);
                int stateValue = stateRowHolder.GetField<int>(mc.ColumnId);

                if (aggType.IsMax)
                {
                    if (inputValue.CompareTo(stateValue) == 1)
                    {
                        // Update state.
                        // TODO: boxing/unboxing hurts perf.
                        stateRowHolder.SetField<int>(mc.ColumnId, inputValue);
                    }
                }
                else if (aggType.IsMin)
                {
                    if (inputValue.CompareTo(stateValue) == -1)
                    {
                        // TODO: boxing/unboxing hurts perf.
                        stateRowHolder.SetField<int>(mc.ColumnId, inputValue);
                    }
                }
                else if (aggType.IsSum)
                {
                    
                    stateRowHolder.SetField<int>(mc.ColumnId, inputValue + stateValue);
                }
                else if (aggType.IsCount)
                {
                    stateRowHolder.SetField<int>(mc.ColumnId, 1 + stateValue);
                }
                else
                {
                    throw new InvalidProgramException("Aggregate not supported.");
                }
            }
            else if (mc.ColumnType.ColumnType == ColumnType.Double)
            {
                double inputValue = inputRowHolder.GetField<double>(mc.ColumnId);
                double stateValue = stateRowHolder.GetField<double>(mc.ColumnId);

                if (aggType.IsMax)
                {
                    if (inputValue.CompareTo(stateValue) == 1)
                    {
                        // Update state.
                        // TODO: boxing/unboxing hurts perf.
                        stateRowHolder.SetField<double>(mc.ColumnId, (double)inputValue);
                    }
                }
                else if (aggType.IsMin)
                {
                    if (inputValue.CompareTo(stateValue) == -1)
                    {
                        // TODO: boxing/unboxing hurts perf.
                        stateRowHolder.SetField<double>(mc.ColumnId, inputValue);
                    }
                }
                else if (aggType.IsSum)
                {
                    stateRowHolder.SetField<double>(mc.ColumnId, inputValue + stateValue);
                }
                else
                {
                    throw new InvalidProgramException("Aggregate not supported.");
                }
            }
            else if (mc.ColumnType.ColumnType == ColumnType.String)
            {
                // TODO: Since char[] doesn't implement IComparable need to cast it to string.
                // This is super slow...
                // Consider creating your own string type.
                string inputValue = new string(inputRowHolder.GetStringField(mc.ColumnId));
                string stateValue = new string(stateRowHolder.GetStringField(mc.ColumnId));

                if (aggType.IsMax)
                {
                    if (inputValue.CompareTo(stateValue) == 1)
                    {
                        // Update state.
                        // TODO: boxing/unboxing hurts perf.
                        stateRowHolder.SetField(mc.ColumnId, inputValue.ToCharArray());
                    }
                }
                else if (aggType.IsMin)
                {
                    if (inputValue.CompareTo(stateValue) == -1)
                    {
                        // TODO: boxing/unboxing hurts perf.
                        stateRowHolder.SetField(mc.ColumnId, inputValue.ToCharArray());
                    }
                }
                else
                {
                    throw new InvalidProgramException("Aggregate not supported.");
                }
            }
            else
            {
                Debug.Fail("Invalid column type");
                throw new InvalidProgramException("Invalid state.");
            }
        }
    }
}
