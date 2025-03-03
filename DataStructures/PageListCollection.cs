﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LockManager.LockImplementation;
using PageManager;

namespace DataStructures
{
    public enum CollectionType
    {
        PageList,
        BTree,
    }

    public static class CollectionHandler
    {
        public static async Task<MixedPage> CreateInitialPage(CollectionType collectionType, ColumnInfo[] columnInfos, IAllocateMixedPage pageAllocator, ITransaction tran)
        {
            ArgumentNullException.ThrowIfNull(collectionType);
            ArgumentNullException.ThrowIfNull(pageAllocator);
            ArgumentNullException.ThrowIfNull(tran);

            if (collectionType == CollectionType.PageList)
            {
                return await pageAllocator.AllocateMixedPage(columnInfos, PageManagerConstants.NullPageId, PageManagerConstants.NullPageId, tran);
            }
            else if (collectionType == CollectionType.BTree)
            {
                // Need to extend it to add page pointers.
                ColumnInfo[] btreeColumnTypes = new ColumnInfo[columnInfos.Length + 1];
                Array.Copy(columnInfos, btreeColumnTypes, columnInfos.Length);
                btreeColumnTypes[columnInfos.Length] = new ColumnInfo(ColumnType.PagePointer);
                return await pageAllocator.AllocateMixedPage(btreeColumnTypes, PageManagerConstants.NullPageId, PageManagerConstants.NullPageId, tran);
            }
            else
            {
                throw new ArgumentException();
            }
        }
    }

    public interface IPageCollection<T>
    {
        Task<ulong> Count(ITransaction tran);
        Task Add(T item, ITransaction tran);
        IAsyncEnumerable<T> Where(Func<T, bool> filter, ITransaction tran);
        Task<U> Max<U>(Func<T, U> projector, U startMin, ITransaction tran) where U : IComparable;
        Task<bool> IsEmpty(ITransaction tran);
        IAsyncEnumerable<T> Iterate(ITransaction tran);
        ColumnInfo[] GetColumnTypes();
        bool SupportsSeek();
        IAsyncEnumerable<T> Seek<K>(K seekVal, ITransaction tran) where K : unmanaged, IComparable<K>;
    }

    public class PageListCollection : IPageCollection<RowHolder>
    {
        private ulong collectionRootPageId;
        private IAllocateMixedPage pageAllocator;
        private ColumnInfo[] columnTypes;
        private ulong lastPageId;

        public PageListCollection(IAllocateMixedPage pageAllocator, ColumnInfo[] columnTypes, ITransaction tran)
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

        public PageListCollection(IAllocateMixedPage pageAllocator, ColumnInfo[] columnTypes, ulong initialPageId)
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

        public async Task Add(RowHolder item, ITransaction tran)
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
                        currPage.Insert(item, tran);
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
                    currPage.Insert(item, tran);
                }
            }
        }

        public async IAsyncEnumerable<RowHolder> Where(Func<RowHolder, bool> filter, ITransaction tran)
        {
            MixedPage currPage;
            for (ulong currPageId = collectionRootPageId; currPageId != PageManagerConstants.NullPageId; currPageId = currPage.NextPageId())
            {
                currPage = await pageAllocator.GetMixedPage(currPageId, tran, this.columnTypes).ConfigureAwait(false);
                using Releaser lck = await tran.AcquireLock(currPage.PageId(), LockManager.LockTypeEnum.Shared).ConfigureAwait(false);

                foreach (RowHolder rhf in currPage.Fetch(tran))
                {
                    if (filter(rhf))
                    {
                        yield return rhf;
                    }
                }
            }
        }

        public async Task<U> Max<U>(Func<RowHolder, U> projector, U startMin, ITransaction tran) where U : IComparable
        {
            MixedPage currPage;
            U max = startMin;

            for (ulong currPageId = collectionRootPageId; currPageId != PageManagerConstants.NullPageId; currPageId = currPage.NextPageId())
            {
                using Releaser lck = await tran.AcquireLock(currPageId, LockManager.LockTypeEnum.Shared).ConfigureAwait(false);
                currPage = await pageAllocator.GetMixedPage(currPageId, tran, this.columnTypes).ConfigureAwait(false);

                foreach (RowHolder rhf in currPage.Fetch(tran))
                {
                    U curr = projector(rhf);
                    if (curr.CompareTo(max) == 1)
                    {
                        max = curr;
                    }
                }
            }

            return max;
        }

        public async IAsyncEnumerable<RowHolder> Iterate(ITransaction tran)
        {
            MixedPage currPage;
            for (ulong currPageId = collectionRootPageId; currPageId != PageManagerConstants.NullPageId; currPageId = currPage.NextPageId())
            {
                using Releaser lck = await tran.AcquireLock(currPageId, LockManager.LockTypeEnum.Shared).ConfigureAwait(false);
                currPage = await pageAllocator.GetMixedPage(currPageId, tran, this.columnTypes).ConfigureAwait(false);

                foreach (RowHolder rhf in currPage.Fetch(tran))
                {
                    yield return rhf;
                }
            }
        }

        public async Task<bool> IsEmpty(ITransaction tran)
        {
            return await this.Count(tran).ConfigureAwait(false) == 0;
        }

        public ColumnInfo[] GetColumnTypes() => this.columnTypes;

        public bool SupportsSeek() => false;

        public IAsyncEnumerable<RowHolder> Seek<K>(K seekVal, ITransaction tran) where K : unmanaged, IComparable<K>
        {
            throw new NotImplementedException();
        }
    }
}
