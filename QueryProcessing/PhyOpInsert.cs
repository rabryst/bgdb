﻿using DataStructures;
using LockManager.LockImplementation;
using MetadataManager;
using PageManager;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QueryProcessing
{
    public class PhyOpTableInsert : IPhysicalOperator<Row>
    {
        private MetadataTable mdTable;
        private PageListCollection pageCollection;
        private IAllocateMixedPage pageAllocator;
        private HeapWithOffsets<char[]> stringHeap;
        private IPhysicalOperator<Row> input;
        private ITransaction tran;

        public PhyOpTableInsert(MetadataTable mdTable, IAllocateMixedPage pageAllocator, HeapWithOffsets<char[]> stringHeap, IPhysicalOperator<Row> input, ITransaction tran)
        {
            this.mdTable = mdTable;
            this.pageAllocator = pageAllocator;

            ColumnType[] columnTypes = mdTable.Columns.Select(x => x.ColumnType).ToArray();

            // TODO: Because I always construct new page list collection last
            // insert is always zero!
            this.pageCollection = new PageListCollection(this.pageAllocator, columnTypes, mdTable.RootPage);
            this.stringHeap = stringHeap;
            this.input = input;
            this.tran = tran;
        }

        public async Task Invoke()
        {
            await foreach (Row row in this.input.Iterate(this.tran))
            {
                await this.pageCollection.Add(await row.ToRowsetHolder(mdTable.Columns.Select(c => c.ColumnType).ToArray(), stringHeap, tran), tran).ConfigureAwait(false);
            }
        }

        public async IAsyncEnumerable<Row> Iterate(ITransaction tran)
        {
            await Task.FromResult(0);
            yield return null;
        }
    }
}
