﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LockManager.LockImplementation;
using PageManager;

namespace DataStructures
{
    public interface UnorderedListCollection<T>
    {
        Task<ulong> Count(ITransaction tran);
        Task Add(T item, ITransaction tran);
        Task<List<T>> Where(Func<T, bool> filter, ITransaction tran);
        Task<U> Max<U>(Func<T, U> projector, U startMin, ITransaction tran) where U : IComparable;
        Task<bool> IsEmpty(ITransaction tran);
        IAsyncEnumerable<T> Iterate(ITransaction tran);
    }

    public class PageListCollection : UnorderedListCollection<RowsetHolder>
    {
        private ulong collectionRootPageId;
        private IAllocateMixedPage pageAllocator;
        private ColumnType[] columnTypes;
        private ulong lastPageId;

        public PageListCollection(IAllocateMixedPage pageAllocator, ColumnType[] columnTypes, ITransaction tran)
        {
            if (pageAllocator == null || columnTypes == null || columnTypes.Length == 0)
            {
                throw new ArgumentNullException();
            }

            this.collectionRootPageId = pageAllocator.AllocateMixedPage(columnTypes, PageManagerConstants.NullPageId, PageManagerConstants.NullPageId, tran).Result.PageId();
            this.pageAllocator = pageAllocator;
            this.columnTypes = columnTypes;
            this.lastPageId = this.collectionRootPageId;
        }

        public PageListCollection(IAllocateMixedPage pageAllocator, ColumnType[] columnTypes, ulong initialPageId)
        {
            if (pageAllocator == null || columnTypes == null || columnTypes.Length == 0)
            {
                throw new ArgumentNullException();
            }

            this.collectionRootPageId = initialPageId;
            this.pageAllocator = pageAllocator;
            this.columnTypes = columnTypes;
            this.lastPageId = this.collectionRootPageId;
        }

        public async Task<ulong> Count(ITransaction tran)
        {
            ulong rowCount = 0;

            IPage currPage;
            for (ulong currPageId = collectionRootPageId; currPageId != PageManagerConstants.NullPageId; currPageId = currPage.NextPageId())
           {
                using Releaser lck = await tran.AcquireLock(currPageId, LockManager.LockTypeEnum.Shared).ConfigureAwait(false);
                currPage = await pageAllocator.GetMixedPage(currPageId, tran, this.columnTypes).ConfigureAwait(false);
                rowCount += currPage.RowCount();
            }

            return rowCount;
        }

        public async Task Add(RowsetHolder item, ITransaction tran)
        {
            MixedPage currPage = null;
            for (ulong currPageId = this.lastPageId; currPageId != PageManagerConstants.NullPageId; currPageId = currPage.NextPageId())
            {
                using Releaser lck = await tran.AcquireLock(currPageId, LockManager.LockTypeEnum.Shared).ConfigureAwait(false);
                currPage = await pageAllocator.GetMixedPage(currPageId, tran, this.columnTypes).ConfigureAwait(false);
                if (currPage.CanFit(item, tran))
                {
                    lck.Dispose();

                    using Releaser writeLock = await tran.AcquireLock(currPageId, LockManager.LockTypeEnum.Exclusive).ConfigureAwait(false);

                    // Need to check can fit one more time.
                    if (currPage.CanFit(item, tran))
                    {
                        currPage.Merge(item, tran);
                        return;
                    }
                }
            }

            {
                using Releaser prevPageLck = await tran.AcquireLock(currPage.PageId(), LockManager.LockTypeEnum.Exclusive).ConfigureAwait(false);

                if (currPage.NextPageId() != PageManagerConstants.NullPageId)
                {
                    // TODO: it would be good if caller had ability to control this lock.
                    // This dispose doesn't mean anything in current implementation of read committed.
                    prevPageLck.Dispose();
                    await Add(item, tran).ConfigureAwait(false);
                }
                else
                {
                    currPage = await this.pageAllocator.AllocateMixedPage(this.columnTypes, currPage.PageId(), PageManagerConstants.NullPageId, tran).ConfigureAwait(false);
                    using Releaser currPageLck = await tran.AcquireLock(currPage.PageId(), LockManager.LockTypeEnum.Exclusive).ConfigureAwait(false);
                    this.lastPageId = currPage.PageId();
                    currPage.Merge(item, tran);
                }
            }
        }

        public async Task<List<RowsetHolder>> Where(Func<RowsetHolder, bool> filter, ITransaction tran)
        {
            MixedPage currPage;
            List<RowsetHolder> result = new List<RowsetHolder>();
            for (ulong currPageId = collectionRootPageId; currPageId != PageManagerConstants.NullPageId; currPageId = currPage.NextPageId())
            {
                currPage = await pageAllocator.GetMixedPage(currPageId, tran, this.columnTypes).ConfigureAwait(false);
                using Releaser lck = await tran.AcquireLock(currPage.PageId(), LockManager.LockTypeEnum.Shared).ConfigureAwait(false);
                RowsetHolder holder = currPage.Fetch(tran);

                if (filter(holder))
                {
                    result.Add(holder);
                }
            }

            return result;
        }

        public async Task<U> Max<U>(Func<RowsetHolder, U> projector, U startMin, ITransaction tran) where U : IComparable
        {
            MixedPage currPage;
            U max = startMin;

            for (ulong currPageId = collectionRootPageId; currPageId != PageManagerConstants.NullPageId; currPageId = currPage.NextPageId())
            {
                using Releaser lck = await tran.AcquireLock(currPageId, LockManager.LockTypeEnum.Shared).ConfigureAwait(false);
                currPage = await pageAllocator.GetMixedPage(currPageId, tran, this.columnTypes).ConfigureAwait(false);
                RowsetHolder holder = currPage.Fetch(tran);

                U curr = projector(holder);

                if (curr.CompareTo(max) == 1)
                {
                    max = curr;
                }
            }

            return max;
        }

        public async IAsyncEnumerable<RowsetHolder> Iterate(ITransaction tran)
        {
            MixedPage currPage;
            for (ulong currPageId = collectionRootPageId; currPageId != PageManagerConstants.NullPageId; currPageId = currPage.NextPageId())
            {
                using Releaser lck = await tran.AcquireLock(currPageId, LockManager.LockTypeEnum.Shared).ConfigureAwait(false);
                currPage = await pageAllocator.GetMixedPage(currPageId, tran, this.columnTypes).ConfigureAwait(false);
                RowsetHolder holder = currPage.Fetch(tran);

                yield return holder;
            }
        }

        public async Task<bool> IsEmpty(ITransaction tran)
        {
            return await this.Count(tran).ConfigureAwait(false) == 0;
        }
    }
}
