﻿using LockManager;
using NUnit.Framework;
using PageManager;
using System.Threading.Tasks;
using Test.Common;

namespace PageManagerTests
{
    public class BufferPoolTests
    {
        private const int DefaultSize = 4096;
        private const ulong DefaultPrevPage = PageManagerConstants.NullPageId;
        private const ulong DefaultNextPage = PageManagerConstants.NullPageId;
        private DummyTran tran = new DummyTran();

        [Test]
        [MaxTime(10000)]
        public async Task BufferPoolCheck()
        {
            IPageEvictionPolicy pageEvictionPolicy = new LruEvictionPolicy(10, 5);
            IBufferPool bp = new BufferPool(pageEvictionPolicy, TestGlobals.DefaultPageSize);
            ILockManager lm = new LockManager.LockManager();

            var pageManager =  new PageManager.PageManager(DefaultSize, TestGlobals.DefaultPersistedStream, bp, lm, TestGlobals.TestFileLogger);

            await pageManager.AllocatePage(PageType.StringPage, DefaultPrevPage, DefaultNextPage, tran);
            await pageManager.AllocatePage(PageType.StringPage, DefaultPrevPage, DefaultNextPage, tran);
            await pageManager.AllocatePage(PageType.StringPage, DefaultPrevPage, DefaultNextPage, tran);

            Assert.AreEqual(3, bp.PagesInPool());
        }

        [Test]
        [MaxTime(10000)]
        public async Task BufferPoolAfterEviction()
        {
            IPageEvictionPolicy pageEvictionPolicy = new LruEvictionPolicy(10, 5);
            IBufferPool bp = new BufferPool(pageEvictionPolicy, TestGlobals.DefaultPageSize);
            ILockManager lm = new LockManager.LockManager();

            var pageManager =  new PageManager.PageManager(DefaultSize, TestGlobals.DefaultPersistedStream, bp, lm, TestGlobals.TestFileLogger);

            for (int i = 0; i < 11; i++)
            {
                await pageManager.AllocatePage(PageType.StringPage, DefaultPrevPage, DefaultNextPage, tran);
            }

            Assert.AreEqual(6, bp.PagesInPool());
        }
    }
}
